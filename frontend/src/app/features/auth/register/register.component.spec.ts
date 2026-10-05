import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthResponse } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { RegisterComponent } from './register.component';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let authServiceSpy: {
    register: ReturnType<typeof vi.fn>;
    redirectAfterAuth: ReturnType<typeof vi.fn>;
  };

  const mockSuccessResponse: AuthResponse = {
    accessToken: 'test-token',
    expiresAt: '2026-10-10T17:14:00Z',
    userId: 'user-cand-1',
    email: 'hana@example.com',
    fullName: 'Hana Tesfaye',
    role: 'Candidate',
    companyName: null,
  };

  beforeEach(async () => {
    authServiceSpy = {
      register: vi.fn().mockReturnValue(of(mockSuccessResponse)),
      redirectAfterAuth: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with Candidate role selected and companyName not required', () => {
    expect(component.selectedRole()).toBe('Candidate');
    expect(component.companyNameControl?.validator).toBeNull();
  });

  it('should require companyName when role is switched to Employer', () => {
    component.setRole('Employer');
    fixture.detectChanges();

    expect(component.selectedRole()).toBe('Employer');
    expect(component.isEmployer).toBe(true);
    expect(component.companyNameControl?.hasError('required')).toBe(true);

    component.companyNameControl?.setValue('Abyssinia Tech');
    expect(component.companyNameControl?.valid).toBe(true);

    // Switch back to Candidate
    component.setRole('Candidate');
    fixture.detectChanges();

    expect(component.selectedRole()).toBe('Candidate');
    expect(component.isEmployer).toBe(false);
    expect(component.companyNameControl?.validator).toBeNull();
  });

  it('should validate password complexity (8+ chars, upper, lower, digit)', () => {
    const pw = component.passwordControl;

    pw?.setValue('short');
    expect(pw?.hasError('minLength')).toBe(true);

    pw?.setValue('alllowercase1');
    expect(pw?.hasError('requiresUpper')).toBe(true);

    pw?.setValue('ALLUPPERCASE1');
    expect(pw?.hasError('requiresLower')).toBe(true);

    pw?.setValue('NoNumbersHere');
    expect(pw?.hasError('requiresNumber')).toBe(true);

    pw?.setValue('ValidPass1');
    expect(pw?.valid).toBe(true);
  });

  it('should disable submit button when form is invalid', () => {
    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(submitBtn.disabled).toBe(true);

    component.registerForm.patchValue({
      fullName: 'Hana Tesfaye',
      email: 'hana@example.com',
      password: 'SecurePass1',
    });
    fixture.detectChanges();

    expect(submitBtn.disabled).toBe(false);
  });

  it('should call authService.register and redirect on successful submit', () => {
    component.registerForm.patchValue({
      role: 'Candidate',
      fullName: 'Hana Tesfaye',
      email: 'hana@example.com',
      password: 'SecurePass1',
    });

    component.onSubmit();

    expect(authServiceSpy.register).toHaveBeenCalledWith({
      fullName: 'Hana Tesfaye',
      email: 'hana@example.com',
      password: 'SecurePass1',
      role: 'Candidate',
      companyName: null,
    });
    expect(authServiceSpy.redirectAfterAuth).toHaveBeenCalledWith('Candidate');
  });

  it('should display dismissible red alert banner when registration returns 400 error', () => {
    const errorResponse = new HttpErrorResponse({
      status: 400,
      error: {
        code: 'auth.email_taken',
        detail: 'This email is already registered.',
      },
    });
    authServiceSpy.register.mockReturnValue(throwError(() => errorResponse));

    component.registerForm.patchValue({
      role: 'Candidate',
      fullName: 'Hana Tesfaye',
      email: 'hana@example.com',
      password: 'SecurePass1',
    });

    component.onSubmit();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('This email is already registered.');

    const banner = fixture.nativeElement.querySelector('.alert-banner');
    expect(banner).toBeTruthy();
    expect(banner.textContent).toContain('This email is already registered.');

    // Dismiss banner
    component.dismissAlert();
    fixture.detectChanges();

    expect(component.errorMessage()).toBeNull();
    expect(fixture.nativeElement.querySelector('.alert-banner')).toBeNull();
  });
});
