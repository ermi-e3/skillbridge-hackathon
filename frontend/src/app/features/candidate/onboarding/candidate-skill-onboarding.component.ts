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
import { SkillDto } from '../../../core/models/profile.models';
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

  // Loading & submission state
  readonly isLoading = signal<boolean>(true);
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly candidateName = signal<string>('Candidate');

  // Skills state
  readonly allSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);
  readonly searchQuery = signal<string>('');

  // Expandable additional info (bio, github)
  readonly showAdditionalInfo = signal<boolean>(false);

  // Reactive Form
  readonly onboardingForm: FormGroup = this.fb.group({
    headline: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    skillIds: [[], [skillCountValidator(1, 30)]],
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

  // Selected skill objects
  readonly selectedSkillsList = computed(() => {
    const ids = new Set(this.selectedSkillIds());
    return this.allSkills().filter((s) => ids.has(s.id));
  });

  get headlineControl() {
    return this.onboardingForm.get('headline');
  }

  get skillIdsControl() {
    return this.onboardingForm.get('skillIds');
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
        if (profile.skillIds && Array.isArray(profile.skillIds)) {
          existingSkillIds = profile.skillIds;
        } else if (profile.skills && Array.isArray(profile.skills)) {
          existingSkillIds = profile.skills.map((s) => s.id);
        }

        this.selectedSkillIds.set(existingSkillIds);

        this.onboardingForm.patchValue({
          headline: profile.headline || '',
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

  onSearchChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchQuery.set(val);
  }

  clearSearch(): void {
    this.searchQuery.set('');
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

  isSkillSelected(skillId: number): boolean {
    return this.selectedSkillIds().includes(skillId);
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
    const payload = {
      headline: formVal.headline.trim(),
      skillIds: this.selectedSkillIds(),
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
