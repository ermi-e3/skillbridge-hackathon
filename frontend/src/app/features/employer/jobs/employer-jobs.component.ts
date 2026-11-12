import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { EmployerJobDto } from '../../../core/models/job.models';
import { ApplicantCounts } from '../../../core/models/realtime.models';
import { AuthService } from '../../../core/services/auth.service';
import { JobsService } from '../../../core/services/jobs.service';
import { RealtimeService } from '../../../core/services/realtime.service';
import { calculateCountdown, CountdownDetails } from '../../../core/utils/countdown';
import { extractErrorMessage } from '../../../core/utils/error-formatter';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { UserProfileMenuComponent } from '../../../shared/components/user-profile-menu/user-profile-menu.component';
import { DATE_PRESET_OPTIONS, DatePreset, matchesDateFilter } from '../../../shared/utils/filter-date.util';
import {
  DEPARTMENT_OPTIONS,
  getPrimaryJobDepartmentLabel,
  jobMatchesDepartment,
} from '../../../shared/utils/job-department.util';

@Component({
  selector: 'app-employer-jobs',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe, PaginationComponent, UserProfileMenuComponent],
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
  readonly closingJobId = signal<number | null>(null);
  readonly jobToClose = signal<EmployerJobDto | null>(null);

  // Filter Signals (Default view shows only Active jobs; filter can apply Closed status)
  readonly searchTitleQuery = signal<string>('');
  readonly departmentFilter = signal<string>('all');
  readonly jobTypeFilter = signal<string>('All');
  readonly statusFilter = signal<'Active' | 'Closed'>('Active');
  readonly datePreset = signal<DatePreset>('all');
  readonly postedFromDate = signal<string>('');
  readonly postedToDate = signal<string>('');
  readonly showAdvancedFilters = signal<boolean>(false);

  readonly departmentOptions = DEPARTMENT_OPTIONS;
  readonly datePresetOptions = DATE_PRESET_OPTIONS;

  // Pagination Signals
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(6);

  readonly activeJobsCount = computed(() => this.jobs().filter((j) => j.isOpen).length);
  readonly closedJobsCount = computed(() => this.jobs().filter((j) => !j.isOpen).length);

  readonly activeFilterCount = computed(() => {
    let count = 0;
    if (this.searchTitleQuery().trim()) count++;
    if (this.departmentFilter() !== 'all') count++;
    if (this.jobTypeFilter() !== 'All') count++;
    if (this.statusFilter() === 'Closed') count++;
    if (this.datePreset() !== 'all' || this.postedFromDate() || this.postedToDate()) count++;
    return count;
  });

  readonly hasActiveFilters = computed(() => this.activeFilterCount() > 0);

  readonly filteredJobs = computed(() => {
    const query = this.searchTitleQuery().trim().toLowerCase();
    const dept = this.departmentFilter();
    const jobType = this.jobTypeFilter();
    const status = this.statusFilter();
    const preset = this.datePreset();
    const from = this.postedFromDate();
    const to = this.postedToDate();

    return this.jobs().filter((job) => {
      if (query && !job.title?.toLowerCase().includes(query)) {
        return false;
      }

      if (dept !== 'all' && !jobMatchesDepartment(job.requiredSkills, job.preferredSkills, dept, job.category)) {
        return false;
      }

      if (jobType !== 'All' && (job.jobType ?? 'FullTime') !== jobType) {
        return false;
      }

      if (status === 'Active' && !job.isOpen) {
        return false;
      }
      if (status === 'Closed' && job.isOpen) {
        return false;
      }

      if (!matchesDateFilter(job.createdAt, preset, from, to)) {
        return false;
      }

      return true;
    });
  });

  readonly effectivePage = computed(() => {
    const totalPages = Math.max(1, Math.ceil(this.filteredJobs().length / this.pageSize()));
    return Math.min(this.currentPage(), totalPages);
  });

  readonly paginatedJobs = computed(() => {
    const page = this.effectivePage();
    const size = this.pageSize();
    const start = (page - 1) * size;
    return this.filteredJobs().slice(start, start + size);
  });

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

    this.realtime.jobUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadJobs();
      });

    this.realtime.jobStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.jobs.update((list) =>
          list.map((job) =>
            job.id === event.jobId
              ? { ...job, isOpen: event.isOpen }
              : job
          )
        );
      });
  }

  openCloseConfirm(job: EmployerJobDto): void {
    this.jobToClose.set(job);
  }

  dismissCloseConfirm(): void {
    this.jobToClose.set(null);
  }

  confirmCloseJob(): void {
    const job = this.jobToClose();
    if (!job || this.closingJobId()) return;

    this.closingJobId.set(job.id);
    this.jobsService.closeJob(job.id).subscribe({
      next: () => {
        this.jobs.update((list) =>
          list.map((j) => (j.id === job.id ? { ...j, isOpen: false } : j))
        );
        this.closingJobId.set(null);
        this.jobToClose.set(null);
      },
      error: (err) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to close job.'));
        this.closingJobId.set(null);
        this.jobToClose.set(null);
      },
    });
  }

  getCountdown(deadline?: string | null, isOpen: boolean = true): CountdownDetails {
    return calculateCountdown(deadline, isOpen);
  }

  isEditingAllowed(createdAt: string): boolean {
    const createdTime = new Date(createdAt).getTime();
    const twoHoursMs = 2 * 60 * 60 * 1000;
    return Date.now() - createdTime <= twoHoursMs;
  }

  getRemainingEditingTime(createdAt: string): string {
    const createdTime = new Date(createdAt).getTime();
    const twoHoursMs = 2 * 60 * 60 * 1000;
    const remainingMs = createdTime + twoHoursMs - Date.now();
    if (remainingMs <= 0) return 'Expired';
    const totalMinutes = Math.floor(remainingMs / 60000);
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return hours > 0 ? `${hours}h ${minutes}m left` : `${minutes}m left`;
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

  onTitleSearchInput(event: Event): void {
    this.searchTitleQuery.set((event.target as HTMLInputElement).value);
    this.currentPage.set(1);
  }

  clearTitleSearch(): void {
    this.searchTitleQuery.set('');
    this.currentPage.set(1);
  }

  onDepartmentFilterChange(event: Event): void {
    this.departmentFilter.set((event.target as HTMLSelectElement).value);
    this.currentPage.set(1);
  }

  onJobTypeFilterChange(event: Event): void {
    this.jobTypeFilter.set((event.target as HTMLSelectElement).value);
    this.currentPage.set(1);
  }

  onStatusFilterChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(val === 'Closed' ? 'Closed' : 'Active');
    this.currentPage.set(1);
  }

  toggleClosedFilter(): void {
    this.statusFilter.update((s) => (s === 'Closed' ? 'Active' : 'Closed'));
    this.currentPage.set(1);
  }

  onDatePresetChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value as DatePreset;
    this.datePreset.set(val);
    if (val !== 'custom') {
      this.postedFromDate.set('');
      this.postedToDate.set('');
    }
    this.currentPage.set(1);
  }

  onPostedFromChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.postedFromDate.set(val);
    if (val) {
      this.datePreset.set('custom');
    } else if (!this.postedToDate()) {
      this.datePreset.set('all');
    }
    this.currentPage.set(1);
  }

  onPostedToChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.postedToDate.set(val);
    if (val) {
      this.datePreset.set('custom');
    } else if (!this.postedFromDate()) {
      this.datePreset.set('all');
    }
    this.currentPage.set(1);
  }

  toggleAdvancedFilters(): void {
    this.showAdvancedFilters.update((v) => !v);
  }

  clearDateFilters(): void {
    this.datePreset.set('all');
    this.postedFromDate.set('');
    this.postedToDate.set('');
    this.currentPage.set(1);
  }

  clearAllFilters(): void {
    this.searchTitleQuery.set('');
    this.departmentFilter.set('all');
    this.jobTypeFilter.set('All');
    this.statusFilter.set('Active');
    this.datePreset.set('all');
    this.postedFromDate.set('');
    this.postedToDate.set('');
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

  getDepartmentLabel(job: EmployerJobDto): string | null {
    return getPrimaryJobDepartmentLabel(job.requiredSkills, job.preferredSkills, job.category);
  }

  getDepartmentNameById(deptId: string): string {
    return this.departmentOptions.find((d) => d.id === deptId)?.name ?? deptId;
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
