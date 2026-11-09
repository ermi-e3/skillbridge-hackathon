import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AccountSecurityStatus,
  AuthResponse,
  AuthUser,
  ChangeEmailRequest,
  ChangePasswordRequest,
  ConfirmChangeEmailRequest,
  ConfirmEmailRequest,
  DeleteAccountRequest,
  ForgotPasswordRequest,
  LinkedAccount,
  LoginRequest,
  RegisterRequest,
  ResendConfirmationRequest,
  ResetPasswordRequest,
  SetPasswordRequest,
  SocialLoginRequest,
  UpdateProfileRequest,
  UserRole,
} from '../models/auth.models';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiUrl}/auth`;
  private readonly accountUrl = `${environment.apiUrl}/account`;

  // Stored strictly in Angular memory (no localStorage or sessionStorage)
  readonly token = signal<string | null>(null);
  readonly currentUser = signal<AuthUser | null>(null);
  readonly role = computed<UserRole | null>(() => this.currentUser()?.role ?? null);
  readonly isAuthenticated = computed<boolean>(() => !!this.token());

  /**
   * Log in with email and password
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login`, credentials, { withCredentials: true })
      .pipe(tap((response) => this.handleAuthSuccess(response)));
  }

  /**
   * Register a new Candidate or Employer
   */
  register(payload: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/register`, payload, { withCredentials: true })
      .pipe(tap((response) => this.handleAuthSuccess(response)));
  }

  /**
   * Social login or registration via Google or GitHub
   */
  socialLogin(payload: SocialLoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/social-login`, payload, { withCredentials: true })
      .pipe(tap((response) => this.handleAuthSuccess(response)));
  }

  /**
   * Confirm email address with token from verification email
   */
  confirmEmail(payload: ConfirmEmailRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/confirm-email`, payload);
  }

  /**
   * Resend email confirmation link
   */
  resendConfirmation(payload: ResendConfirmationRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/resend-confirmation`, payload);
  }

  /**
   * Request password reset link
   */
  forgotPassword(payload: ForgotPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/forgot-password`, payload);
  }

  /**
   * Reset password with token from email
   */
  resetPassword(payload: ResetPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/reset-password`, payload);
  }

  /**
   * Get account security status (email confirmation, password, linked accounts)
   */
  getSecurityStatus(): Observable<AccountSecurityStatus> {
    return this.http.get<AccountSecurityStatus>(`${this.accountUrl}/security-status`, { withCredentials: true });
  }

  /**
   * Update user profile information
   */
  updateProfile(payload: UpdateProfileRequest): Observable<AuthUser> {
    return this.http.put<AuthUser>(`${this.accountUrl}/profile`, payload, { withCredentials: true }).pipe(
      tap((updated) => {
        const cur = this.currentUser();
        if (cur) {
          this.currentUser.set({
            ...cur,
            fullName: updated.fullName,
            companyName: updated.companyName,
          });
        }
      })
    );
  }

  /**
   * Change current password
   */
  changePassword(payload: ChangePasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/change-password`, payload, { withCredentials: true });
  }

  /**
   * Set password for accounts created via social login without a password
   */
  setPassword(payload: SetPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/set-password`, payload, { withCredentials: true });
  }

  /**
   * Request email address change
   */
  requestEmailChange(payload: ChangeEmailRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/change-email-request`, payload, { withCredentials: true });
  }

  /**
   * Confirm email address change
   */
  confirmChangeEmail(payload: ConfirmChangeEmailRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/confirm-change-email`, payload, { withCredentials: true });
  }

  /**
   * Get linked social accounts
   */
  getLinkedAccounts(): Observable<LinkedAccount[]> {
    return this.http.get<LinkedAccount[]>(`${this.accountUrl}/social-accounts`, { withCredentials: true });
  }

  /**
   * Link a new social account
   */
  linkSocialAccount(payload: SocialLoginRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/social-accounts/link`, payload, { withCredentials: true });
  }

  /**
   * Unlink a social account
   */
  unlinkSocialAccount(provider: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/social-accounts/unlink`, { provider }, { withCredentials: true });
  }

  /**
   * Log out from all active devices and sessions
   */
  logoutAll(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/logout-all`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.token.set(null);
        this.currentUser.set(null);
        this.router.navigate(['/login']);
      })
    );
  }

  /**
   * Delete account permanently
   */
  deleteAccount(payload: DeleteAccountRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/delete`, payload, { withCredentials: true }).pipe(
      tap(() => {
        this.token.set(null);
        this.currentUser.set(null);
        this.router.navigate(['/login']);
      })
    );
  }

  /**
   * Exchange the HttpOnly refresh token cookie for a new access token and rotated refresh token
   */
  refreshToken(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/refresh`, {}, { withCredentials: true })
      .pipe(tap((response) => this.handleAuthSuccess(response)));
  }

  /**
   * Perform silent refresh on app startup to rehydrate in-memory session if a valid refresh cookie exists
   */
  initAuth(): Observable<AuthResponse | null> {
    return this.refreshToken().pipe(
      catchError(() => {
        this.token.set(null);
        this.currentUser.set(null);
        return of(null);
      })
    );
  }

  /**
   * Process and store authentication state strictly in memory
   */
  handleAuthSuccess(response: AuthResponse): void {
    const user: AuthUser = {
      userId: response.userId,
      email: response.email,
      fullName: response.fullName,
      role: response.role,
      companyName: response.companyName,
      isEmailConfirmed: response.isEmailConfirmed,
    };

    this.token.set(response.accessToken);
    this.currentUser.set(user);
  }

  /**
   * Redirect user based on role:
   * Candidate -> /jobs
   * Employer -> /employer/jobs
   */
  redirectAfterAuth(role: UserRole): Promise<boolean> {
    if (role === 'Employer') {
      return this.router.navigate(['/employer/jobs']);
    }
    return this.router.navigate(['/jobs']);
  }

  /**
   * Logout user: revoke token on backend, clear HttpOnly cookie, clear in-memory state and redirect to /login
   */
  logout(): void {
    this.http
      .post(`${this.baseUrl}/logout`, {}, { withCredentials: true })
      .pipe(catchError(() => of(null)))
      .subscribe();

    this.token.set(null);
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.token();
  }

  getRole(): UserRole | null {
    return this.role();
  }

  getUserId(): string | null {
    return this.currentUser()?.userId ?? null;
  }
}
