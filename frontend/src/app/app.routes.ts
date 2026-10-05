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
    //canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/candidate/jobs/job-list/job-list.component').then(
        (m) => m.JobListComponent
      ),
  },
  {
    path: 'jobs/:id',
    //canActivate: [authGuard, roleGuard('Candidate')],
    loadComponent: () =>
      import('./features/candidate/jobs/job-detail/job-detail.component').then(
        (m) => m.JobDetailComponent
      ),
  },
  {
    path: 'candidate/profile',
    //canActivate: [authGuard, roleGuard('Candidate')],
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
    //canActivate: [authGuard, roleGuard('Candidate')],
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
    //canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import('./features/employer/jobs/employer-post-job/employer-post-job.component').then(
        (m) => m.EmployerPostJobComponent
      ),
  },
  {
    path: 'employer/jobs/:id/applicants',
    //canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import(
        './features/employer/jobs/employer-job-applicants/employer-job-applicants.component'
      ).then((m) => m.EmployerJobApplicantsComponent),
  },
  {
    path: 'employer/jobs',
    //canActivate: [authGuard, roleGuard('Employer')],
    loadComponent: () =>
      import('./features/employer/jobs/employer-jobs.component').then(
        (m) => m.EmployerJobsComponent
      ),
  },
  {
    path: '**',
    redirectTo: 'login',
  },
];
