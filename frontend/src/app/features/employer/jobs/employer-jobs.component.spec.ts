import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { EmployerJobDto } from '../../../core/models/job.models';
import { JobsService } from '../../../core/services/jobs.service';
import { EmployerJobsComponent } from './employer-jobs.component';

describe('EmployerJobsComponent', () => {
  let fixture: ComponentFixture<EmployerJobsComponent>;
  let jobsServiceSpy: { getEmployerJobs: ReturnType<typeof vi.fn> };

  const mockJobs: EmployerJobDto[] = [
    {
      id: 12,
      title: 'Junior Backend Developer',
      location: 'Addis Ababa',
      isOpen: true,
      createdAt: '2026-10-10T08:55:00Z',
      requiredSkills: [
        { id: 1, name: 'ASP.NET Core' },
        { id: 3, name: 'C#' },
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
    };

    await TestBed.configureTestingModule({
      imports: [EmployerJobsComponent],
      providers: [
        provideRouter([]),
        { provide: JobsService, useValue: jobsServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EmployerJobsComponent);
  });

  it('loads and renders the employer jobs and required skills', () => {
    fixture.detectChanges();

    expect(jobsServiceSpy.getEmployerJobs).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Junior Backend Developer');
    expect(fixture.nativeElement.textContent).toContain('Addis Ababa');
    expect(fixture.nativeElement.textContent).toContain('ASP.NET Core');
    expect(fixture.nativeElement.textContent).toContain('4 applicants');

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
});
