import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { EmployerApplicationsComponent } from './employer-applications.component';
import { JobsService } from '../../../core/services/jobs.service';
import { AuthService } from '../../../core/services/auth.service';
import { EmployerJobDto } from '../../../core/models/job.models';

describe('EmployerApplicationsComponent', () => {
  let component: EmployerApplicationsComponent;
  let fixture: ComponentFixture<EmployerApplicationsComponent>;
  let jobsServiceSpy: {
    getEmployerJobs: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: {
    currentUser: ReturnType<typeof vi.fn>;
  };

  const mockJobs: EmployerJobDto[] = [
    {
      id: 1,
      title: 'backend developer',
      category: 'Technology & Software',
      location: 'Addis Ababa',
      isOpen: true,
      createdAt: '2026-10-10T12:00:00Z',
      requiredSkills: [{ id: 1, name: 'C#' }],
      applicantCounts: { total: 3, received: 1, shortlisted: 1, rejected: 1, withdrawn: 0 },
    },
    {
      id: 2,
      title: 'Frontend Angular Specialist',
      category: 'Technology & Software',
      location: 'Remote',
      isOpen: true,
      createdAt: '2026-10-12T12:00:00Z',
      requiredSkills: [{ id: 2, name: 'Angular' }],
      applicantCounts: { total: 2, received: 1, shortlisted: 1, rejected: 0, withdrawn: 0 },
    },
    {
      id: 3,
      title: 'Project Manager',
      category: 'Business & Operations',
      location: 'Addis Ababa',
      isOpen: true,
      createdAt: '2026-10-14T12:00:00Z',
      requiredSkills: [{ id: 3, name: 'Scrum' }],
      applicantCounts: { total: 2, received: 1, shortlisted: 1, rejected: 0, withdrawn: 0 },
    },
    {
      id: 4,
      title: 'Customer Support Team Lead',
      category: 'Hospitality & Customer Service',
      location: 'Addis Ababa',
      isOpen: true,
      createdAt: '2026-10-15T12:00:00Z',
      requiredSkills: [{ id: 4, name: 'Support' }],
      applicantCounts: { total: 1, received: 0, shortlisted: 1, rejected: 0, withdrawn: 0 },
    },
    {
      id: 5,
      title: 'Devops',
      category: 'Technology & Software',
      location: 'Remote',
      isOpen: true,
      createdAt: '2026-10-16T12:00:00Z',
      requiredSkills: [{ id: 5, name: 'Docker' }],
      applicantCounts: { total: 1, received: 0, shortlisted: 0, rejected: 1, withdrawn: 0 },
    },
    {
      id: 6,
      title: 'Full-Stack Software Engineer',
      category: 'Technology & Software',
      location: 'Addis Ababa',
      isOpen: true,
      createdAt: '2026-10-17T12:00:00Z',
      requiredSkills: [{ id: 6, name: 'TypeScript' }],
      applicantCounts: { total: 1, received: 0, shortlisted: 0, rejected: 0, withdrawn: 1 },
    },
    {
      id: 7,
      title: 'Operations',
      category: 'Business & Operations',
      location: 'Addis Ababa',
      isOpen: false,
      createdAt: '2026-10-18T12:00:00Z',
      requiredSkills: [{ id: 7, name: 'Ops' }],
      applicantCounts: { total: 0, received: 0, shortlisted: 0, rejected: 0, withdrawn: 0 },
    },
    {
      id: 8,
      title: 'frontend',
      category: 'Technology & Software',
      location: 'Addis Ababa',
      isOpen: false,
      createdAt: '2026-10-19T12:00:00Z',
      requiredSkills: [{ id: 8, name: 'HTML' }],
      applicantCounts: { total: 0, received: 0, shortlisted: 0, rejected: 0, withdrawn: 0 },
    },
  ];

  beforeEach(async () => {
    jobsServiceSpy = {
      getEmployerJobs: vi.fn().mockReturnValue(of(mockJobs)),
    };

    authServiceSpy = {
      currentUser: vi.fn().mockReturnValue({
        fullName: 'Demo Employer',
        email: 'employer@demo.com',
        role: 'Employer',
        companyName: 'SkillBridge Inc',
      }),
    };

    await TestBed.configureTestingModule({
      imports: [EmployerApplicationsComponent],
      providers: [
        provideRouter([]),
        { provide: JobsService, useValue: jobsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EmployerApplicationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and load employer jobs on init', () => {
    expect(component).toBeTruthy();
    expect(jobsServiceSpy.getEmployerJobs).toHaveBeenCalled();
    expect(component.jobs().length).toBe(8);
    expect(component.isLoading()).toBe(false);
  });

  it('should compute executive telemetry KPI metrics accurately', () => {
    // Total applicants = 3 + 2 + 2 + 1 + 1 + 1 + 0 + 0 = 10
    expect(component.totalApplications()).toBe(10);
    // Total received = 1 + 1 + 1 + 0 + 0 + 0 + 0 + 0 = 3
    expect(component.totalReceived()).toBe(3);
    // Total shortlisted = 1 + 1 + 1 + 1 + 0 + 0 + 0 + 0 = 4
    expect(component.totalShortlisted()).toBe(4);
  });

  it('should filter jobs when search query is entered', () => {
    component.onSearchInput({ target: { value: 'Angular' } } as unknown as Event);
    expect(component.searchQuery()).toBe('Angular');
    expect(component.filteredJobs().length).toBe(1);
    expect(component.filteredJobs()[0].title).toBe('Frontend Angular Specialist');
    expect(component.currentPage()).toBe(1);
  });

  it('should filter jobs when clicking status filter pills', () => {
    // Active jobs
    component.setStatusFilter('active');
    expect(component.statusFilter()).toBe('active');
    expect(component.filteredJobs().length).toBe(6);

    // Closed jobs
    component.setStatusFilter('closed');
    expect(component.statusFilter()).toBe('closed');
    expect(component.filteredJobs().length).toBe(2);

    // Has applicants
    component.setStatusFilter('has_applicants');
    expect(component.statusFilter()).toBe('has_applicants');
    expect(component.filteredJobs().length).toBe(6);

    // Pending review
    component.setStatusFilter('pending');
    expect(component.filteredJobs().length).toBe(3);

    // Shortlisted
    component.setStatusFilter('shortlisted');
    expect(component.filteredJobs().length).toBe(4);
  });

  it('should handle pagination correctly with default pageSize of 6', () => {
    expect(component.pageSize()).toBe(6);
    expect(component.totalPages()).toBe(2);
    expect(component.currentPage()).toBe(1);
    expect(component.paginatedJobs().length).toBe(6);
    expect(component.showingRange()).toEqual({ start: 1, end: 6, total: 8 });

    // Navigate to Page 2
    component.nextPage();
    expect(component.currentPage()).toBe(2);
    expect(component.paginatedJobs().length).toBe(2);
    expect(component.showingRange()).toEqual({ start: 7, end: 8, total: 8 });

    // Navigate back to Page 1
    component.prevPage();
    expect(component.currentPage()).toBe(1);

    // Jump directly to page
    component.goToPage(2);
    expect(component.currentPage()).toBe(2);
  });

  it('should update page size dynamically', () => {
    component.onPageSizeChange({ target: { value: '10' } } as unknown as Event);
    expect(component.pageSize()).toBe(10);
    expect(component.totalPages()).toBe(1);
    expect(component.paginatedJobs().length).toBe(8);
    expect(component.showingRange()).toEqual({ start: 1, end: 8, total: 8 });
  });

  it('should reset filters cleanly', () => {
    component.onSearchInput({ target: { value: 'Devops' } } as unknown as Event);
    component.setStatusFilter('closed');
    expect(component.hasActiveFilter()).toBe(true);

    component.resetFilters();
    expect(component.searchQuery()).toBe('');
    expect(component.statusFilter()).toBe('all');
    expect(component.currentPage()).toBe(1);
    expect(component.filteredJobs().length).toBe(8);
  });

  it('should handle error when loading jobs fails', () => {
    jobsServiceSpy.getEmployerJobs.mockReturnValue(
      throwError(() => new Error('Server unavailable'))
    );
    component.loadEmployerJobs();
    expect(component.isLoading()).toBe(false);
    expect(component.errorMessage()).toBeTruthy();
  });
});
