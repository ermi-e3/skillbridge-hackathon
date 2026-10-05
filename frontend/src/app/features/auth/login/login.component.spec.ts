import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthResponse } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceSpy: {
    login: ReturnType<typeof vi.fn>;
    redirectAfterAuth: ReturnType<typeof vi.fn>;
  };

  const mockSuccessResponse: AuthResponse = {
    accessToken: 'test-token',
    expiresAt: '2026-10-10T17:14:00Z',
    userId: 'user-1',
    email: 'user@hackathon.local',
    fullName: 'Test User',
    role: 'Candidate',
  };

  beforeEach(async () => {
    authServiceSpy = {
      login: vi.fn().mockReturnValue(of(mockSuccessResponse)),
      redirectAfterAuth: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with an invalid form', () => {
    expect(component.loginForm.valid).toBe(false);
    expect(component.emailControl?.valid).toBe(false);
    expect(component.passwordControl?.valid).toBe(false);
  });

  it('should validate email format', () => {
    component.emailControl?.setValue('invalid-email');
    expect(component.emailControl?.hasError('email')).toBe(true);

    component.emailControl?.setValue('valid@example.com');
    expect(component.emailControl?.hasError('email')).toBe(false);
  });

  it('should disable submit button when form is invalid', () => {
    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(submitBtn.disabled).toBe(true);

    component.loginForm.setValue({
      email: 'valid@example.com',
      password: 'Password123',
    });
    fixture.detectChanges();

    expect(submitBtn.disabled).toBe(false);
  });

  it('should call authService.login and redirect on valid submission', () => {
    component.loginForm.setValue({
      email: 'user@hackathon.local',
      password: 'User123!',
    });

    component.onSubmit();

    expect(authServiceSpy.login).toHaveBeenCalledWith({
      email: 'user@hackathon.local',
      password: 'User123!',
    });
    expect(authServiceSpy.redirectAfterAuth).toHaveBeenCalledWith('Candidate');
    expect(component.errorMessage()).toBeNull();
  });

  it('should display dismissible error alert banner on 401 error', () => {
    const errorResponse = new HttpErrorResponse({
      status: 401,
      error: {
        code: 'auth.invalid_credentials',
        detail: 'Invalid email or password.',
      },
    });
    authServiceSpy.login.mockReturnValue(throwError(() => errorResponse));

    component.loginForm.setValue({
      email: 'user@hackathon.local',
      password: 'WrongPassword',
    });

    component.onSubmit();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('Invalid email or password.');

    const alertBanner = fixture.nativeElement.querySelector('.alert-banner');
    expect(alertBanner).toBeTruthy();
    expect(alertBanner.textContent).toContain('Invalid email or password.');

    // Dismiss banner
    component.dismissAlert();
    fixture.detectChanges();

    expect(component.errorMessage()).toBeNull();
    expect(fixture.nativeElement.querySelector('.alert-banner')).toBeNull();
  });
});
