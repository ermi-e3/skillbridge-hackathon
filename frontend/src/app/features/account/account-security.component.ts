import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AccountSecurityStatus } from '../../core/models/auth.models';
import { AuthService } from '../../core/services/auth.service';
import { extractErrorMessage } from '../../core/utils/error-formatter';
import { UserProfileMenuComponent } from '../../shared/components/user-profile-menu/user-profile-menu.component';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-account-security',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, UserProfileMenuComponent],
  templateUrl: './account-security.component.html',
  styleUrl: './account-security.component.scss',
})
export class AccountSecurityComponent implements OnInit, OnDestroy {
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

  // Password Visibility Toggles
  readonly showCurrentPassword = signal<boolean>(false);
  readonly showNewPassword = signal<boolean>(false);
  readonly showConfirmPassword = signal<boolean>(false);

  // Live Password Validation & Strength
  readonly newPasswordValue = signal<string>('');
  private passwordSub?: Subscription;

  readonly hasMinLength = computed(() => this.newPasswordValue().length >= 8);
  readonly hasUppercase = computed(() => /[A-Z]/.test(this.newPasswordValue()));
  readonly hasDigit = computed(() => /\d/.test(this.newPasswordValue()));

  readonly passwordStrengthPercent = computed(() => {
    let met = 0;
    if (this.hasMinLength()) met++;
    if (this.hasUppercase()) met++;
    if (this.hasDigit()) met++;
    return met === 0 ? 0 : met === 1 ? 33 : met === 2 ? 66 : 100;
  });

  readonly passwordStrengthLabel = computed(() => {
    const p = this.passwordStrengthPercent();
    if (p === 0) return 'Enter password';
    if (p <= 33) return 'Weak';
    if (p <= 66) return 'Moderate';
    return 'Strong & Verified';
  });

  // Account Security Score (0 - 100)
  readonly securityScore = computed(() => {
    const s = this.status();
    if (!s) return 0;
    let score = 35; // base profile initialized
    if (s.hasPassword) score += 35;
    if (s.isEmailConfirmed) score += 30;
    return score;
  });

  // Quick-Jump Anchor Navigation
  readonly activeSection = signal<string>('profile');

  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.loadSecurityStatus();

    this.passwordSub = this.passwordForm.get('newPassword')?.valueChanges.subscribe((val) => {
      this.newPasswordValue.set(val || '');
    });
  }

  ngOnDestroy(): void {
    this.passwordSub?.unsubscribe();
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
  }

  scrollToSection(sectionId: string): void {
    this.activeSection.set(sectionId);
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  toggleCurrentPassword(): void {
    this.showCurrentPassword.update((v) => !v);
  }

  toggleNewPassword(): void {
    this.showNewPassword.update((v) => !v);
  }

  toggleConfirmPassword(): void {
    this.showConfirmPassword.update((v) => !v);
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
        this.showToast('Profile information updated successfully.');
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
        this.showToast('Password updated successfully.');
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
        this.showToast('Verification email sent! Please check your inbox.');
      },
      error: (err) => {
        this.isResendingVerification.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to send verification email.'));
      },
    });
  }

  showToast(msg: string): void {
    this.successMessage.set(msg);
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this.toastTimeoutId = setTimeout(() => {
      this.successMessage.set(null);
    }, 4500);
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
