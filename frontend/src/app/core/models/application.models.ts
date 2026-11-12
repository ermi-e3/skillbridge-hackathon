import { JobMatchBreakdownDto, JobMatchDto } from './job.models';
import { SkillDto } from './profile.models';

export type ApplicationStatus = 'Received' | 'Shortlisted' | 'Rejected' | 'Withdrawn';

export interface ApplyRequest {
  coverNote?: string | null;
  useDefaultCv?: boolean;
}

export type { JobMatchBreakdownDto, JobMatchDto };

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
  hasCv?: boolean;
  cvOriginalFileName?: string | null;
  cvFileSizeBytes?: number | null;
  cvUploadedAt?: string | null;
  cvDownloadUrl?: string | null;
}

export interface JobSummaryDto {
  id: number;
  title: string;
  isOpen: boolean;
  requiredSkills: SkillDto[];
  preferredSkills?: SkillDto[];
  createdAt?: string;
  canManageApplications?: boolean;
  canEditJob?: boolean;
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
  hasCv?: boolean;
  cvOriginalFileName?: string | null;
  cvFileSizeBytes?: number | null;
  cvUploadedAt?: string | null;
  cvDownloadUrl?: string | null;
}

export interface CandidateProfileSkillItem {
  id: number;
  name: string;
  proficiency: string;
  yearsOfExperience: number;
  isPreferred: boolean;
}

export interface ApplicantProfileDto {
  candidateId: string;
  fullName: string;
  email: string;
  headline?: string | null;
  bio?: string | null;
  gitHubUrl?: string | null;
  location?: string | null;
  experienceLevel?: string | null;
  yearsOfExperience?: number | null;
  preferredWorkArrangement?: string | null;
  preferredJobType?: string | null;
  skills: CandidateProfileSkillItem[];
  experienceHistory: import('./profile.models').CandidateExperienceDto[];
  applicationId: number;
  applicationStatus: ApplicationStatus;
  appliedAt: string;
  statusChangedAt?: string | null;
  coverNote?: string | null;
  match?: JobMatchDto | null;
  hasCv: boolean;
  cvOriginalFileName?: string | null;
  cvFileSizeBytes?: number | null;
  cvUploadedAt?: string | null;
  cvDownloadUrl?: string | null;
}

export interface JobApplicantsResponse {
  job: JobSummaryDto;
  applicants: JobApplicantItemDto[];
}

export interface ChangeStatusRequest {
  status: 'Shortlisted' | 'Rejected';
}
