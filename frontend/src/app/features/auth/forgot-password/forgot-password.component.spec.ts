import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { AuthService } from '../../../core/services/auth.service';
import { ForgotPasswordComponent } from './forgot-password.component';

describe('ForgotPasswordComponent', () => {
  let component: ForgotPasswordComponent;
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let authServiceSpy: {
    forgotPassword: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    authServiceSpy = {
      forgotPassword: vi.fn().mockReturnValue(of({ message: 'Reset instructions sent.' })),
    };

    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with an empty, invalid form', () => {
    expect(component).toBeTruthy();
    expect(component.form.valid).toBe(false);
    expect(component.isSubmitted()).toBe(false);
    expect(component.isSubmitting()).toBe(false);
  });

  it('should validate email format correctly', () => {
    const emailControl = component.emailControl;
    emailControl?.setValue('');
    expect(emailControl?.valid).toBe(false);

    emailControl?.setValue('invalid-email-string');
    expect(emailControl?.valid).toBe(false);

    emailControl?.setValue('valid.user@example.com');
    expect(emailControl?.valid).toBe(true);
    expect(component.form.valid).toBe(true);
  });

  it('should submit valid email, call authService, and transition to confirmation state', () => {
    component.form.patchValue({ email: 'developer@example.com' });
    component.onSubmit();

    expect(authServiceSpy.forgotPassword).toHaveBeenCalledWith({ email: 'developer@example.com' });
    expect(component.isSubmitted()).toBe(true);
    expect(component.submittedEmail()).toBe('developer@example.com');
    expect(component.isSubmitting()).toBe(false);
  });

  it('should protect against enumeration and show confirmation even if service errors', () => {
    authServiceSpy.forgotPassword.mockReturnValueOnce(
      throwError(() => new Error('Service unavailable'))
    );

    component.form.patchValue({ email: 'unknown@example.com' });
    component.onSubmit();

    expect(component.isSubmitted()).toBe(true);
    expect(component.submittedEmail()).toBe('unknown@example.com');
    expect(component.isSubmitting()).toBe(false);
  });

  it('should dismiss alert when dismissAlert() is triggered', () => {
    component.errorMessage.set('Test error banner message');
    expect(component.errorMessage()).toBe('Test error banner message');

    component.dismissAlert();
    expect(component.errorMessage()).toBeNull();
  });
});

