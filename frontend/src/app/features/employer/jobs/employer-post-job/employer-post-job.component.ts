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
import { Router, RouterLink } from '@angular/router';
import { CreateJobRequest } from '../../../../core/models/job.models';
import { SkillDto } from '../../../../core/models/profile.models';
import { JobsService } from '../../../../core/services/jobs.service';
import { SkillsService } from '../../../../core/services/skills.service';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';

/**
 * Custom validator ensuring at least 1 and at most 15 skills are selected
 */
export const requiredSkillsValidator = (min = 1, max = 15): ValidatorFn => {
  return (control: AbstractControl): ValidationErrors | null => {
    const ids = control.value as number[] | null;
    if (!ids || !Array.isArray(ids) || ids.length < min) {
      return { minSkills: { min, actual: ids?.length ?? 0 } };
    }
    if (ids.length > max) {
      return { maxSkills: { max, actual: ids.length } };
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

  // State Signals
  readonly isLoadingSkills = signal<boolean>(true);
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly skillSearchQuery = signal<string>('');

  // Skills Catalog & Selected IDs
  readonly allSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);

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

  // Reactive Form
  readonly jobForm: FormGroup = this.fb.group({
    title: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    location: ['', [Validators.maxLength(100)]],
    description: ['', [Validators.required, Validators.minLength(20), Validators.maxLength(4000)]],
    requiredSkillIds: [[], [requiredSkillsValidator(1, 15)]],
  });

  ngOnInit(): void {
    this.loadSkills();
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

  onSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.skillSearchQuery.set(input.value);
  }

  clearSearch(): void {
    this.skillSearchQuery.set('');
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  /**
   * Submit job post form
   */
  onSubmit(): void {
    if (this.jobForm.invalid || this.isSubmitting()) {
      this.jobForm.markAllAsTouched();
      return;
    }

    this.dismissAlert();
    this.isSubmitting.set(true);

    const formValue = this.jobForm.value;
    const payload: CreateJobRequest = {
      title: formValue.title.trim(),
      description: formValue.description.trim(),
      location: formValue.location?.trim() ? formValue.location.trim() : null,
      requiredSkillIds: formValue.requiredSkillIds as number[],
    };

    this.jobsService.postJob(payload).subscribe({
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
