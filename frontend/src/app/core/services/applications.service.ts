import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApplicationDto, ApplyRequest } from '../models/application.models';

@Injectable({
  providedIn: 'root',
})
export class ApplicationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /**
   * Submit an application for a specific job
   * POST /api/jobs/{jobId}/applications
   */
  apply(jobId: number, payload: ApplyRequest): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>(`${this.baseUrl}/jobs/${jobId}/applications`, payload);
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
}
