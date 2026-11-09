import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AccountSecurityStatus } from '../../core/models/auth.models';
import { AuthService } from '../../core/services/auth.service';
import { extractErrorMessage } from '../../core/utils/error-formatter';

@Component({
  selector: 'app-account-security',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './account-security.component.html',
  styleUrl: './account-security.component.scss',
})
export class AccountSecurityComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly status = signal<AccountSecurityStatus | null>(null);
  readonly isLoading = signal<boolean>(true);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  // Forms
  readonly profileForm: FormGroup = this.fb.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    companyName: [''],
  });

  readonly passwordForm: FormGroup = this.fb.group({
    currentPassword: [''],
    newPassword: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/),
      ],
    ],
    confirmPassword: ['', [Validators.required]],
  });

  readonly deleteForm: FormGroup = this.fb.group({
    password: [''],
  });

  readonly isSavingProfile = signal<boolean>(false);
  readonly isSavingPassword = signal<boolean>(false);
  readonly isResendingVerification = signal<boolean>(false);
  readonly isDeletingAccount = signal<boolean>(false);
  readonly showDeleteModal = signal<boolean>(false);

  ngOnInit(): void {
    this.loadSecurityStatus();
  }

  loadSecurityStatus(): void {
    this.isLoading.set(true);
    this.authService.getSecurityStatus().subscribe({
      next: (data) => {
        this.status.set(data);
        this.isLoading.set(false);
        this.profileForm.patchValue({
          fullName: data.fullName,
          companyName: data.companyName ?? '',
        });

        if (data.hasPassword) {
          this.passwordForm.get('currentPassword')?.setValidators([Validators.required]);
        } else {
          this.passwordForm.get('currentPassword')?.clearValidators();
        }
        this.passwordForm.get('currentPassword')?.updateValueAndValidity();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to load account settings.'));
      },
    });
  }

  onSaveProfile(): void {
    if (this.profileForm.invalid || this.isSavingProfile()) return;
    this.isSavingProfile.set(true);
    this.clearAlerts();

    const { fullName, companyName } = this.profileForm.value;
    this.authService.updateProfile({ fullName, companyName }).subscribe({
      next: () => {
        this.isSavingProfile.set(false);
        this.successMessage.set('Profile information updated successfully.');
        this.loadSecurityStatus();
      },
      error: (err) => {
        this.isSavingProfile.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to update profile.'));
      },
    });
  }

  onSavePassword(): void {
    if (this.passwordForm.invalid || this.isSavingPassword()) return;

    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.value;
    if (newPassword !== confirmPassword) {
      this.errorMessage.set('New passwords do not match.');
      return;
    }

    this.isSavingPassword.set(true);
    this.clearAlerts();

    const currentStatus = this.status();
    const action$ = currentStatus?.hasPassword
      ? this.authService.changePassword({ currentPassword, newPassword })
      : this.authService.setPassword({ newPassword });

    action$.subscribe({
      next: () => {
        this.isSavingPassword.set(false);
        this.passwordForm.reset();
        this.successMessage.set('Password updated successfully.');
        this.loadSecurityStatus();
      },
      error: (err) => {
        this.isSavingPassword.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to update password.'));
      },
    });
  }

  onResendVerification(): void {
    const email = this.status()?.email;
    if (!email || this.isResendingVerification()) return;

    this.isResendingVerification.set(true);
    this.clearAlerts();

    this.authService.resendConfirmation({ email }).subscribe({
      next: () => {
        this.isResendingVerification.set(false);
        this.successMessage.set('Verification email sent! Please check your inbox.');
      },
      error: (err) => {
        this.isResendingVerification.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to send verification email.'));
      },
    });
  }

  onLogoutAll(): void {
    if (!confirm('Are you sure you want to log out from all devices? You will be signed out.')) return;
    this.authService.logoutAll().subscribe();
  }

  openDeleteModal(): void {
    this.showDeleteModal.set(true);
  }

  closeDeleteModal(): void {
    this.showDeleteModal.set(false);
    this.deleteForm.reset();
  }

  onConfirmDelete(): void {
    if (this.isDeletingAccount()) return;
    this.isDeletingAccount.set(true);

    const password = this.deleteForm.value.password;
    this.authService.deleteAccount({ password }).subscribe({
      next: () => {
        this.isDeletingAccount.set(false);
      },
      error: (err) => {
        this.isDeletingAccount.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to delete account. Please verify your password.'));
        this.closeDeleteModal();
      },
    });
  }

  clearAlerts(): void {
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  goBack(): void {
    const role = this.authService.getRole();
    if (role === 'Employer') {
      this.router.navigate(['/employer/jobs']);
    } else {
      this.router.navigate(['/jobs']);
    }
  }
}
