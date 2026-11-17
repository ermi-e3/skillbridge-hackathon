import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmployerJobDto } from '../../../core/models/job.models';
import { JobsService } from '../../../core/services/jobs.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';
import { UserProfileMenuComponent } from '../../../shared/components/user-profile-menu/user-profile-menu.component';

export type ApplicationFilterType =
  | 'all'
  | 'active'
  | 'closed'
  | 'has_applicants'
  | 'pending'
  | 'shortlisted';

@Component({
  selector: 'app-employer-applications',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe, UserProfileMenuComponent],
  templateUrl: './employer-applications.component.html',
  styleUrl: './employer-applications.component.scss',
})
export class EmployerApplicationsComponent implements OnInit {
  private readonly jobsService = inject(JobsService);

  readonly jobs = signal<EmployerJobDto[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly searchQuery = signal<string>('');
  readonly statusFilter = signal<ApplicationFilterType>('all');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(6);
  readonly pageSizeOptions: number[] = [6, 10, 12];

  readonly totalApplications = computed(() =>
    this.jobs().reduce((sum, job) => sum + (job.applicantCounts?.total ?? 0), 0)
  );

  readonly totalReceived = computed(() =>
    this.jobs().reduce((sum, job) => sum + (job.applicantCounts?.received ?? 0), 0)
  );

  readonly totalShortlisted = computed(() =>
    this.jobs().reduce((sum, job) => sum + (job.applicantCounts?.shortlisted ?? 0), 0)
  );

  readonly allCount = computed(() => this.jobs().length);
  readonly activeCount = computed(() => this.jobs().filter((j) => j.isOpen).length);
  readonly closedCount = computed(() => this.jobs().filter((j) => !j.isOpen).length);
  readonly hasApplicantsCount = computed(
    () => this.jobs().filter((j) => (j.applicantCounts?.total ?? 0) > 0).length
  );

  readonly filteredJobs = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const filter = this.statusFilter();

    return [...this.jobs()]
      .filter((j) => {
        // Search filter matching title, category, or location
        if (
          query &&
          !j.title.toLowerCase().includes(query) &&
          !(j.category?.toLowerCase() || '').includes(query) &&
          !(j.location?.toLowerCase() || '').includes(query)
        ) {
          return false;
        }

        // Status / Telemetry filter
        switch (filter) {
          case 'active':
            return j.isOpen;
          case 'closed':
            return !j.isOpen;
          case 'has_applicants':
            return (j.applicantCounts?.total ?? 0) > 0;
          case 'pending':
            return (j.applicantCounts?.received ?? 0) > 0;
          case 'shortlisted':
            return (j.applicantCounts?.shortlisted ?? 0) > 0;
          case 'all':
          default:
            return true;
        }
      })
      .sort((a, b) => (b.applicantCounts?.total ?? 0) - (a.applicantCounts?.total ?? 0));
  });

  readonly totalPages = computed(() => {
    const size = Math.max(1, this.pageSize());
    return Math.max(1, Math.ceil(this.filteredJobs().length / size));
  });

  readonly paginatedJobs = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize();
    return this.filteredJobs().slice(start, start + this.pageSize());
  });

  readonly showingRange = computed(() => {
    const total = this.filteredJobs().length;
    if (total === 0) return { start: 0, end: 0, total: 0 };
    const start = (this.currentPage() - 1) * this.pageSize() + 1;
    const end = Math.min(this.currentPage() * this.pageSize(), total);
    return { start, end, total };
  });

  readonly pagesArray = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  readonly hasActiveFilter = computed(
    () => this.searchQuery().trim() !== '' || this.statusFilter() !== 'all'
  );

  ngOnInit(): void {
    this.loadEmployerJobs();
  }

  loadEmployerJobs(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.jobsService.getEmployerJobs().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs ?? []);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(
          extractErrorMessage(err, 'Unable to load job application summaries. Please try again.')
        );
      },
    });
  }

  onSearchInput(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.currentPage.set(1);
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.currentPage.set(1);
  }

  setStatusFilter(filter: ApplicationFilterType): void {
    this.statusFilter.set(filter);
    this.currentPage.set(1);
  }

  resetFilters(): void {
    this.searchQuery.set('');
    this.statusFilter.set('all');
    this.currentPage.set(1);
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update((p) => p + 1);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update((p) => p - 1);
    }
  }

  onPageSizeChange(event: Event): void {
    const newSize = Number((event.target as HTMLSelectElement).value);
    if (newSize > 0) {
      this.pageSize.set(newSize);
      this.currentPage.set(1);
    }
  }

  padZero(val: number): string {
    return val < 10 ? `0${val}` : `${val}`;
  }
}
