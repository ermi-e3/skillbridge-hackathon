import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, HostListener, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { JobDto } from '../../../../core/models/job.models';
import { SkillDto } from '../../../../core/models/profile.models';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { RealtimeService } from '../../../../core/services/realtime.service';
import { SkillsService } from '../../../../core/services/skills.service';
import { calculateCountdown, CountdownDetails } from '../../../../core/utils/countdown';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';
import { PaginationComponent } from '../../../../shared/components/pagination/pagination.component';
import { UserProfileMenuComponent } from '../../../../shared/components/user-profile-menu/user-profile-menu.component';
import { DEPARTMENT_OPTIONS, jobMatchesDepartment } from '../../../../shared/utils/job-department.util';
import { extractSalaryFromJob } from '../../../../shared/utils/salary.util';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe, PaginationComponent, UserProfileMenuComponent],
  templateUrl: './job-list.component.html',
  styleUrl: './job-list.component.scss',
})
export class JobListComponent implements OnInit {
  private readonly jobsService = inject(JobsService);
  private readonly skillsService = inject(SkillsService);
  private readonly profileService = inject(ProfileService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  readonly authService = inject(AuthService);

  // State Signals
  readonly isLoadingSkills = signal<boolean>(true);
  readonly isLoadingJobs = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly hasSkillsRegistered = signal<boolean>(true);
  readonly showWelcomeBanner = signal<boolean>(false);

  readonly allSkills = signal<SkillDto[]>([]);
  readonly candidateSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);
  readonly skillFilterSearch = signal<string>('');
  readonly searchKeyword = signal<string>('');
  readonly activeDropdown = signal<string | null>(null);
  readonly selectedDepartment = signal<string>('all');
  readonly departmentOptions = DEPARTMENT_OPTIONS;
  readonly selectedWorkArrangement = signal<string>('All');
  readonly selectedJobType = signal<string>('All');
  readonly selectedExperienceLevel = signal<string>('All');
  readonly jobs = signal<JobDto[]>([]);
  readonly sortHighestMatch = signal<boolean>(false);

  // Pagination signals
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(9);
  readonly totalCount = signal<number>(0);
  readonly usedServerPagination = signal<boolean>(false);

  readonly effectiveTotalCount = computed(() => {
    if (this.selectedDepartment() !== 'all') {
      return this.displayedJobs().length;
    }
    return this.totalCount();
  });

  readonly displayedJobs = computed(() => {
    let list = [...this.jobs()];
    const dept = this.selectedDepartment();
    if (dept && dept !== 'all') {
      list = list.filter((job) =>
        jobMatchesDepartment(job.requiredSkills, job.preferredSkills, dept, job.category)
      );
    }
    if (this.sortHighestMatch()) {
      list.sort((a, b) => {
        const matchA = this.getMatchPercent(a) ?? -1;
        const matchB = this.getMatchPercent(b) ?? -1;
        return matchB - matchA;
      });
    }
    return list;
  });

  readonly paginatedJobs = computed(() => {
    if (this.usedServerPagination() && !this.sortHighestMatch() && this.selectedDepartment() === 'all') {
      return this.jobs();
    }
    const source = this.displayedJobs();
    const start = (this.currentPage() - 1) * this.pageSize();
    return source.slice(start, start + this.pageSize());
  });

  // Filter skills for multi-select dropdown search
  readonly visibleSkills = computed(() => {
    const query = this.skillFilterSearch().trim().toLowerCase();
    const skills = this.allSkills();
    if (!query) return skills;
    return skills.filter((s) => s.name.toLowerCase().includes(query));
  });

  // Filtered/active count summary
  readonly selectedCount = computed(() => this.selectedSkillIds().length);

