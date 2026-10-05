import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ApplicationStatus,
  JobApplicantItemDto,
  JobSummaryDto,
} from '../../../../core/models/application.models';
import { ApplicationsService } from '../../../../core/services/applications.service';
import { AuthService } from '../../../../core/services/auth.service';
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

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const parsedId = Number(idParam);

    if (!idParam || isNaN(parsedId) || parsedId <= 0) {
      this.errorMessage.set('Invalid job identifier specified.');
      this.isLoading.set(false);
      return;
    }

    this.jobId.set(parsedId);
    this.loadApplicants();
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
