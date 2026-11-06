import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CreateJobRequest, UpdateJobRequest } from '../../../../core/models/job.models';
import { SkillDto } from '../../../../core/models/profile.models';
import { JobsService } from '../../../../core/services/jobs.service';
import { SkillsService } from '../../../../core/services/skills.service';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';

/**
 * Custom validator ensuring at least 1 and at most 25 skills (including custom tags) are selected
 */
export const requiredSkillsValidator = (min = 1, max = 25, getCustomCount?: () => number): ValidatorFn => {
  return (control: AbstractControl): ValidationErrors | null => {
    const ids = control.value as number[] | null;
    const customCount = getCustomCount ? getCustomCount() : 0;
    const total = (ids?.length ?? 0) + customCount;
    if (total < min) {
      return { minSkills: { min, actual: total } };
    }
    if (total > max) {
      return { maxSkills: { max, actual: total } };
    }
    return null;
  };
};

@Component({
  selector: 'app-employer-post-job',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './employer-post-job.component.html',
  styleUrl: './employer-post-job.component.scss',
})
export class EmployerPostJobComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  readonly jobsService = inject(JobsService);
  readonly skillsService = inject(SkillsService);
  readonly router = inject(Router);
  readonly route = inject(ActivatedRoute);

  // Edit Mode & State Signals
  readonly jobId = signal<number | null>(null);
  readonly isEditMode = computed(() => this.jobId() !== null);
  readonly isEditingExpired = signal<boolean>(false);
  readonly editingTimeRemaining = signal<string>('');

  readonly isLoadingSkills = signal<boolean>(true);
  readonly isLoadingJob = signal<boolean>(false);
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly skillSearchQuery = signal<string>('');

  // Custom Skills
  readonly customSkillInput = signal<string>('');
  readonly customSkills = signal<string[]>([]);

  // Skills Catalog & Selected IDs
  readonly allSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);
  readonly selectedPreferredSkillIds = signal<number[]>([]);
  readonly preferredSkillSearchQuery = signal<string>('');

  // Filtered skills based on chip search input
  readonly filteredSkills = computed(() => {
    const query = this.skillSearchQuery().trim().toLowerCase();
    const skills = this.allSkills();
    if (!query) return skills;
    return skills.filter((s) => s.name.toLowerCase().includes(query));
  });

  // Selected skill objects for quick display
  readonly selectedSkills = computed(() => {
    const selectedIds = new Set(this.selectedSkillIds());
    return this.allSkills().filter((s) => selectedIds.has(s.id));
  });

  // Total required skills count (catalog + custom tags)
  readonly totalRequiredSkillsCount = computed(() => {
    return this.selectedSkillIds().length + this.customSkills().length;
  });

  // Filtered preferred skills based on preferred search input
  readonly filteredPreferredSkills = computed(() => {
    const query = this.preferredSkillSearchQuery().trim().toLowerCase();
    const skills = this.allSkills();
    if (!query) return skills;
    return skills.filter((s) => s.name.toLowerCase().includes(query));
  });

  // Selected preferred skill objects for quick display
  readonly selectedPreferredSkills = computed(() => {
    const selectedIds = new Set(this.selectedPreferredSkillIds());
    return this.allSkills().filter((s) => selectedIds.has(s.id));
  });

  // Reactive Form
  readonly jobForm: FormGroup = this.fb.group({
    title: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    location: ['', [Validators.maxLength(100)]],
    jobType: ['FullTime', [Validators.required]],
    workArrangement: ['Remote', [Validators.required]],
    experienceLevel: ['MidLevel', [Validators.required]],
    minExperienceYears: [0, [Validators.min(0), Validators.max(50)]],
    deadline: [''],
    description: ['', [Validators.required, Validators.minLength(20), Validators.maxLength(4000)]],
    requiredSkillIds: [[], [requiredSkillsValidator(1, 25, () => this.customSkills().length)]],
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      const parsedId = Number(idParam);
      if (!isNaN(parsedId) && parsedId > 0) {
        this.jobId.set(parsedId);
      }
    }

    this.loadSkills();
    if (this.isEditMode()) {
      this.loadJobForEdit(this.jobId()!);
    }
  }

  loadJobForEdit(id: number): void {
    this.isLoadingJob.set(true);
    this.jobsService.getJobById(id).subscribe({
      next: (job) => {
        this.isLoadingJob.set(false);
        const created = new Date(job.createdAt).getTime();
        const now = Date.now();
        const twoHoursMs = 2 * 60 * 60 * 1000;

        if (now - created > twoHoursMs) {
          this.isEditingExpired.set(true);
          this.jobForm.disable();
        } else {
          const remainingMs = created + twoHoursMs - now;
          const mins = Math.floor(remainingMs / 60000);
          const hrs = Math.floor(mins / 60);
          const remMins = mins % 60;
          this.editingTimeRemaining.set(hrs > 0 ? `${hrs}h ${remMins}m` : `${remMins}m`);
        }

        const reqIds = job.requiredSkills.map((s) => s.id);
        const prefIds = job.preferredSkills ? job.preferredSkills.map((s) => s.id) : [];

        this.selectedSkillIds.set(reqIds);
        this.selectedPreferredSkillIds.set(prefIds);

        this.jobForm.patchValue({
          title: job.title,
          description: job.description,
          location: job.location || '',
          jobType: job.jobType || 'FullTime',
          workArrangement: job.workArrangement || 'Remote',
          experienceLevel: job.experienceLevel || 'MidLevel',
          minExperienceYears: job.minExperienceYears || 0,
          deadline: job.deadline ? this.formatDateForInput(job.deadline) : '',
          requiredSkillIds: reqIds,
        });

        if (this.isEditingExpired()) {
          this.jobForm.disable();
        }
      },
      error: (err) => {
        this.isLoadingJob.set(false);
        this.errorMessage.set(extractErrorMessage(err, 'Failed to load job details for editing.'));
      },
    });
  }

  formatDateForInput(dateStr: string): string {
    const d = new Date(dateStr);
    const pad = (n: number) => (n < 10 ? '0' + n : n);
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  get titleControl() {
    return this.jobForm.get('title');
  }

  get locationControl() {
    return this.jobForm.get('location');
  }

  get descriptionControl() {
    return this.jobForm.get('description');
  }

  get requiredSkillIdsControl() {
    return this.jobForm.get('requiredSkillIds');
  }

  get titleLength(): number {
    return this.titleControl?.value?.length || 0;
  }

  get locationLength(): number {
    return this.locationControl?.value?.length || 0;
  }

  get descriptionLength(): number {
    return this.descriptionControl?.value?.length || 0;
  }

  /**
   * Load available skills catalog from backend
   */
  loadSkills(): void {
    this.isLoadingSkills.set(true);
    this.errorMessage.set(null);

    this.skillsService.getSkills().subscribe({
      next: (skills) => {
        this.allSkills.set(skills);
        this.isLoadingSkills.set(false);
      },
      error: (err) => {
        this.isLoadingSkills.set(false);
        this.errorMessage.set(
          extractErrorMessage(err, 'Failed to load skill catalog. Please reload the page.')
        );
      },
    });
  }

  /**
   * Toggle skill selection chip
   */
  toggleSkill(skillId: number): void {
    const current = [...this.selectedSkillIds()];
    const index = current.indexOf(skillId);

    if (index > -1) {
      current.splice(index, 1);
    } else {
      if (current.length >= 15) {
        return; // Max 15 skills reached
      }
      // If currently selected in preferred skills, remove from preferred
      if (this.isPreferredSkillSelected(skillId)) {
        this.removePreferredSkill(skillId);
      }
      current.push(skillId);
    }

    this.selectedSkillIds.set(current);
    this.requiredSkillIdsControl?.setValue(current);
    this.requiredSkillIdsControl?.markAsDirty();
    this.requiredSkillIdsControl?.markAsTouched();
    this.requiredSkillIdsControl?.updateValueAndValidity();
  }

  isSkillSelected(skillId: number): boolean {
    return this.selectedSkillIds().includes(skillId);
  }

  removeSkill(skillId: number): void {
    if (this.isSkillSelected(skillId)) {
      this.toggleSkill(skillId);
    }
  }

  /**
   * Toggle preferred / nice-to-have skill selection chip
   */
  togglePreferredSkill(skillId: number): void {
    const current = [...this.selectedPreferredSkillIds()];
    const index = current.indexOf(skillId);

    if (index > -1) {
      current.splice(index, 1);
    } else {
      if (current.length >= 15) {
        return; // Max 15 preferred skills
      }
      // If currently selected in required skills, remove from required
      if (this.isSkillSelected(skillId)) {
        this.removeSkill(skillId);
      }
      current.push(skillId);
    }

    this.selectedPreferredSkillIds.set(current);
  }

  isPreferredSkillSelected(skillId: number): boolean {
    return this.selectedPreferredSkillIds().includes(skillId);
  }

  removePreferredSkill(skillId: number): void {
    if (this.isPreferredSkillSelected(skillId)) {
      this.togglePreferredSkill(skillId);
    }
  }

  onCustomSkillInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.customSkillInput.set(input.value);
  }

  addCustomSkill(): void {
    if (this.isEditingExpired()) return;
    const raw = this.customSkillInput().trim();
    if (!raw) return;

    const normalized = raw.toLowerCase();
    const current = this.customSkills();
    if (current.some((s) => s.toLowerCase() === normalized)) {
      this.customSkillInput.set('');
      return;
    }

    // If exists in catalog, select it from catalog instead
    const catalogMatch = this.allSkills().find((s) => s.name.toLowerCase() === normalized);
    if (catalogMatch) {
      if (!this.isSkillSelected(catalogMatch.id)) {
        this.toggleSkill(catalogMatch.id);
      }
      this.customSkillInput.set('');
      return;
    }

    if (current.length + this.selectedSkillIds().length >= 25) {
      return;
    }

    this.customSkills.set([...current, raw]);
    this.customSkillInput.set('');
    this.updateRequiredSkillsValidity();
  }

  removeCustomSkill(tag: string): void {
    if (this.isEditingExpired()) return;
    const current = this.customSkills().filter((s) => s.toLowerCase() !== tag.toLowerCase());
    this.customSkills.set(current);
    this.updateRequiredSkillsValidity();
  }

  private updateRequiredSkillsValidity(): void {
    this.requiredSkillIdsControl?.updateValueAndValidity();
  }

  onSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.skillSearchQuery.set(input.value);
  }

  clearSearch(): void {
    this.skillSearchQuery.set('');
  }

  onPreferredSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.preferredSkillSearchQuery.set(input.value);
  }

  clearPreferredSearch(): void {
    this.preferredSkillSearchQuery.set('');
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  /**
   * Submit job post form
   */
  onSubmit(): void {
    if (this.isEditingExpired()) {
      return;
    }

    if (this.jobForm.invalid || this.isSubmitting()) {
      this.jobForm.markAllAsTouched();
      return;
    }

    this.dismissAlert();
    this.isSubmitting.set(true);

    const formValue = this.jobForm.value;
    const custom = this.customSkills().length > 0 ? this.customSkills() : undefined;

    if (this.isEditMode()) {
      const editPayload: UpdateJobRequest = {
        title: formValue.title.trim(),
        description: formValue.description.trim(),
        location: formValue.location?.trim() ? formValue.location.trim() : null,
        jobType: formValue.jobType,
        workArrangement: formValue.workArrangement,
        experienceLevel: formValue.experienceLevel,
        minExperienceYears: Number(formValue.minExperienceYears || 0),
        requiredSkillIds: formValue.requiredSkillIds as number[],
        preferredSkillIds: this.selectedPreferredSkillIds(),
        customSkills: custom,
        deadline: formValue.deadline ? new Date(formValue.deadline).toISOString() : null,
      };

      this.jobsService.updateJob(this.jobId()!, editPayload).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.router.navigate(['/employer/jobs']);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          const msg = extractErrorMessage(
            err,
            'Failed to update job opening. Please check your inputs and try again.'
          );
          this.errorMessage.set(msg);
        },
      });
    } else {
      const createPayload: CreateJobRequest = {
        title: formValue.title.trim(),
        description: formValue.description.trim(),
        location: formValue.location?.trim() ? formValue.location.trim() : null,
        jobType: formValue.jobType,
        workArrangement: formValue.workArrangement,
        experienceLevel: formValue.experienceLevel,
        minExperienceYears: Number(formValue.minExperienceYears || 0),
        requiredSkillIds: formValue.requiredSkillIds as number[],
        preferredSkillIds: this.selectedPreferredSkillIds(),
        customSkills: custom,
        deadline: formValue.deadline ? new Date(formValue.deadline).toISOString() : null,
      };

      this.jobsService.postJob(createPayload).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.router.navigate(['/employer/jobs']);
        },
        error: (err) => {
          this.isSubmitting.set(false);
          const msg = extractErrorMessage(
            err,
            'Failed to publish job opening. Please check your inputs and try again.'
          );
          this.errorMessage.set(msg);
        },
      });
    }
  }
}
