import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateJobRequest, EmployerJobDto, JobDto, PagedJobsResponse } from '../models/job.models';

@Injectable({
  providedIn: 'root',
})
export class JobsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/jobs`;

  /**
   * Fetch open jobs with optional multi-skill filtering, search, work arrangement, job type, and experience level.
   * Seamlessly handles both JobDto[] and PagedJobsResponse shapes from the server.
   */
  getJobs(
    skillIds?: number[],
    search?: string,
    sort?: string,
    page?: number,
    pageSize?: number,
    jobType?: string,
    workArrangement?: string,
    experienceLevel?: string,
    location?: string
  ): Observable<JobDto[]> {
    let params = new HttpParams();

    if (skillIds && skillIds.length > 0) {
      params = params.set('skillIds', skillIds.join(','));
    }

    if (search && search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    if (sort) {
      params = params.set('sort', sort);
    }

    if (page) {
      params = params.set('page', page.toString());
    }

    if (pageSize) {
      params = params.set('pageSize', pageSize.toString());
    }

    if (jobType) {
      params = params.set('jobType', jobType);
    }

    if (workArrangement) {
      params = params.set('workArrangement', workArrangement);
    }

    if (experienceLevel) {
      params = params.set('experienceLevel', experienceLevel);
    }

    if (location && location.trim().length > 0) {
      params = params.set('location', location.trim());
    }

    return this.http.get<JobDto[] | PagedJobsResponse>(this.baseUrl, { params }).pipe(
      map((response) => {
        if (Array.isArray(response)) {
          return response;
        }
        if (response && Array.isArray(response.items)) {
          return response.items;
        }
        return [];
      })
    );
  }

  /**
   * Get single job detail by ID
   */
  getJobById(id: number): Observable<JobDto> {
    return this.http.get<JobDto>(`${this.baseUrl}/${id}`);
  }

  getEmployerJobs(): Observable<EmployerJobDto[]> {
    return this.http.get<EmployerJobDto[]>(`${environment.apiUrl}/employer/jobs`);
  }

  /**
   * Post a new job opening
   * POST /api/jobs -> Request: { title, description, location?, requiredSkillIds: number[] } -> Response 201: JobDto
   */
  postJob(payload: CreateJobRequest): Observable<JobDto> {
    return this.http.post<JobDto>(this.baseUrl, payload);
  }

  createJob(payload: CreateJobRequest): Observable<JobDto> {
    return this.postJob(payload);
  }
}
