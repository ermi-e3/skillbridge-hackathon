import { SkillDto } from './profile.models';

export interface JobMatchBreakdownDto {
  requiredSkillsScore: number;
  maxRequiredSkillsScore: number;
  preferredSkillsScore: number;
  maxPreferredSkillsScore: number;
  experienceScore: number;
  maxExperienceScore: number;
  workArrangementScore: number;
  maxWorkArrangementScore: number;
  locationScore: number;
  maxLocationScore: number;
  jobTypeScore: number;
  maxJobTypeScore: number;
  explanation: string;
}

export interface JobMatchDto {
  percent: number;
  matchedSkills?: SkillDto[];
  missingSkills?: SkillDto[];
  matchedPreferredSkills?: SkillDto[];
  missingPreferredSkills?: SkillDto[];
  breakdown?: JobMatchBreakdownDto | null;
}

export interface JobDto {
  id: number;
  title: string;
  description: string;
  location?: string;
  jobType?: string;
  workArrangement?: string;
  experienceLevel?: string;
  minExperienceYears?: number;
  requiredSkills: SkillDto[];
  preferredSkills?: SkillDto[];
  companyName: string;
  createdAt: string;
  isOpen?: boolean;
  myMatchPercent?: number;
  hasApplied?: boolean;
  match?: JobMatchDto | null;
  applicationStatus?: string;
  applicationId?: number;
  deadline?: string | null;
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
  description?: string;
  location?: string | null;
  jobType?: string;
  workArrangement?: string;
  experienceLevel?: string;
  minExperienceYears?: number;
  isOpen: boolean;
  createdAt: string;
  requiredSkills: SkillDto[];
  preferredSkills?: SkillDto[];
  applicantCounts: {
    total: number;
    received: number;
    shortlisted: number;
    rejected: number;
    withdrawn: number;
  };
  deadline?: string | null;
}

export interface CreateJobSkillRequirementRequest {
  skillId: number;
  isRequired: boolean;
  minProficiency: string;
}

export interface CreateJobRequest {
  title: string;
  description: string;
  location?: string | null;
  jobType?: string;
  workArrangement?: string;
  experienceLevel?: string;
  minExperienceYears?: number;
  requiredSkillIds: number[];
  preferredSkillIds?: number[];
  skillRequirements?: CreateJobSkillRequirementRequest[];
  deadline?: string | null;
}
