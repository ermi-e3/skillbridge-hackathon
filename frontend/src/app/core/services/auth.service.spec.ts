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
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should post login request and store session in localStorage', () => {
    const creds: LoginRequest = { email: 'user@hackathon.local', password: 'Password1' };

    service.login(creds).subscribe((res) => {
      expect(res).toEqual(mockResponse);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(creds);
    req.flush(mockResponse);

    expect(localStorage.getItem('accessToken')).toBe(mockResponse.accessToken);
    expect(localStorage.getItem('role')).toBe('Candidate');
    expect(localStorage.getItem('userId')).toBe(mockResponse.userId);
    expect(service.getToken()).toBe(mockResponse.accessToken);
    expect(service.getRole()).toBe('Candidate');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should post register request and store session in localStorage', () => {
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
    req.flush(mockResponse);

    expect(localStorage.getItem('accessToken')).toBe(mockResponse.accessToken);
    expect(localStorage.getItem('userId')).toBe(mockResponse.userId);
  });

  it('should redirect Candidate to /jobs', () => {
    service.redirectAfterAuth('Candidate');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/jobs']);
  });

  it('should redirect Employer to /employer/jobs', () => {
    service.redirectAfterAuth('Employer');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/employer/jobs']);
  });

  it('should clear localStorage and navigate to /login on logout', () => {
    service.saveSession(mockResponse);
    expect(localStorage.getItem('accessToken')).toBe('test-jwt-token');

    service.logout();
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('role')).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
  });
});
