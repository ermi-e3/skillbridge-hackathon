import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { AuthService } from '../../../core/services/auth.service';
import { ResetPasswordComponent } from './reset-password.component';

describe('ResetPasswordComponent', () => {
  let component: ResetPasswordComponent;
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let authServiceSpy: {
    resetPassword: ReturnType<typeof vi.fn>;
  };

  const mockQueryParamMap = {
    get: (key: string) => {
      if (key === 'email') return 'test@example.com';
      if (key === 'token') return 'valid-reset-token-123';
      return null;
    },
  };

  beforeEach(async () => {
    authServiceSpy = {
      resetPassword: vi.fn().mockReturnValue(of({ message: 'Password reset successful.' })),
    };

    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: mockQueryParamMap,
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ResetPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize and extract email and token from query parameters', () => {
    expect(component).toBeTruthy();
    expect(component.email).toBe('test@example.com');
    expect(component.token).toBe('valid-reset-token-123');
    expect(component.errorMessage()).toBeNull();
  });

  it('should toggle password visibility on togglePassword()', () => {
    expect(component.showPassword()).toBe(false);
    component.togglePassword();
    expect(component.showPassword()).toBe(true);
    component.togglePassword();
    expect(component.showPassword()).toBe(false);
  });

  it('should dynamically evaluate the 3-pill password checklist rules', () => {
    component.form.patchValue({ newPassword: 'short' });
    expect(component.hasMinLength()).toBe(false);
    expect(component.hasUppercase()).toBe(false);
    expect(component.hasDigit()).toBe(false);

    component.form.patchValue({ newPassword: 'longenoughpassword' });
    expect(component.hasMinLength()).toBe(true);
    expect(component.hasUppercase()).toBe(false);
    expect(component.hasDigit()).toBe(false);

    component.form.patchValue({ newPassword: 'LongEnoughPassword' });
    expect(component.hasMinLength()).toBe(true);
    expect(component.hasUppercase()).toBe(true);
    expect(component.hasDigit()).toBe(false);

    component.form.patchValue({ newPassword: 'LongEnoughPassword1' });
    expect(component.hasMinLength()).toBe(true);
    expect(component.hasUppercase()).toBe(true);
    expect(component.hasDigit()).toBe(true);
  });

  it('should detect matching and mismatched passwords', () => {
    component.form.patchValue({
      newPassword: 'SecurePassword123!',
      confirmPassword: 'DifferentPassword123!',
    });
    expect(component.isMatching()).toBe(false);
    expect(component.isMismatch()).toBe(true);

    component.form.patchValue({
      confirmPassword: 'SecurePassword123!',
    });
    expect(component.isMatching()).toBe(true);
    expect(component.isMismatch()).toBe(false);
  });

  it('should call authService.resetPassword and show success on valid submit', () => {
    component.form.patchValue({
      newPassword: 'SecurePassword123!',
      confirmPassword: 'SecurePassword123!',
    });

    component.onSubmit();

    expect(authServiceSpy.resetPassword).toHaveBeenCalledWith({
      email: 'test@example.com',
      token: 'valid-reset-token-123',
      newPassword: 'SecurePassword123!',
    });
    expect(component.isSuccess()).toBe(true);
    expect(component.isSubmitting()).toBe(false);
  });

  it('should handle errors on submission failure', () => {
    authServiceSpy.resetPassword.mockReturnValueOnce(
      throwError(() => new HttpErrorResponse({
        error: { detail: 'The reset link has expired.' },
        status: 400,
      }))
    );

    component.form.patchValue({
      newPassword: 'SecurePassword123!',
      confirmPassword: 'SecurePassword123!',
    });

    component.onSubmit();

    expect(component.isSuccess()).toBe(false);
    expect(component.isSubmitting()).toBe(false);
    expect(component.errorMessage()).toBe('The reset link has expired.');
  });
});
