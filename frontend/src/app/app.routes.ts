import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/landing/landing.component').then((m) => m.LandingComponent),
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'confirm-email',
    loadComponent: () =>
      import('./features/auth/confirm-email/confirm-email.component').then(
        (m) => m.ConfirmEmailComponent
      ),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent
      ),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password/reset-password.component').then(
        (m) => m.ResetPasswordComponent
      ),
  },
  {
    path: 'account/security',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/account/account-security.component').then(
        (m) => m.AccountSecurityComponent
      ),
  },
  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./features/auth/unauthorized/unauthorized.component').then(
        (m) => m.UnauthorizedComponent
      ),
  },
  {
    path: 'jobs',
    canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/candidate/jobs/job-list/job-list.component').then(
        (m) => m.JobListComponent
      ),
  },
  {
    path: 'jobs/:id',
    canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/candidate/jobs/job-detail/job-detail.component').then(
        (m) => m.JobDetailComponent
      ),
  },
  {
    path: 'candidate/onboarding/skills',
    canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/candidate/onboarding/candidate-skill-onboarding.component').then(
        (m) => m.CandidateSkillOnboardingComponent
      ),
  },
  {
    path: 'candidate/skill-registration',
    redirectTo: 'candidate/onboarding/skills',
    pathMatch: 'full',
  },
  {
    path: 'candidate/profile',
    canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/candidate/profile/candidate-profile.component').then(
        (m) => m.CandidateProfileComponent
      ),
  },
  {
    path: 'profile',
    redirectTo: 'candidate/profile',
    pathMatch: 'full',
  },
  {
    path: 'my-applications',
    canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/candidate/applications/candidate-applications.component').then(
        (m) => m.CandidateApplicationsComponent
      ),
  },
  {
    path: 'candidate/applications',
    redirectTo: 'my-applications',
    pathMatch: 'full',
  },
  {
    path: 'employer/jobs/new',
    canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import('./features/employer/jobs/employer-post-job/employer-post-job.component').then(
        (m) => m.EmployerPostJobComponent
      ),
  },
  {
    path: 'employer/jobs/:id/edit',
    canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import('./features/employer/jobs/employer-post-job/employer-post-job.component').then(
        (m) => m.EmployerPostJobComponent
      ),
  },
  {
    path: 'employer/jobs/:id/applicants',
    canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import(
        './features/employer/jobs/employer-job-applicants/employer-job-applicants.component'
      ).then((m) => m.EmployerJobApplicantsComponent),
  },
  {
    path: 'employer/applications',
    canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import('./features/employer/applications/employer-applications.component').then(
        (m) => m.EmployerApplicationsComponent
      ),
  },
  {
    path: 'employer/jobs',
    canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import('./features/employer/jobs/employer-jobs.component').then(
        (m) => m.EmployerJobsComponent
      ),
  },
  {
    path: '**',
    redirectTo: '',
  },
];

