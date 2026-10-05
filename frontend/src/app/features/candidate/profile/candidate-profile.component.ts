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
import { forkJoin } from 'rxjs';
import { CandidateProfileDto, SkillDto } from '../../../core/models/profile.models';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { SkillsService } from '../../../core/services/skills.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

/**
 * Validates that an optional string is a valid HTTP/HTTPS URL
 */
export const urlValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const val = control.value;
  if (!val || typeof val !== 'string' || val.trim() === '') {
    return null;
  }
  try {
    const parsed = new URL(val.trim());
    if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
      return null;
    }
    return { invalidUrl: true };
  } catch {
    return { invalidUrl: true };
  }
};

/**
 * Validates that selected skillIds are between min and max (1 to 30)
 */
export const skillCountValidator = (min = 1, max = 30): ValidatorFn => {
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
  selector: 'app-candidate-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './candidate-profile.component.html',
  styleUrl: './candidate-profile.component.scss',
})
export class CandidateProfileComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  readonly profileService = inject(ProfileService);
  readonly skillsService = inject(SkillsService);
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // Component Signals
  readonly isLoading = signal<boolean>(true);
  readonly isSaving = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successToast = signal<string | null>(null);
  readonly skillSearchQuery = signal<string>('');
  readonly candidateUser = signal<{ fullName?: string; email?: string } | null>(null);

  // Available and Selected Skills
  readonly allSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);

  // Filtered skills based on search chip input
  readonly filteredSkills = computed(() => {
    const query = this.skillSearchQuery().trim().toLowerCase();
    const skills = this.allSkills();
    if (!query) return skills;
    return skills.filter((s) => s.name.toLowerCase().includes(query));
  });

  // Reactive Form
  readonly profileForm: FormGroup = this.fb.group({
    headline: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    gitHubUrl: ['', [Validators.maxLength(300), urlValidator]],
    bio: ['', [Validators.maxLength(2000)]],
    skillIds: [[], [skillCountValidator(1, 30)]],
  });

  private toastTimeoutId: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.loadData();
  }

  get headlineControl() {
    return this.profileForm.get('headline');
  }

  get gitHubUrlControl() {
    return this.profileForm.get('gitHubUrl');
  }

  get bioControl() {
    return this.profileForm.get('bio');
  }

  get skillIdsControl() {
    return this.profileForm.get('skillIds');
  }

  get bioLength(): number {
    return this.bioControl?.value?.length || 0;
  }

  /**
   * Concurrently fetch skills catalog and current candidate profile
   */
  loadData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      skills: this.skillsService.getSkills(),
      profile: this.profileService.getMyProfile(),
    }).subscribe({
      next: ({ skills, profile }) => {
        this.allSkills.set(skills);

        // Pre-populate candidate metadata
        this.candidateUser.set({
          fullName: profile.fullName || this.authService.currentUser()?.fullName || 'Candidate',
          email: profile.email || this.authService.currentUser()?.email || '',
        });

        // Determine pre-selected skill IDs
        let initialSkillIds: number[] = [];
        if (profile.skillIds && Array.isArray(profile.skillIds)) {
          initialSkillIds = profile.skillIds;
        } else if (profile.skills && Array.isArray(profile.skills)) {
          initialSkillIds = profile.skills.map((s) => s.id);
        }

        this.selectedSkillIds.set(initialSkillIds);

        // Pre-populate reactive form
        this.profileForm.patchValue({
          headline: profile.headline || '',
          gitHubUrl: profile.gitHubUrl || '',
          bio: profile.bio || '',
          skillIds: initialSkillIds,
        });

        this.profileForm.markAsPristine();
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = extractErrorMessage(err, 'Failed to load profile data. Please try again.');
        this.errorMessage.set(msg);
      },
    });
  }

  /**
   * Toggle skill chip selection
   */
  toggleSkill(skillId: number): void {
    const current = [...this.selectedSkillIds()];
    const index = current.indexOf(skillId);

    if (index > -1) {
      current.splice(index, 1);
    } else {
      if (current.length >= 30) {
        return; // Max 30 reached
      }
      current.push(skillId);
    }

    this.selectedSkillIds.set(current);
    this.skillIdsControl?.setValue(current);
    this.skillIdsControl?.markAsDirty();
    this.skillIdsControl?.markAsTouched();
    this.skillIdsControl?.updateValueAndValidity();
  }

  isSkillSelected(skillId: number): boolean {
    return this.selectedSkillIds().includes(skillId);
  }

  onSearchChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.skillSearchQuery.set(value);
  }

  clearSearch(): void {
    this.skillSearchQuery.set('');
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

  showToast(message: string): void {
    this.dismissToast();
    this.successToast.set(message);
    this.toastTimeoutId = setTimeout(() => {
      this.dismissToast();
    }, 4500);
  }

  onSubmit(): void {
    if (this.profileForm.invalid || this.isSaving()) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.dismissAlert();
    this.isSaving.set(true);

    const formValue = this.profileForm.value;
    const payload = {
      headline: formValue.headline.trim(),
      bio: formValue.bio ? formValue.bio.trim() : null,
      gitHubUrl: formValue.gitHubUrl ? formValue.gitHubUrl.trim() : null,
      skillIds: formValue.skillIds as number[],
    };

    this.profileService.updateMyProfile(payload).subscribe({
      next: (updatedProfile: CandidateProfileDto) => {
        this.isSaving.set(false);
        this.profileForm.markAsPristine();
        this.showToast('Profile updated successfully!');

        // Update skillIds if returned in response
        if (updatedProfile.skillIds) {
          this.selectedSkillIds.set(updatedProfile.skillIds);
        } else if (updatedProfile.skills) {
          this.selectedSkillIds.set(updatedProfile.skills.map((s) => s.id));
        }
      },
      error: (err) => {
        this.isSaving.set(false);
        const msg = extractErrorMessage(err, 'Failed to update profile. Please verify the information and try again.');
        this.errorMessage.set(msg);
      },
    });
  }
}
