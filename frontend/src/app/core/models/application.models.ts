import { SkillDto } from './profile.models';

export type ApplicationStatus = 'Received' | 'Shortlisted' | 'Rejected' | 'Withdrawn';

export interface ApplyRequest {
  coverNote?: string | null;
}

export interface JobMatchDto {
  percent: number;
  matchedSkills: SkillDto[];
  missingSkills: SkillDto[];
}

export interface ApplicationDto {
  id: number;
  jobId: number;
  jobTitle: string;
  companyName?: string;
  status: ApplicationStatus;
  appliedAt: string;
  statusChangedAt?: string | null;
  coverNote?: string | null;
  matchPercent?: number;
  match?: JobMatchDto | null;
}

export interface JobSummaryDto {
  id: number;
  title: string;
  isOpen: boolean;
  requiredSkills: SkillDto[];
}

export interface CandidateApplicantSummary {
  userId: string;
  fullName: string;
  email: string;
  headline?: string | null;
  gitHubUrl?: string | null;
  skills: SkillDto[];
}

export interface JobApplicantItemDto {
  applicationId: number;
  candidate: CandidateApplicantSummary;
  status: ApplicationStatus;
  appliedAt: string;
  statusChangedAt?: string | null;
  coverNote?: string | null;
  match: JobMatchDto;
}

export interface JobApplicantsResponse {
  job: JobSummaryDto;
  applicants: JobApplicantItemDto[];
}

export interface ChangeStatusRequest {
  status: 'Shortlisted' | 'Rejected';
}
