import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ApplicationDto, ApplyRequest } from '../models/application.models';
import { ApplicationsService } from './applications.service';

describe('ApplicationsService', () => {
  let service: ApplicationsService;
  let httpTesting: HttpTestingController;

  const mockApplication: ApplicationDto = {
    id: 31,
    jobId: 12,
    jobTitle: 'Junior Backend Developer',
    companyName: 'Abyssinia Tech',
    status: 'Received',
    appliedAt: '2026-10-10T09:14:00Z',
    statusChangedAt: null,
    coverNote: 'I built a training management API in ASP.NET Core.',
    matchPercent: 67,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApplicationsService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(ApplicationsService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should send POST /api/jobs/{jobId}/applications when applying', () => {
    const payload: ApplyRequest = { coverNote: 'Strong background in C#' };

    service.apply(12, payload).subscribe((res) => {
      expect(res).toEqual(mockApplication);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/jobs/12/applications`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(mockApplication, { status: 201, statusText: 'Created' });
  });

  it('should send GET /api/candidates/me/applications', () => {
    service.getMyApplications().subscribe((list) => {
      expect(list).toEqual([mockApplication]);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/candidates/me/applications`);
    expect(req.request.method).toBe('GET');
    req.flush([mockApplication]);
  });

  it('should send GET /api/applications/{id}', () => {
    service.getApplicationById(31).subscribe((app) => {
      expect(app).toEqual(mockApplication);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/applications/31`);
    expect(req.request.method).toBe('GET');
    req.flush(mockApplication);
  });

  it('should send PATCH /api/applications/{id}/withdraw', () => {
    const withdrawnResponse: ApplicationDto = { ...mockApplication, status: 'Withdrawn' };

    service.withdraw(31).subscribe((res) => {
      expect(res.status).toBe('Withdrawn');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/applications/31/withdraw`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({});
    req.flush(withdrawnResponse);
  });

  it('should send GET /api/jobs/{jobId}/applications with query parameters', () => {
    const mockApplicantsResponse = {
      job: { id: 12, title: 'Junior Backend Developer', isOpen: true, requiredSkills: [] },
      applicants: [],
    };

    service.getJobApplicants(12, 'Received', true).subscribe((res) => {
      expect(res).toEqual(mockApplicantsResponse);
    });

    const req = httpTesting.expectOne(
      (r) =>
        r.url === `${environment.apiUrl}/jobs/12/applications` &&
        r.params.get('status') === 'Received' &&
        r.params.get('fullMatchOnly') === 'true'
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockApplicantsResponse);
  });

  it('should send PATCH /api/applications/{id}/status when changing status', () => {
    const shortlistedResponse: ApplicationDto = { ...mockApplication, status: 'Shortlisted' };

    service.changeStatus(31, 'Shortlisted').subscribe((res) => {
      expect(res.status).toBe('Shortlisted');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/applications/31/status`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ status: 'Shortlisted' });
    req.flush(shortlistedResponse);
  });
});
