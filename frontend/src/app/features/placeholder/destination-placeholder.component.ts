import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';

@Component({
  selector: 'app-candidate-home-placeholder',
  standalone: true,
  imports: [CommonModule, RouterLink, ThemeToggleComponent],
  template: `
    <div class="destination-page">
      <div class="auth-theme-corner">
        <app-theme-toggle variant="icon" />
      </div>
      <div class="destination-card">
        <div class="badge candidate-badge">Candidate Portal</div>
        <h1>Welcome to /jobs</h1>
        <p class="desc">
          Authenticated as <strong>{{ authService.currentUser()?.fullName }}</strong> ({{ authService.currentUser()?.email }})
        </p>
        <div class="meta-box">
          <div><strong>Role:</strong> {{ authService.getRole() }}</div>
          <div><strong>User ID:</strong> {{ authService.getUserId() }}</div>
          <div><strong>Token in Memory:</strong> {{ !!authService.getToken() ? 'Present' : 'Missing' }}</div>
        </div>
        <div class="actions-row">
          <a routerLink="/candidate/profile" class="profile-btn">Edit Candidate Profile</a>
          <button class="logout-btn" (click)="authService.logout()">Sign Out</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .destination-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--bg);
      padding: 1.5rem;
      font-family: system-ui, sans-serif;
    }
    .destination-card {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: 16px;
      padding: 2.5rem;
      max-width: 500px;
      width: 100%;
      text-align: center;
      box-shadow: var(--shadow-lg);
    }
    .badge {
      display: inline-block;
      padding: 0.25rem 0.75rem;
      font-size: 0.75rem;
      font-weight: 700;
      border-radius: 9999px;
      margin-bottom: 1rem;
    }
    .candidate-badge {
      background: var(--primary-tint);
      color: var(--primary-text);
    }
    .employer-badge {
      background: var(--accent-tint);
      color: var(--accent-text);
    }
    h1 {
      margin: 0 0 0.5rem;
      color: var(--ink);
      font-size: 1.75rem;
    }
    .desc {
      color: var(--ink-muted);
      margin-bottom: 1.5rem;
    }
    .meta-box {
      text-align: left;
      background: var(--surface-muted);
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 1rem;
      margin-bottom: 1.5rem;
      font-size: 0.875rem;
      color: var(--ink-strong);
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .actions-row {
      display: flex;
      gap: 0.75rem;
      justify-content: center;
      flex-wrap: wrap;
    }
    .profile-btn {
      display: inline-flex;
      align-items: center;
      background: var(--primary-btn-bg);
      color: var(--primary-btn-ink);
      text-decoration: none;
      padding: 0.625rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.875rem;
      cursor: pointer;
    }
    .profile-btn:hover {
      background: var(--primary-btn-hover);
    }
    .logout-btn {
      background: transparent;
      color: var(--ink);
      border: 1px solid var(--line-strong);
      padding: 0.625rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.875rem;
      cursor: pointer;
    }
    .logout-btn:hover {
      background: var(--bg-subtle);
    }
  `],
})
export class CandidateHomePlaceholderComponent {
  readonly authService = inject(AuthService);
}

@Component({
  selector: 'app-employer-home-placeholder',
  standalone: true,
  imports: [CommonModule, ThemeToggleComponent],
  template: `
    <div class="destination-page">
      <div class="auth-theme-corner">
        <app-theme-toggle variant="icon" />
      </div>
      <div class="destination-card">
        <div class="badge employer-badge">Employer Portal</div>
        <h1>Welcome to /employer/jobs</h1>
        <p class="desc">
          Authenticated as <strong>{{ authService.currentUser()?.fullName }}</strong> ({{ authService.currentUser()?.email }})
        </p>
        <div class="meta-box">
          <div><strong>Role:</strong> {{ authService.getRole() }}</div>
          <div><strong>User ID:</strong> {{ authService.getUserId() }}</div>
          <div><strong>Token in Memory:</strong> {{ !!authService.getToken() ? 'Present' : 'Missing' }}</div>
        </div>
        <button class="logout-btn" (click)="authService.logout()">Sign Out</button>
      </div>
    </div>
  `,
  styles: [`
    .destination-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--bg);
      padding: 1.5rem;
      font-family: system-ui, sans-serif;
    }
    .destination-card {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: 16px;
      padding: 2.5rem;
      max-width: 500px;
      width: 100%;
      text-align: center;
      box-shadow: var(--shadow-lg);
    }
    .badge {
      display: inline-block;
      padding: 0.25rem 0.75rem;
      font-size: 0.75rem;
      font-weight: 700;
      border-radius: 9999px;
      margin-bottom: 1rem;
    }
    .employer-badge {
      background: var(--accent-tint);
      color: var(--accent-text);
    }
    h1 {
      margin: 0 0 0.5rem;
      color: var(--ink);
      font-size: 1.75rem;
    }
    .desc {
      color: var(--ink-muted);
      margin-bottom: 1.5rem;
    }
    .meta-box {
      text-align: left;
      background: var(--surface-muted);
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 1rem;
      margin-bottom: 1.5rem;
      font-size: 0.875rem;
      color: var(--ink-strong);
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .logout-btn {
      background: var(--primary-btn-bg);
      color: var(--primary-btn-ink);
      border: none;
      padding: 0.625rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      cursor: pointer;
    }
    .logout-btn:hover {
      background: var(--primary-btn-hover);
    }
  `],
})
export class EmployerHomePlaceholderComponent {
  readonly authService = inject(AuthService);
}
