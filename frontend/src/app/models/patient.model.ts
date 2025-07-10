export interface Patient {
  id: string;
  medicalRecordNumber: string;
  
  // Personal Information
  firstName: string;
  lastName: string;
  dateOfBirth: Date;
  gender: Gender;
  ssn?: string; // Encrypted in real implementation
  
  // Contact Information
  email?: string;
  phone?: string;
  address: Address;
  
  // Emergency Contact
  emergencyContact: EmergencyContact;
  
  // Medical Information
  bloodType: BloodType;
  allergies: Allergy[];
  medications: Medication[];
  medicalHistory: MedicalHistoryEntry[];
  
  // Insurance Information
  insurance: Insurance;
  
  // Clinical Status
  status: PatientStatus;
  assignedPhysician: string;
  lastVisit?: Date;
  nextAppointment?: Date;
  
  // Audit Information
  createdAt: Date;
  updatedAt: Date;
  createdBy: string;
  lastModifiedBy: string;
}

export interface Address {
  street: string;
  city: string;
  state: string;
  zipCode: string;
  country: string;
}

export interface EmergencyContact {
  name: string;
  relationship: string;
  phone: string;
  email?: string;
}

export interface Allergy {
  id: string;
  allergen: string;
  severity: AllergySeverity;
  reaction: string;
  onsetDate?: Date;
  notes?: string;
}

export interface Medication {
  id: string;
  name: string;
  dosage: string;
  frequency: string;
  prescribedBy: string;
  prescribedDate: Date;
  startDate: Date;
  endDate?: Date;
  isActive: boolean;
  notes?: string;
}

export interface MedicalHistoryEntry {
  id: string;
  condition: string;
  diagnosisDate: Date;
  status: ConditionStatus;
  treatingPhysician: string;
  notes?: string;
  icd10Code?: string;
}

export interface Insurance {
  provider: string;
  policyNumber: string;
  groupNumber?: string;
  coverageType: CoverageType;
  effectiveDate: Date;
  expirationDate?: Date;
  copay?: number;
  deductible?: number;
}

export interface VitalSigns {
  id: string;
  patientId: string;
  recordedAt: Date;
  recordedBy: string;
  temperature?: number; // Fahrenheit
  bloodPressure?: {
    systolic: number;
    diastolic: number;
  };
  heartRate?: number; // BPM
  respiratoryRate?: number; // per minute
  oxygenSaturation?: number; // percentage
  weight?: number; // pounds
  height?: number; // inches
  bmi?: number;
  notes?: string;
}

export interface Appointment {
  id: string;
  patientId: string;
  physicianId: string;
  scheduledDate: Date;
  duration: number; // minutes
  type: AppointmentType;
  status: AppointmentStatus;
  reason: string;
  notes?: string;
  roomNumber?: string;
  
  // Clinical Data
  vitalSigns?: VitalSigns;
  diagnosis?: string[];
  treatment?: string[];
  followUpRequired?: boolean;
  
  // Audit
  createdAt: Date;
  updatedAt: Date;
}

// Enums
export enum Gender {
  MALE = 'male',
  FEMALE = 'female',
  OTHER = 'other',
  PREFER_NOT_TO_SAY = 'prefer_not_to_say'
}

export enum BloodType {
  A_POSITIVE = 'A+',
  A_NEGATIVE = 'A-',
  B_POSITIVE = 'B+',
  B_NEGATIVE = 'B-',
  AB_POSITIVE = 'AB+',
  AB_NEGATIVE = 'AB-',
  O_POSITIVE = 'O+',
  O_NEGATIVE = 'O-'
}

export enum AllergySeverity {
  MILD = 'mild',
  MODERATE = 'moderate',
  SEVERE = 'severe',
  LIFE_THREATENING = 'life_threatening'
}

export enum PatientStatus {
  ACTIVE = 'active',
  INACTIVE = 'inactive',
  DECEASED = 'deceased',
  TRANSFERRED = 'transferred'
}

export enum ConditionStatus {
  ACTIVE = 'active',
  RESOLVED = 'resolved',
  CHRONIC = 'chronic',
  UNDER_TREATMENT = 'under_treatment'
}

export enum CoverageType {
  PRIMARY = 'primary',
  SECONDARY = 'secondary',
  MEDICARE = 'medicare',
  MEDICAID = 'medicaid',
  PRIVATE = 'private'
}

export enum AppointmentType {
  ROUTINE_CHECKUP = 'routine_checkup',
  FOLLOW_UP = 'follow_up',
  EMERGENCY = 'emergency',
  CONSULTATION = 'consultation',
  PROCEDURE = 'procedure',
  TELEMEDICINE = 'telemedicine'
}

export enum AppointmentStatus {
  SCHEDULED = 'scheduled',
  CONFIRMED = 'confirmed',
  IN_PROGRESS = 'in_progress',
  COMPLETED = 'completed',
  CANCELLED = 'cancelled',
  NO_SHOW = 'no_show'
}

// Data Transfer Objects
export interface PatientCreateDto {
  firstName: string;
  lastName: string;
  dateOfBirth: Date;
  gender: Gender;
  email?: string;
  phone?: string;
  address: Address;
  emergencyContact: EmergencyContact;
  insurance: Insurance;
}

export interface PatientUpdateDto extends Partial<PatientCreateDto> {
  id: string;
}

export interface PatientSearchCriteria {
  name?: string;
  medicalRecordNumber?: string;
  dateOfBirth?: Date;
  phone?: string;
  assignedPhysician?: string;
  status?: PatientStatus;
}

// API Response Types
export interface PatientListResponse {
  patients: Patient[];
  totalCount: number;
  pageSize: number;
  currentPage: number;
  totalPages: number;
}

export interface PatientDetailsResponse {
  patient: Patient;
  recentAppointments: Appointment[];
  recentVitalSigns: VitalSigns[];
  activeMedications: Medication[];
  activeAllergies: Allergy[];
} 