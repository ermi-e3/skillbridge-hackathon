import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { UserRole } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

/**
 * Validates that the password has at least:
 * - 8 characters
 * - 1 uppercase letter
 * - 1 lowercase letter
 * - 1 number
 */
export const passwordComplexityValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;
  if (!value) {
    return null; // Let 'required' validator handle empty state
  }

  const hasMinLength = value.length >= 8;
  const hasUpper = /[A-Z]/.test(value);
  const hasLower = /[a-z]/.test(value);
  const hasNumber = /\d/.test(value);

  const errors: ValidationErrors = {};

  if (!hasMinLength) {
    errors['minLength'] = { requiredLength: 8, actualLength: value.length };
  }
  if (!hasUpper) {
    errors['requiresUpper'] = true;
  }
  if (!hasLower) {
    errors['requiresLower'] = true;
  }
  if (!hasNumber) {
    errors['requiresNumber'] = true;
  }

  return Object.keys(errors).length > 0 ? errors : null;
};

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);

  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showPassword = signal<boolean>(false);
  readonly selectedRole = signal<UserRole>('Candidate');

  readonly registerForm: FormGroup = this.fb.group({
    role: ['Candidate', [Validators.required]],
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(100), passwordComplexityValidator]],
    companyName: [''],
  });

  ngOnInit(): void {
    // React to role changes to adjust validators for companyName dynamically
    this.registerForm.get('role')?.valueChanges.subscribe((role: UserRole) => {
      this.selectedRole.set(role);
      this.updateCompanyValidators(role);
    });
  }

  get roleControl() {
    return this.registerForm.get('role');
  }

  get fullNameControl() {
    return this.registerForm.get('fullName');
  }

  get emailControl() {
    return this.registerForm.get('email');
  }

  get passwordControl() {
    return this.registerForm.get('password');
  }

  get companyNameControl() {
    return this.registerForm.get('companyName');
  }

  get isEmployer(): boolean {
    return this.selectedRole() === 'Employer';
  }

  setRole(role: UserRole): void {
    this.registerForm.get('role')?.setValue(role);
  }

  private updateCompanyValidators(role: UserRole): void {
    const companyControl = this.companyNameControl;
    if (!companyControl) return;

    if (role === 'Employer') {
      companyControl.setValidators([Validators.required, Validators.minLength(2), Validators.maxLength(120)]);
    } else {
      companyControl.clearValidators();
      companyControl.setValue('');
    }
    companyControl.updateValueAndValidity();
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((visible) => !visible);
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  getPasswordFeedback(): string {
    const ctrl = this.passwordControl;
    if (!ctrl || !ctrl.errors || (!ctrl.touched && !ctrl.dirty)) {
      return '';
    }

    if (ctrl.errors['required']) {
      return 'Password is required.';
    }

    const missingRules: string[] = [];
    if (ctrl.errors['minLength']) {
      missingRules.push('at least 8 characters');
    }
    if (ctrl.errors['requiresUpper']) {
      missingRules.push('one uppercase letter');
    }
    if (ctrl.errors['requiresLower']) {
      missingRules.push('one lowercase letter');
    }
    if (ctrl.errors['requiresNumber']) {
      missingRules.push('one number');
    }

    if (missingRules.length > 0) {
      return `Password must include: ${missingRules.join(', ')}.`;
    }

    return '';
  }

  onSubmit(): void {
    if (this.registerForm.invalid || this.isSubmitting()) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.dismissAlert();
    this.isSubmitting.set(true);

    const formVal = this.registerForm.value;
    const payload = {
      fullName: formVal.fullName.trim(),
      email: formVal.email.trim(),
      password: formVal.password,
      role: formVal.role as UserRole,
      companyName: formVal.role === 'Employer' ? formVal.companyName?.trim() : null,
    };

    this.authService.register(payload).subscribe({
      next: (response) => {
        this.isSubmitting.set(false);
        // AuthService saved token, role, and userId to localStorage
        this.authService.redirectAfterAuth(response.role);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = extractErrorMessage(err, 'Registration failed. Please check the entered information and try again.');
        this.errorMessage.set(msg);
      },
    });
  }
}
