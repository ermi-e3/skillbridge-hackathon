import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SkillDto } from '../models/profile.models';

@Injectable({
  providedIn: 'root',
})
export class SkillsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/skills`;

  /**
   * Fetch all skills from catalog
   */
  getSkills(): Observable<SkillDto[]> {
    return this.http.get<SkillDto[]>(this.baseUrl);
  }
}
