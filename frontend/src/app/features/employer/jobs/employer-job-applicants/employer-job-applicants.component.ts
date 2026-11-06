import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ApplicationStatus,
  JobApplicantItemDto,
  JobSummaryDto,
} from '../../../../core/models/application.models';
import { ApplicationsService } from '../../../../core/services/applications.service';
import { AuthService } from '../../../../core/services/auth.service';
import { RealtimeService } from '../../../../core/services/realtime.service';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';

@Component({
  selector: 'app-employer-job-applicants',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  templateUrl: './employer-job-applicants.component.html',
  styleUrl: './employer-job-applicants.component.scss',
})
export class EmployerJobApplicantsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly applicationsService = inject(ApplicationsService);
  private readonly authService = inject(AuthService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  readonly jobId = signal<number | null>(null);
  readonly jobSummary = signal<JobSummaryDto | null>(null);
  readonly applicants = signal<JobApplicantItemDto[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly statusFilter = signal<string>('All');
  readonly fullMatchOnly = signal<boolean>(false);
  readonly updatingApplicationId = signal<number | null>(null);
  readonly feedbackMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  readonly totalApplicantsCount = computed(() => this.applicants().length);
  readonly receivedCount = computed(
    () => this.applicants().filter((a) => a.status === 'Received').length
  );
  readonly shortlistedCount = computed(
    () => this.applicants().filter((a) => a.status === 'Shortlisted').length
  );
  readonly rejectedCount = computed(
    () => this.applicants().filter((a) => a.status === 'Rejected').length
  );
  readonly withdrawnCount = computed(
    () => this.applicants().filter((a) => a.status === 'Withdrawn').length
  );

  readonly canManageApplications = computed(() => {
    const summary = this.jobSummary();
    if (!summary) return false;
    if (typeof summary.canManageApplications === 'boolean') {
      return summary.canManageApplications;
    }
    if (summary.createdAt) {
      const created = new Date(summary.createdAt).getTime();
      return Date.now() - created <= 2 * 60 * 60 * 1000;
    }
    return true;
  });

  readonly remainingManagementTime = computed(() => {
    const summary = this.jobSummary();
    if (!summary?.createdAt) return '';
    const created = new Date(summary.createdAt).getTime();
    const twoHoursMs = 2 * 60 * 60 * 1000;
    const remainingMs = created + twoHoursMs - Date.now();
    if (remainingMs <= 0) return 'Expired';
    const totalMins = Math.floor(remainingMs / 60000);
    const hrs = Math.floor(totalMins / 60);
    const mins = totalMins % 60;
    return hrs > 0 ? `${hrs}h ${mins}m` : `${mins}m`;
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const parsedId = Number(idParam);

    if (!idParam || isNaN(parsedId) || parsedId <= 0) {
      this.errorMessage.set('Invalid job identifier specified.');
      this.isLoading.set(false);
      return;
    }

    this.jobId.set(parsedId);
    this.realtime.joinJobRoom(parsedId);
    this.destroyRef.onDestroy(() => {
      this.realtime.leaveJobRoom(parsedId);
    });

    this.loadApplicants();
    this.setupRealtimeListeners(parsedId);
  }

  private setupRealtimeListeners(jobId: number): void {
    this.realtime.applicationSubmitted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.jobId === jobId) {
          this.applicants.update((list) => {
            if (list.some((a) => a.applicationId === event.applicationId)) {
              return list;
            }
            return [event.applicant, ...list];
          });
        }
      });

    this.realtime.applicationWithdrawn$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.jobId === jobId) {
          this.applicants.update((list) =>
            list.map((applicant) =>
              applicant.applicationId === event.applicationId
                ? { ...applicant, status: 'Withdrawn' }
                : applicant
            )
          );
        }
      });

    this.realtime.applicationStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.jobId === jobId) {
          this.applicants.update((list) =>
            list.map((applicant) =>
              applicant.applicationId === event.applicationId
                ? { ...applicant, status: event.newStatus }
                : applicant
            )
          );
        }
      });

    this.realtime.jobUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.job.id === jobId) {
          this.loadApplicants();
        }
      });
  }

  loadApplicants(): void {
    const id = this.jobId();
    if (!id) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const filter = this.statusFilter();
    const queryStatus = filter === 'All' ? undefined : filter;

    this.applicationsService
      .getJobApplicants(id, queryStatus, this.fullMatchOnly())
      .subscribe({
        next: (response) => {
          this.jobSummary.set(response.job);
          this.applicants.set(response.applicants);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.errorMessage.set(
            extractErrorMessage(err, 'Unable to load applicants for this job. Please try again.')
          );
          this.isLoading.set(false);
        },
      });
  }

  setStatusFilter(status: string): void {
    if (this.statusFilter() === status) return;
    this.statusFilter.set(status);
    this.loadApplicants();
  }

  toggleFullMatch(): void {
    this.fullMatchOnly.update((v) => !v);
    this.loadApplicants();
  }

  updateStatus(applicant: JobApplicantItemDto, newStatus: 'Shortlisted' | 'Rejected'): void {
    if (applicant.status !== 'Received' || this.updatingApplicationId()) {
      return;
    }

    if (!this.canManageApplications()) {
      this.feedbackMessage.set({
        type: 'error',
        text: 'The 2-hour window for managing job applications has expired. Applicant statuses can no longer be updated.',
      });
      return;
    }

    this.updatingApplicationId.set(applicant.applicationId);
    this.feedbackMessage.set(null);

    this.applicationsService
      .changeStatus(applicant.applicationId, newStatus)
      .subscribe({
        next: (updated) => {
          this.applicants.update((list) =>
            list.map((item) =>
              item.applicationId === applicant.applicationId
                ? {
                    ...item,
                    status: updated.status,
                    statusChangedAt: updated.statusChangedAt ?? new Date().toISOString(),
                  }
                : item
            )
          );

          this.feedbackMessage.set({
            type: 'success',
            text: `${applicant.candidate.fullName} has been marked as ${newStatus}.`,
          });
          this.updatingApplicationId.set(null);
        },
        error: (err) => {
          this.feedbackMessage.set({
            type: 'error',
            text: extractErrorMessage(err, `Failed to update status to ${newStatus}.`),
          });
          this.updatingApplicationId.set(null);
        },
      });
  }

  getMatchBadgeClass(percent: number): string {
    if (percent >= 75) return 'match-high';
    if (percent >= 50) return 'match-medium';
    return 'match-low';
  }

  getStatusBadgeClass(status: ApplicationStatus): string {
    switch (status) {
      case 'Received':
        return 'status-received';
      case 'Shortlisted':
        return 'status-shortlisted';
      case 'Rejected':
        return 'status-rejected';
      case 'Withdrawn':
        return 'status-withdrawn';
      default:
        return 'status-default';
    }
  }

  signOut(): void {
    this.authService.logout();
  }
}
