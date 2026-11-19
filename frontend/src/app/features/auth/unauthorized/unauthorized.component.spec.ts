import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { AuthUser } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { UnauthorizedComponent } from './unauthorized.component';

describe('UnauthorizedComponent', () => {
  let router: Router;
  let authServiceSpy: {
    isAuthenticated: ReturnType<typeof vi.fn>;
    currentUser: ReturnType<typeof vi.fn>;
    getRole: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };

  const mockUser: AuthUser = {
    userId: 'user-emp-1',
    email: 'employer@hackathon.local',
    fullName: 'Demo Employer',
    role: 'Employer',
    companyName: 'SkillBridge Enterprise',
  };

  async function createComponent(role: 'Employer' | 'Candidate' | null = 'Employer', isAuth = true): Promise<ComponentFixture<UnauthorizedComponent>> {
    authServiceSpy.isAuthenticated.mockReturnValue(isAuth);
    authServiceSpy.getRole.mockReturnValue(role);
    authServiceSpy.currentUser.mockReturnValue(isAuth ? mockUser : null);

    const fixture = TestBed.createComponent(UnauthorizedComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  beforeEach(async () => {
    authServiceSpy = {
      isAuthenticated: vi.fn().mockReturnValue(true),
      currentUser: vi.fn().mockReturnValue(mockUser),
      getRole: vi.fn().mockReturnValue('Employer'),
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [UnauthorizedComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate');
  });

  it('should initialize and display the 403 Access Denied layout', async () => {
    const fixture = await createComponent('Employer', true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.auth-title')?.textContent).toContain('Access Denied');
    expect(compiled.querySelector('.error-eyebrow-pill')?.textContent).toContain('ERROR 403 // RESTRICTED');
  });

  it('should display the signed in user session info when authenticated', async () => {
    const fixture = await createComponent('Employer', true);
    const compiled = fixture.nativeElement as HTMLElement;
    const sessionBanner = compiled.querySelector('.user-session-banner');
    expect(sessionBanner).toBeTruthy();
    expect(sessionBanner?.textContent).toContain('employer@hackathon.local');
    expect(sessionBanner?.textContent).toContain('Employer');
  });

  it('should display link to Employer Portal when authenticated as Employer', async () => {
    const fixture = await createComponent('Employer', true);
    const compiled = fixture.nativeElement as HTMLElement;
    const primaryBtn = compiled.querySelector('.action-btn-primary');
    expect(primaryBtn?.textContent).toContain('Go to Employer Portal');
    expect(primaryBtn?.getAttribute('href')).toBe('/employer/jobs');
  });

  it('should display link to Candidate Portal when authenticated as Candidate', async () => {
    const fixture = await createComponent('Candidate', true);
    const compiled = fixture.nativeElement as HTMLElement;
    const primaryBtn = compiled.querySelector('.action-btn-primary');
    expect(primaryBtn?.textContent).toContain('Go to Candidate Portal');
    expect(primaryBtn?.getAttribute('href')).toBe('/jobs');
  });

  it('should display link to Sign In when not authenticated', async () => {
    const fixture = await createComponent(null, false);
    const compiled = fixture.nativeElement as HTMLElement;
    const primaryBtn = compiled.querySelector('.action-btn-primary');
    expect(primaryBtn?.textContent).toContain('Sign In');
    expect(primaryBtn?.getAttribute('href')).toBe('/login');
  });

  it('should call authService.logout and navigate to /login when switchAccount() is clicked', async () => {
    const fixture = await createComponent('Employer', true);
    fixture.componentInstance.switchAccount();
    expect(authServiceSpy.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});
