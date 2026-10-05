import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { JobDto } from '../../../../core/models/job.models';
import { JobsService } from '../../../../core/services/jobs.service';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  template: `
    <div class="job-detail-page">
      <div class="job-detail-container">
        <a routerLink="/jobs" class="back-link">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="19" y1="12" x2="5" y2="12"></line>
            <polyline points="12 19 5 12 12 5"></polyline>
          </svg>
          <span>Back to all jobs</span>
        </a>

        @if (isLoading()) {
          <div class="detail-card skeleton-card">
            <div class="skel-line" style="width: 50%; height: 2rem;"></div>
            <div class="skel-line" style="width: 30%; height: 1.25rem;"></div>
            <div class="skel-line" style="width: 100%; height: 6rem; margin-top: 1rem;"></div>
          </div>
        } @else if (job(); as j) {
          <article class="detail-card">
            <header class="detail-header">
              <div class="title-group">
                <span class="company-badge">{{ j.companyName }}</span>
                <h1 class="job-title">{{ j.title }}</h1>
                <div class="meta-row">
                  @if (j.location) {
                    <span>📍 {{ j.location }}</span>
                  }
                  <span>📅 Posted {{ j.createdAt | date: 'mediumDate' }}</span>
                  @if (j.hasApplied) {
                    <span class="applied-badge">✓ You Applied</span>
                  }
                </div>
              </div>

              @if (getMatchPercent(j); as matchScore) {
                <div class="match-score-pill" [ngClass]="getMatchClass(matchScore)">
                  <span class="match-num">{{ matchScore }}%</span>
                  <span class="match-label">Match</span>
                </div>
              }
            </header>

            <section class="section">
              <h2>Required Skills</h2>
              <div class="skills-list">
                @for (skill of j.requiredSkills; track skill.id) {
                  <span class="skill-pill">{{ skill.name }}</span>
                }
              </div>
            </section>

            <section class="section">
              <h2>Job Description</h2>
              <p class="description-text">{{ j.description }}</p>
            </section>
          </article>
        } @else {
          <div class="detail-card error-card">
            <h2>Job not found</h2>
            <p>{{ errorMessage() || 'The requested job opening could not be loaded.' }}</p>
            <a routerLink="/jobs" class="action-btn">Return to Jobs</a>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .job-detail-page {
      min-height: 100vh;
      background: #f8fafc;
      padding: 2rem 1.5rem;
      font-family: system-ui, sans-serif;
    }
    .job-detail-container {
      max-width: 800px;
      margin: 0 auto;
    }
    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      font-size: 0.875rem;
      font-weight: 600;
      color: #0d9488;
      text-decoration: none;
      margin-bottom: 1.5rem;
      svg { width: 1.125rem; height: 1.125rem; }
      &:hover { color: #0f766e; }
    }
    .detail-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 2.25rem;
      box-shadow: 0 4px 6px -1px rgba(30, 41, 59, 0.04);
    }
    .detail-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1.5rem;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 1.5rem;
      margin-bottom: 1.75rem;
      flex-wrap: wrap;
    }
    .company-badge {
      display: inline-block;
      font-size: 0.8125rem;
      font-weight: 700;
      color: #0d9488;
      background: #ccfbf1;
      padding: 0.25rem 0.625rem;
      border-radius: 9999px;
      margin-bottom: 0.5rem;
    }
    .job-title {
      font-size: 1.875rem;
      font-weight: 800;
      color: #1e293b;
      margin: 0 0 0.5rem;
      letter-spacing: -0.025em;
    }
    .meta-row {
      display: flex;
      gap: 1rem;
      font-size: 0.875rem;
      color: #64748b;
      flex-wrap: wrap;
    }
    .applied-badge {
      color: #166534;
      background: #dcfce7;
      padding: 0.15rem 0.5rem;
      border-radius: 9999px;
      font-weight: 700;
      font-size: 0.75rem;
    }
    .match-score-pill {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 0.625rem 1rem;
      border-radius: 12px;
      &.match-high { background: #dcfce7; color: #15803d; border: 1px solid #bbf7d0; }
      &.match-mid { background: #fef9c3; color: #a16207; border: 1px solid #fef08a; }
      &.match-low { background: #fee2e2; color: #b91c1c; border: 1px solid #fecaca; }
      .match-num { font-size: 1.35rem; font-weight: 800; }
      .match-label { font-size: 0.75rem; font-weight: 600; text-transform: uppercase; }
    }
    .section {
      margin-bottom: 2rem;
      h2 { font-size: 1.15rem; font-weight: 700; color: #1e293b; margin: 0 0 0.875rem; }
    }
    .skills-list { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .skill-pill {
      padding: 0.35rem 0.75rem;
      background: #f1f5f9;
      color: #334155;
      font-size: 0.8125rem;
      font-weight: 600;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .description-text {
      color: #475569;
      line-height: 1.65;
      font-size: 0.95rem;
      white-space: pre-line;
    }
    .action-btn {
      display: inline-block;
      margin-top: 1rem;
      background: #0d9488;
      color: white;
      padding: 0.5rem 1rem;
      border-radius: 8px;
      text-decoration: none;
      font-weight: 600;
    }
  `],
})
export class JobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly jobsService = inject(JobsService);

  readonly isLoading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly job = signal<JobDto | null>(null);

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const jobId = idParam ? parseInt(idParam, 10) : null;

    if (jobId) {
      this.jobsService.getJobById(jobId).subscribe({
        next: (data) => {
          this.job.set(data);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.isLoading.set(false);
          this.errorMessage.set(extractErrorMessage(err, 'Failed to load job details.'));
        },
      });
    } else {
      this.isLoading.set(false);
      this.errorMessage.set('Invalid job identifier.');
    }
  }

  getMatchPercent(job: JobDto): number | null {
    if (typeof job.myMatchPercent === 'number') return job.myMatchPercent;
    if (job.match && typeof job.match.percent === 'number') return job.match.percent;
    return null;
  }

  getMatchClass(percent: number | null): string {
    if (percent === null || percent === undefined) return '';
    if (percent >= 75) return 'match-high';
    if (percent >= 50) return 'match-mid';
    return 'match-low';
  }
}
