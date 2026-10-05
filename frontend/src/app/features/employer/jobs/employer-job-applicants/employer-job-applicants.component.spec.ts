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

  it('signs out when clicking the sign out button', () => {
    fixture.detectChanges();

    const signoutBtn = fixture.nativeElement.querySelector('.signout-btn');
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
});
