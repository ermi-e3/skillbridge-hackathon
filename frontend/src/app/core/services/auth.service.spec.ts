import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest } from '../models/auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;
  let routerSpy: { navigate: ReturnType<typeof vi.fn> };

  const mockResponse: AuthResponse = {
    accessToken: 'test-jwt-token',
    expiresAt: '2026-10-10T17:14:00Z',
    userId: 'user-guid-1234',
    email: 'user@hackathon.local',
    fullName: 'Demo Candidate',
    role: 'Candidate',
    companyName: null,
  };

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    routerSpy = { navigate: vi.fn().mockResolvedValue(true) };

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
      ],
    });

    service = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should post login request with credentials and store access token in memory only', () => {
    const creds: LoginRequest = { email: 'user@hackathon.local', password: 'Password1' };

    service.login(creds).subscribe((res) => {
      expect(res).toEqual(mockResponse);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(creds);
    expect(req.request.withCredentials).toBe(true);
    req.flush(mockResponse);

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(sessionStorage.getItem('accessToken')).toBeNull();
    expect(service.getToken()).toBe(mockResponse.accessToken);
    expect(service.getRole()).toBe('Candidate');
    expect(service.isAuthenticated()).toBe(true);
    expect(service.getUserId()).toBe(mockResponse.userId);
  });

  it('should post register request with credentials and store session in memory only', () => {
    const payload: RegisterRequest = {
      email: 'hana@example.com',
      password: 'Password1',
      fullName: 'Hana Tesfaye',
      role: 'Candidate',
      companyName: null,
    };

    service.register(payload).subscribe((res) => {
      expect(res).toEqual(mockResponse);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/register`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    expect(req.request.withCredentials).toBe(true);
    req.flush(mockResponse);

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(sessionStorage.getItem('accessToken')).toBeNull();
    expect(service.getToken()).toBe(mockResponse.accessToken);
  });

  it('should call refresh token endpoint and update in-memory session', () => {
    const rotatedResponse: AuthResponse = {
      ...mockResponse,
      accessToken: 'new-rotated-token',
    };

    service.refreshToken().subscribe((res) => {
      expect(res).toEqual(rotatedResponse);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/refresh`);
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush(rotatedResponse);

    expect(service.getToken()).toBe('new-rotated-token');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should redirect Candidate to /jobs', () => {
    service.redirectAfterAuth('Candidate');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/jobs']);
  });

  it('should redirect Employer to /employer/jobs', () => {
    service.redirectAfterAuth('Employer');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/employer/jobs']);
  });

  it('should clear in-memory state, call logout endpoint, and navigate to /login on logout', () => {
    service.handleAuthSuccess(mockResponse);
    expect(service.isAuthenticated()).toBe(true);

    service.logout();

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/logout`);
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush(null);

    expect(service.getToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('should post social login request and update session', () => {
    service.socialLogin({ provider: 'Google', idToken: 'google-token' }).subscribe((res) => {
      expect(res).toEqual(mockResponse);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/social-login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ provider: 'Google', idToken: 'google-token' });
    req.flush(mockResponse);

    expect(service.getToken()).toBe(mockResponse.accessToken);
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should post email confirmation request', () => {
    service.confirmEmail({ userId: 'u1', token: 'tok1' }).subscribe((res) => {
      expect(res.message).toBe('Email confirmed');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/confirm-email`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'u1', token: 'tok1' });
    req.flush({ message: 'Email confirmed' });
  });

  it('should post forgot password request', () => {
    service.forgotPassword({ email: 'user@example.com' }).subscribe((res) => {
      expect(res.message).toBe('Link sent');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/forgot-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'user@example.com' });
    req.flush({ message: 'Link sent' });
  });

  it('should post reset password request', () => {
    service.resetPassword({ email: 'user@example.com', token: 'tok', newPassword: 'NewPassword1!' }).subscribe((res) => {
      expect(res.message).toBe('Password reset');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/reset-password`);
    expect(req.request.method).toBe('POST');
    req.flush({ message: 'Password reset' });
  });

  it('should call logout-all and clear session', () => {
    service.handleAuthSuccess(mockResponse);
    expect(service.isAuthenticated()).toBe(true);

    service.logoutAll().subscribe();

    const req = httpTesting.expectOne(`${environment.apiUrl}/account/logout-all`);
    expect(req.request.method).toBe('POST');
    req.flush({ message: 'Logged out all' });

    expect(service.getToken()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
  });
});
