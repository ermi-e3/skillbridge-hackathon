import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CandidateProfileDto, SkillDto } from '../../../core/models/profile.models';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { SkillsService } from '../../../core/services/skills.service';
import { CandidateSkillOnboardingComponent } from './candidate-skill-onboarding.component';

describe('CandidateSkillOnboardingComponent', () => {
  let component: CandidateSkillOnboardingComponent;
  let fixture: ComponentFixture<CandidateSkillOnboardingComponent>;
  let router: Router;

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
    { id: 4, name: 'TypeScript' },
    { id: 5, name: 'PostgreSQL' },
  ];

  const mockProfile: CandidateProfileDto = {
    userId: 'cand-123',
    fullName: 'Yonas Kebede',
    email: 'yonas@example.com',
    headline: '',
    bio: '',
    gitHubUrl: '',
    skills: [],
    skillIds: [],
  };

  beforeEach(async () => {
    profileServiceSpy = {
      getMyProfile: vi.fn().mockReturnValue(of(mockProfile)),
      updateMyProfile: vi.fn().mockReturnValue(of({ ...mockProfile, headline: 'Full Stack Dev', skillIds: [1, 2] })),
    };
    skillsServiceSpy = {
      getSkills: vi.fn().mockReturnValue(of(mockSkills)),
    };
    authServiceSpy = {
      currentUser: vi.fn().mockReturnValue({
        userId: 'cand-123',
        fullName: 'Yonas Kebede',
        email: 'yonas@example.com',
        role: 'Candidate',
      }),
    };

    await TestBed.configureTestingModule({
      imports: [CandidateSkillOnboardingComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profileServiceSpy },
        { provide: SkillsService, useValue: skillsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture = TestBed.createComponent(CandidateSkillOnboardingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize and load skills catalog and candidate info', () => {
    expect(component).toBeTruthy();
    expect(skillsServiceSpy.getSkills).toHaveBeenCalled();
    expect(profileServiceSpy.getMyProfile).toHaveBeenCalled();
    expect(component.allSkills().length).toBe(5);
    expect(component.candidateName()).toBe('Yonas Kebede');
  });

  it('should filter skills by search query', () => {
    component.searchQuery.set('ang');
    const filtered = component.filteredSkills();
    expect(filtered.length).toBe(1);
    expect(filtered[0].name).toBe('Angular');
  });

  it('should filter skills by category', () => {
    component.setCategory('frontend');
    const filtered = component.filteredSkills();
    const names = filtered.map((s) => s.name);
    expect(names).toContain('Angular');
    expect(names).toContain('TypeScript');
    expect(names).not.toContain('PostgreSQL');
  });

  it('should toggle and remove skills correctly', () => {
    expect(component.selectedSkillIds().length).toBe(0);

    component.toggleSkill(2); // Select Angular
    expect(component.selectedSkillIds()).toEqual([2]);
    expect(component.isSkillSelected(2)).toBe(true);

    component.toggleSkill(1); // Select ASP.NET Core
    expect(component.selectedSkillIds()).toEqual([2, 1]);

    component.removeSkill(2); // Remove Angular
    expect(component.selectedSkillIds()).toEqual([1]);
    expect(component.isSkillSelected(2)).toBe(false);
  });

  it('should validate form requiring headline and at least 1 skill', () => {
    expect(component.onboardingForm.valid).toBe(false);

    component.onboardingForm.patchValue({ headline: 'Junior Developer' });
    expect(component.onboardingForm.valid).toBe(false); // 0 skills

    component.toggleSkill(1);
    expect(component.onboardingForm.valid).toBe(true);
  });

  it('should submit successfully and navigate to /jobs with welcome parameter', () => {
    component.onboardingForm.patchValue({ headline: 'Junior Full Stack Developer' });
    component.toggleSkill(1);
    component.toggleSkill(2);

    component.onSubmit();

    expect(profileServiceSpy.updateMyProfile).toHaveBeenCalledWith({
      headline: 'Junior Full Stack Developer',
      skillIds: [1, 2],
      bio: null,
      gitHubUrl: null,
    });
    expect(router.navigate).toHaveBeenCalledWith(['/jobs'], { queryParams: { welcome: '1' } });
  });

  it('should navigate to /jobs when skipped', () => {
    component.skipForNow();
    expect(router.navigate).toHaveBeenCalledWith(['/jobs']);
  });
});
