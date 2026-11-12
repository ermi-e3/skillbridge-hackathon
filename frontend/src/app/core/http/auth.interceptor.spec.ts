import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { authInterceptor, resetAuthStateForTesting } from './auth.interceptor';
import { AuthService } from '../services/auth.service';
import { AuthResponse } from '../models/auth.models';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authServiceSpy: {
    getToken: ReturnType<typeof vi.fn>;
    refreshToken: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let routerSpy: { navigate: ReturnType<typeof vi.fn> };

  const mockAuthResponse: AuthResponse = {
    accessToken: 'new-access-token-123',
    role: 'Candidate',
    userId: 'user-1',
    email: 'test@example.com',
    fullName: 'Test User',
    expiresAt: '2026-11-06T23:00:00Z',
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    resetAuthStateForTesting();

    authServiceSpy = {
      getToken: vi.fn(),
      refreshToken: vi.fn(),
      logout: vi.fn(),
    };
    routerSpy = { navigate: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authServiceSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    resetAuthStateForTesting();
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;';
  });

  it('should add Authorization header and withCredentials when token is present', () => {
    authServiceSpy.getToken.mockReturnValue('initial-access-token');

    http.get('/api/test-data').subscribe();

    const req = httpMock.expectOne('/api/test-data');
    expect(req.request.headers.get('Authorization')).toBe('Bearer initial-access-token');
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('should not add Authorization header to auth endpoints', () => {
    authServiceSpy.getToken.mockReturnValue('initial-access-token');

    http.post('/api/auth/login', {}).subscribe();

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.withCredentials).toBe(true);
    req.flush({});
  });

  it('should attach X-XSRF-TOKEN header when XSRF-TOKEN cookie is set', () => {
    authServiceSpy.getToken.mockReturnValue(null);
    document.cookie = 'XSRF-TOKEN=csrf-secret-value; path=/;';

    http.get('/api/test-csrf').subscribe();

    const req = httpMock.expectOne('/api/test-csrf');
    expect(req.request.headers.get('X-XSRF-TOKEN')).toBe('csrf-secret-value');
    req.flush({});
  });

  it('should intercept 401, call refreshToken, and retry original request with new token', () => {
    authServiceSpy.getToken.mockReturnValue('old-expired-token');
    authServiceSpy.refreshToken.mockReturnValue(of(mockAuthResponse));

    let responseData: any = null;
    http.get('/api/protected').subscribe((res) => {
      responseData = res;
    });

    // Initial request fails with 401
    const req1 = httpMock.expectOne('/api/protected');
    expect(req1.request.headers.get('Authorization')).toBe('Bearer old-expired-token');
    req1.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authServiceSpy.refreshToken).toHaveBeenCalledTimes(1);

    // Retried request should have new access token
    const req2 = httpMock.expectOne('/api/protected');
    expect(req2.request.headers.get('Authorization')).toBe('Bearer new-access-token-123');
    req2.flush({ success: true });

    expect(responseData).toEqual({ success: true });
  });

  it('should handle concurrent 401 requests by calling refreshToken only once', () => {
    authServiceSpy.getToken.mockReturnValue('old-expired-token');
    const refreshSubject = new Subject<AuthResponse>();
    authServiceSpy.refreshToken.mockReturnValue(refreshSubject.asObservable());

    let res1: any = null;
    let res2: any = null;

    http.get('/api/req1').subscribe((res) => (res1 = res));
    http.get('/api/req2').subscribe((res) => (res2 = res));

    // First request fails with 401 -> triggers refresh
    const r1 = httpMock.expectOne('/api/req1');
    r1.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    // Second request fails with 401 while refresh is in-flight -> queues behind refreshTokenSubject
    const r2 = httpMock.expectOne('/api/req2');
    r2.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authServiceSpy.refreshToken).toHaveBeenCalledTimes(1);

    // Now resolve the in-flight refresh call
    refreshSubject.next(mockAuthResponse);
    refreshSubject.complete();

    // Both should retry with new token
    const retry1 = httpMock.expectOne('/api/req1');
    expect(retry1.request.headers.get('Authorization')).toBe('Bearer new-access-token-123');
    retry1.flush({ id: 1 });

    const retry2 = httpMock.expectOne('/api/req2');
    expect(retry2.request.headers.get('Authorization')).toBe('Bearer new-access-token-123');
    retry2.flush({ id: 2 });

    expect(res1).toEqual({ id: 1 });
    expect(res2).toEqual({ id: 2 });
  });

  it('should logout and redirect to /login when refreshToken fails on 401', () => {
    authServiceSpy.getToken.mockReturnValue('invalid-token');
    authServiceSpy.refreshToken.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 401, statusText: 'Invalid refresh token' }))
    );

    let errorReceived: any = null;
    http.get('/api/protected-failed').subscribe({
      next: () => {},
      error: (err) => {
        errorReceived = err;
      },
    });

    const req = httpMock.expectOne('/api/protected-failed');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authServiceSpy.logout).toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
    expect(errorReceived).toBeTruthy();
  });
});
