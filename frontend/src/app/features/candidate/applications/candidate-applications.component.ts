import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApplicationDto, ApplicationStatus } from '../../../core/models/application.models';
import { ApplicationsService } from '../../../core/services/applications.service';
import { AuthService } from '../../../core/services/auth.service';
import { RealtimeService } from '../../../core/services/realtime.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { UserProfileMenuComponent } from '../../../shared/components/user-profile-menu/user-profile-menu.component';
import { DATE_PRESET_OPTIONS, DatePreset, matchesDateFilter } from '../../../shared/utils/filter-date.util';

@Component({
  selector: 'app-candidate-applications',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe, PaginationComponent, UserProfileMenuComponent],
  templateUrl: './candidate-applications.component.html',
  styleUrl: './candidate-applications.component.scss',
})
export class CandidateApplicationsComponent implements OnInit, OnDestroy {
  private readonly applicationsService = inject(ApplicationsService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly sanitizer = inject(DomSanitizer);
  readonly authService = inject(AuthService);

  // State Signals
  readonly isLoading = signal<boolean>(true);
  readonly isWithdrawingId = signal<number | null>(null);
  readonly applications = signal<ApplicationDto[]>([]);
  readonly errorMessage = signal<string | null>(null);
  readonly successToast = signal<string | null>(null);
  readonly pendingWithdrawApp = signal<ApplicationDto | null>(null);
  readonly downloadingCvId = signal<number | null>(null);

  // CV Preview Modal Signals
  readonly showCvPreviewModal = signal<boolean>(false);
  readonly cvPreviewLoading = signal<boolean>(false);
  readonly cvPreviewError = signal<string | null>(null);
  readonly cvPreviewFileName = signal<string | null>(null);
  readonly cvPreviewBlob = signal<Blob | null>(null);
  readonly cvPreviewSafeUrl = signal<SafeResourceUrl | null>(null);
  readonly isDocxPreview = signal<boolean>(false);
  private cvPreviewObjectUrl: string | null = null;

  // Filtering Signals
  readonly searchTitleQuery = signal<string>('');
  readonly statusFilter = signal<string>('All');
  readonly companyFilter = signal<string>('All');
  readonly datePreset = signal<DatePreset>('all');
  readonly appliedFromDate = signal<string>('');
  readonly appliedToDate = signal<string>('');
  readonly showAdvancedFilters = signal<boolean>(false);
  readonly datePresetOptions = DATE_PRESET_OPTIONS;

  // Pagination Signals
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(10);

  // Sorted list: newest first (appliedAt desc)
  readonly sortedApplications = computed(() => {
    return [...this.applications()].sort((a, b) => {
      const dateA = new Date(a.appliedAt).getTime();
      const dateB = new Date(b.appliedAt).getTime();
      return dateB - dateA;
    });
  });

  // Dynamically extracted companies from candidate's own applications
  readonly availableCompanies = computed(() => {
    const set = new Set<string>();
    for (const app of this.applications()) {
      const name = app.companyName?.trim();
      if (name) {
        set.add(name);
      }
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  });

  readonly activeFilterCount = computed(() => {
    let count = 0;
    if (this.searchTitleQuery().trim()) count++;
    if (this.statusFilter() !== 'All') count++;
    if (this.companyFilter() !== 'All') count++;
    if (this.datePreset() !== 'all' || this.appliedFromDate() || this.appliedToDate()) count++;
    return count;
  });

  readonly hasActiveFilters = computed(() => this.activeFilterCount() > 0);

  // Filtered applications (candidate's own submitted applications)
  readonly filteredApplications = computed(() => {
    const titleQuery = this.searchTitleQuery().trim().toLowerCase();
    const status = this.statusFilter();
    const company = this.companyFilter();
    const preset = this.datePreset();
    const from = this.appliedFromDate();
    const to = this.appliedToDate();

    return this.sortedApplications().filter((app) => {
      if (status !== 'All' && app.status !== status) {
        return false;
      }

      if (titleQuery) {
        const matchesTitle = app.jobTitle?.toLowerCase().includes(titleQuery);
        if (!matchesTitle) {
          return false;
        }
      }

      if (company !== 'All') {
        if ((app.companyName?.trim() ?? '') !== company) {
          return false;
        }
      }

      if (!matchesDateFilter(app.appliedAt, preset, from, to)) {
        return false;
      }

      return true;
    });
  });

  readonly effectivePage = computed(() => {
    const totalPages = Math.max(1, Math.ceil(this.filteredApplications().length / this.pageSize()));
    return Math.min(this.currentPage(), totalPages);
  });

  readonly paginatedApplications = computed(() => {
    const page = this.effectivePage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return this.filteredApplications().slice(start, start + size);
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
  readonly withdrawnCount = computed(
    () => this.applications().filter((a) => a.status === 'Withdrawn').length
  );

  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.loadApplications();
    this.setupRealtimeListeners();
  }

  private setupRealtimeListeners(): void {
    this.realtime.applicationStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.applications.update((list) =>
          list.map((app) =>
            app.id === event.applicationId
              ? { ...app, status: event.newStatus }
              : app
          )
        );
      });

    this.realtime.applicationWithdrawn$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.applications.update((list) =>
          list.map((app) =>
            app.id === event.applicationId
              ? { ...app, status: 'Withdrawn' }
              : app
          )
        );
      });

