import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

@Component({
  selector: 'app-confirm-email',
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
          <h1 class="auth-title">Email Verification</h1>
          <p class="auth-subtitle">Verify your email to secure your SkillBridge account</p>
        </header>

        @if (isLoading()) {
          <div class="status-box loading">
            <div class="spinner"></div>
            <p>Verifying your email address...</p>
          </div>
        } @else if (isSuccess()) {
          <div class="status-box success">
            <svg class="status-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
              <polyline points="22 4 12 14.01 9 11.01"></polyline>
            </svg>
            <h3>Email verified successfully!</h3>
            <p>{{ successMessage() }}</p>
            <a routerLink="/login" class="submit-btn text-center" style="margin-top: 1.5rem; text-decoration: none;">
              Sign in to your account
            </a>
          </div>
        } @else {
          <div class="status-box error">
            <svg class="status-icon text-danger" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="8" x2="12" y2="12"></line>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
            <h3>Verification Link Expired or Invalid</h3>
            <p>{{ errorMessage() }}</p>

            <div class="resend-section">
              <h4>Request a new verification link</h4>
              <form [formGroup]="resendForm" (ngSubmit)="onResend()" class="resend-form">
                <input
                  type="email"
                  formControlName="email"
                  placeholder="Enter your email"
                  class="form-input"
                />
                <button
                  type="submit"
                  class="submit-btn"
                  [disabled]="resendForm.invalid || isResending()"
                >
                  {{ isResending() ? 'Sending...' : 'Resend Link' }}
                </button>
              </form>
              @if (resendSuccess()) {
                <p class="inline-success">If an account exists, a new verification link has been sent.</p>
              }
            </div>

            <div class="footer-links">
              <a routerLink="/login" class="auth-link">Back to Sign In</a>
            </div>
          </div>
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
      max-width: 460px;
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
    .status-box {
      text-align: center;
      padding: 1rem 0;
    }
    .status-icon {
      width: 48px;
      height: 48px;
      color: #0d9488;
      margin-bottom: 1rem;
    }
    .text-danger {
      color: #dc2626 !important;
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
      cursor: pointer;
      margin-top: 0.75rem;
    }
    .submit-btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .spinner {
      width: 36px;
      height: 36px;
      border: 3px solid #e2e8f0;
      border-top-color: #0d9488;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
      margin: 1.5rem auto;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .resend-section {
      margin-top: 1.75rem;
      padding-top: 1.5rem;
      border-top: 1px solid #e2e8f0;
      text-align: left;
    }
    .resend-section h4 {
      font-size: 0.95rem;
      color: #1e293b;
      margin: 0 0 0.75rem;
    }
    .form-input {
      width: 100%;
      padding: 0.65rem 0.85rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      box-sizing: border-box;
    }
    .inline-success {
      font-size: 0.85rem;
      color: #059669;
      margin-top: 0.5rem;
    }
    .footer-links {
      margin-top: 1.5rem;
    }
    .auth-link {
      color: #0d9488;
      font-weight: 600;
      text-decoration: none;
    }
  `]
})
export class ConfirmEmailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  readonly isLoading = signal<boolean>(false);
  readonly isSuccess = signal<boolean>(false);
  readonly successMessage = signal<string>('Your email address has been verified.');
  readonly errorMessage = signal<string>('The verification link is invalid or has expired.');
  readonly isResending = signal<boolean>(false);
  readonly resendSuccess = signal<boolean>(false);

  readonly resendForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (userId && token) {
      this.isLoading.set(true);
      this.authService.confirmEmail({ userId, token }).subscribe({
        next: (res) => {
          this.isLoading.set(false);
          this.isSuccess.set(true);
          this.successMessage.set(res.message || 'Your email address has been verified.');
        },
        error: (err) => {
          this.isLoading.set(false);
          this.isSuccess.set(false);
          this.errorMessage.set(extractErrorMessage(err, 'The confirmation link is invalid or has expired.'));
        }
      });
    }
  }

  onResend(): void {
    if (this.resendForm.invalid || this.isResending()) return;
    this.isResending.set(true);
    this.resendSuccess.set(false);

    this.authService.resendConfirmation({ email: this.resendForm.value.email }).subscribe({
      next: () => {
        this.isResending.set(false);
        this.resendSuccess.set(true);
      },
      error: () => {
        this.isResending.set(false);
        this.resendSuccess.set(true); // Prevent enumeration
      }
    });
  }
}

