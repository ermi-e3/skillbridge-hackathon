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
    gitHubUrl: 'https://github.com/hana',
    skills: [
      { id: 1, name: 'ASP.NET Core' },
      { id: 3, name: 'C#' },
    ],
    skillIds: [1, 3],
  };

  beforeEach(async () => {
    profileServiceSpy = {
      getMyProfile: vi.fn().mockReturnValue(of(mockProfile)),
      updateMyProfile: vi.fn().mockReturnValue(of({ ...mockProfile, headline: 'Updated Headline' })),
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
    expect(component.gitHubUrlControl?.value).toBe('https://github.com/hana');
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

  it('should validate gitHubUrl format', () => {
    const gh = component.gitHubUrlControl;

    gh?.setValue('not-a-valid-url');
    expect(gh?.hasError('invalidUrl')).toBe(true);

    gh?.setValue('https://github.com/valid-user');
    expect(gh?.valid).toBe(true);

    // Optional field: empty string is valid
    gh?.setValue('');
    expect(gh?.valid).toBe(true);
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
      gitHubUrl: 'https://github.com/hana-dev',
      bio: 'Updated bio description',
      skillIds: [1, 2, 4],
    });

    component.onSubmit();

    expect(profileServiceSpy.updateMyProfile).toHaveBeenCalledWith({
      headline: 'Senior Full Stack Specialist',
      gitHubUrl: 'https://github.com/hana-dev',
      bio: 'Updated bio description',
      skillIds: [1, 2, 4],
    });

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
});
