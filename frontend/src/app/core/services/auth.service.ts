import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, throwError } from 'rxjs';
import { map, catchError, tap } from 'rxjs/operators';
import { JwtHelperService } from '@auth0/angular-jwt';
import { Router } from '@angular/router';
import { environment } from '@environments/environment';

export interface LoginCredentials {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  user: User;
  permissions: string[];
  expires: number;
}

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  department: string;
  licenseNumber?: string;
  isActive: boolean;
  lastLogin?: Date;
  passwordExpires?: Date;
  mfaEnabled: boolean;
}

export enum UserRole {
  ADMIN = 'admin',
  PHYSICIAN = 'physician',
  NURSE = 'nurse',
  PHARMACIST = 'pharmacist',
  RECEPTIONIST = 'receptionist',
  TECHNICIAN = 'technician',
  PATIENT = 'patient'
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly AUTH_TOKEN_KEY = 'healthcare_auth_token';
  private readonly REFRESH_TOKEN_KEY = 'healthcare_refresh_token';
  private readonly USER_KEY = 'healthcare_user';
  
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  private isAuthenticatedSubject = new BehaviorSubject<boolean>(false);
  
  public currentUser$ = this.currentUserSubject.asObservable();
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();
  
  private readonly apiUrl = environment.apiUrl;
  private tokenRefreshTimer: any;

  constructor(
    private http: HttpClient,
    private jwtHelper: JwtHelperService,
    private router: Router
  ) {
    this.initializeAuthState();
  }

  /**
   * Initialize authentication state from local storage
   */
  private initializeAuthState(): void {
    const token = this.getToken();
    const user = this.getStoredUser();
    
    if (token && user && !this.jwtHelper.isTokenExpired(token)) {
      this.currentUserSubject.next(user);
      this.isAuthenticatedSubject.next(true);
      this.scheduleTokenRefresh();
    } else {
      this.clearAuthData();
    }
  }

