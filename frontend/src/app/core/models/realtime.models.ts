import { ApplicationStatus, JobApplicantItemDto } from './application.models';
import { JobDto } from './job.models';

export interface ApplicantCounts {
  total: number;
  received: number;
  shortlisted: number;
  rejected: number;
  withdrawn: number;
}

export interface ApplicationSubmittedRealtimeEvent {
  applicationId: number;
  jobId: number;
  jobTitle: string;
  employerId: string;
  candidateId: string;
  candidateName: string;
  matchPercent: number;
  applicant: JobApplicantItemDto;
  counts: ApplicantCounts;
}

export interface ApplicationWithdrawnRealtimeEvent {
  applicationId: number;
  jobId: number;
  jobTitle: string;
  employerId: string;
  candidateId: string;
  counts: ApplicantCounts;
}

export interface ApplicationStatusChangedRealtimeEvent {
  applicationId: number;
  jobId: number;
  jobTitle: string;
  employerId: string;
  candidateId: string;
  newStatus: ApplicationStatus;
  counts: ApplicantCounts;
}

export interface JobCreatedRealtimeEvent {
  job: JobDto;
}

export interface CandidateProfileUpdatedRealtimeEvent {
  candidateId: string;
}

export interface RealtimeNotification {
  id: string;
  title: string;
  message: string;
  type: 'info' | 'success' | 'warning' | 'error';
  timestamp: string;
  actionUrl?: string;
}

