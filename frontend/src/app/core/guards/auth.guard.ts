import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};

export const roleGuard = (requiredRole: 'Candidate' | 'Employer'): CanActivateFn => {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }

    if (authService.getRole() === requiredRole) {
      return true;
    }

    // Role mismatch: redirect to their respective home
    const role = authService.getRole();
    if (role === 'Employer') {
      return router.createUrlTree(['/employer/jobs']);
    }
    return router.createUrlTree(['/jobs']);
  };
};
