import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApplicantProfileDto,
  ApplicationDto,
  ApplyRequest,
  ChangeStatusRequest,
  JobApplicantsResponse,
} from '../models/application.models';

@Injectable({
  providedIn: 'root',
})
export class ApplicationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /**
   * Submit an application for a specific job (JSON payload)
   * POST /api/jobs/{jobId}/applications
   */
  apply(jobId: number, payload: ApplyRequest): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>(`${this.baseUrl}/jobs/${jobId}/applications`, payload);
  }

  /**
   * Submit an application with a CV (either uploaded file or using profile default CV)
   * POST /api/jobs/{jobId}/applications
   */
  applyWithCv(
    jobId: number,
    coverNote: string | null,
    file?: File | null,
    useDefaultCv = false
  ): Observable<ApplicationDto> {
    if (!file) {
      return this.apply(jobId, { coverNote, useDefaultCv });
    }

    const formData = new FormData();
    if (coverNote && coverNote.trim().length > 0) {
      formData.append('coverNote', coverNote.trim());
    }
    formData.append('useDefaultCv', String(useDefaultCv));
    formData.append('cv', file, file.name);

    return this.http.post<ApplicationDto>(`${this.baseUrl}/jobs/${jobId}/applications`, formData);
  }

  /**
   * Download a candidate's CV attachment for a specific application
   * GET /api/applications/{id}/cv
   */
  downloadCv(applicationId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/applications/${applicationId}/cv`, {
      responseType: 'blob',
    });
  }

  /**
   * Get full candidate applicant profile for a specific job
   * GET /api/jobs/{jobId}/applicants/{candidateId}/profile
   */
  getApplicantProfile(jobId: number, candidateId: string): Observable<ApplicantProfileDto> {
    return this.http.get<ApplicantProfileDto>(
      `${this.baseUrl}/jobs/${jobId}/applicants/${candidateId}/profile`
    );
  }

  /**
   * Get all applications submitted by the current candidate
   * GET /api/candidates/me/applications
   */
  getMyApplications(): Observable<ApplicationDto[]> {
    return this.http.get<ApplicationDto[]>(`${this.baseUrl}/candidates/me/applications`);
  }

  /**
   * Get single application by ID
   * GET /api/applications/{id}
   */
  getApplicationById(id: number): Observable<ApplicationDto> {
    return this.http.get<ApplicationDto>(`${this.baseUrl}/applications/${id}`);
  }

  /**
   * Withdraw an application in Received status
   * PATCH /api/applications/{id}/withdraw
   */
  withdraw(id: number): Observable<ApplicationDto> {
    return this.http.patch<ApplicationDto>(`${this.baseUrl}/applications/${id}/withdraw`, {});
  }

  /**
   * Get applicants for a specific employer job with optional status and full match filters
   * GET /api/jobs/{jobId}/applications
   */
  getJobApplicants(
    jobId: number,
    status?: string,
    fullMatchOnly = false
  ): Observable<JobApplicantsResponse> {
    let params = new HttpParams();
    if (status && status !== 'All') {
      params = params.set('status', status);
    }
    if (fullMatchOnly) {
      params = params.set('fullMatchOnly', 'true');
    }
    return this.http.get<JobApplicantsResponse>(`${this.baseUrl}/jobs/${jobId}/applications`, {
      params,
    });
  }

  /**
   * Update application status (Shortlisted or Rejected) by the employer
   * PATCH /api/applications/{id}/status
   */
  changeStatus(id: number, status: 'Shortlisted' | 'Rejected'): Observable<ApplicationDto> {
    const payload: ChangeStatusRequest = { status };
    return this.http.patch<ApplicationDto>(`${this.baseUrl}/applications/${id}/status`, payload);
  }
}
