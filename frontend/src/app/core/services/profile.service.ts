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
}
