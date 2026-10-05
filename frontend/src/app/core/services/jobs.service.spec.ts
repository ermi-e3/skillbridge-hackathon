import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { EmployerJobDto, JobDto } from '../models/job.models';
import { JobsService } from './jobs.service';

describe('JobsService', () => {
  let service: JobsService;
  let httpTesting: HttpTestingController;

  const mockJobs: JobDto[] = [
    {
      id: 1,
      title: 'Junior Backend Developer',
      description: 'Build robust APIs with ASP.NET Core.',
      companyName: 'Abyssinia Tech',
      location: 'Addis Ababa',
      createdAt: '2026-10-10T08:55:00Z',
      requiredSkills: [
        { id: 1, name: 'ASP.NET Core' },
        { id: 3, name: 'C#' },
      ],
      myMatchPercent: 67,
      hasApplied: false,
    },
  ];

  const mockEmployerJobs: EmployerJobDto[] = [
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

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [JobsService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(JobsService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should fetch jobs without filter', () => {
    service.getJobs().subscribe((jobs) => {
      expect(jobs).toEqual(mockJobs);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/jobs`);
    expect(req.request.method).toBe('GET');
    req.flush(mockJobs);
  });

  it('should fetch jobs with skillIds query parameter', () => {
    service.getJobs([1, 3]).subscribe((jobs) => {
      expect(jobs).toEqual(mockJobs);
    });

    const req = httpTesting.expectOne((r) => r.url === `${environment.apiUrl}/jobs` && r.params.get('skillIds') === '1,3');
    expect(req.request.method).toBe('GET');
    req.flush(mockJobs);
  });

  it('should handle paged response shape { items: [] }', () => {
    service.getJobs().subscribe((jobs) => {
      expect(jobs).toEqual(mockJobs);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/jobs`);
    req.flush({ items: mockJobs, totalCount: 1, page: 1, pageSize: 20, totalPages: 1 });
  });

  it('should fetch single job by ID', () => {
    service.getJobById(1).subscribe((job) => {
      expect(job).toEqual(mockJobs[0]);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/jobs/1`);
    expect(req.request.method).toBe('GET');
    req.flush(mockJobs[0]);
  });

  it('should fetch the logged-in employer jobs', () => {
    service.getEmployerJobs().subscribe((jobs) => {
      expect(jobs).toEqual(mockEmployerJobs);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/employer/jobs`);
    expect(req.request.method).toBe('GET');
    req.flush(mockEmployerJobs);
  });
});
