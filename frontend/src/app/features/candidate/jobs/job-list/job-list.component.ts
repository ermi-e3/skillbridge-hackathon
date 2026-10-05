import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { JobDto } from '../../../../core/models/job.models';
import { SkillDto } from '../../../../core/models/profile.models';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
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
  readonly authService = inject(AuthService);

  // State Signals
  readonly isLoadingSkills = signal<boolean>(true);
  readonly isLoadingJobs = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);

  readonly allSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);
  readonly skillFilterSearch = signal<string>('');
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
    this.loadSkills();
    this.loadJobs();
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

    this.jobsService.getJobs(filterIds).subscribe({
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
