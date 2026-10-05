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

export interface EmployerJobDto {
  id: number;
  title: string;
  location?: string | null;
  isOpen: boolean;
  createdAt: string;
  requiredSkills: SkillDto[];
  applicantCounts: {
    total: number;
    received: number;
    shortlisted: number;
    rejected: number;
    withdrawn: number;
  };
}
