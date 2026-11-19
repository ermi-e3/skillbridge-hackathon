import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';
import { ThemeToggleComponent } from '../../../shared/components/theme-toggle/theme-toggle.component';

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('newPassword')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password && confirmPassword && password !== confirmPassword
    ? { passwordMismatch: true }
    : null;
}

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, ThemeToggleComponent],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss',
})
export class ResetPasswordComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  email = '';
  token = '';

  readonly isSubmitting = signal<boolean>(false);
  readonly isSuccess = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showPassword = signal<boolean>(false);
  readonly isPasswordFocused = signal<boolean>(false);
  readonly isConfirmFocused = signal<boolean>(false);

  readonly form: FormGroup = this.fb.group(
    {
      newPassword: [
        '',
        [
          Validators.required,
          Validators.minLength(8),
          Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/),
        ],
      ],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordMatchValidator }
  );

  get newPasswordControl() {
    return this.form.get('newPassword');
  }

  get confirmPasswordControl() {
    return this.form.get('confirmPassword');
  }

  // Live rule checklist evaluations
  hasMinLength(): boolean {
    const val = this.newPasswordControl?.value || '';
    return val.length >= 8;
  }

  hasUppercase(): boolean {
    const val = this.newPasswordControl?.value || '';
    return /[A-Z]/.test(val);
  }

  hasDigit(): boolean {
    const val = this.newPasswordControl?.value || '';
    return /\d/.test(val);
  }

  isMatching(): boolean {
    const p1 = this.newPasswordControl?.value || '';
    const p2 = this.confirmPasswordControl?.value || '';
    return Boolean(p1 && p2 && p1 === p2);
  }

  isMismatch(): boolean {
    const p1 = this.newPasswordControl?.value || '';
    const p2 = this.confirmPasswordControl?.value || '';
    return Boolean(p2 && p1 !== p2);
  }

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') || '';
    this.token = this.route.snapshot.queryParamMap.get('token') || '';

    if (!this.email || !this.token) {
      this.errorMessage.set('Missing reset token or email address. Please click the link in your email.');
    }
  }

  togglePassword(): void {
    this.showPassword.update((v) => !v);
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  onSubmit(): void {
    if (this.form.invalid || this.isSubmitting() || !this.email || !this.token) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const payload = {
      email: this.email,
      token: this.token,
      newPassword: this.form.value.newPassword,
    };

    this.authService.resetPassword(payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.isSuccess.set(true);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to reset password. The link may have expired.'));
      },
    });
  }
}
