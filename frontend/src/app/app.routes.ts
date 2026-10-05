import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'login',
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
    path: 'jobs',
    canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/placeholder/destination-placeholder.component').then(
        (m) => m.CandidateHomePlaceholderComponent
      ),
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
    path: 'employer/jobs',
    canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import('./features/placeholder/destination-placeholder.component').then(
        (m) => m.EmployerHomePlaceholderComponent
      ),
  },
  {
    path: '**',
    redirectTo: 'login',
  },
];

