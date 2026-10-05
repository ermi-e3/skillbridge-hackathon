import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApplicationDto, ApplicationStatus } from '../../../core/models/application.models';
import { ApplicationsService } from '../../../core/services/applications.service';
import { AuthService } from '../../../core/services/auth.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

@Component({
  selector: 'app-candidate-applications',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  templateUrl: './candidate-applications.component.html',
  styleUrl: './candidate-applications.component.scss',
})
export class CandidateApplicationsComponent implements OnInit {
  private readonly applicationsService = inject(ApplicationsService);
  readonly authService = inject(AuthService);

  // State Signals
  readonly isLoading = signal<boolean>(true);
  readonly isWithdrawingId = signal<number | null>(null);
  readonly applications = signal<ApplicationDto[]>([]);
  readonly errorMessage = signal<string | null>(null);
  readonly successToast = signal<string | null>(null);
  readonly pendingWithdrawApp = signal<ApplicationDto | null>(null);

  // Sorted list: newest first (appliedAt desc)
  readonly sortedApplications = computed(() => {
    return [...this.applications()].sort((a, b) => {
      const dateA = new Date(a.appliedAt).getTime();
      const dateB = new Date(b.appliedAt).getTime();
      return dateB - dateA;
    });
  });

  // Summary Metrics
  readonly totalCount = computed(() => this.applications().length);
  readonly receivedCount = computed(
    () => this.applications().filter((a) => a.status === 'Received').length
  );
  readonly shortlistedCount = computed(
    () => this.applications().filter((a) => a.status === 'Shortlisted').length
  );
  readonly rejectedCount = computed(
    () => this.applications().filter((a) => a.status === 'Rejected').length
  );

  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.loadApplications();
  }

  loadApplications(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.applicationsService.getMyApplications().subscribe({
      next: (list) => {
        this.applications.set(list || []);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = extractErrorMessage(err, 'Failed to load your applications. Please try again.');
        this.errorMessage.set(msg);
      },
    });
  }

  /**
   * Status Badge color mapping:
   * Received = Blue, Shortlisted = Green, Rejected = Red, Withdrawn = Gray
   */
  getStatusBadgeClass(status: ApplicationStatus): string {
    switch (status) {
      case 'Received':
        return 'badge-blue';
      case 'Shortlisted':
        return 'badge-green';
      case 'Rejected':
        return 'badge-red';
      case 'Withdrawn':
        return 'badge-gray';
      default:
        return 'badge-gray';
    }
  }

  /**
   * Match % color band mapping:
   * Green >= 75%, Yellow 50-74%, Red < 50%
   */
  getMatchClass(percent: number | undefined | null): string {
    if (percent === undefined || percent === null) return 'match-none';
    if (percent >= 75) return 'match-high';
    if (percent >= 50) return 'match-mid';
    return 'match-low';
  }

  getMatchPercent(app: ApplicationDto): number {
    if (typeof app.matchPercent === 'number') {
      return app.matchPercent;
    }
    if (app.match && typeof app.match.percent === 'number') {
      return app.match.percent;
    }
    return 0;
  }

  promptWithdraw(app: ApplicationDto): void {
    if (app.status !== 'Received') return;
    this.pendingWithdrawApp.set(app);
  }

  cancelWithdraw(): void {
    this.pendingWithdrawApp.set(null);
  }

  confirmWithdraw(): void {
    const target = this.pendingWithdrawApp();
    if (!target || target.status !== 'Received' || this.isWithdrawingId() !== null) {
      return;
    }

    this.isWithdrawingId.set(target.id);
    this.dismissAlert();

    this.applicationsService.withdraw(target.id).subscribe({
      next: (updated) => {
        this.isWithdrawingId.set(null);
        this.pendingWithdrawApp.set(null);

        // Optimistically update status in local signal list
        this.applications.update((current) =>
          current.map((item) =>
            item.id === target.id ? { ...item, status: 'Withdrawn' as ApplicationStatus } : item
          )
        );

        this.showToast(`Application for "${target.jobTitle}" has been withdrawn.`);
      },
      error: (err) => {
        this.isWithdrawingId.set(null);
        this.pendingWithdrawApp.set(null);
        const msg = extractErrorMessage(err, 'Failed to withdraw application.');
        this.errorMessage.set(msg);
      },
    });
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  dismissToast(): void {
    this.successToast.set(null);
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
      this.toastTimeoutId = null;
    }
  }

  showToast(msg: string): void {
    this.dismissToast();
    this.successToast.set(msg);
    this.toastTimeoutId = setTimeout(() => {
      this.dismissToast();
    }, 4500);
  }
}
