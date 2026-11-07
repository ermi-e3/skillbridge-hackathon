import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  ApplicantProfileDto,
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
export class EmployerJobApplicantsComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly applicationsService = inject(ApplicationsService);
  private readonly authService = inject(AuthService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly sanitizer = inject(DomSanitizer);

  readonly jobId = signal<number | null>(null);
  readonly jobSummary = signal<JobSummaryDto | null>(null);
  readonly applicants = signal<JobApplicantItemDto[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly statusFilter = signal<string>('All');
  readonly fullMatchOnly = signal<boolean>(false);
  readonly updatingApplicationId = signal<number | null>(null);
  readonly feedbackMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Applicant Profile & CV Signals
  readonly selectedApplicantProfile = signal<ApplicantProfileDto | null>(null);
  readonly isLoadingProfile = signal<boolean>(false);
  readonly showProfileModal = signal<boolean>(false);
  readonly profileError = signal<string | null>(null);
  readonly downloadingCvApplicationId = signal<number | null>(null);

  // CV Preview Modal Signals
  readonly showCvPreviewModal = signal<boolean>(false);
  readonly cvPreviewLoading = signal<boolean>(false);
  readonly cvPreviewError = signal<string | null>(null);
  readonly cvPreviewApplicationId = signal<number | null>(null);
  readonly cvPreviewFileName = signal<string | null>(null);
  readonly cvPreviewCandidateName = signal<string | null>(null);
  readonly cvPreviewBlob = signal<Blob | null>(null);
  readonly cvPreviewSafeUrl = signal<SafeResourceUrl | null>(null);
  readonly isDocxPreview = signal<boolean>(false);
  private cvPreviewObjectUrl: string | null = null;

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

  // 2-hour window applies strictly to editing job details, NOT to hiring decisions (shortlisting/rejecting)
  readonly canEditJob = computed(() => {
    const summary = this.jobSummary();
    if (!summary) return false;
    if (typeof summary.canEditJob === 'boolean') {
      return summary.canEditJob;
    }
    if (summary.createdAt) {
      const created = new Date(summary.createdAt).getTime();
      return Date.now() - created <= 2 * 60 * 60 * 1000;
    }
    return true;
  });

  readonly remainingEditingTime = computed(() => {
    const summary = this.jobSummary();
    if (!summary?.createdAt) return '';
    const created = new Date(summary.createdAt).getTime();
    const twoHoursMs = 2 * 60 * 60 * 1000;
    const remainingMs = created + twoHoursMs - Date.now();
    if (remainingMs <= 0) return 'Expired';
    const totalMins = Math.floor(remainingMs / 60000);
    const hrs = Math.floor(totalMins / 60);
    const mins = totalMins % 60;
    return hrs > 0 ? `${hrs}h ${mins}m left` : `${mins}m left`;
  });

  // Applicant status updates (shortlisting and rejecting) are always allowed for the job owner
  readonly canManageApplications = computed(() => true);

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

  openApplicantProfile(applicant: JobApplicantItemDto): void {
    const jId = this.jobId();
    if (!jId) return;

    this.showProfileModal.set(true);
    this.isLoadingProfile.set(true);
    this.profileError.set(null);
    this.selectedApplicantProfile.set(null);

    this.applicationsService.getApplicantProfile(jId, applicant.candidate.userId).subscribe({
      next: (profile) => {
        this.selectedApplicantProfile.set(profile);
        this.isLoadingProfile.set(false);
      },
      error: (err) => {
        this.profileError.set(
          extractErrorMessage(err, 'Failed to load full candidate profile details.')
        );
        this.isLoadingProfile.set(false);
      },
    });
  }

  closeProfileModal(): void {
    this.showProfileModal.set(false);
    this.selectedApplicantProfile.set(null);
    this.profileError.set(null);
  }

  downloadCv(applicationId: number, originalFileName?: string | null): void {
    if (this.downloadingCvApplicationId()) return;

    this.downloadingCvApplicationId.set(applicationId);
    this.applicationsService.downloadCv(applicationId).subscribe({
      next: (blob) => {
        this.downloadingCvApplicationId.set(null);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = originalFileName || 'candidate-cv.pdf';
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        a.remove();
      },
      error: (err) => {
        this.downloadingCvApplicationId.set(null);
        this.feedbackMessage.set({
          type: 'error',
          text: extractErrorMessage(err, 'Failed to download candidate CV.'),
        });
      },
    });
  }

  formatCvSize(bytes?: number | null): string {
    if (!bytes || bytes <= 0) return '0 B';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  formatDateRange(startDate: string, endDate?: string | null, isCurrent?: boolean): string {
    const start = new Date(startDate);
    const startStr = start.toLocaleDateString(undefined, { year: 'numeric', month: 'short' });
    if (isCurrent) return `${startStr} - Present`;
    if (!endDate) return startStr;
    const end = new Date(endDate);
    const endStr = end.toLocaleDateString(undefined, { year: 'numeric', month: 'short' });
    return `${startStr} - ${endStr}`;
  }

  openCvPreview(applicationId: number, originalFileName?: string | null, candidateName?: string): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }

    this.showCvPreviewModal.set(true);
    this.cvPreviewLoading.set(true);
    this.cvPreviewError.set(null);
    this.cvPreviewApplicationId.set(applicationId);
    this.cvPreviewFileName.set(originalFileName || 'resume.pdf');
    this.cvPreviewCandidateName.set(candidateName || null);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);

    const isPdf = !originalFileName || originalFileName.toLowerCase().endsWith('.pdf');
    this.isDocxPreview.set(!isPdf);

    this.applicationsService.downloadCv(applicationId).subscribe({
      next: (blob) => {
        this.cvPreviewLoading.set(false);
        const mime = isPdf
          ? 'application/pdf'
          : blob.type || 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
        const typedBlob = new Blob([blob], { type: mime });
        this.cvPreviewBlob.set(typedBlob);
        this.cvPreviewObjectUrl = window.URL.createObjectURL(typedBlob);
        this.cvPreviewSafeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.cvPreviewObjectUrl));
      },
      error: (err) => {
        this.cvPreviewLoading.set(false);
        this.cvPreviewError.set(
          extractErrorMessage(err, 'Failed to load CV document for preview.')
        );
      },
    });
  }

  retryCvPreview(): void {
    const appId = this.cvPreviewApplicationId();
    if (appId) {
      this.openCvPreview(appId, this.cvPreviewFileName(), this.cvPreviewCandidateName() || undefined);
    }
  }

  closeCvPreview(): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }
    this.showCvPreviewModal.set(false);
    this.cvPreviewLoading.set(false);
    this.cvPreviewError.set(null);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);
  }

  downloadCurrentPreviewCv(): void {
    const blob = this.cvPreviewBlob();
    const fileName = this.cvPreviewFileName() || 'candidate-cv.pdf';
    if (blob) {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      a.remove();
    } else if (this.cvPreviewApplicationId()) {
      this.downloadCv(this.cvPreviewApplicationId()!, fileName);
    }
  }

  ngOnDestroy(): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }
  }

  signOut(): void {
    this.authService.logout();
  }
}
