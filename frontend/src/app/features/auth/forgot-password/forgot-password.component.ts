import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-page">
      <div class="auth-card">
        <header class="auth-header">
          <div class="brand-badge">
            <svg class="brand-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M16 16v1a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h11a2 2 0 0 1 2 2v1"></path>
              <path d="M18 8h4a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-4"></path>
              <circle cx="8" cy="12" r="2"></circle>
            </svg>
            <span class="brand-title">SkillBridge</span>
          </div>
          <h1 class="auth-title">Reset your password</h1>
          <p class="auth-subtitle">Enter your email and we'll send a link to reset your password</p>
        </header>

        @if (isSubmitted()) {
          <div class="success-box">
            <svg class="success-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
              <polyline points="22 4 12 14.01 9 11.01"></polyline>
            </svg>
            <h3>Check your email</h3>
            <p>If an account exists for <strong>{{ submittedEmail() }}</strong>, we have sent instructions to reset your password.</p>
            <a routerLink="/login" class="submit-btn text-center" style="margin-top: 1.5rem; text-decoration: none;">
              Return to Sign In
            </a>
          </div>
        } @else {
          @if (errorMessage()) {
            <div class="alert-banner" role="alert">
              <span>{{ errorMessage() }}</span>
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="onSubmit()" novalidate class="auth-form">
            <div class="form-group">
              <label for="email" class="form-label">Email address</label>
              <input
                id="email"
                type="email"
                formControlName="email"
                placeholder="you@example.com"
                class="form-input"
              />
              @if (emailControl?.invalid && (emailControl?.touched || emailControl?.dirty)) {
                <div class="inline-error">
                  <span>Please enter a valid email address.</span>
                </div>
              }
            </div>

            <button
              type="submit"
              class="submit-btn"
              [disabled]="form.invalid || isSubmitting()"
            >
              {{ isSubmitting() ? 'Sending link...' : 'Send Reset Link' }}
            </button>
          </form>

          <footer class="auth-footer">
            <p>
              Remember your password?
              <a routerLink="/login" class="auth-link">Sign In</a>
            </p>
          </footer>
        }
      </div>
    </div>
  `,
  styles: [`
    .auth-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background-color: #f8fafc;
      padding: 1.5rem;
    }
    .auth-card {
      width: 100%;
      max-width: 440px;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      box-shadow: 0 10px 25px -5px rgba(30, 41, 59, 0.06);
      padding: 2.25rem;
    }
    .auth-header {
      text-align: center;
      margin-bottom: 1.75rem;
    }
    .brand-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
    }
    .brand-icon {
      width: 24px;
      height: 24px;
      color: #0d9488;
    }
    .brand-title {
      font-weight: 700;
      font-size: 1.25rem;
      color: #0f172a;
    }
    .auth-title {
      font-size: 1.5rem;
      font-weight: 700;
      color: #0f172a;
      margin: 0 0 0.5rem;
    }
    .auth-subtitle {
      font-size: 0.875rem;
      color: #64748b;
      margin: 0;
    }
    .form-group {
      margin-bottom: 1.25rem;
    }
    .form-label {
      display: block;
      font-size: 0.875rem;
      font-weight: 600;
      color: #334155;
      margin-bottom: 0.5rem;
    }
    .form-input {
      width: 100%;
      padding: 0.75rem 1rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      box-sizing: border-box;
      font-size: 0.95rem;
    }
    .inline-error {
      color: #dc2626;
      font-size: 0.85rem;
      margin-top: 0.35rem;
    }
    .submit-btn {
      display: block;
      width: 100%;
      padding: 0.75rem 1rem;
      background: #0d9488;
      color: white;
      border: none;
      border-radius: 8px;
      font-weight: 600;
      font-size: 1rem;
      cursor: pointer;
    }
    .submit-btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .success-box {
      text-align: center;
      padding: 1rem 0;
    }
    .success-icon {
      width: 48px;
      height: 48px;
      color: #0d9488;
      margin-bottom: 1rem;
    }
    .auth-footer {
      text-align: center;
      margin-top: 1.5rem;
      padding-top: 1.25rem;
      border-top: 1px solid #f1f5f9;
      font-size: 0.875rem;
      color: #64748b;
    }
    .auth-link {
      color: #0d9488;
      font-weight: 600;
      text-decoration: none;
    }
    .alert-banner {
      background-color: #fef2f2;
      border: 1px solid #fecaca;
      color: #b91c1c;
      padding: 0.75rem;
      border-radius: 8px;
      font-size: 0.875rem;
      margin-bottom: 1rem;
    }
  `]
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);

  readonly isSubmitting = signal<boolean>(false);
  readonly isSubmitted = signal<boolean>(false);
  readonly submittedEmail = signal<string>('');
  readonly errorMessage = signal<string | null>(null);

  readonly form: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  get emailControl() {
    return this.form.get('email');
  }

  onSubmit(): void {
    if (this.form.invalid || this.isSubmitting()) return;
    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const email = this.form.value.email.trim();
    this.submittedEmail.set(email);

    this.authService.forgotPassword({ email }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.isSubmitted.set(true);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        // Generic success to prevent enumeration
        this.isSubmitted.set(true);
      }
    });
  }
}

