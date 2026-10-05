export type ApplicationStatus = 'Received' | 'Shortlisted' | 'Rejected' | 'Withdrawn';

export interface ApplyRequest {
  coverNote?: string | null;
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
  match?: {
    percent: number;
    matchedSkills?: { id: number; name: string }[];
    missingSkills?: { id: number; name: string }[];
  } | null;
}
