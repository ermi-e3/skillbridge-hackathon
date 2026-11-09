import { CommonModule, DatePipe } from '@angular/common';
import {
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
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
import { CandidateProfileDto, SkillDto, UpdateCandidateProfileRequest } from '../../../core/models/profile.models';
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
 * Validates that selected skillIds are between min and max (1 to 20)
 */
export const skillCountValidator = (min = 1, max = 20): ValidatorFn => {
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

import { SKILL_CATEGORIES } from '../onboarding/candidate-skill-onboarding.component';

@Component({
  selector: 'app-candidate-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './candidate-profile.component.html',
  styleUrl: './candidate-profile.component.scss',
})
export class CandidateProfileComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  readonly profileService = inject(ProfileService);
  readonly skillsService = inject(SkillsService);
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly elementRef = inject(ElementRef);

  // Component Signals
  readonly isLoading = signal<boolean>(true);
  readonly isSaving = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successToast = signal<string | null>(null);
  readonly skillSearchQuery = signal<string>('');
  readonly preferredSkillSearchQuery = signal<string>('');
  readonly candidateUser = signal<{ fullName?: string; email?: string } | null>(null);

  // Dropdown Open State Signals
  readonly isCoreDropdownOpen = signal<boolean>(false);
  readonly isPreferredDropdownOpen = signal<boolean>(false);

  // Default CV Signals
  readonly hasDefaultCv = signal<boolean>(false);
  readonly defaultCvOriginalFileName = signal<string | null>(null);
  readonly defaultCvFileSizeBytes = signal<number | null>(null);
  readonly defaultCvUploadedAt = signal<string | null>(null);
  readonly isUploadingCv = signal<boolean>(false);
  readonly isDeletingCv = signal<boolean>(false);
  readonly isDownloadingCv = signal<boolean>(false);
  readonly cvUploadError = signal<string | null>(null);

  // CV Preview Modal Signals
  readonly showCvPreviewModal = signal<boolean>(false);
  readonly cvPreviewLoading = signal<boolean>(false);
  readonly cvPreviewError = signal<string | null>(null);
  readonly cvPreviewBlob = signal<Blob | null>(null);
  readonly cvPreviewSafeUrl = signal<SafeResourceUrl | null>(null);
  readonly isDocxPreview = signal<boolean>(false);
  private cvPreviewObjectUrl: string | null = null;

  // Categories
  readonly categories = SKILL_CATEGORIES;
  readonly activeCategory = signal<string>('all');
  readonly activePreferredCategory = signal<string>('all');

  // Available and Selected Skills
  readonly allSkills = signal<SkillDto[]>([]);
  readonly selectedSkillIds = signal<number[]>([]);
  readonly selectedPreferredSkillIds = signal<number[]>([]);

  // Filtered skills based on search chip input and active category
  readonly filteredSkills = computed(() => {
    const query = this.skillSearchQuery().trim().toLowerCase();
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

  // Filtered preferred / nice-to-have skills
  readonly filteredPreferredSkills = computed(() => {
    const query = this.preferredSkillSearchQuery().trim().toLowerCase();
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

  // Selected preferred skill objects
  readonly selectedPreferredSkills = computed(() => {
    const prefIds = new Set(this.selectedPreferredSkillIds());
    return this.allSkills().filter((s) => prefIds.has(s.id));
  });

  // Selected core skill objects
  readonly selectedCoreSkills = computed(() => {
    const coreIds = new Set(this.selectedSkillIds());
    return this.allSkills().filter((s) => coreIds.has(s.id));
  });

  // Reactive Form
  readonly profileForm: FormGroup = this.fb.group({
    headline: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    gitHubUrl: ['', [Validators.maxLength(300), urlValidator]],
    bio: ['', [Validators.maxLength(2000)]],
    location: ['', [Validators.maxLength(100)]],
    preferredWorkArrangement: [''],
    preferredJobType: [''],
    experienceLevel: [''],
    yearsOfExperience: [null, [Validators.min(0), Validators.max(50)]],
    skillIds: [[], [skillCountValidator(1, 20)]],
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

  get locationControl() {
    return this.profileForm.get('location');
  }

  get preferredWorkArrangementControl() {
    return this.profileForm.get('preferredWorkArrangement');
  }

  get preferredJobTypeControl() {
    return this.profileForm.get('preferredJobType');
  }

  get experienceLevelControl() {
    return this.profileForm.get('experienceLevel');
  }

  get yearsOfExperienceControl() {
    return this.profileForm.get('yearsOfExperience');
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
        let initialPreferredIds: number[] = [];

        if (profile.preferredSkills && profile.preferredSkills.length > 0) {
          initialPreferredIds = profile.preferredSkills.map((s) => s.id);
        } else if (profile.preferredSkillIds && profile.preferredSkillIds.length > 0) {
          initialPreferredIds = profile.preferredSkillIds;
        }

        if (profile.skills && Array.isArray(profile.skills)) {
          const core = profile.skills.filter((s) => !s.isPreferred).map((s) => s.id);
          const pref = profile.skills.filter((s) => s.isPreferred).map((s) => s.id);
          initialSkillIds = core.length > 0 || pref.length > 0 ? core : profile.skills.map((s) => s.id);
          if (initialPreferredIds.length === 0 && pref.length > 0) {
            initialPreferredIds = pref;
          }
        } else if (profile.skillIds && Array.isArray(profile.skillIds)) {
          initialSkillIds = profile.skillIds.filter((id) => !initialPreferredIds.includes(id));
        }

        this.selectedSkillIds.set(initialSkillIds);
        this.selectedPreferredSkillIds.set(initialPreferredIds);

        // Populate Default CV state
        this.hasDefaultCv.set(!!profile.hasDefaultCv);
        this.defaultCvOriginalFileName.set(profile.defaultCvOriginalFileName || null);
        this.defaultCvFileSizeBytes.set(profile.defaultCvFileSizeBytes ?? null);
        this.defaultCvUploadedAt.set(profile.defaultCvUploadedAt || null);

        // Pre-populate reactive form
        this.profileForm.patchValue({
          headline: profile.headline || '',
          gitHubUrl: profile.gitHubUrl || '',
          bio: profile.bio || '',
          location: profile.location || '',
          preferredWorkArrangement: profile.preferredWorkArrangement || '',
          preferredJobType: profile.preferredJobType || '',
          experienceLevel: profile.experienceLevel || '',
          yearsOfExperience: profile.yearsOfExperience ?? null,
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
   * Toggle core skill chip selection
   */
  toggleSkill(skillId: number): void {
    const current = [...this.selectedSkillIds()];
    const index = current.indexOf(skillId);

    if (index > -1) {
      current.splice(index, 1);
    } else {
      if (current.length >= 20) {
        return; // Max 20 reached
      }
      current.push(skillId);
      // Mutual exclusivity: remove from preferred skills if present
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

  removeCoreSkill(skillId: number): void {
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

  /**
   * Toggle preferred / nice-to-have skill chip selection
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
      current.push(skillId);
      // Mutual exclusivity: remove from core skills if present
      if (this.selectedSkillIds().includes(skillId)) {
        const nextCore = this.selectedSkillIds().filter((id) => id !== skillId);
        this.selectedSkillIds.set(nextCore);
        this.skillIdsControl?.setValue(nextCore);
        this.skillIdsControl?.updateValueAndValidity();
      }
    }

    this.selectedPreferredSkillIds.set(current);
    this.profileForm.markAsDirty();
  }

  removePreferredSkill(skillId: number): void {
    const current = this.selectedPreferredSkillIds().filter((id) => id !== skillId);
    this.selectedPreferredSkillIds.set(current);
    this.profileForm.markAsDirty();
  }

  isPreferredSkillSelected(skillId: number): boolean {
    return this.selectedPreferredSkillIds().includes(skillId);
  }

  onSearchChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.skillSearchQuery.set(value);
  }

  clearSearch(): void {
    this.skillSearchQuery.set('');
  }

  onPreferredSearchChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.preferredSkillSearchQuery.set(value);
  }

  clearPreferredSearch(): void {
    this.preferredSkillSearchQuery.set('');
  }

  setCategory(categoryId: string): void {
    this.activeCategory.set(categoryId);
  }

  setPreferredCategory(categoryId: string): void {
    this.activePreferredCategory.set(categoryId);
  }

  // --- Dropdown Controls & Click-Outside ---

  toggleCoreDropdown(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.isCoreDropdownOpen.update((v) => !v);
    if (this.isCoreDropdownOpen()) {
      this.isPreferredDropdownOpen.set(false);
    }
  }

  togglePreferredDropdown(event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.isPreferredDropdownOpen.update((v) => !v);
    if (this.isPreferredDropdownOpen()) {
      this.isCoreDropdownOpen.set(false);
    }
  }

  closeCoreDropdown(): void {
    this.isCoreDropdownOpen.set(false);
  }

  closePreferredDropdown(): void {
    this.isPreferredDropdownOpen.set(false);
  }

  clearAllCoreSkills(): void {
    this.selectedSkillIds.set([]);
    this.skillIdsControl?.setValue([]);
    this.skillIdsControl?.markAsDirty();
    this.skillIdsControl?.markAsTouched();
    this.skillIdsControl?.updateValueAndValidity();
  }

  clearAllPreferredSkills(): void {
    this.selectedPreferredSkillIds.set([]);
    this.profileForm.markAsDirty();
  }

  getSkillCategoryName(skillName: string): string {
    for (const cat of this.categories) {
      if (cat.id === 'all') continue;
      if (cat.skillNames.some((name) => name.toLowerCase() === skillName.toLowerCase())) {
        return cat.name;
      }
    }
    return 'Other';
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target) return;

    const coreWrapper = this.elementRef.nativeElement.querySelector('.core-dropdown-wrapper');
    if (this.isCoreDropdownOpen() && coreWrapper && !coreWrapper.contains(target)) {
      this.isCoreDropdownOpen.set(false);
    }

    const preferredWrapper = this.elementRef.nativeElement.querySelector('.preferred-dropdown-wrapper');
    if (this.isPreferredDropdownOpen() && preferredWrapper && !preferredWrapper.contains(target)) {
      this.isPreferredDropdownOpen.set(false);
    }
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

  ngOnDestroy(): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
      this.toastTimeoutId = null;
    }
  }

  // --- Default CV Management Methods ---

  formatFileSize(bytes?: number | null): string {
    if (!bytes || bytes <= 0) return '0 B';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  onDefaultCvSelected(event: Event, inputEl?: HTMLInputElement): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploadFile(file, inputEl || input);
  }

  onDefaultCvDropped(event: DragEvent, inputEl?: HTMLInputElement): void {
    event.preventDefault();
    event.stopPropagation();
    const file = event.dataTransfer?.files?.[0];
    if (!file) return;
    this.uploadFile(file, inputEl);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
  }

  uploadFile(file: File, inputEl?: HTMLInputElement): void {
    this.cvUploadError.set(null);
    const validExts = ['.pdf', '.docx'];
    const lowerName = file.name.toLowerCase();
    const hasValidExt = validExts.some((ext) => lowerName.endsWith(ext));

    if (!hasValidExt) {
      this.cvUploadError.set('Only PDF and DOCX files are allowed (.pdf, .docx).');
      if (inputEl) inputEl.value = '';
      return;
    }

    const maxBytes = 5 * 1024 * 1024; // 5 MB
    if (file.size > maxBytes) {
      this.cvUploadError.set(
        `File size exceeds 5 MB limit (${this.formatFileSize(file.size)}). Please choose a smaller file.`
      );
      if (inputEl) inputEl.value = '';
      return;
    }

    this.isUploadingCv.set(true);
    this.profileService.uploadDefaultCv(file).subscribe({
      next: (profile) => {
        this.isUploadingCv.set(false);
        this.hasDefaultCv.set(!!profile.hasDefaultCv);
        this.defaultCvOriginalFileName.set(profile.defaultCvOriginalFileName || file.name);
        this.defaultCvFileSizeBytes.set(profile.defaultCvFileSizeBytes ?? file.size);
        this.defaultCvUploadedAt.set(profile.defaultCvUploadedAt || new Date().toISOString());
        this.showToast('Default CV uploaded and saved to your profile successfully!');
        if (inputEl) inputEl.value = '';
      },
      error: (err) => {
        this.isUploadingCv.set(false);
        const msg = extractErrorMessage(err, 'Failed to upload CV. Please try again.');
        this.cvUploadError.set(msg);
        if (inputEl) inputEl.value = '';
      },
    });
  }

  removeDefaultCv(): void {
    if (this.isDeletingCv()) return;
    if (!confirm('Are you sure you want to remove your default profile CV?')) {
      return;
    }

    this.isDeletingCv.set(true);
    this.cvUploadError.set(null);

    this.profileService.removeDefaultCv().subscribe({
      next: () => {
        this.isDeletingCv.set(false);
        this.hasDefaultCv.set(false);
        this.defaultCvOriginalFileName.set(null);
        this.defaultCvFileSizeBytes.set(null);
        this.defaultCvUploadedAt.set(null);
        this.showToast('Default CV removed from your profile.');
      },
      error: (err) => {
        this.isDeletingCv.set(false);
        const msg = extractErrorMessage(err, 'Failed to remove default CV.');
        this.cvUploadError.set(msg);
      },
    });
  }

  downloadDefaultCv(): void {
    if (this.isDownloadingCv()) return;
    this.isDownloadingCv.set(true);

    this.profileService.downloadDefaultCv().subscribe({
      next: (blob) => {
        this.isDownloadingCv.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = this.defaultCvOriginalFileName() || 'resume.pdf';
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        a.remove();
      },
      error: (err) => {
        this.isDownloadingCv.set(false);
        const msg = extractErrorMessage(err, 'Failed to download default CV.');
        this.cvUploadError.set(msg);
      },
    });
  }

  openDefaultCvPreview(): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }

    const fileName = this.defaultCvOriginalFileName() || 'resume.pdf';
    this.showCvPreviewModal.set(true);
    this.cvPreviewLoading.set(true);
    this.cvPreviewError.set(null);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);

    const isPdf = fileName.toLowerCase().endsWith('.pdf');
    this.isDocxPreview.set(!isPdf);

    this.profileService.downloadDefaultCv().subscribe({
      next: (blob) => {
        this.cvPreviewLoading.set(false);
        const mime = isPdf
          ? 'application/pdf'
          : blob.type || 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
        const typedBlob = new Blob([blob], { type: mime });
        this.cvPreviewBlob.set(typedBlob);
        this.cvPreviewObjectUrl = window.URL.createObjectURL(typedBlob);
        this.cvPreviewSafeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.cvPreviewObjectUrl));
      },
      error: (err) => {
        this.cvPreviewLoading.set(false);
        this.cvPreviewError.set(extractErrorMessage(err, 'Failed to load CV preview.'));
      },
    });
  }

  closeCvPreview(): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }
    this.showCvPreviewModal.set(false);
    this.cvPreviewLoading.set(false);
    this.cvPreviewError.set(null);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);
  }

  downloadCurrentPreviewCv(): void {
    const blob = this.cvPreviewBlob();
    const fileName = this.defaultCvOriginalFileName() || 'resume.pdf';
    if (blob) {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      a.remove();
    } else {
      this.downloadDefaultCv();
    }
  }

  // --- End Default CV Management Methods ---

  onSubmit(): void {
    if (this.profileForm.invalid || this.isSaving()) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.dismissAlert();
    this.isSaving.set(true);

    const formValue = this.profileForm.value;
    const payload: UpdateCandidateProfileRequest = {
      headline: formValue.headline.trim(),
      bio: formValue.bio ? formValue.bio.trim() : null,
      gitHubUrl: formValue.gitHubUrl ? formValue.gitHubUrl.trim() : null,
      location: formValue.location ? formValue.location.trim() : null,
      preferredWorkArrangement: formValue.preferredWorkArrangement || null,
      preferredJobType: formValue.preferredJobType || null,
      experienceLevel: formValue.experienceLevel || null,
      yearsOfExperience: formValue.yearsOfExperience !== null && formValue.yearsOfExperience !== undefined && formValue.yearsOfExperience !== '' ? Number(formValue.yearsOfExperience) : null,
      skillIds: formValue.skillIds as number[],
      preferredSkillIds: this.selectedPreferredSkillIds(),
    };

    this.profileService.updateMyProfile(payload).subscribe({
      next: (updatedProfile: CandidateProfileDto) => {
        this.isSaving.set(false);
        this.profileForm.markAsPristine();
        this.showToast('Profile updated successfully!');

        // Update matching criteria fields if returned in response
        if (updatedProfile.location !== undefined) {
          this.profileForm.patchValue({
            location: updatedProfile.location || '',
            preferredWorkArrangement: updatedProfile.preferredWorkArrangement || '',
            preferredJobType: updatedProfile.preferredJobType || '',
            experienceLevel: updatedProfile.experienceLevel || '',
            yearsOfExperience: updatedProfile.yearsOfExperience ?? null,
          }, { emitEvent: false });
        }

        // Update preferred skills
        if (updatedProfile.preferredSkills) {
          this.selectedPreferredSkillIds.set(updatedProfile.preferredSkills.map((s) => s.id));
        } else if (updatedProfile.preferredSkillIds) {
          this.selectedPreferredSkillIds.set(updatedProfile.preferredSkillIds);
        }

        // Update core skills
        if (updatedProfile.skills) {
          const core = updatedProfile.skills.filter((s) => !s.isPreferred).map((s) => s.id);
          this.selectedSkillIds.set(core.length > 0 ? core : updatedProfile.skills.map((s) => s.id));
        } else if (updatedProfile.skillIds) {
          this.selectedSkillIds.set(updatedProfile.skillIds);
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