    this.realtime.applicationSubmitted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadApplications();
      });

    this.realtime.jobUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.applications.update((list) =>
          list.map((app) =>
            app.jobId === event.job.id
              ? { ...app, jobTitle: event.job.title }
              : app
          )
        );
      });
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

  onTitleSearchInput(event: Event): void {
    this.searchTitleQuery.set((event.target as HTMLInputElement).value);
    this.currentPage.set(1);
  }

  clearTitleSearch(): void {
    this.searchTitleQuery.set('');
    this.currentPage.set(1);
  }

  onStatusFilterChange(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    this.currentPage.set(1);
  }

  onCompanyFilterChange(event: Event): void {
    this.companyFilter.set((event.target as HTMLSelectElement).value);
    this.currentPage.set(1);
  }

  onDatePresetChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value as DatePreset;
    this.datePreset.set(val);
    if (val !== 'custom') {
      this.appliedFromDate.set('');
      this.appliedToDate.set('');
    }
    this.currentPage.set(1);
  }

  onFromDateChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.appliedFromDate.set(val);
    if (val) {
      this.datePreset.set('custom');
    } else if (!this.appliedToDate()) {
      this.datePreset.set('all');
    }
    this.currentPage.set(1);
  }

  onToDateChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.appliedToDate.set(val);
    if (val) {
      this.datePreset.set('custom');
    } else if (!this.appliedFromDate()) {
      this.datePreset.set('all');
    }
    this.currentPage.set(1);
  }

  toggleAdvancedFilters(): void {
    this.showAdvancedFilters.update((v) => !v);
  }

  clearDateFilters(): void {
    this.datePreset.set('all');
    this.appliedFromDate.set('');
    this.appliedToDate.set('');
    this.currentPage.set(1);
  }

  clearAllFilters(): void {
    this.searchTitleQuery.set('');
    this.statusFilter.set('All');
    this.companyFilter.set('All');
    this.datePreset.set('all');
    this.appliedFromDate.set('');
    this.appliedToDate.set('');
    this.currentPage.set(1);
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    window?.scrollTo?.({ top: 0, behavior: 'smooth' });
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1);
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

  downloadCv(app: ApplicationDto): void {
    if (this.downloadingCvId()) return;

    this.downloadingCvId.set(app.id);
    this.applicationsService.downloadCv(app.id).subscribe({
      next: (blob) => {
        this.downloadingCvId.set(null);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = app.cvOriginalFileName || 'my-cv.pdf';
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        a.remove();
      },
      error: (err) => {
        this.downloadingCvId.set(null);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to download CV file.'));
      },
    });
  }

  openCvPreview(app: ApplicationDto): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }

    const fileName = app.cvOriginalFileName || 'my-cv.pdf';
    this.showCvPreviewModal.set(true);
    this.cvPreviewLoading.set(true);
    this.cvPreviewError.set(null);
    this.cvPreviewFileName.set(fileName);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);

    const isPdf = fileName.toLowerCase().endsWith('.pdf');
    this.isDocxPreview.set(!isPdf);

    this.applicationsService.downloadCv(app.id).subscribe({
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
        this.cvPreviewError.set(extractErrorMessage(err, 'Failed to load CV preview.'));
      },
    });
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
    const fileName = this.cvPreviewFileName() || 'my-cv.pdf';
    if (blob) {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      a.remove();
    }
  }

  ngOnDestroy(): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }
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
