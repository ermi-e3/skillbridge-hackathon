import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { JobDto, PagedJobsResponse } from '../models/job.models';

@Injectable({
  providedIn: 'root',
})
export class JobsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/jobs`;

  /**
   * Fetch open jobs with optional multi-skill filtering and title search.
   * Seamlessly handles both JobDto[] and PagedJobsResponse shapes from the server.
   */
  getJobs(skillIds?: number[], search?: string): Observable<JobDto[]> {
    let params = new HttpParams();

    if (skillIds && skillIds.length > 0) {
      // Pass comma-separated skillIds (e.g. ?skillIds=1,2) as per endpoint specification
      params = params.set('skillIds', skillIds.join(','));
    }

    if (search && search.trim().length > 0) {
      params = params.set('search', search.trim());
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
}