  /**
   * User login with credentials
   */
  login(credentials: LoginCredentials): Observable<AuthResponse> {
    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
      'X-Requested-With': 'XMLHttpRequest'
    });

    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, credentials, { headers })
      .pipe(
        tap(response => {
          this.setAuthData(response);
          this.scheduleTokenRefresh();
          
          // Log successful login for audit
          this.logSecurityEvent('login_success', {
            userId: response.user.id,
            timestamp: new Date(),
            ipAddress: this.getClientIP()
          });
        }),
        catchError(this.handleAuthError.bind(this))
      );
  }

  /**
   * User logout
   */
  logout(): Observable<any> {
    const token = this.getToken();
    
    if (token) {
      // Call backend logout endpoint
      return this.http.post(`${this.apiUrl}/auth/logout`, {}, {
        headers: { Authorization: `Bearer ${token}` }
      }).pipe(
        tap(() => {
          this.logSecurityEvent('logout_success', {
            userId: this.getCurrentUser()?.id,
            timestamp: new Date()
          });
        }),
        catchError(error => {
          // Even if logout fails, clear local data
          console.error('Logout error:', error);
          return throwError(error);
        }),
        tap(() => this.clearAuthData())
      );
    } else {
      this.clearAuthData();
      return new Observable(observer => observer.complete());
    }
  }

  /**
   * Refresh authentication token
   */
  refreshToken(): Observable<AuthResponse> {
    const refreshToken = this.getRefreshToken();
    
    if (!refreshToken) {
      return throwError('No refresh token available');
    }

    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/refresh`, { 
      refreshToken 
    }).pipe(
      tap(response => {
        this.setAuthData(response);
        this.scheduleTokenRefresh();
      }),
      catchError(error => {
        this.clearAuthData();
        this.router.navigate(['/login']);
        return throwError(error);
      })
    );
  }

  /**
   * Check if user has required permission
   */
  hasPermission(permission: string): boolean {
    const user = this.getCurrentUser();
    if (!user) return false;
    
    // Admin has all permissions
    if (user.role === UserRole.ADMIN) return true;
    
    // Check role-based permissions
    const rolePermissions = this.getRolePermissions(user.role);
    return rolePermissions.includes(permission);
  }

  /**
   * Check if user has required role
   */
  hasRole(role: UserRole): boolean {
    const user = this.getCurrentUser();
    return user?.role === role;
  }

  /**
   * Get current authenticated user
   */
  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  /**
   * Get authentication token
   */
  getToken(): string | null {
    return localStorage.getItem(this.AUTH_TOKEN_KEY);
  }

  /**
   * Get refresh token
   */
  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  /**
   * Check if user is authenticated
   */
  isAuthenticated(): boolean {
    const token = this.getToken();
    return token !== null && !this.jwtHelper.isTokenExpired(token);
  }

  /**
   * Set authentication data
   */
  private setAuthData(response: AuthResponse): void {
    localStorage.setItem(this.AUTH_TOKEN_KEY, response.token);
    localStorage.setItem(this.REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(this.USER_KEY, JSON.stringify(response.user));
    
    this.currentUserSubject.next(response.user);
    this.isAuthenticatedSubject.next(true);
  }

  /**
   * Clear authentication data
   */
  private clearAuthData(): void {
    localStorage.removeItem(this.AUTH_TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    
    this.currentUserSubject.next(null);
    this.isAuthenticatedSubject.next(false);
    
    if (this.tokenRefreshTimer) {
      clearTimeout(this.tokenRefreshTimer);
    }
  }

  /**
   * Get stored user data
   */
  private getStoredUser(): User | null {
    const userData = localStorage.getItem(this.USER_KEY);
    return userData ? JSON.parse(userData) : null;
  }

  /**
   * Schedule token refresh
   */
  private scheduleTokenRefresh(): void {
    const token = this.getToken();
    if (!token) return;

    const tokenExpiration = this.jwtHelper.getTokenExpirationDate(token);
    if (!tokenExpiration) return;

    // Refresh token 5 minutes before expiration
    const refreshTime = tokenExpiration.getTime() - Date.now() - (5 * 60 * 1000);
    
    if (refreshTime > 0) {
      this.tokenRefreshTimer = setTimeout(() => {
        this.refreshToken().subscribe();
      }, refreshTime);
    }
  }

  /**
   * Get role-based permissions
   */
  private getRolePermissions(role: UserRole): string[] {
    const permissions: { [key in UserRole]: string[] } = {
      [UserRole.ADMIN]: ['*'], // All permissions
      [UserRole.PHYSICIAN]: [
        'patient.read',
        'patient.write',
        'appointment.read',
        'appointment.write',
        'medical_record.read',
        'medical_record.write',
        'prescription.write'
      ],
      [UserRole.NURSE]: [
        'patient.read',
        'patient.write',
        'appointment.read',
        'appointment.write',
        'vital_signs.read',
        'vital_signs.write',
        'medical_record.read'
      ],
      [UserRole.PHARMACIST]: [
        'patient.read',
        'prescription.read',
        'prescription.write',
        'medication.read',
        'medication.write'
      ],
      [UserRole.RECEPTIONIST]: [
        'patient.read',
        'patient.write',
        'appointment.read',
        'appointment.write',
        'insurance.read',
        'insurance.write'
      ],
      [UserRole.TECHNICIAN]: [
        'patient.read',
        'vital_signs.read',
        'vital_signs.write',
        'lab_results.read',
        'lab_results.write'
      ],
      [UserRole.PATIENT]: [
        'patient.read_own',
        'appointment.read_own',
        'medical_record.read_own'
      ]
    };

    return permissions[role] || [];
  }

  /**
   * Handle authentication errors
   */
  private handleAuthError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'Authentication failed';
    
    if (error.error?.message) {
      errorMessage = error.error.message;
    } else if (error.status === 401) {
      errorMessage = 'Invalid credentials';
    } else if (error.status === 403) {
      errorMessage = 'Access denied';
    } else if (error.status === 429) {
      errorMessage = 'Too many login attempts. Please try again later.';
    }

    // Log security event
    this.logSecurityEvent('login_failure', {
      error: errorMessage,
      timestamp: new Date(),
      ipAddress: this.getClientIP()
    });

    return throwError(errorMessage);
  }

  /**
   * Log security events for audit trail
   */
  private logSecurityEvent(eventType: string, details: any): void {
    // In a real application, this would send to a security logging service
    console.log(`Security Event: ${eventType}`, details);
    
    // Send to audit log endpoint
    this.http.post(`${this.apiUrl}/audit/security-event`, {
      eventType,
      details,
      timestamp: new Date()
    }).subscribe({
      error: (error) => console.error('Failed to log security event:', error)
    });
  }

  /**
   * Get client IP address (simplified for demo)
   */
  private getClientIP(): string {
    // In a real application, this would be determined server-side
    return '127.0.0.1';
  }
} 