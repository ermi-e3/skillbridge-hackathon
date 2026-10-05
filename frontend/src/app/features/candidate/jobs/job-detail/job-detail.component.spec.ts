import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApplicationDto } from '../../../../core/models/application.models';
import { JobDto } from '../../../../core/models/job.models';
import { ApplicationsService } from '../../../../core/services/applications.service';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
import { JobDetailComponent } from './job-detail.component';

describe('JobDetailComponent', () => {
  let component: JobDetailComponent;
  let fixture: ComponentFixture<JobDetailComponent>;

  let jobsServiceSpy: {
    getJobById: ReturnType<typeof vi.fn>;
  };
  let applicationsServiceSpy: {
    apply: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: {
    currentUser: ReturnType<typeof vi.fn>;
  };

  const mockJob: JobDto = {
    id: 12,
    title: 'Junior Backend Developer',
    description: 'Build and maintain REST APIs for our logistics platform.',
    companyName: 'Abyssinia Tech',
    location: 'Addis Ababa',
    createdAt: '2026-10-10T08:55:00Z',
    requiredSkills: [
      { id: 1, name: 'ASP.NET Core' },
      { id: 3, name: 'C#' },
      { id: 5, name: 'PostgreSQL' },
    ],
    myMatchPercent: 67,
    hasApplied: false,
    match: {
      percent: 67,
      matchedSkills: [
        { id: 1, name: 'ASP.NET Core' },
        { id: 3, name: 'C#' },
      ],
      missingSkills: [{ id: 5, name: 'PostgreSQL' }],
    },
  };

  const mockApplicationResponse: ApplicationDto = {
    id: 31,
    jobId: 12,
    jobTitle: 'Junior Backend Developer',
    companyName: 'Abyssinia Tech',
    status: 'Received',
    appliedAt: '2026-10-10T09:14:00Z',
    matchPercent: 67,
  };

  beforeEach(async () => {
    jobsServiceSpy = {
      getJobById: vi.fn().mockReturnValue(of(mockJob)),
    };
    applicationsServiceSpy = {
      apply: vi.fn().mockReturnValue(of(mockApplicationResponse)),
    };
    authServiceSpy = {
      currentUser: vi.fn().mockReturnValue({ userId: 'u-1', fullName: 'Hana' }),
    };

    await TestBed.configureTestingModule({
      imports: [JobDetailComponent],
      providers: [
        provideRouter([]),
        { provide: JobsService, useValue: jobsServiceSpy },
        { provide: ApplicationsService, useValue: applicationsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: '12' }),
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(JobDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load job details on init from route param', () => {
    expect(component).toBeTruthy();
    expect(jobsServiceSpy.getJobById).toHaveBeenCalledWith(12);
    expect(component.job()).toEqual(mockJob);
    expect(component.isLoading()).toBe(false);
  });

  it('should display header information, required skills, and description', () => {
    const el = fixture.nativeElement;

    expect(el.querySelector('.job-title').textContent).toContain('Junior Backend Developer');
    expect(el.querySelector('.company-badge').textContent).toContain('Abyssinia Tech');
    expect(el.querySelector('.description-body').textContent).toContain('Build and maintain REST APIs');

    const skills = el.querySelectorAll('.skill-pill');
    expect(skills.length).toBe(3);
    expect(skills[0].textContent).toContain('ASP.NET Core');
  });

  it('should display color-coded match badge and skill breakdown', () => {
    const el = fixture.nativeElement;
    const matchBadge = el.querySelector('.match-badge-box');

    expect(matchBadge).toBeTruthy();
    expect(matchBadge.classList).toContain('match-mid'); // 67% is yellow mid
    expect(matchBadge.textContent).toContain('67%');

    const matchedChip = el.querySelector('.mini-chip.matched');
    expect(matchedChip).toBeTruthy();
    expect(matchedChip.textContent).toContain('ASP.NET Core');

    const missingChip = el.querySelector('.mini-chip.missing');
    expect(missingChip).toBeTruthy();
    expect(missingChip.textContent).toContain('PostgreSQL');
  });

  it('should submit application with cover note and display success state', () => {
    component.applyForm.patchValue({
      coverNote: 'I have 1 year experience in ASP.NET Core.',
    });

    component.onApplySubmit();

    expect(applicationsServiceSpy.apply).toHaveBeenCalledWith(12, {
      coverNote: 'I have 1 year experience in ASP.NET Core.',
    });

    expect(component.hasAppliedState()).toBe(true);
    expect(component.successMessage()).toContain('Application submitted successfully');
    fixture.detectChanges();

    const successBanner = fixture.nativeElement.querySelector('.alert-success');
    expect(successBanner).toBeTruthy();

    const appliedBtn = fixture.nativeElement.querySelector('.already-applied-btn');
    expect(appliedBtn).toBeTruthy();
    expect(appliedBtn.disabled).toBe(true);
  });

  it('should show already-applied banner and disable button when hasApplied is initially true', () => {
    jobsServiceSpy.getJobById.mockReturnValue(of({ ...mockJob, hasApplied: true }));
    component.loadJobDetail(12);
    fixture.detectChanges();

    expect(component.hasAppliedState()).toBe(true);
    const alreadyBanner = fixture.nativeElement.querySelector('.already-applied-banner');
    expect(alreadyBanner).toBeTruthy();
    expect(alreadyBanner.textContent).toContain('You have already applied to this job');

    const submitBtn = fixture.nativeElement.querySelector('.already-applied-btn');
    expect(submitBtn.disabled).toBe(true);
  });

  it('should display red error banner directly from ProblemDetails.detail on 400 error', () => {
    const errorResponse = new HttpErrorResponse({
      status: 400,
      error: {
        code: 'applications.duplicate',
        title: 'Already applied',
        detail: 'You have already applied to this job.',
      },
    });
    applicationsServiceSpy.apply.mockReturnValue(throwError(() => errorResponse));

    component.onApplySubmit();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('You have already applied to this job.');
    const errorBanner = fixture.nativeElement.querySelector('.alert-error');
    expect(errorBanner).toBeTruthy();
    expect(errorBanner.textContent).toContain('You have already applied to this job.');

    // And duplicate error updates hasApplied state
    expect(component.hasAppliedState()).toBe(true);

    component.dismissAlert();
    fixture.detectChanges();
    expect(component.errorMessage()).toBeNull();
  });
});
