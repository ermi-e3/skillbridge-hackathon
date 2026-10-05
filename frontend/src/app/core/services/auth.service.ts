import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest, UserRole } from '../models/auth.models';

const STORAGE_KEYS = {
  TOKEN: 'accessToken',
  ROLE: 'role',
  USER_ID: 'userId',
  FULL_NAME: 'fullName',
  EMAIL: 'email',
  EXPIRES_AT: 'expiresAt',
} as const;

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  // Signals for reactive state
  readonly token = signal<string | null>(this.getStoredToken());
  readonly currentUser = signal<AuthUser | null>(this.getStoredUser());
  readonly role = computed<UserRole | null>(() => this.currentUser()?.role ?? null);
  readonly isAuthenticated = computed<boolean>(() => !!this.token());

  /**
   * Log in with email and password
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, credentials).pipe(
      tap((response) => this.handleAuthSuccess(response))
    );
  }

  /**
   * Register a new Candidate or Employer
   */
  register(payload: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/register`, payload).pipe(
      tap((response) => this.handleAuthSuccess(response))
    );
  }

  /**
   * Process and store authentication state in localStorage and signals
   */
  handleAuthSuccess(response: AuthResponse): void {
    this.saveSession(response);
  }

  /**
   * Store received JWT token, role, and userId via AuthService into localStorage
   */
  saveSession(response: AuthResponse): void {
    try {
      localStorage.setItem(STORAGE_KEYS.TOKEN, response.accessToken);
      localStorage.setItem(STORAGE_KEYS.ROLE, response.role);
      localStorage.setItem(STORAGE_KEYS.USER_ID, response.userId);
      localStorage.setItem(STORAGE_KEYS.EMAIL, response.email);
      localStorage.setItem(STORAGE_KEYS.FULL_NAME, response.fullName);
      localStorage.setItem(STORAGE_KEYS.EXPIRES_AT, response.expiresAt);
    } catch (e) {
      console.error('Failed to save auth session to localStorage', e);
    }

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
   * Logout user, clear localStorage and redirect to /login
   */
  logout(): void {
    try {
      localStorage.removeItem(STORAGE_KEYS.TOKEN);
      localStorage.removeItem(STORAGE_KEYS.ROLE);
      localStorage.removeItem(STORAGE_KEYS.USER_ID);
      localStorage.removeItem(STORAGE_KEYS.EMAIL);
      localStorage.removeItem(STORAGE_KEYS.FULL_NAME);
      localStorage.removeItem(STORAGE_KEYS.EXPIRES_AT);
    } catch (e) {
      console.error('Failed to clear auth session from localStorage', e);
    }

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
    return this.currentUser()?.userId ?? this.getStoredUserId();
  }

  private getStoredToken(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEYS.TOKEN);
    } catch {
      return null;
    }
  }

  private getStoredUserId(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEYS.USER_ID);
    } catch {
      return null;
    }
  }

  private getStoredUser(): AuthUser | null {
    try {
      const token = localStorage.getItem(STORAGE_KEYS.TOKEN);
      const userId = localStorage.getItem(STORAGE_KEYS.USER_ID);
      const email = localStorage.getItem(STORAGE_KEYS.EMAIL);
      const fullName = localStorage.getItem(STORAGE_KEYS.FULL_NAME);
      const role = localStorage.getItem(STORAGE_KEYS.ROLE) as UserRole | null;

      if (token && userId && email && fullName && (role === 'Candidate' || role === 'Employer')) {
        return {
          userId,
          email,
          fullName,
          role,
        };
      }
      return null;
    } catch {
      return null;
    }
  }
}
