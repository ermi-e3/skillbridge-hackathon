import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="unauthorized-page">
      <div class="unauthorized-card">
        <div class="icon-circle">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="40"
            height="40"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <rect width="18" height="11" x="3" y="11" rx="2" ry="2" />
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
        </div>
        <div class="status-code">403</div>
        <h1>Access Denied</h1>
        <p class="description">
          You do not have permission to view or manage this page. This section requires
          elevated or role-specific privileges.
        </p>

        @if (authService.isAuthenticated()) {
          <div class="user-info">
            Signed in as <strong>{{ authService.currentUser()?.email }}</strong>
            ({{ authService.getRole() || 'No Role' }})
          </div>
        }

        <div class="actions">
          @if (authService.getRole() === 'Employer') {
            <a routerLink="/employer/jobs" class="btn btn-primary">Go to Employer Portal</a>
          } @else if (authService.getRole() === 'Candidate') {
            <a routerLink="/jobs" class="btn btn-primary">Go to Candidate Portal</a>
          } @else {
            <a routerLink="/login" class="btn btn-primary">Sign In</a>
          }

          @if (authService.isAuthenticated()) {
            <button (click)="switchAccount()" class="btn btn-secondary">
              Sign In as Different User
            </button>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .unauthorized-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: #f8fafc;
      padding: 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    }

    .unauthorized-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 3rem 2rem;
      max-width: 480px;
      width: 100%;
      text-align: center;
      box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.08);
    }

    .icon-circle {
      width: 72px;
      height: 72px;
      border-radius: 50%;
      background: #fef2f2;
      color: #ef4444;
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto 1.5rem;
    }

    .status-code {
      font-size: 0.875rem;
      font-weight: 700;
      color: #ef4444;
      letter-spacing: 0.1em;
      text-transform: uppercase;
      margin-bottom: 0.5rem;
    }

    h1 {
      font-size: 1.75rem;
      font-weight: 700;
      color: #0f172a;
      margin: 0 0 0.75rem;
    }

    .description {
      font-size: 0.95rem;
      color: #64748b;
      line-height: 1.5;
      margin: 0 0 1.5rem;
    }

    .user-info {
      background: #f1f5f9;
      border-radius: 8px;
      padding: 0.75rem 1rem;
      font-size: 0.875rem;
      color: #334155;
      margin-bottom: 1.5rem;
    }

    .actions {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0.75rem 1.25rem;
      border-radius: 8px;
      font-size: 0.95rem;
      font-weight: 600;
      cursor: pointer;
      text-decoration: none;
      transition: all 0.2s ease;
      border: none;
    }

    .btn-primary {
      background: #0284c7;
      color: #ffffff;
    }

    .btn-primary:hover {
      background: #0369a1;
    }

    .btn-secondary {
      background: #f1f5f9;
      color: #475569;
    }

    .btn-secondary:hover {
      background: #e2e8f0;
      color: #1e293b;
    }
  `]
})
export class UnauthorizedComponent {
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  switchAccount(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

