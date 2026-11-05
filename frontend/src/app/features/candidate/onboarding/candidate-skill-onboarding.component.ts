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
import { SkillDto, UpdateCandidateProfileRequest } from '../../../core/models/profile.models';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { SkillsService } from '../../../core/services/skills.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

export interface SkillCategoryGroup {
  id: string;
  name: string;
  skillNames: string[];
}

export const SKILL_CATEGORIES: SkillCategoryGroup[] = [
  {
    id: 'all',
    name: 'All Skills',
    skillNames: [],
  },
  {
    id: 'frontend',
    name: 'Frontend',
    skillNames: ['Angular', 'React', 'Vue.js', 'JavaScript', 'TypeScript', 'HTML', 'CSS', 'Tailwind CSS', 'Figma'],
  },
  {
    id: 'backend',
    name: 'Backend',
    skillNames: [
      'ASP.NET Core', 'C#', 'Entity Framework Core', 'Node.js', 'Express.js',
      'Python', 'Django', 'Flask', 'Java', 'Spring Boot', 'Kotlin', 'PHP',
      'Laravel', 'Go', 'REST APIs', 'SignalR',
    ],
  },
  {
    id: 'database',
    name: 'Database',
    skillNames: ['PostgreSQL', 'SQL Server', 'MySQL', 'MongoDB', 'Redis'],
  },
  {
    id: 'devops',
    name: 'DevOps & Cloud',
    skillNames: ['Docker', 'Linux', 'Git', 'Azure', 'AWS', 'CI/CD', 'Unit Testing'],
  },
  {
    id: 'mobile',
    name: 'Mobile',
    skillNames: ['Flutter', 'Dart', 'Swift'],
  },
];

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
  selector: 'app-candidate-skill-onboarding',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './candidate-skill-onboarding.component.html',
  styleUrl: './candidate-skill-onboarding.component.scss',
})
export class CandidateSkillOnboardingComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  readonly profileService = inject(ProfileService);
  readonly skillsService = inject(SkillsService);
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // Categories definition
  readonly categories = SKILL_CATEGORIES;
  readonly activeCategory = signal<string>('all');
  readonly activePreferredCategory = signal<string>('all');

  // Loading & submission state
  readonly isLoading = signal<boolean>(true);
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly candidateName = signal<string>('Candidate');

  // Skills state
  readonly allSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);
  readonly selectedPreferredSkillIds = signal<number[]>([]);
  readonly searchQuery = signal<string>('');
  readonly preferredSearchQuery = signal<string>('');

  // Expandable additional info (bio, github)
  readonly showAdditionalInfo = signal<boolean>(false);

  // Reactive Form
  readonly onboardingForm: FormGroup = this.fb.group({
    headline: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    skillIds: [[], [skillCountValidator(1, 30)]],
    location: ['', [Validators.maxLength(100)]],
    preferredWorkArrangement: ['Remote', [Validators.required]],
    preferredJobType: ['FullTime', [Validators.required]],
    experienceLevel: ['EntryLevel', [Validators.required]],
    yearsOfExperience: [0, [Validators.min(0), Validators.max(50)]],
    bio: ['', [Validators.maxLength(2000)]],
    gitHubUrl: ['', [Validators.maxLength(300), urlValidator]],
  });

  // Filtered skills based on active category & search query
  readonly filteredSkills = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    const categoryId = this.activeCategory();
    let skills = this.allSkills();

    if (categoryId !== 'all') {
      const group = this.categories.find((c) => c.id === categoryId);
      if (group && group.skillNames.length > 0) {
        skills = skills.filter((s) => group.skillNames.some((name) => name.toLowerCase() === s.name.toLowerCase()));
      }
    }

    if (query) {
      skills = skills.filter((s) => s.name.toLowerCase().includes(query));
    }

    return skills;
  });

  // Filtered preferred skills
  readonly filteredPreferredSkills = computed(() => {
    const query = this.preferredSearchQuery().trim().toLowerCase();
    const categoryId = this.activePreferredCategory();
    let skills = this.allSkills();

    if (categoryId !== 'all') {
      const group = this.categories.find((c) => c.id === categoryId);
      if (group && group.skillNames.length > 0) {
        skills = skills.filter((s) => group.skillNames.some((name) => name.toLowerCase() === s.name.toLowerCase()));
      }
    }

    if (query) {
      skills = skills.filter((s) => s.name.toLowerCase().includes(query));
    }

    return skills;
  });

  // Selected skill objects
  readonly selectedSkillsList = computed(() => {
    const ids = new Set(this.selectedSkillIds());
    return this.allSkills().filter((s) => ids.has(s.id));
  });

  // Selected preferred skill objects
  readonly selectedPreferredSkillsList = computed(() => {
    const ids = new Set(this.selectedPreferredSkillIds());
    return this.allSkills().filter((s) => ids.has(s.id));
  });

  get headlineControl() {
    return this.onboardingForm.get('headline');
  }

  get skillIdsControl() {
    return this.onboardingForm.get('skillIds');
  }

  get locationControl() {
    return this.onboardingForm.get('location');
  }

  get preferredWorkArrangementControl() {
    return this.onboardingForm.get('preferredWorkArrangement');
  }

  get preferredJobTypeControl() {
    return this.onboardingForm.get('preferredJobType');
  }

  get experienceLevelControl() {
    return this.onboardingForm.get('experienceLevel');
  }

  get yearsOfExperienceControl() {
    return this.onboardingForm.get('yearsOfExperience');
  }

  get gitHubUrlControl() {
    return this.onboardingForm.get('gitHubUrl');
  }

  get bioControl() {
    return this.onboardingForm.get('bio');
  }

  ngOnInit(): void {
    const currentUser = this.authService.currentUser();
    if (currentUser?.fullName) {
      this.candidateName.set(currentUser.fullName);
    }
    this.loadCatalogAndProfile();
  }

  loadCatalogAndProfile(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      skills: this.skillsService.getSkills(),
      profile: this.profileService.getMyProfile(),
    }).subscribe({
      next: ({ skills, profile }) => {
        this.allSkills.set(skills);

        if (profile.fullName) {
          this.candidateName.set(profile.fullName);
        }

        let existingSkillIds: number[] = [];
        let existingPreferredIds: number[] = [];

        if (profile.preferredSkills && profile.preferredSkills.length > 0) {
          existingPreferredIds = profile.preferredSkills.map((s) => s.id);
        } else if (profile.preferredSkillIds && profile.preferredSkillIds.length > 0) {
          existingPreferredIds = profile.preferredSkillIds;
        }

        if (profile.skills && Array.isArray(profile.skills)) {
          const core = profile.skills.filter((s) => !s.isPreferred).map((s) => s.id);
          const pref = profile.skills.filter((s) => s.isPreferred).map((s) => s.id);
          existingSkillIds = core.length > 0 || pref.length > 0 ? core : profile.skills.map((s) => s.id);
          if (existingPreferredIds.length === 0 && pref.length > 0) {
            existingPreferredIds = pref;
          }
        } else if (profile.skillIds && Array.isArray(profile.skillIds)) {
          existingSkillIds = profile.skillIds.filter((id) => !existingPreferredIds.includes(id));
        }

        this.selectedSkillIds.set(existingSkillIds);
        this.selectedPreferredSkillIds.set(existingPreferredIds);

        this.onboardingForm.patchValue({
          headline: profile.headline || '',
          location: profile.location || '',
          preferredWorkArrangement: profile.preferredWorkArrangement || 'Remote',
          preferredJobType: profile.preferredJobType || 'FullTime',
          experienceLevel: profile.experienceLevel || 'EntryLevel',
          yearsOfExperience: profile.yearsOfExperience ?? 0,
          skillIds: existingSkillIds,
          bio: profile.bio || '',
          gitHubUrl: profile.gitHubUrl || '',
        });

        if (profile.bio || profile.gitHubUrl) {
          this.showAdditionalInfo.set(true);
        }

        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = extractErrorMessage(err, 'Failed to load skills catalog. Please try again.');
        this.errorMessage.set(msg);
      },
    });
  }

  setCategory(categoryId: string): void {
    this.activeCategory.set(categoryId);
  }

  setPreferredCategory(categoryId: string): void {
    this.activePreferredCategory.set(categoryId);
  }

  onSearchChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchQuery.set(val);
  }

  clearSearch(): void {
    this.searchQuery.set('');
  }

  onPreferredSearchChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.preferredSearchQuery.set(val);
  }

  clearPreferredSearch(): void {
    this.preferredSearchQuery.set('');
  }

  toggleSkill(skillId: number): void {
    const current = [...this.selectedSkillIds()];
    const index = current.indexOf(skillId);

    if (index > -1) {
      current.splice(index, 1);
    } else {
      if (current.length >= 30) {
        return;
      }
      current.push(skillId);
      // Mutual exclusivity: remove from preferred if present
      if (this.selectedPreferredSkillIds().includes(skillId)) {
        this.selectedPreferredSkillIds.update((ids) => ids.filter((id) => id !== skillId));
      }
    }

    this.selectedSkillIds.set(current);
    this.skillIdsControl?.setValue(current);
    this.skillIdsControl?.markAsDirty();
    this.skillIdsControl?.markAsTouched();
    this.skillIdsControl?.updateValueAndValidity();
  }

  removeSkill(skillId: number): void {
    const current = this.selectedSkillIds().filter((id) => id !== skillId);
    this.selectedSkillIds.set(current);
    this.skillIdsControl?.setValue(current);
    this.skillIdsControl?.markAsDirty();
    this.skillIdsControl?.markAsTouched();
    this.skillIdsControl?.updateValueAndValidity();
  }

  togglePreferredSkill(skillId: number): void {
    const current = [...this.selectedPreferredSkillIds()];
    const index = current.indexOf(skillId);

    if (index > -1) {
      current.splice(index, 1);
    } else {
      if (current.length >= 15) {
        return;
      }
      current.push(skillId);
      // Mutual exclusivity: remove from core if present
      if (this.selectedSkillIds().includes(skillId)) {
        const nextCore = this.selectedSkillIds().filter((id) => id !== skillId);
        this.selectedSkillIds.set(nextCore);
        this.skillIdsControl?.setValue(nextCore);
        this.skillIdsControl?.updateValueAndValidity();
      }
    }

    this.selectedPreferredSkillIds.set(current);
  }

  removePreferredSkill(skillId: number): void {
    const current = this.selectedPreferredSkillIds().filter((id) => id !== skillId);
    this.selectedPreferredSkillIds.set(current);
  }

  isSkillSelected(skillId: number): boolean {
    return this.selectedSkillIds().includes(skillId);
  }

  isPreferredSkillSelected(skillId: number): boolean {
    return this.selectedPreferredSkillIds().includes(skillId);
  }

  toggleAdditionalInfo(): void {
    this.showAdditionalInfo.update((v) => !v);
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  skipForNow(): void {
    this.router.navigate(['/jobs']);
  }

  onSubmit(): void {
    if (this.onboardingForm.invalid || this.isSubmitting()) {
      this.onboardingForm.markAllAsTouched();
      return;
    }

    this.dismissAlert();
    this.isSubmitting.set(true);

    const formVal = this.onboardingForm.value;
    const payload: UpdateCandidateProfileRequest = {
      headline: formVal.headline.trim(),
      skillIds: this.selectedSkillIds(),
      preferredSkillIds: this.selectedPreferredSkillIds(),
      location: formVal.location ? formVal.location.trim() : null,
      preferredWorkArrangement: formVal.preferredWorkArrangement || 'Remote',
      preferredJobType: formVal.preferredJobType || 'FullTime',
      experienceLevel: formVal.experienceLevel || 'EntryLevel',
      yearsOfExperience: formVal.yearsOfExperience !== null && formVal.yearsOfExperience !== undefined ? Number(formVal.yearsOfExperience) : 0,
      bio: formVal.bio?.trim() || null,
      gitHubUrl: formVal.gitHubUrl?.trim() || null,
    };

    this.profileService.updateMyProfile(payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        // Successfully saved skills; navigate to jobs catalog with active match scores
        this.router.navigate(['/jobs'], { queryParams: { welcome: '1' } });
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = extractErrorMessage(err, 'Failed to register your skills. Please check all fields and try again.');
        this.errorMessage.set(msg);
      },
    });
  }
}
