import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest, UserRole } from '../models/auth.models';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

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
