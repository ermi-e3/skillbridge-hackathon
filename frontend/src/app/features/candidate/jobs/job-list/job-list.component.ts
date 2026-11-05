import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { JobDto } from '../../../../core/models/job.models';
import { SkillDto } from '../../../../core/models/profile.models';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { RealtimeService } from '../../../../core/services/realtime.service';
import { SkillsService } from '../../../../core/services/skills.service';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
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
  readonly selectedSkillIds = signal<number[]>([]);
  readonly skillFilterSearch = signal<string>('');
  readonly selectedWorkArrangement = signal<string>('All');
  readonly selectedJobType = signal<string>('All');
  readonly selectedExperienceLevel = signal<string>('All');
  readonly jobs = signal<JobDto[]>([]);

  // Filter skills for chip bar search
  readonly visibleSkills = computed(() => {
    const query = this.skillFilterSearch().trim().toLowerCase();
    const skills = this.allSkills();
    if (!query) return skills;
    return skills.filter((s) => s.name.toLowerCase().includes(query));
  });

  // Filtered/active count summary
  readonly selectedCount = computed(() => this.selectedSkillIds().length);

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
  }

  checkCandidateSkills(): void {
    if (this.authService.getRole() === 'Candidate') {
      this.profileService.getMyProfile().subscribe({
        next: (profile) => {
          const count = (profile.skills?.length ?? 0) || (profile.skillIds?.length ?? 0);
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
   * Load jobs from GET /api/jobs with optional skillIds query parameter
   */
  loadJobs(): void {
    this.isLoadingJobs.set(true);
    this.errorMessage.set(null);

    const filterIds = this.selectedSkillIds();
    const arrangement = this.selectedWorkArrangement() === 'All' ? undefined : this.selectedWorkArrangement();
    const type = this.selectedJobType() === 'All' ? undefined : this.selectedJobType();
    const exp = this.selectedExperienceLevel() === 'All' ? undefined : this.selectedExperienceLevel();

    const hasExtraFilters = arrangement !== undefined || type !== undefined || exp !== undefined;

    const call$ = hasExtraFilters
      ? this.jobsService.getJobs(filterIds, undefined, 'newest', 1, 50, type, arrangement, exp)
      : this.jobsService.getJobs(filterIds);

    call$.subscribe({
      next: (jobsList) => {
        this.jobs.set(jobsList);
        this.isLoadingJobs.set(false);
      },
      error: (err) => {
        this.isLoadingJobs.set(false);
        const msg = extractErrorMessage(err, 'Failed to load jobs. Please try again.');
        this.errorMessage.set(msg);
      },
    });
  }

  setWorkArrangement(val: string): void {
    this.selectedWorkArrangement.set(val);
    this.loadJobs();
  }

  setJobType(val: string): void {
    this.selectedJobType.set(val);
    this.loadJobs();
  }

  setExperienceLevel(val: string): void {
    this.selectedExperienceLevel.set(val);
    this.loadJobs();
  }

  clearAllFilters(): void {
    this.selectedSkillIds.set([]);
    this.selectedWorkArrangement.set('All');
    this.selectedJobType.set('All');
    this.selectedExperienceLevel.set('All');
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
    this.loadJobs();
  }

  isSkillSelected(skillId: number): boolean {
    return this.selectedSkillIds().includes(skillId);
  }

  clearSkillFilters(): void {
    if (this.selectedSkillIds().length === 0) return;
    this.selectedSkillIds.set([]);
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
}
