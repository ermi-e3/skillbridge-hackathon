import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  ApplicationDto,
  JobApplicantItemDto,
  JobApplicantsResponse,
} from '../../../../core/models/application.models';
import { ApplicationsService } from '../../../../core/services/applications.service';
import { AuthService } from '../../../../core/services/auth.service';
import { EmployerJobApplicantsComponent } from './employer-job-applicants.component';

describe('EmployerJobApplicantsComponent', () => {
  let fixture: ComponentFixture<EmployerJobApplicantsComponent>;
  let component: EmployerJobApplicantsComponent;
  let applicationsServiceSpy: {
    getJobApplicants: ReturnType<typeof vi.fn>;
    changeStatus: ReturnType<typeof vi.fn>;
    getApplicantProfile: ReturnType<typeof vi.fn>;
    downloadCv: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: { logout: ReturnType<typeof vi.fn> };

  const mockApplicant: JobApplicantItemDto = {
    applicationId: 42,
    candidate: {
      userId: 'cand-1',
      fullName: 'Abebe Bikila',
      email: 'abebe@example.com',
      headline: 'Passionate C# & Angular Engineer',
      gitHubUrl: 'https://github.com/abebe',
      skills: [
        { id: 1, name: 'C#' },
        { id: 2, name: 'ASP.NET Core' },
      ],
    },
    status: 'Received',
    appliedAt: '2026-10-10T12:00:00Z',
    statusChangedAt: null,
    coverNote: 'I have 3 years of building full-stack web apps.',
    match: {
      percent: 100,
      matchedSkills: [
        { id: 1, name: 'C#' },
        { id: 2, name: 'ASP.NET Core' },
      ],
      missingSkills: [],
    },
  };

  const mockApplicantsResponse: JobApplicantsResponse = {
    job: {
      id: 10,
      title: 'Senior Software Engineer',
      isOpen: true,
      requiredSkills: [
        { id: 1, name: 'C#' },
        { id: 2, name: 'ASP.NET Core' },
      ],
    },
    applicants: [mockApplicant],
  };

  beforeEach(async () => {
    applicationsServiceSpy = {
      getJobApplicants: vi.fn().mockReturnValue(of(mockApplicantsResponse)),
      changeStatus: vi.fn(),
      getApplicantProfile: vi.fn(),
      downloadCv: vi.fn(),
    };
    authServiceSpy = {
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [EmployerJobApplicantsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? '10' : null),
              },
            },
          },
        },
        { provide: ApplicationsService, useValue: applicationsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EmployerJobApplicantsComponent);
    component = fixture.componentInstance;
  });

  it('loads applicants and displays job information on init', () => {
    fixture.detectChanges();

    expect(applicationsServiceSpy.getJobApplicants).toHaveBeenCalledWith(10, undefined, false);
    expect(fixture.nativeElement.textContent).toContain('Senior Software Engineer');
    expect(fixture.nativeElement.textContent).toContain('Abebe Bikila');
    expect(fixture.nativeElement.textContent).toContain('100% Match');
    expect(fixture.nativeElement.textContent).toContain('Passionate C# & Angular Engineer');
    expect(fixture.nativeElement.textContent).toContain('abebe@example.com');
  });

  it('updates application status to Shortlisted', () => {
    const updatedApplication: ApplicationDto = {
      id: 42,
      jobId: 10,
      jobTitle: 'Senior Software Engineer',
      status: 'Shortlisted',
      appliedAt: '2026-10-10T12:00:00Z',
      statusChangedAt: '2026-10-11T10:00:00Z',
    };
    applicationsServiceSpy.changeStatus.mockReturnValue(of(updatedApplication));

    fixture.detectChanges();

    const shortlistBtn = fixture.nativeElement.querySelector('.shortlist-btn');
    expect(shortlistBtn).toBeTruthy();
    shortlistBtn.click();

    expect(applicationsServiceSpy.changeStatus).toHaveBeenCalledWith(42, 'Shortlisted');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('marked as Shortlisted');
  });

  it('changes filter when clicking filter tabs', () => {
    fixture.detectChanges();

    const tabs = fixture.nativeElement.querySelectorAll('.tab-btn');
    const shortlistedTab = Array.from(tabs).find((el: any) =>
      el.textContent.includes('Shortlisted')
    ) as HTMLElement;

    expect(shortlistedTab).toBeTruthy();
    shortlistedTab.click();

    expect(applicationsServiceSpy.getJobApplicants).toHaveBeenCalledWith(10, 'Shortlisted', false);
  });

  it('toggles full match only filter', () => {
    fixture.detectChanges();

    const toggleBtn = fixture.nativeElement.querySelector('.toggle-btn');
    expect(toggleBtn).toBeTruthy();
    toggleBtn.click();

    expect(applicationsServiceSpy.getJobApplicants).toHaveBeenCalledWith(10, undefined, true);
  });

  it('signs out when clicking sign out in the user profile dropdown', () => {
    fixture.detectChanges();

    const avatarTrigger = fixture.nativeElement.querySelector('.user-avatar-trigger') as HTMLButtonElement;
    expect(avatarTrigger).toBeTruthy();
    avatarTrigger.click();
    fixture.detectChanges();

    const signoutBtn = fixture.nativeElement.querySelector('.dropdown-signout-btn') as HTMLButtonElement;
    expect(signoutBtn).toBeTruthy();
    signoutBtn.click();

    expect(authServiceSpy.logout).toHaveBeenCalledOnce();
  });

  it('displays error message and allows retry', () => {
    applicationsServiceSpy.getJobApplicants.mockReturnValue(
      throwError(() => new Error('Server error'))
    );

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Unable to load applicants');
    const retryBtn = fixture.nativeElement.querySelector('.retry-button');
    expect(retryBtn).toBeTruthy();

    applicationsServiceSpy.getJobApplicants.mockReturnValue(of(mockApplicantsResponse));
    retryBtn.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Senior Software Engineer');
  });

  it('allows shortlisting and rejecting applicants even after 2 hours, while displaying job details locked badge', () => {
    const expiredResponse: JobApplicantsResponse = {
      ...mockApplicantsResponse,
      job: {
        ...mockApplicantsResponse.job,
        canEditJob: false,
        createdAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
      },
    };
    applicationsServiceSpy.getJobApplicants.mockReturnValue(of(expiredResponse));
    applicationsServiceSpy.changeStatus.mockReturnValue(of({ ...mockApplicant, id: 42, jobId: 10, jobTitle: 'Senior Software Engineer', appliedAt: '2026-10-10T12:00:00Z', status: 'Shortlisted' }));

    fixture.detectChanges();

    expect(component.canEditJob()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Job Details Locked');

    const shortlistBtn = fixture.nativeElement.querySelector('.shortlist-btn');
    const rejectBtn = fixture.nativeElement.querySelector('.reject-btn');
    expect(shortlistBtn.hasAttribute('disabled')).toBe(false);
    expect(rejectBtn.hasAttribute('disabled')).toBe(false);

    // Clicking Shortlist succeeds
    shortlistBtn.click();
    expect(applicationsServiceSpy.changeStatus).toHaveBeenCalledWith(42, 'Shortlisted');
  });

  it('opens candidate full profile modal when clicking View Profile', () => {
    const mockFullProfile = {
      candidateId: 'cand-1',
      fullName: 'Abebe Bikila',
      email: 'abebe@example.com',
      headline: 'Passionate C# & Angular Engineer',
      bio: 'Full stack developer for 4 years.',
      skills: [
        { id: 1, name: 'C#', proficiency: 'Expert', yearsOfExperience: 4, isPreferred: false },
      ],
      experienceHistory: [
        {
          id: 1,
          title: 'Software Engineer',
          company: 'Fintech Hub',
          location: 'Addis Ababa',
          startDate: '2022-01-01',
          endDate: null,
          isCurrent: true,
          description: 'Building payment gateways.',
        },
      ],
      applicationId: 42,
      applicationStatus: 'Received' as const,
      appliedAt: '2026-10-10T12:00:00Z',
      hasCv: true,
      cvOriginalFileName: 'abebe-cv.pdf',
      cvFileSizeBytes: 102400,
    };
    applicationsServiceSpy.getApplicantProfile.mockReturnValue(of(mockFullProfile));

    fixture.detectChanges();

    const viewProfileBtn = fixture.nativeElement.querySelector('.view-profile-btn');
    expect(viewProfileBtn).toBeTruthy();
    viewProfileBtn.click();

    expect(applicationsServiceSpy.getApplicantProfile).toHaveBeenCalledWith(10, 'cand-1');
    fixture.detectChanges();

    expect(component.showProfileModal()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Work Experience (1)');
    expect(fixture.nativeElement.textContent).toContain('Fintech Hub');
    expect(fixture.nativeElement.textContent).toContain('Submitted Resume / CV');
    expect(fixture.nativeElement.textContent).toContain('abebe-cv.pdf');
  });

  it('opens CV Preview when clicking CV Preview button and allows downloading the CV', () => {
    const applicantWithCv: JobApplicantItemDto = {
      ...mockApplicant,
      hasCv: true,
      cvOriginalFileName: 'abebe-resume.pdf',
    };
    const responseWithCv: JobApplicantsResponse = {
      ...mockApplicantsResponse,
      applicants: [applicantWithCv],
    };
    applicationsServiceSpy.getJobApplicants.mockReturnValue(of(responseWithCv));
    applicationsServiceSpy.downloadCv.mockReturnValue(
      of(new Blob(['test pdf'], { type: 'application/pdf' }))
    );

    fixture.detectChanges();

    const previewCvBtn = fixture.nativeElement.querySelector('.preview-cv-btn');
    expect(previewCvBtn).toBeTruthy();
    expect(previewCvBtn.textContent).toContain('CV Preview');
    previewCvBtn.click();

    expect(applicationsServiceSpy.downloadCv).toHaveBeenCalledWith(42);
    expect(component.showCvPreviewModal()).toBe(true);
    expect(component.cvPreviewFileName()).toBe('abebe-resume.pdf');

    fixture.detectChanges();
    const downloadBtn = fixture.nativeElement.querySelector('.btn-preview-download');
    expect(downloadBtn).toBeTruthy();
    expect(downloadBtn.textContent).toContain('Download CV');
  });

  it('filters applicants by name, skills match percentage, and application date, and paginates results', () => {
    const secondApplicant: JobApplicantItemDto = {
      ...mockApplicant,
      applicationId: 43,
      candidate: {
        ...mockApplicant.candidate,
        userId: 'cand-2',
        fullName: 'Sara Tadesse',
        headline: 'Financial Controller',
      },
      appliedAt: '2026-10-02T10:00:00Z',
      match: { ...mockApplicant.match, percent: 62 },
    };
    const thirdApplicant: JobApplicantItemDto = {
      ...mockApplicant,
      applicationId: 44,
      candidate: {
        ...mockApplicant.candidate,
        userId: 'cand-3',
        fullName: 'Dawit Mekonnen',
        headline: 'Data Specialist',
      },
      appliedAt: '2026-10-12T10:00:00Z',
      match: { ...mockApplicant.match, percent: 40 },
    };

    applicationsServiceSpy.getJobApplicants.mockReturnValue(
      of({ ...mockApplicantsResponse, applicants: [mockApplicant, secondApplicant, thirdApplicant] })
    );
    fixture.detectChanges();

    // Filter by applicant name
    component.searchNameQuery.set('Sara');
    expect(component.filteredApplicants().map((a) => a.applicationId)).toEqual([43]);

    // Filter by skills match percentage
    component.clearNameSearch();
    component.matchRangeFilter.set('75');
    expect(component.filteredApplicants().map((a) => a.applicationId)).toEqual([42]);

    component.matchRangeFilter.set('under50');
    expect(component.filteredApplicants().map((a) => a.applicationId)).toEqual([44]);

    // Filter by application date
    component.matchRangeFilter.set('all');
    component.appliedFromDate.set('2026-10-11');
    component.datePreset.set('custom');
    expect(component.filteredApplicants().map((a) => a.applicationId)).toEqual([44]);

    // Pagination check
    component.clearDateFilters();
    component.pageSize.set(2);
    expect(component.paginatedApplicants().length).toBe(2);
    component.onPageChange(2);
    expect(component.paginatedApplicants().length).toBe(1);
    expect(component.paginatedApplicants()[0].applicationId).toBe(44);
  });
});
