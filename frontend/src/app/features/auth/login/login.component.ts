import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly showPassword = signal<boolean>(false);

  readonly loginForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  get emailControl() {
    return this.loginForm.get('email');
  }

  get passwordControl() {
    return this.loginForm.get('password');
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((visible) => !visible);
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  // Pre-fill demo credentials for hackathon evaluation
  fillDemoCredentials(role: 'Candidate' | 'Employer'): void {
    if (role === 'Employer') {
      this.loginForm.setValue({
        email: 'admin@hackathon.local',
        password: 'Admin123!',
      });
    } else {
      this.loginForm.setValue({
        email: 'user@hackathon.local',
        password: 'User123!',
      });
    }
    this.loginForm.markAsDirty();
    this.dismissAlert();
  }

  onSubmit(): void {
    if (this.loginForm.invalid || this.isSubmitting()) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.dismissAlert();
    this.isSubmitting.set(true);

    const { email, password } = this.loginForm.value;

    this.authService.login({ email, password }).subscribe({
      next: (response) => {
        this.isSubmitting.set(false);
        // AuthService already saved token, role, and userId to localStorage
        this.authService.redirectAfterAuth(response.role);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = extractErrorMessage(err, 'Failed to sign in. Please verify your email and password.');
        this.errorMessage.set(msg);
      },
    });
  }
}
