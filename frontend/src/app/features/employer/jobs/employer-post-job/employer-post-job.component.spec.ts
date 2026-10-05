import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { JobDto } from '../../../../core/models/job.models';
import { SkillDto } from '../../../../core/models/profile.models';
import { JobsService } from '../../../../core/services/jobs.service';
import { SkillsService } from '../../../../core/services/skills.service';
import { EmployerPostJobComponent } from './employer-post-job.component';

describe('EmployerPostJobComponent', () => {
  let component: EmployerPostJobComponent;
  let fixture: ComponentFixture<EmployerPostJobComponent>;
  let jobsServiceSpy: { postJob: ReturnType<typeof vi.fn> };
  let skillsServiceSpy: { getSkills: ReturnType<typeof vi.fn> };
  let router: Router;

  const mockSkills: SkillDto[] = [
    { id: 1, name: 'ASP.NET Core' },
    { id: 2, name: 'Angular' },
    { id: 3, name: 'C#' },
  ];

  const mockCreatedJob: JobDto = {
    id: 101,
    title: 'Senior .NET Developer',
    description: 'We are seeking an experienced developer with solid C# skills to build scalable APIs.',
    location: 'Remote',
    requiredSkills: [
      { id: 1, name: 'ASP.NET Core' },
      { id: 3, name: 'C#' },
    ],
    companyName: 'Acme Corp',
    createdAt: '2026-10-10T10:00:00Z',
    isOpen: true,
  };

  beforeEach(async () => {
    jobsServiceSpy = {
      postJob: vi.fn().mockReturnValue(of(mockCreatedJob)),
    };
    skillsServiceSpy = {
      getSkills: vi.fn().mockReturnValue(of(mockSkills)),
    };

    await TestBed.configureTestingModule({
      imports: [EmployerPostJobComponent],
      providers: [
        provideRouter([]),
        { provide: JobsService, useValue: jobsServiceSpy },
        { provide: SkillsService, useValue: skillsServiceSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockReturnValue(Promise.resolve(true));

    fixture = TestBed.createComponent(EmployerPostJobComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component and fetch skills catalog on init', () => {
    expect(component).toBeTruthy();
    expect(skillsServiceSpy.getSkills).toHaveBeenCalled();
    expect(component.allSkills().length).toBe(3);
    expect(component.isLoadingSkills()).toBe(false);
  });

  it('should initialize with an invalid form', () => {
    expect(component.jobForm.valid).toBe(false);
    expect(component.titleControl?.valid).toBe(false);
    expect(component.descriptionControl?.valid).toBe(false);
    expect(component.requiredSkillIdsControl?.valid).toBe(false);
  });

  it('should validate title length (3 to 120 characters)', () => {
    const title = component.titleControl;

    title?.setValue('ab');
    expect(title?.hasError('minlength')).toBe(true);

    title?.setValue('Valid Job Title');
    expect(title?.valid).toBe(true);

    title?.setValue('a'.repeat(121));
    expect(title?.hasError('maxlength')).toBe(true);
  });

  it('should validate description length (min 20, max 4000)', () => {
    const desc = component.descriptionControl;

    desc?.setValue('Too short');
    expect(desc?.hasError('minlength')).toBe(true);

    desc?.setValue('This description contains more than twenty characters for validation.');
    expect(desc?.valid).toBe(true);
  });

  it('should toggle required skill chips and update form validity', () => {
    expect(component.selectedSkillIds().length).toBe(0);
    expect(component.requiredSkillIdsControl?.hasError('minSkills')).toBe(true);

    // Select skill 1
    component.toggleSkill(1);
    expect(component.isSkillSelected(1)).toBe(true);
    expect(component.selectedSkillIds()).toEqual([1]);
    expect(component.requiredSkillIdsControl?.valid).toBe(true);

    // Select skill 3
    component.toggleSkill(3);
    expect(component.selectedSkillIds()).toEqual([1, 3]);

    // Unselect skill 1
    component.toggleSkill(1);
    expect(component.isSkillSelected(1)).toBe(false);
    expect(component.selectedSkillIds()).toEqual([3]);

    // Remove skill 3 via removeSkill
    component.removeSkill(3);
    expect(component.selectedSkillIds().length).toBe(0);
    expect(component.requiredSkillIdsControl?.hasError('minSkills')).toBe(true);
  });

  it('should filter skills based on search query', () => {
    component.skillSearchQuery.set('ang');
    expect(component.filteredSkills().length).toBe(1);
    expect(component.filteredSkills()[0].name).toBe('Angular');

    component.clearSearch();
    expect(component.filteredSkills().length).toBe(3);
  });

  it('should submit valid form, post job, and navigate to /employer/jobs on success', () => {
    component.jobForm.patchValue({
      title: 'Senior .NET Developer',
      location: 'Remote',
      description: 'We are seeking an experienced developer with solid C# skills to build scalable APIs.',
    });
    component.toggleSkill(1);
    component.toggleSkill(3);

    expect(component.jobForm.valid).toBe(true);

    component.onSubmit();

    expect(jobsServiceSpy.postJob).toHaveBeenCalledWith({
      title: 'Senior .NET Developer',
      location: 'Remote',
      description: 'We are seeking an experienced developer with solid C# skills to build scalable APIs.',
      requiredSkillIds: [1, 3],
    });
    expect(component.isSubmitting()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/employer/jobs']);
  });

  it('should display error message and not navigate on submission error', () => {
    jobsServiceSpy.postJob.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { code: 'validation', detail: 'Invalid job data.' },
          })
      )
    );

    component.jobForm.patchValue({
      title: 'Senior .NET Developer',
      location: 'Remote',
      description: 'We are seeking an experienced developer with solid C# skills to build scalable APIs.',
    });
    component.toggleSkill(1);

    component.onSubmit();

    expect(jobsServiceSpy.postJob).toHaveBeenCalled();
    expect(component.isSubmitting()).toBe(false);
    expect(component.errorMessage()).toContain('Invalid job data.');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
