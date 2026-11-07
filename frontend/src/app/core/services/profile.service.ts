import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CandidateProfileDto, UpdateCandidateProfileRequest } from '../models/profile.models';

@Injectable({
  providedIn: 'root',
})
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/candidates/me/profile`;

  /**
   * Get caller's candidate profile
   */
  getMyProfile(): Observable<CandidateProfileDto> {
    return this.http.get<CandidateProfileDto>(this.baseUrl);
  }

  /**
   * Update candidate profile with headline, bio, gitHubUrl, and skillIds
   */
  updateMyProfile(payload: UpdateCandidateProfileRequest): Observable<CandidateProfileDto> {
    return this.http.put<CandidateProfileDto>(this.baseUrl, payload);
  }

  /**
   * Upload or replace default CV for candidate profile
   * POST /api/candidates/me/profile/cv
   */
  uploadDefaultCv(file: File): Observable<CandidateProfileDto> {
    const formData = new FormData();
    formData.append('cv', file, file.name);
    return this.http.post<CandidateProfileDto>(`${this.baseUrl}/cv`, formData);
  }

  /**
   * Download / stream current candidate's default CV
   * GET /api/candidates/me/profile/cv
   */
  downloadDefaultCv(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/cv`, {
      responseType: 'blob',
    });
  }

  /**
   * Remove current candidate's default CV
   * DELETE /api/candidates/me/profile/cv
   */
  removeDefaultCv(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/cv`);
  }
}
