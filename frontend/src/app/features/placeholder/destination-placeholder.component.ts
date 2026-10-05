import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-candidate-home-placeholder',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="destination-page">
      <div class="destination-card">
        <div class="badge candidate-badge">Candidate Portal</div>
        <h1>Welcome to /jobs</h1>
        <p class="desc">
          Authenticated as <strong>{{ authService.currentUser()?.fullName }}</strong> ({{ authService.currentUser()?.email }})
        </p>
        <div class="meta-box">
          <div><strong>Role:</strong> {{ authService.getRole() }}</div>
          <div><strong>User ID:</strong> {{ authService.getUserId() }}</div>
          <div><strong>Token in localStorage:</strong> {{ !!authService.getToken() ? 'Stored' : 'Missing' }}</div>
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
      background: #f8fafc;
      padding: 1.5rem;
      font-family: system-ui, sans-serif;
    }
    .destination-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 2.5rem;
      max-width: 500px;
      width: 100%;
      text-align: center;
      box-shadow: 0 10px 25px -5px rgba(30, 41, 59, 0.06);
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
      background: #ccfbf1;
      color: #0d9488;
    }
    .employer-badge {
      background: #ffedd5;
      color: #ea580c;
    }
    h1 {
      margin: 0 0 0.5rem;
      color: #1e293b;
      font-size: 1.75rem;
    }
    .desc {
      color: #64748b;
      margin-bottom: 1.5rem;
    }
    .meta-box {
      text-align: left;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1rem;
      margin-bottom: 1.5rem;
      font-size: 0.875rem;
      color: #334155;
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
      background: #0d9488;
      color: white;
      text-decoration: none;
      padding: 0.625rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.875rem;
      cursor: pointer;
    }
    .profile-btn:hover {
      background: #0f766e;
    }
    .logout-btn {
      background: transparent;
      color: #1e293b;
      border: 1px solid #cbd5e1;
      padding: 0.625rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.875rem;
      cursor: pointer;
    }
    .logout-btn:hover {
      background: #f1f5f9;
    }
  `],
})
export class CandidateHomePlaceholderComponent {
  readonly authService = inject(AuthService);
}

@Component({
  selector: 'app-employer-home-placeholder',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="destination-page">
      <div class="destination-card">
        <div class="badge employer-badge">Employer Portal</div>
        <h1>Welcome to /employer/jobs</h1>
        <p class="desc">
          Authenticated as <strong>{{ authService.currentUser()?.fullName }}</strong> ({{ authService.currentUser()?.email }})
        </p>
        <div class="meta-box">
          <div><strong>Role:</strong> {{ authService.getRole() }}</div>
          <div><strong>User ID:</strong> {{ authService.getUserId() }}</div>
          <div><strong>Token in localStorage:</strong> {{ !!authService.getToken() ? 'Stored' : 'Missing' }}</div>
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
      background: #f8fafc;
      padding: 1.5rem;
      font-family: system-ui, sans-serif;
    }
    .destination-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 2.5rem;
      max-width: 500px;
      width: 100%;
      text-align: center;
      box-shadow: 0 10px 25px -5px rgba(30, 41, 59, 0.06);
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
      background: #ffedd5;
      color: #ea580c;
    }
    h1 {
      margin: 0 0 0.5rem;
      color: #1e293b;
      font-size: 1.75rem;
    }
    .desc {
      color: #64748b;
      margin-bottom: 1.5rem;
    }
    .meta-box {
      text-align: left;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1rem;
      margin-bottom: 1.5rem;
      font-size: 0.875rem;
      color: #334155;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .logout-btn {
      background: #0d9488;
      color: white;
      border: none;
      padding: 0.625rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      cursor: pointer;
    }
    .logout-btn:hover {
      background: #0f766e;
    }
  `],
})
export class EmployerHomePlaceholderComponent {
  readonly authService = inject(AuthService);
}
