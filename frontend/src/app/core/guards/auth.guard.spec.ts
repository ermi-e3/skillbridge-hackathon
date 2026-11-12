import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { authGuard, roleGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('Route Guards', () => {
  let authServiceSpy: {
    isAuthenticated: ReturnType<typeof vi.fn>;
    getRole: ReturnType<typeof vi.fn>;
  };
  let router: Router;

  beforeEach(() => {
    authServiceSpy = {
      isAuthenticated: vi.fn(),
      getRole: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authServiceSpy },
        {
          provide: Router,
          useValue: {
            createUrlTree: (commands: any[]) => ({ path: commands.join('/') } as unknown as UrlTree),
          },
        },
      ],
    });

    router = TestBed.inject(Router);
  });

  describe('authGuard', () => {
    it('should allow navigation when user is authenticated', () => {
      authServiceSpy.isAuthenticated.mockReturnValue(true);

      const result = TestBed.runInInjectionContext(() =>
        authGuard({} as any, {} as any)
      );

      expect(result).toBe(true);
    });

    it('should redirect to /login when user is not authenticated', () => {
      authServiceSpy.isAuthenticated.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        authGuard({} as any, {} as any)
      ) as UrlTree;

      expect(result).toEqual({ path: '/login' } as unknown as UrlTree);
    });
  });

  describe('roleGuard', () => {
    it('should allow access when user is authenticated and has required role', () => {
      authServiceSpy.isAuthenticated.mockReturnValue(true);
      authServiceSpy.getRole.mockReturnValue('Candidate');

      const guard = roleGuard('Candidate');
      const result = TestBed.runInInjectionContext(() =>
        guard({} as any, {} as any)
      );

      expect(result).toBe(true);
    });

    it('should redirect to /login when user is not authenticated', () => {
      authServiceSpy.isAuthenticated.mockReturnValue(false);
      authServiceSpy.getRole.mockReturnValue(null);

      const guard = roleGuard('Employer');
      const result = TestBed.runInInjectionContext(() =>
        guard({} as any, {} as any)
      ) as UrlTree;

      expect(result).toEqual({ path: '/login' } as unknown as UrlTree);
    });

    it('should redirect unauthorized users to /unauthorized on role mismatch', () => {
      authServiceSpy.isAuthenticated.mockReturnValue(true);
      authServiceSpy.getRole.mockReturnValue('Candidate');

      // Candidate trying to access Employer route
      const employerGuard = roleGuard('Employer');
      const result = TestBed.runInInjectionContext(() =>
        employerGuard({} as any, {} as any)
      ) as UrlTree;

      expect(result).toEqual({ path: '/unauthorized' } as unknown as UrlTree);
    });

    it('should redirect employer to /unauthorized when accessing candidate-only route', () => {
      authServiceSpy.isAuthenticated.mockReturnValue(true);
      authServiceSpy.getRole.mockReturnValue('Employer');

      const candidateGuard = roleGuard('Candidate');
      const result = TestBed.runInInjectionContext(() =>
        candidateGuard({} as any, {} as any)
      ) as UrlTree;

      expect(result).toEqual({ path: '/unauthorized' } as unknown as UrlTree);
    });
  });
});