  readonly hasActiveFilters = computed(() => {
    return (
      this.selectedSkillIds().length > 0 ||
      this.selectedDepartment() !== 'all' ||
      this.selectedWorkArrangement() !== 'All' ||
      this.selectedJobType() !== 'All' ||
      this.selectedExperienceLevel() !== 'All' ||
      this.searchKeyword().trim().length > 0
    );
  });

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.has('welcome')) {
      this.showWelcomeBanner.set(true);
    }

    this.checkCandidateSkills();
    this.loadSkills();
    this.loadJobs();
    this.setupRealtimeListeners();
  }

  private setupRealtimeListeners(): void {
    this.realtime.jobCreated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadJobs();
      });

    this.realtime.jobUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadJobs();
        this.loadSkills();
      });

    this.realtime.candidateProfileUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        const currentId = typeof this.authService?.currentUser === 'function'
          ? this.authService.currentUser()?.userId
          : (this.authService as any)?.getUserId?.();
        if (currentId && event.candidateId === currentId) {
          this.checkCandidateSkills();
          this.loadJobs();
        }
      });

    this.realtime.applicationSubmitted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        const currentId = typeof this.authService?.currentUser === 'function'
          ? this.authService.currentUser()?.userId
          : (this.authService as any)?.getUserId?.();
        if (currentId && event.candidateId === currentId) {
          this.jobs.update((list) =>
            list.map((job) =>
              job.id === event.jobId
                ? { ...job, hasApplied: true, applicationStatus: 'Received' }
                : job
            )
          );
        }
      });

    this.realtime.applicationWithdrawn$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        const currentId = typeof this.authService?.currentUser === 'function'
          ? this.authService.currentUser()?.userId
          : (this.authService as any)?.getUserId?.();
        if (currentId && event.candidateId === currentId) {
          this.jobs.update((list) =>
            list.map((job) =>
              job.id === event.jobId
                ? { ...job, hasApplied: true, applicationStatus: 'Withdrawn' }
                : job
            )
          );
        }
      });

    this.realtime.applicationStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        const currentId = typeof this.authService?.currentUser === 'function'
          ? this.authService.currentUser()?.userId
          : (this.authService as any)?.getUserId?.();
        if (currentId && event.candidateId === currentId) {
          this.jobs.update((list) =>
            list.map((job) =>
              job.id === event.jobId
                ? { ...job, applicationStatus: event.newStatus }
                : job
            )
          );
        }
      });

    this.realtime.jobStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        this.jobs.update((list) =>
          list.map((job) =>
            job.id === event.jobId
              ? { ...job, isOpen: event.isOpen, deadline: event.deadline ?? job.deadline }
              : job
          )
        );
      });
  }

  checkCandidateSkills(): void {
    if (this.authService.getRole() === 'Candidate') {
      this.profileService.getMyProfile().subscribe({
        next: (profile) => {
          const skills = profile.skills ?? [];
          this.candidateSkills.set(skills);
          const count = skills.length || (profile.skillIds?.length ?? 0);
          this.hasSkillsRegistered.set(count > 0);
        },
        error: () => {
          // Keep default if call fails
        },
      });
    }
  }

  dismissWelcome(): void {
    this.showWelcomeBanner.set(false);
  }

  /**
   * Load skills catalog from GET /api/skills
   */
  loadSkills(): void {
    this.isLoadingSkills.set(true);
    this.skillsService.getSkills().subscribe({
      next: (skills) => {
        this.allSkills.set(skills);
        this.isLoadingSkills.set(false);
      },
      error: () => {
        this.isLoadingSkills.set(false);
      },
    });
  }

  /**
   * Load jobs from GET /api/jobs with server-side pagination (with fallback for mocked getJobs)
   */
  loadJobs(): void {
    this.isLoadingJobs.set(true);
    this.errorMessage.set(null);

    const filterIds = this.selectedSkillIds();
    const search = this.searchKeyword().trim() || undefined;
    const arrangement = this.selectedWorkArrangement() === 'All' ? undefined : this.selectedWorkArrangement();
    const type = this.selectedJobType() === 'All' ? undefined : this.selectedJobType();
    const exp = this.selectedExperienceLevel() === 'All' ? undefined : this.selectedExperienceLevel();
    const fetchPageSize = this.selectedDepartment() !== 'all' ? 50 : this.pageSize();

    if (typeof this.jobsService.getJobsPaged === 'function') {
      this.jobsService
        .getJobsPaged(
          filterIds,
          search,
          'newest',
          this.currentPage(),
          fetchPageSize,
          type,
          arrangement,
          exp
        )
        .subscribe({
          next: (res) => {
            const items = res.items ?? [];
            this.usedServerPagination.set(true);
            this.jobs.set(items);
            this.totalCount.set(res.totalCount ?? items.length);
            this.isLoadingJobs.set(false);
          },
          error: (err) => {
            this.isLoadingJobs.set(false);
            const msg = extractErrorMessage(err, 'Failed to load jobs. Please try again.');
            this.errorMessage.set(msg);
          },
        });
      return;
    }

    const hasExtraFilters = arrangement !== undefined || type !== undefined || exp !== undefined || search !== undefined;
    const call$ = hasExtraFilters
      ? this.jobsService.getJobs(filterIds, search, 'newest', 1, 50, type, arrangement, exp)
      : this.jobsService.getJobs(filterIds);

    call$.subscribe({
      next: (jobsList) => {
        this.usedServerPagination.set(false);
        this.jobs.set(jobsList);
        this.totalCount.set(jobsList.length);
        this.isLoadingJobs.set(false);
      },
      error: (err) => {
        this.isLoadingJobs.set(false);
        const msg = extractErrorMessage(err, 'Failed to load jobs. Please try again.');
        this.errorMessage.set(msg);
      },
    });
  }

  onPageChange(page: number): void {
    this.currentPage.set(page);
    if (this.usedServerPagination()) {
      this.loadJobs();
    }
    window?.scrollTo?.({ top: 0, behavior: 'smooth' });
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1);
    if (this.usedServerPagination()) {
      this.loadJobs();
    }
  }

  toggleDropdown(name: string, event?: MouseEvent): void {
    if (event) {
      event.stopPropagation();
    }
    this.activeDropdown.update((curr) => (curr === name ? null : name));
  }

  closeDropdowns(): void {
    this.activeDropdown.set(null);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.filter-dropdown-container')) {
      this.closeDropdowns();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeDropdowns();
  }

  onSearchInput(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchKeyword.set(val);
    this.currentPage.set(1);
    this.loadJobs();
  }

  clearSearch(): void {
    this.searchKeyword.set('');
    this.currentPage.set(1);
    this.loadJobs();
  }

  getSkillName(skillId: number): string {
    const skill = this.allSkills().find((s) => s.id === skillId);
    return skill ? skill.name : `Skill #${skillId}`;
  }

  formatArrangement(val: string): string {
    switch (val) {
      case 'OnSite':
        return 'On-site';
      default:
        return val;
    }
  }

  formatJobType(val: string): string {
    switch (val) {
      case 'FullTime':
        return 'Full-time';
      case 'PartTime':
        return 'Part-time';
      default:
        return val;
    }
  }

  formatExperienceLevel(val: string): string {
    switch (val) {
      case 'EntryLevel':
        return 'Entry-level';
      case 'MidLevel':
        return 'Mid-level';
      default:
        return val;
    }
  }

  setDepartment(val: string): void {
    this.selectedDepartment.set(val);
    this.currentPage.set(1);
    this.closeDropdowns();
    this.loadJobs();
  }

  getDepartmentName(id: string): string {
    const found = this.departmentOptions.find((d) => d.id === id);
    return found ? found.name : id;
  }

  setWorkArrangement(val: string): void {
    this.selectedWorkArrangement.set(val);
    this.currentPage.set(1);
    this.closeDropdowns();
    this.loadJobs();
  }

  setJobType(val: string): void {
    this.selectedJobType.set(val);
    this.currentPage.set(1);
    this.closeDropdowns();
    this.loadJobs();
  }

  setExperienceLevel(val: string): void {
    this.selectedExperienceLevel.set(val);
    this.currentPage.set(1);
    this.closeDropdowns();
    this.loadJobs();
  }

  clearAllFilters(): void {
    this.selectedSkillIds.set([]);
    this.selectedDepartment.set('all');
    this.selectedWorkArrangement.set('All');
    this.selectedJobType.set('All');
    this.selectedExperienceLevel.set('All');
    this.searchKeyword.set('');
    this.skillFilterSearch.set('');
    this.currentPage.set(1);
    this.closeDropdowns();
    this.loadJobs();
  }

  /**
   * Toggle skill chip selection and re-query jobs
   */
  toggleSkillFilter(skillId: number): void {
    const current = [...this.selectedSkillIds()];
    const index = current.indexOf(skillId);

    if (index > -1) {
      current.splice(index, 1);
    } else {
      current.push(skillId);
    }

    this.selectedSkillIds.set(current);
    this.currentPage.set(1);
    this.loadJobs();
  }

  isSkillSelected(skillId: number): boolean {
    return this.selectedSkillIds().includes(skillId);
  }

  clearSkillFilters(): void {
    if (this.selectedSkillIds().length === 0) return;
    this.selectedSkillIds.set([]);
    this.currentPage.set(1);
    this.loadJobs();
  }

  onSkillSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.skillFilterSearch.set(value);
  }

  clearSkillSearch(): void {
    this.skillFilterSearch.set('');
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  getCountdown(deadline?: string | null, isOpen: boolean = true): CountdownDetails {
    return calculateCountdown(deadline, isOpen);
  }

  /**
   * Extract match percentage from job (supports both myMatchPercent and match.percent)
   */
  getMatchPercent(job: JobDto): number | null {
    if (typeof job.myMatchPercent === 'number') {
      return job.myMatchPercent;
    }
    if (job.match && typeof job.match.percent === 'number') {
      return job.match.percent;
    }
    return null;
  }

  /**
   * Determine color-coded class for match percentage:
   * Green >= 75%, Yellow 50-74%, Red < 50%
   */
  getMatchClass(percent: number | null): 'match-high' | 'match-mid' | 'match-low' | 'match-none' {
    if (percent === null || percent === undefined) {
      return 'match-none';
    }
    if (percent >= 75) {
      return 'match-high';
    }
    if (percent >= 50) {
      return 'match-mid';
    }
    return 'match-low';
  }

  @HostListener('window:keydown', ['$event'])
  onWindowKeyDown(event: KeyboardEvent): void {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      const input = document.getElementById('mainJobSearchInput') as HTMLInputElement | null;
      input?.focus();
    }
  }

  toggleSortHighestMatch(): void {
    this.sortHighestMatch.update((v) => !v);
    this.currentPage.set(1);
  }

  isJobMatchedSkill(skillId: number, job: JobDto): boolean {
    if (job.match?.matchedSkills && job.match.matchedSkills.length > 0) {
      return job.match.matchedSkills.some((s) => s.id === skillId);
    }
    return this.candidateSkills().some((s) => s.id === skillId);
  }

  getJobSalary(job: JobDto): string {
    return extractSalaryFromJob(job).displayText;
  }

  isJobSalaryNegotiable(job: JobDto): boolean {
    return extractSalaryFromJob(job).isNegotiable;
  }
}
