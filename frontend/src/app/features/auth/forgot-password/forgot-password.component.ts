import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';
import { ThemeToggleComponent } from '../../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, ThemeToggleComponent],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);

  readonly isSubmitting = signal<boolean>(false);
  readonly isSubmitted = signal<boolean>(false);
  readonly submittedEmail = signal<string>('');
  readonly errorMessage = signal<string | null>(null);
  readonly isEmailFocused = signal<boolean>(false);

  readonly form: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  get emailControl() {
    return this.form.get('email');
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
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
      error: () => {
        this.isSubmitting.set(false);
        // Generic success to prevent account enumeration
        this.isSubmitted.set(true);
      },
    });
  }
}
