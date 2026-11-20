import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CandidateProfileDto, SkillDto } from '../../../core/models/profile.models';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { SkillsService } from '../../../core/services/skills.service';
import { CandidateProfileComponent } from './candidate-profile.component';

describe('CandidateProfileComponent', () => {
  let component: CandidateProfileComponent;
  let fixture: ComponentFixture<CandidateProfileComponent>;

  let profileServiceSpy: {
    getMyProfile: ReturnType<typeof vi.fn>;
    updateMyProfile: ReturnType<typeof vi.fn>;
    uploadDefaultCv: ReturnType<typeof vi.fn>;
    downloadDefaultCv: ReturnType<typeof vi.fn>;
    removeDefaultCv: ReturnType<typeof vi.fn>;
  };
  let skillsServiceSpy: {
    getSkills: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: {
    currentUser: ReturnType<typeof vi.fn>;
  };

  const mockSkills: SkillDto[] = [
    { id: 1, name: 'ASP.NET Core' },
    { id: 2, name: 'Angular' },
    { id: 3, name: 'C#' },
    { id: 4, name: 'PostgreSQL' },
  ];

  const mockProfile: CandidateProfileDto = {
    userId: 'cand-1',
    fullName: 'Hana Tesfaye',
    email: 'hana@example.com',
    headline: 'Junior .NET Developer',
    bio: 'Passionate about building scalable backend services.',
    linkedInUrl: 'https://www.linkedin.com/in/hana',
    skills: [
      { id: 1, name: 'ASP.NET Core' },
      { id: 3, name: 'C#' },
    ],
    skillIds: [1, 3],
    hasDefaultCv: true,
    defaultCvOriginalFileName: 'hana-cv.pdf',
    defaultCvFileSizeBytes: 204800,
    defaultCvUploadedAt: '2026-11-01T10:00:00Z',
  };

  beforeEach(async () => {
    profileServiceSpy = {
      getMyProfile: vi.fn().mockReturnValue(of(mockProfile)),
      updateMyProfile: vi.fn().mockReturnValue(of({ ...mockProfile, headline: 'Updated Headline' })),
      uploadDefaultCv: vi.fn().mockReturnValue(of({
        ...mockProfile,
        hasDefaultCv: true,
        defaultCvOriginalFileName: 'new-cv.pdf',
        defaultCvFileSizeBytes: 150000,
      })),
      downloadDefaultCv: vi.fn().mockReturnValue(of(new Blob(['pdf content'], { type: 'application/pdf' }))),
      removeDefaultCv: vi.fn().mockReturnValue(of(void 0)),
    };
    skillsServiceSpy = {
      getSkills: vi.fn().mockReturnValue(of(mockSkills)),
    };
    authServiceSpy = {
      currentUser: vi.fn().mockReturnValue({
        userId: 'cand-1',
        fullName: 'Hana Tesfaye',
        email: 'hana@example.com',
        role: 'Candidate',
      }),
    };

    await TestBed.configureTestingModule({
      imports: [CandidateProfileComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profileServiceSpy },
        { provide: SkillsService, useValue: skillsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CandidateProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should fetch skills and profile concurrently on init and pre-populate form', () => {
    expect(skillsServiceSpy.getSkills).toHaveBeenCalled();
    expect(profileServiceSpy.getMyProfile).toHaveBeenCalled();

    expect(component.isLoading()).toBe(false);
    expect(component.allSkills()).toEqual(mockSkills);
    expect(component.headlineControl?.value).toBe('Junior .NET Developer');
    expect(component.linkedInUrlControl?.value).toBe('https://www.linkedin.com/in/hana');
    expect(component.bioControl?.value).toBe('Passionate about building scalable backend services.');
    expect(component.selectedSkillIds()).toEqual([1, 3]);
    expect(component.skillIdsControl?.value).toEqual([1, 3]);
  });

  it('should validate headline (required, min 3, max 120)', () => {
    const hl = component.headlineControl;
    hl?.setValue('');
    expect(hl?.hasError('required')).toBe(true);

    hl?.setValue('ab');
    expect(hl?.hasError('minlength')).toBe(true);

    hl?.setValue('Valid Headline');
    expect(hl?.valid).toBe(true);
  });

  it('should validate linkedInUrl format', () => {
    const li = component.linkedInUrlControl;

    li?.setValue('not-a-valid-url');
    expect(li?.hasError('invalidLinkedInUrl')).toBe(true);

    li?.setValue('http://www.linkedin.com/in/valid-user');
    expect(li?.hasError('invalidLinkedInUrl')).toBe(true);

    li?.setValue('https://github.com/valid-user');
    expect(li?.hasError('invalidLinkedInUrl')).toBe(true);

    li?.setValue('https://www.linkedin.com/in/valid-user');
    expect(li?.valid).toBe(true);

    li?.setValue('https://linkedin.com/in/valid-user');
    expect(li?.valid).toBe(true);

    // Optional field: empty string is valid
    li?.setValue('');
    expect(li?.valid).toBe(true);
  });

  it('should toggle skill chips on click and enforce 1 to 30 skills', () => {
    expect(component.isSkillSelected(2)).toBe(false);

    // Add Angular (id: 2)
    component.toggleSkill(2);
    expect(component.selectedSkillIds()).toContain(2);
    expect(component.isSkillSelected(2)).toBe(true);

    // Remove C# (id: 3)
    component.toggleSkill(3);
    expect(component.selectedSkillIds()).not.toContain(3);
    expect(component.isSkillSelected(3)).toBe(false);

    // Remove all skills
    component.toggleSkill(1);
    component.toggleSkill(2);
    expect(component.selectedSkillIds().length).toBe(0);
    expect(component.skillIdsControl?.hasError('minSkills')).toBe(true);
  });

  it('should filter skills by search query', () => {
    component.skillSearchQuery.set('Angular');
    expect(component.filteredSkills().length).toBe(1);
    expect(component.filteredSkills()[0].name).toBe('Angular');

    component.skillSearchQuery.set('xyzNonExistent');
    expect(component.filteredSkills().length).toBe(0);

    component.clearSearch();
    expect(component.filteredSkills().length).toBe(4);
  });

  it('should submit updates via PUT /api/candidates/me/profile and show green success toast', () => {
    vi.useFakeTimers();

    component.profileForm.patchValue({
      headline: 'Senior Full Stack Specialist',
      linkedInUrl: 'https://www.linkedin.com/in/hana-dev',
      bio: 'Updated bio description',
      skillIds: [1, 2, 4],
    });

    component.onSubmit();

    expect(profileServiceSpy.updateMyProfile).toHaveBeenCalledWith(
      expect.objectContaining({
        headline: 'Senior Full Stack Specialist',
        linkedInUrl: 'https://www.linkedin.com/in/hana-dev',
        bio: 'Updated bio description',
        skillIds: [1, 2, 4],
        preferredSkillIds: [],
      })
    );

    expect(component.successToast()).toBe('Profile updated successfully!');
    fixture.detectChanges();

    const toastEl = fixture.nativeElement.querySelector('.success-toast');
    expect(toastEl).toBeTruthy();
    expect(toastEl.textContent).toContain('Profile updated successfully!');

    // Test toast auto-dismiss after timeout
    vi.advanceTimersByTime(5000);
    expect(component.successToast()).toBeNull();

    vi.useRealTimers();
  });

  it('should support selecting, toggling, and submitting preferred skills under additional skills category', () => {
    component.profileForm.patchValue({
      headline: 'Full Stack Engineer',
      skillIds: [1, 2],
    });
    component.togglePreferredSkill(3);
    expect(component.selectedPreferredSkillIds()).toEqual([3]);
    expect(component.isPreferredSkillSelected(3)).toBe(true);

    // Mutual exclusivity: selecting skill 3 as core removes it from preferred
    component.toggleSkill(3);
    expect(component.selectedSkillIds()).toContain(3);
    expect(component.selectedPreferredSkillIds()).not.toContain(3);

    // Re-add to preferred
    component.togglePreferredSkill(4);
    expect(component.selectedPreferredSkillIds()).toContain(4);

    component.onSubmit();

    expect(profileServiceSpy.updateMyProfile).toHaveBeenCalledWith(
      expect.objectContaining({
        headline: 'Full Stack Engineer',
        preferredSkillIds: [4],
      })
    );
  });

  it('should support updating location, work arrangement, job type, and experience criteria', () => {
    component.profileForm.patchValue({
      headline: 'Principal Architect',
      location: 'Hawassa, Ethiopia',
      preferredWorkArrangement: 'Hybrid',
      preferredJobType: 'FullTime',
      experienceLevel: 'Senior',
      yearsOfExperience: 7,
      skillIds: [1, 2],
    });

    component.onSubmit();

    expect(profileServiceSpy.updateMyProfile).toHaveBeenCalledWith(
      expect.objectContaining({
        headline: 'Principal Architect',
        location: 'Hawassa, Ethiopia',
        preferredWorkArrangement: 'Hybrid',
        preferredJobType: 'FullTime',
        experienceLevel: 'Senior',
        yearsOfExperience: 7,
      })
    );
  });

  it('should show dismissible error banner when update fails', () => {
    const errorResponse = new HttpErrorResponse({
      status: 400,
      error: {
        code: 'skills.unknown',
        detail: 'Some selected skills do not exist.',
      },
    });
    profileServiceSpy.updateMyProfile.mockReturnValue(throwError(() => errorResponse));

    component.onSubmit();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('Some selected skills do not exist.');
    const alertBanner = fixture.nativeElement.querySelector('.alert-banner');
    expect(alertBanner).toBeTruthy();
    expect(alertBanner.textContent).toContain('Some selected skills do not exist.');

    component.dismissAlert();
    fixture.detectChanges();
    expect(component.errorMessage()).toBeNull();
  });

  it('should initialize and display default CV state from candidate profile', () => {
    expect(component.hasDefaultCv()).toBe(true);
    expect(component.defaultCvOriginalFileName()).toBe('hana-cv.pdf');
    expect(component.defaultCvFileSizeBytes()).toBe(204800);

    const el = fixture.nativeElement;
    const cvCard = el.querySelector('.default-cv-active-card');
    expect(cvCard).toBeTruthy();
    expect(cvCard.textContent).toContain('hana-cv.pdf');
    expect(cvCard.textContent).toContain('200.0 KB');
  });

  it('should upload a valid new default CV file and update state', () => {
    const file = new File(['%PDF-1.4 test data'], 'new-cv.pdf', { type: 'application/pdf' });
    component.uploadFile(file);

    expect(profileServiceSpy.uploadDefaultCv).toHaveBeenCalledWith(file);
    expect(component.hasDefaultCv()).toBe(true);
    expect(component.defaultCvOriginalFileName()).toBe('new-cv.pdf');
    expect(component.successToast()).toContain('Default CV uploaded');
  });

  it('should reject invalid default CV file extension and show validation error', () => {
    const file = new File(['bad'], 'script.js', { type: 'text/javascript' });
    component.uploadFile(file);

    expect(profileServiceSpy.uploadDefaultCv).not.toHaveBeenCalled();
    expect(component.cvUploadError()).toContain('Only PDF and DOCX files are allowed');
  });

  it('should remove default CV when confirmed', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.removeDefaultCv();

    expect(profileServiceSpy.removeDefaultCv).toHaveBeenCalled();
    expect(component.hasDefaultCv()).toBe(false);
    expect(component.defaultCvOriginalFileName()).toBeNull();
    expect(component.successToast()).toContain('Default CV removed');
  });

  it('should render experience level select dropdown with text-only labels matching the clean design', () => {
    fixture.detectChanges();
    const selectEl = fixture.nativeElement.querySelector('#experienceLevel') as HTMLSelectElement;
    expect(selectEl).toBeTruthy();

    const optionLabels = Array.from(selectEl.options)
      .filter((opt) => !opt.disabled)
      .map((opt) => opt.textContent?.trim() ?? '');

    expect(optionLabels).toEqual([
      'Entry Level',
      'Junior',
      'Mid Level',
      'Senior',
      'Lead / Architect',
    ]);

    // Ensure no numerical ranges are shown
    for (const label of optionLabels) {
      expect(/\d/.test(label)).toBe(false);
    }

    selectEl.value = 'MidLevel';
    selectEl.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(component.experienceLevelControl?.value).toBe('MidLevel');
  });

  it('should allow toggling multiple job type preferences with checkboxes', () => {
    component.selectedJobTypes.set([]);
    component.toggleJobType('FullTime');
    expect(component.selectedJobTypes()).toEqual(['FullTime']);
    expect(component.isJobTypeSelected('FullTime')).toBe(true);
    expect(component.isJobTypeSelected('PartTime')).toBe(false);

    // Add second job type preference
    component.toggleJobType('PartTime');
    expect(component.selectedJobTypes()).toEqual(['FullTime', 'PartTime']);
    expect(component.isJobTypeSelected('FullTime')).toBe(true);
    expect(component.isJobTypeSelected('PartTime')).toBe(true);
    expect(component.selectedJobTypesSummary()).toBe('Full-time, Part-time');

    // Deselect one
    component.toggleJobType('FullTime');
    expect(component.selectedJobTypes()).toEqual(['PartTime']);
    expect(component.isJobTypeSelected('FullTime')).toBe(false);
    expect(component.isJobTypeSelected('PartTime')).toBe(true);

    // Clear all
    component.clearAllJobTypes();
    expect(component.selectedJobTypes()).toEqual([]);
    expect(component.selectedJobTypesSummary()).toBe('Select job type');
  });

  it('should toggle job type dropdown open and closed and render checkbox options', () => {
    expect(component.isJobTypeDropdownOpen()).toBe(false);
    component.toggleJobTypeDropdown();
    expect(component.isJobTypeDropdownOpen()).toBe(true);
    fixture.detectChanges();

    const dropdownMenu = fixture.nativeElement.querySelector('.job-type-dropdown-menu');
    expect(dropdownMenu).toBeTruthy();

    const rows = dropdownMenu.querySelectorAll('.dropdown-option-row');
    expect(rows.length).toBe(5);

    component.closeJobTypeDropdown();
    expect(component.isJobTypeDropdownOpen()).toBe(false);
  });
});
