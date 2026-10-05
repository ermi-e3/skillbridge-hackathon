export type UserRole = 'Candidate' | 'Employer';

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  userId: string;
  email: string;
  fullName: string;
  role: UserRole;
  companyName?: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  role: UserRole;
  companyName?: string | null;
}

export interface AuthUser {
  userId: string;
  email: string;
  fullName: string;
  role: UserRole;
  companyName?: string | null;
}

export interface ApiError {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
