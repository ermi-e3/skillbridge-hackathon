import { SkillDto } from './profile.models';

export interface JobMatchDto {
  percent: number;
  matchedSkills?: SkillDto[];
  missingSkills?: SkillDto[];
}

export interface JobDto {
  id: number;
  title: string;
  description: string;
  location?: string;
  requiredSkills: SkillDto[];
  companyName: string;
  createdAt: string;
  isOpen?: boolean;
  myMatchPercent?: number;
  hasApplied?: boolean;
  match?: JobMatchDto | null;
}

export interface PagedJobsResponse {
  items: JobDto[];
  page?: number;
  pageSize?: number;
  totalCount?: number;
  totalPages?: number;
}
