import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  title = 'Healthcare Patient Care Dashboard';
  isAuthenticated = false;
  currentUser: any = null;
  
  navigationItems = [
    { 
      label: 'Dashboard', 
      icon: 'dashboard', 
      route: '/dashboard',
      description: 'Patient overview and metrics' 
    },
    { 
      label: 'Patients', 
      icon: 'people', 
      route: '/patients',
      description: 'Patient management and records' 
    },
    { 
      label: 'Appointments', 
      icon: 'schedule', 
      route: '/appointments',
      description: 'Scheduling and calendar' 
    },
    { 
      label: 'Clinical Data', 
      icon: 'medical_services', 
      route: '/clinical',
      description: 'Medical records and history' 
    },
    { 
      label: 'Reports', 
      icon: 'assessment', 
      route: '/reports',
      description: 'Analytics and population health' 
    }
  ];

  constructor(
    private authService: AuthService,
    private router: Router,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.checkAuthenticationStatus();
    this.setupNotifications();
  }

  private checkAuthenticationStatus(): void {
    this.authService.isAuthenticated$.subscribe(
      isAuth => {
        this.isAuthenticated = isAuth;
        if (isAuth) {
          this.currentUser = this.authService.getCurrentUser();
        }
      }
    );
  }

  private setupNotifications(): void {
    // Setup real-time notifications for clinical events
    this.notificationService.initializeNotifications();
  }

  onNavigate(route: string): void {
    this.router.navigate([route]);
  }

  onLogout(): void {
    this.authService.logout().subscribe({
      next: () => {
        this.router.navigate(['/login']);
      },
      error: (error) => {
        console.error('Logout error:', error);
      }
    });
  }

  getUserRole(): string {
    return this.currentUser?.role || 'Guest';
  }

  getUserInitials(): string {
    if (!this.currentUser?.firstName || !this.currentUser?.lastName) {
      return 'U';
    }
    return `${this.currentUser.firstName.charAt(0)}${this.currentUser.lastName.charAt(0)}`;
  }
} 