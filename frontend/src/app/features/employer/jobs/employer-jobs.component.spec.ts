import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { EmployerJobDto } from '../../../core/models/job.models';
import { AuthService } from '../../../core/services/auth.service';
import { JobsService } from '../../../core/services/jobs.service';
import { EmployerJobsComponent } from './employer-jobs.component';

describe('EmployerJobsComponent', () => {
  let fixture: ComponentFixture<EmployerJobsComponent>;
  let component: EmployerJobsComponent;
  let jobsServiceSpy: {
    getEmployerJobs: ReturnType<typeof vi.fn>;
    closeJob: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: { logout: ReturnType<typeof vi.fn> };

  const mockJobs: EmployerJobDto[] = [
    {
      id: 12,
      title: 'Junior Backend Developer',
      location: 'Addis Ababa',
      jobType: 'FullTime',
      workArrangement: 'Hybrid',
      experienceLevel: 'MidLevel',
      minExperienceYears: 3,
      description: 'We are looking for a skilled developer to build scalable backend services.',
      isOpen: true,
      createdAt: '2026-10-10T08:55:00Z',
      requiredSkills: [
        { id: 1, name: 'ASP.NET Core' },
        { id: 3, name: 'C#' },
      ],
      preferredSkills: [
        { id: 5, name: 'Docker' },
      ],
      applicantCounts: {
        total: 4,
        received: 2,
        shortlisted: 1,
        rejected: 1,
        withdrawn: 0,
      },
    },
  ];

  beforeEach(async () => {
    jobsServiceSpy = {
      getEmployerJobs: vi.fn().mockReturnValue(of(mockJobs)),
      closeJob: vi.fn().mockReturnValue(of({ ...mockJobs[0], isOpen: false })),
    };
    authServiceSpy = {
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [EmployerJobsComponent],
      providers: [
        provideRouter([]),
        { provide: JobsService, useValue: jobsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EmployerJobsComponent);
    component = fixture.componentInstance;
  });

  it('loads and renders the employer jobs and required skills', () => {
    fixture.detectChanges();

    expect(jobsServiceSpy.getEmployerJobs).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Junior Backend Developer');
    expect(fixture.nativeElement.textContent).toContain('Addis Ababa');
    expect(fixture.nativeElement.textContent).toContain('Full-time');
    expect(fixture.nativeElement.textContent).toContain('Hybrid');
    expect(fixture.nativeElement.textContent).toContain('Mid Level');
    expect(fixture.nativeElement.textContent).toContain('3+ yrs exp');
    expect(fixture.nativeElement.textContent).toContain('We are looking for a skilled developer');
    expect(fixture.nativeElement.textContent).toContain('ASP.NET Core');
    expect(fixture.nativeElement.textContent).toContain('Docker');
    expect(fixture.nativeElement.textContent).toContain('4 applicants');
    expect(fixture.nativeElement.textContent).toContain('2 received');
    expect(fixture.nativeElement.textContent).toContain('1 shortlisted');

    const applicantsLink = fixture.nativeElement.querySelector('.applicants-action');
    expect(applicantsLink.getAttribute('href')).toBe('/employer/jobs/12/applicants');
  });

  it('shows the friendly empty state when no jobs are returned', () => {
    jobsServiceSpy.getEmployerJobs.mockReturnValue(of([]));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No jobs posted yet');
    expect(fixture.nativeElement.querySelector('.empty-action').getAttribute('href')).toBe(
      '/employer/jobs/new'
    );
  });

  it('displays an error and allows the employer to retry', () => {
    jobsServiceSpy.getEmployerJobs.mockReturnValue(
      throwError(() => new Error('Request failed'))
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('We couldn’t load your jobs');
    expect(fixture.nativeElement.querySelector('.retry-button')).toBeTruthy();
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

  it('displays countdown chip on active job cards and shows urgent badge when within 24 hours', () => {
    const nearDeadline = new Date(Date.now() + 10 * 60 * 60 * 1000).toISOString();
    jobsServiceSpy.getEmployerJobs.mockReturnValue(
      of([{ ...mockJobs[0], deadline: nearDeadline, isOpen: true }])
    );
    fixture.detectChanges();

    const chip = fixture.nativeElement.querySelector('.countdown-chip');
    expect(chip).toBeTruthy();
    expect(chip.classList).toContain('urgent');
    expect(chip.textContent).toContain('Closing soon');
  });

  it('allows employer to manually close an active job with confirmation modal', () => {
    jobsServiceSpy.getEmployerJobs.mockReturnValue(of([{ ...mockJobs[0], isOpen: true }]));
    fixture.detectChanges();

    const closeBtn = fixture.nativeElement.querySelector('.btn-close-job');
    expect(closeBtn).toBeTruthy();

    // Trigger close modal prompt
    component.openCloseConfirm(mockJobs[0]);
    fixture.detectChanges();
    expect(component.jobToClose()?.id).toBe(12);

    const modal = fixture.nativeElement.querySelector('.modal-dialog');
    expect(modal).toBeTruthy();
    expect(modal.textContent).toContain('Close Job Opening?');

    // Confirm close
    component.confirmCloseJob();
    fixture.detectChanges();

    expect(jobsServiceSpy.closeJob).toHaveBeenCalledWith(12);
    expect(component.jobToClose()).toBeNull();
  });

  it('displays active Edit Job link when within 2 hours of posting', () => {
    const recentCreated = new Date(Date.now() - 30 * 60 * 1000).toISOString();
    jobsServiceSpy.getEmployerJobs.mockReturnValue(
      of([{ ...mockJobs[0], createdAt: recentCreated }])
    );
    fixture.detectChanges();

    const editBtn = fixture.nativeElement.querySelector('.btn-edit-job');
    expect(editBtn).toBeTruthy();
    expect(editBtn.tagName.toLowerCase()).toBe('a');
    expect(editBtn.getAttribute('href')).toBe('/employer/jobs/12/edit');
    expect(editBtn.textContent).toContain('Edit Job');
  });

  it('displays disabled Edit Expired button when older than 2 hours', () => {
    const expiredCreated = new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString();
    jobsServiceSpy.getEmployerJobs.mockReturnValue(
      of([{ ...mockJobs[0], createdAt: expiredCreated }])
    );
    fixture.detectChanges();

    const editBtn = fixture.nativeElement.querySelector('.btn-edit-job');
    expect(editBtn).toBeTruthy();
    expect(editBtn.tagName.toLowerCase()).toBe('button');
    expect(editBtn.hasAttribute('disabled')).toBe(true);
    expect(editBtn.textContent).toContain('Edit Expired');
  });

  it('shows only active jobs by default, filters to closed status via toggle, and supports pagination and attribute filters', () => {
    const multiJobs: EmployerJobDto[] = [
      mockJobs[0], // id: 12 Junior Backend Developer (Technology, FullTime, Active, 2026-10-10)
      {
        ...mockJobs[0],
        id: 13,
        title: 'Registered Nurse',
        jobType: 'PartTime',
        isOpen: false,
        createdAt: '2026-10-01T10:00:00Z',
        requiredSkills: [{ id: 41, name: 'Patient Care' }],
        preferredSkills: [],
      },
      {
        ...mockJobs[0],
        id: 14,
        title: 'Financial Analyst',
        jobType: 'Contract',
        isOpen: true,
        createdAt: '2026-10-12T10:00:00Z',
        requiredSkills: [{ id: 56, name: 'Financial Analysis' }],
        preferredSkills: [],
      },
      {
        ...mockJobs[0],
        id: 15,
        title: 'High School Educator',
        jobType: 'FullTime',
        isOpen: true,
        createdAt: '2026-10-13T10:00:00Z',
        requiredSkills: [{ id: 72, name: 'Classroom Management' }],
        preferredSkills: [],
      },
    ];
    jobsServiceSpy.getEmployerJobs.mockReturnValue(of(multiJobs));
    fixture.detectChanges();

    // Default state shows ONLY active jobs (12, 14, 15) and hides closed job (13)
    expect(component.statusFilter()).toBe('Active');
    expect(component.filteredJobs().map((j) => j.id)).toEqual([12, 14, 15]);

    // Filter by job title (within active jobs)
    component.searchTitleQuery.set('Educator');
    expect(component.filteredJobs().map((j) => j.id)).toEqual([15]);

    // Filter by department
    component.clearTitleSearch();
    component.departmentFilter.set('finance');
    expect(component.filteredJobs().map((j) => j.id)).toEqual([14]);

    // Filter by job type
    component.departmentFilter.set('all');
    component.jobTypeFilter.set('Contract');
    expect(component.filteredJobs().map((j) => j.id)).toEqual([14]);

    // Filter by Closed status toggle -> shows ONLY closed jobs (13)
    component.jobTypeFilter.set('All');
    const closedBtn = fixture.nativeElement.querySelector('#emp-job-status-filter') as HTMLButtonElement;
    expect(closedBtn).toBeTruthy();
    closedBtn.click();
    fixture.detectChanges();
    expect(component.statusFilter()).toBe('Closed');
    expect(component.filteredJobs().map((j) => j.id)).toEqual([13]);

    // Clear filters -> returns to showing Active jobs (12, 14, 15)
    component.clearAllFilters();
    fixture.detectChanges();
    expect(component.statusFilter()).toBe('Active');
    expect(component.filteredJobs().map((j) => j.id)).toEqual([12, 14, 15]);

    // Filter by posting date
    component.postedFromDate.set('2026-10-12');
    component.datePreset.set('custom');
    expect(component.filteredJobs().map((j) => j.id)).toEqual([14, 15]);

    // Pagination check over active jobs
    component.clearAllFilters();
    component.pageSize.set(2);
    expect(component.paginatedJobs().length).toBe(2);
    component.onPageChange(2);
    expect(component.paginatedJobs().length).toBe(1);
    expect(component.paginatedJobs()[0].id).toBe(15);
  });
});
