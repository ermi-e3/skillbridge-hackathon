import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { EmployerJobDto } from '../../../core/models/job.models';
import { ApplicantCounts } from '../../../core/models/realtime.models';
import { AuthService } from '../../../core/services/auth.service';
import { JobsService } from '../../../core/services/jobs.service';
import { RealtimeService } from '../../../core/services/realtime.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

@Component({
  selector: 'app-employer-jobs',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  templateUrl: './employer-jobs.component.html',
  styleUrl: './employer-jobs.component.scss',
})
export class EmployerJobsComponent implements OnInit {
  private readonly jobsService = inject(JobsService);
  private readonly authService = inject(AuthService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  readonly jobs = signal<EmployerJobDto[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadJobs();
    this.setupRealtimeListeners();
  }

  private setupRealtimeListeners(): void {
    this.realtime.applicationSubmitted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.updateJobApplicantCounts(event.jobId, event.counts);
      });

    this.realtime.applicationWithdrawn$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.updateJobApplicantCounts(event.jobId, event.counts);
      });

    this.realtime.applicationStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.updateJobApplicantCounts(event.jobId, event.counts);
      });

    this.realtime.jobCreated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadJobs();
      });
  }

  private updateJobApplicantCounts(jobId: number, counts: ApplicantCounts): void {
    this.jobs.update((list) =>
      list.map((job) =>
        job.id === jobId
          ? {
              ...job,
              applicantCounts: {
                total: counts.total,
                received: counts.received,
                shortlisted: counts.shortlisted,
                rejected: counts.rejected,
                withdrawn: counts.withdrawn,
              },
            }
          : job
      )
    );
  }

  signOut(): void {
    this.authService.logout();
  }

  loadJobs(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.jobsService.getEmployerJobs().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.isLoading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(
          extractErrorMessage(error, 'Unable to load your jobs. Please try again.')
        );
        this.isLoading.set(false);
      },
    });
  }

  formatJobType(type?: string): string {
    switch (type) {
      case 'FullTime':
        return 'Full-time';
      case 'PartTime':
        return 'Part-time';
      case 'Contract':
        return 'Contract';
      case 'Internship':
        return 'Internship';
      case 'Freelance':
        return 'Freelance';
      default:
        return type || 'Full-time';
    }
  }

  formatWorkArrangement(arrangement?: string): string {
    switch (arrangement) {
      case 'Remote':
        return 'Remote';
      case 'Hybrid':
        return 'Hybrid';
      case 'OnSite':
        return 'On-site';
      default:
        return arrangement || 'On-site';
    }
  }

  formatExperienceLevel(level?: string): string {
    switch (level) {
      case 'EntryLevel':
        return 'Entry Level';
      case 'Junior':
        return 'Junior';
      case 'MidLevel':
        return 'Mid Level';
      case 'Senior':
        return 'Senior';
      case 'Lead':
        return 'Lead / Principal';
      default:
        return level || 'Entry Level';
    }
  }
}
