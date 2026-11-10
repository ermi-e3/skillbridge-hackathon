import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';
import { ThemeToggleComponent } from '../../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, ThemeToggleComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

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

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    const oauthError = this.route.snapshot.queryParamMap.get('error');

    if (oauthError) {
      this.errorMessage.set('External authentication was canceled or denied.');
      this.clearQueryParams();
      return;
    }

    if (code) {
      const stateParam = this.route.snapshot.queryParamMap.get('state') || '';
      let provider = '';
      let role: UserRole | undefined;
      let returnTo: string | undefined;

      if (stateParam) {
        const parts = stateParam.split(':');
        const p = parts[0]?.toLowerCase();
        if (p === 'github') provider = 'GitHub';
        else if (p === 'google') provider = 'Google';

        if (parts[1] === 'Candidate' || parts[1] === 'Employer') {
          role = parts[1] as UserRole;
        }
        if (parts[2]) {
          returnTo = parts[2];
        }
      }

      if (!provider) {
        provider = sessionStorage.getItem('sb_oauth_provider') || 'Google';
      }
      if (!role) {
        role = (sessionStorage.getItem('sb_oauth_role') as UserRole) || undefined;
      }
      if (!returnTo) {
        returnTo = sessionStorage.getItem('sb_oauth_return_to') || undefined;
      }

      sessionStorage.removeItem('sb_oauth_provider');
      sessionStorage.removeItem('sb_oauth_role');
      sessionStorage.removeItem('sb_oauth_return_to');

      this.clearQueryParams();
      this.isSubmitting.set(true);

      if (returnTo === '/account/security') {
        this.authService
          .linkSocialAccount({
            provider,
            code,
            redirectUri: environment.oauth.redirectUri,
          })
          .subscribe({
            next: () => {
              this.isSubmitting.set(false);
              this.router.navigate(['/account/security']);
            },
            error: (err) => {
              this.isSubmitting.set(false);
              this.errorMessage.set(extractErrorMessage(err, `Failed to link ${provider} account.`));
            },
          });
      } else {
        this.authService
          .socialLogin({
            provider,
            code,
            redirectUri: environment.oauth.redirectUri,
            role,
          })
          .subscribe({
            next: (response) => {
              this.isSubmitting.set(false);
              this.authService.redirectAfterAuth(response.role);
            },
            error: (err) => {
              this.isSubmitting.set(false);
              this.errorMessage.set(extractErrorMessage(err, `Failed to sign in with ${provider}.`));
            },
          });
      }
    }
  }

  private clearQueryParams(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {},
      replaceUrl: true,
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((visible) => !visible);
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

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
        this.authService.redirectAfterAuth(response.role);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = extractErrorMessage(err, 'Failed to sign in. Please verify your email and password.');
        this.errorMessage.set(msg);
      },
    });
  }

  signInWithSocial(provider: 'Google' | 'GitHub'): void {
    if (this.isSubmitting()) return;
    this.dismissAlert();

    const redirectUri = encodeURIComponent(environment.oauth.redirectUri);
    let authUrl = '';

    if (provider === 'Google') {
      if (!environment.oauth.googleClientId) {
        this.errorMessage.set('Google Client ID is not configured.');
        return;
      }
      sessionStorage.setItem('sb_oauth_provider', 'Google');
      authUrl = `https://accounts.google.com/o/oauth2/v2/auth?client_id=${encodeURIComponent(
        environment.oauth.googleClientId
      )}&redirect_uri=${redirectUri}&response_type=code&scope=openid%20email%20profile&access_type=offline&prompt=consent&state=google:Candidate:`;
    } else if (provider === 'GitHub') {
      if (!environment.oauth.githubClientId) {
        this.errorMessage.set('GitHub Client ID is not configured.');
        return;
      }
      sessionStorage.setItem('sb_oauth_provider', 'GitHub');
      authUrl = `https://github.com/login/oauth/authorize?client_id=${encodeURIComponent(
        environment.oauth.githubClientId
      )}&redirect_uri=${redirectUri}&scope=user:email&state=github:Candidate:`;
    }

    if (authUrl) {
      window.location.href = authUrl;
    }
  }
}
