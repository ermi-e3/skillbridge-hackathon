import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApplicationDto } from '../../../../core/models/application.models';
import { JobDto } from '../../../../core/models/job.models';
import { CandidateProfileDto } from '../../../../core/models/profile.models';
import { ApplicationsService } from '../../../../core/services/applications.service';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { JobDetailComponent } from './job-detail.component';

describe('JobDetailComponent', () => {
  let component: JobDetailComponent;
  let fixture: ComponentFixture<JobDetailComponent>;

  let jobsServiceSpy: {
    getJobById: ReturnType<typeof vi.fn>;
  };
  let applicationsServiceSpy: {
    apply: ReturnType<typeof vi.fn>;
    applyWithCv: ReturnType<typeof vi.fn>;
    withdraw: ReturnType<typeof vi.fn>;
    getApplicationById: ReturnType<typeof vi.fn>;
    downloadCv: ReturnType<typeof vi.fn>;
  };
  let profileServiceSpy: {
    getMyProfile: ReturnType<typeof vi.fn>;
    downloadDefaultCv: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: {
    currentUser: ReturnType<typeof vi.fn>;
    getRole?: ReturnType<typeof vi.fn>;
    isAuthenticated: ReturnType<typeof vi.fn>;
  };

  const mockProfile: CandidateProfileDto = {
    userId: 'u-1',
    fullName: 'Hana Tesfaye',
    email: 'hana@example.com',
    headline: 'Junior .NET Developer',
    hasDefaultCv: true,
    defaultCvOriginalFileName: 'default-cv.pdf',
    defaultCvFileSizeBytes: 102400,
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
      applyWithCv: vi.fn().mockReturnValue(of(mockApplicationResponse)),
      withdraw: vi.fn().mockReturnValue(of({ ...mockApplicationResponse, status: 'Withdrawn' })),
      getApplicationById: vi.fn().mockReturnValue(of(mockApplicationResponse)),
      downloadCv: vi.fn().mockReturnValue(of(new Blob(['test']))),
    };
    profileServiceSpy = {
      getMyProfile: vi.fn().mockReturnValue(of(mockProfile)),
      downloadDefaultCv: vi.fn().mockReturnValue(of(new Blob(['test default cv'], { type: 'application/pdf' }))),
    };
    authServiceSpy = {
      currentUser: vi.fn().mockReturnValue({ userId: 'u-1', fullName: 'Hana', role: 'Candidate' }),
      getRole: vi.fn().mockReturnValue('Candidate'),
      isAuthenticated: vi.fn().mockReturnValue(true),
    };

    await TestBed.configureTestingModule({
      imports: [JobDetailComponent],
      providers: [
        provideRouter([]),
        { provide: JobsService, useValue: jobsServiceSpy },
        { provide: ApplicationsService, useValue: applicationsServiceSpy },
        { provide: ProfileService, useValue: profileServiceSpy },
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

  it('should not display internal score breakdown points or factor grid to candidates', () => {
    const el = fixture.nativeElement;

    expect(el.querySelector('.factor-scores-grid')).toBeNull();
    expect(el.textContent).not.toContain('50 pts');
    expect(el.textContent).not.toContain('15 pts');
    expect(el.textContent).not.toContain('10 pts');
    expect(el.textContent).not.toContain('5 pts');
  });

  it('should submit application with cover note and default profile CV, displaying success state', () => {
    component.applyForm.patchValue({
      coverNote: 'I have 1 year experience in ASP.NET Core.',
    });

    component.onApplySubmit();

    expect(applicationsServiceSpy.applyWithCv).toHaveBeenCalledWith(
      12,
      'I have 1 year experience in ASP.NET Core.',
      null,
      true
    );

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
    applicationsServiceSpy.applyWithCv.mockReturnValue(throwError(() => errorResponse));
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

  it('should display Withdrawn banner and pill when applicationStatus is Withdrawn', () => {
    jobsServiceSpy.getJobById.mockReturnValue(
      of({ ...mockJob, hasApplied: true, applicationStatus: 'Withdrawn' })
    );
    component.loadJobDetail(12);
    fixture.detectChanges();

    expect(component.applicationStatusState()).toBe('Withdrawn');

    const withdrawnPill = fixture.nativeElement.querySelector('.withdrawn-pill');
    expect(withdrawnPill).toBeTruthy();
    expect(withdrawnPill.textContent).toContain('Withdrawn');

    const withdrawnBanner = fixture.nativeElement.querySelector('.withdrawn-banner');
    expect(withdrawnBanner).toBeTruthy();
    expect(withdrawnBanner.textContent).toContain('Application Withdrawn');

    const withdrawnBtn = fixture.nativeElement.querySelector('.withdrawn-btn');
    expect(withdrawnBtn).toBeTruthy();
    expect(withdrawnBtn.disabled).toBe(true);
    expect(withdrawnBtn.textContent).toContain('Withdrawn');
  });

  it('should allow candidate to withdraw application from Job Details page and update status to Withdrawn', () => {
    jobsServiceSpy.getJobById.mockReturnValue(
      of({ ...mockJob, hasApplied: true, applicationStatus: 'Received', applicationId: 31 })
    );
    component.loadJobDetail(12);
    fixture.detectChanges();

    expect(component.applicationStatusState()).toBe('Received');
    expect(component.applicationIdState()).toBe(31);

    // Prompt withdraw opens modal
    component.promptWithdraw();
    fixture.detectChanges();
    expect(component.showWithdrawModal()).toBe(true);

    const modal = fixture.nativeElement.querySelector('.modal-card');
    expect(modal).toBeTruthy();
    expect(modal.textContent).toContain('Withdraw Application');

    // Confirm withdraw calls service and transitions status
    component.confirmWithdraw();
    fixture.detectChanges();

    expect(applicationsServiceSpy.withdraw).toHaveBeenCalledWith(31);
    expect(component.applicationStatusState()).toBe('Withdrawn');
    expect(component.showWithdrawModal()).toBe(false);
    expect(component.successMessage()).toContain('Application withdrawn successfully');

    const withdrawnPill = fixture.nativeElement.querySelector('.withdrawn-pill');
    expect(withdrawnPill).toBeTruthy();
    expect(withdrawnPill.textContent).toContain('Withdrawn');
  });

  it('should display full countdown timer with days, hours, minutes, seconds when deadline is set', () => {
    const futureDate = new Date(Date.now() + 5 * 24 * 60 * 60 * 1000).toISOString();
    jobsServiceSpy.getJobById.mockReturnValue(of({ ...mockJob, deadline: futureDate, isOpen: true }));
    component.loadJobDetail(12);
    fixture.detectChanges();

    const el = fixture.nativeElement;
    const countdownCard = el.querySelector('.deadline-countdown-card');
    expect(countdownCard).toBeTruthy();
    expect(countdownCard.classList).not.toContain('urgent');

    const timerBoard = el.querySelector('.countdown-timer-board');
    expect(timerBoard).toBeTruthy();

    const units = el.querySelectorAll('.timer-segment .timer-unit-label');
    expect(units.length).toBe(4);
    expect(units[0].textContent).toContain('Days');
    expect(units[1].textContent).toContain('Hours');
    expect(units[2].textContent).toContain('Minutes');
    expect(units[3].textContent).toContain('Seconds');
  });

  it('should display urgent red styling and warning alert when deadline is within 24 hours', () => {
    const nearDeadline = new Date(Date.now() + 6 * 60 * 60 * 1000).toISOString(); // 6 hours left
    jobsServiceSpy.getJobById.mockReturnValue(of({ ...mockJob, deadline: nearDeadline, isOpen: true }));
    component.loadJobDetail(12);
    fixture.detectChanges();

    const el = fixture.nativeElement;
    const countdownCard = el.querySelector('.deadline-countdown-card');
    expect(countdownCard).toBeTruthy();
    expect(countdownCard.classList).toContain('urgent');

    const urgentBanner = el.querySelector('.urgent-alert-banner');
    expect(urgentBanner).toBeTruthy();
    expect(urgentBanner.textContent).toContain('Deadline Approaching: Less than 24 hours remaining to apply!');
  });

  it('should display closed status and disable application submit when job is closed', () => {
    jobsServiceSpy.getJobById.mockReturnValue(of({ ...mockJob, isOpen: false }));
    component.loadJobDetail(12);
    fixture.detectChanges();

    const el = fixture.nativeElement;
    const closedStatus = el.querySelector('.status-pill.closed');
    expect(closedStatus).toBeTruthy();
    expect(closedStatus.textContent).toContain('Closed');

    const closedBanner = el.querySelector('.already-applied-banner.closed-banner');
    expect(closedBanner).toBeTruthy();
    expect(closedBanner.textContent).toContain('Job Closed');

    const submitBtn = el.querySelector('.apply-submit-btn.closed-btn');
    expect(submitBtn).toBeTruthy();
    expect(submitBtn.disabled).toBe(true);
  });

  it('should NOT render Close Job button or modal on candidate job details view', () => {
    authServiceSpy.currentUser.mockReturnValue({ userId: 'emp-1', fullName: 'Acme HR', role: 'Employer' });
    jobsServiceSpy.getJobById.mockReturnValue(of({ ...mockJob, isOpen: true }));
    component.loadJobDetail(12);
    fixture.detectChanges();

    const el = fixture.nativeElement;
    expect(el.querySelector('.btn-manual-close')).toBeNull();
    expect(el.querySelector('.btn-close-job')).toBeNull();
    expect(el.querySelector('.close-modal')).toBeNull();
    expect(el.textContent).not.toContain('Close Job');
  });

  it('validates and accepts valid PDF or DOCX file, rejects invalid file formats or >5MB files', () => {
    // Invalid extension
    const invalidFile = new File(['executable'], 'virus.exe', { type: 'application/octet-stream' });
    const mockEvent1 = { target: { files: [invalidFile] } } as unknown as Event;
    component.onFileSelected(mockEvent1);
    expect(component.selectedCvFile()).toBeNull();
    expect(component.cvFileError()).toContain('Only PDF and DOCX files are allowed');

    // Oversized file (> 5 MB)
    const bigFile = new File([new Uint8Array(6 * 1024 * 1024)], 'big.pdf', { type: 'application/pdf' });
    const mockEvent2 = { target: { files: [bigFile] } } as unknown as Event;
    component.onFileSelected(mockEvent2);
    expect(component.selectedCvFile()).toBeNull();
    expect(component.cvFileError()).toContain('File size exceeds 5 MB');

    // Valid PDF
    const validFile = new File(['%PDF-1.4 test'], 'my-cv.pdf', { type: 'application/pdf' });
    const mockEvent3 = { target: { files: [validFile] } } as unknown as Event;
    component.onFileSelected(mockEvent3);
    expect(component.selectedCvFile()).toBe(validFile);
    expect(component.cvFileError()).toBeNull();

    // Remove file
    component.removeSelectedCvFile();
    expect(component.selectedCvFile()).toBeNull();
  });

  it('submits application with attached CV using applyWithCv (Option 2)', () => {
    component.setCvApplyOption('upload');
    const validDocx = new File(['PK\x03\x04'], 'resume.docx', { type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' });
    component.selectedCvFile.set(validDocx);
    component.applyForm.patchValue({ coverNote: 'Attached is my DOCX resume' });

    component.onApplySubmit();

    expect(applicationsServiceSpy.applyWithCv).toHaveBeenCalledWith(12, 'Attached is my DOCX resume', validDocx, false);
    expect(component.hasAppliedState()).toBe(true);
  });

  it('rejects submitting application when candidate has no default CV and no uploaded CV', () => {
    component.hasDefaultCv.set(false);
    component.setCvApplyOption('default');
    component.selectedCvFile.set(null);

    expect(component.isCvValid()).toBe(false);

    component.onApplySubmit();

    expect(applicationsServiceSpy.applyWithCv).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('A valid CV must always be attached before an application can be submitted');
  });

  it('allows candidate to switch between default CV option and upload different CV option', () => {
    expect(component.cvApplyOption()).toBe('default');
    expect(component.hasDefaultCv()).toBe(true);
    expect(component.isCvValid()).toBe(true);

    component.setCvApplyOption('upload');
    expect(component.cvApplyOption()).toBe('upload');
    expect(component.isCvValid()).toBe(false); // No file chosen yet

    const file = new File(['pdf data'], 'custom.pdf', { type: 'application/pdf' });
    component.selectedCvFile.set(file);
    expect(component.isCvValid()).toBe(true);
  });
});
