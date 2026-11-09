export type UserRole = 'Candidate' | 'Employer';

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  userId: string;
  email: string;
  fullName: string;
  role: UserRole;
  companyName?: string | null;
  isEmailConfirmed?: boolean;
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

export interface SocialLoginRequest {
  provider: 'Google' | 'GitHub' | string;
  idToken?: string;
  code?: string;
  redirectUri?: string;
  role?: UserRole;
}

export interface ConfirmEmailRequest {
  userId: string;
  token: string;
}

export interface ResendConfirmationRequest {
  email: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface SetPasswordRequest {
  newPassword: string;
}

export interface ChangeEmailRequest {
  newEmail: string;
}

export interface ConfirmChangeEmailRequest {
  newEmail: string;
  token: string;
}

export interface UpdateProfileRequest {
  fullName: string;
  companyName?: string | null;
}

export interface DeleteAccountRequest {
  password?: string | null;
}

export interface LinkedAccount {
  provider: string;
  providerDisplayName: string;
  isLinked: boolean;
  linkedAt?: string | null;
}

export interface AccountSecurityStatus {
  userId: string;
  email: string;
  fullName: string;
  role: UserRole;
  companyName?: string | null;
  isEmailConfirmed: boolean;
  hasPassword: boolean;
  linkedAccounts: LinkedAccount[];
}

export interface AuthUser {
  userId: string;
  email: string;
  fullName: string;
  role: UserRole;
  companyName?: string | null;
  isEmailConfirmed?: boolean;
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
