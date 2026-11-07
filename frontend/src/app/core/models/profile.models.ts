export interface SkillDto {
  id: number;
  name: string;
  isPreferred?: boolean;
}

export interface CandidateExperienceDto {
  id?: number;
  title: string;
  company: string;
  location?: string | null;
  startDate: string;
  endDate?: string | null;
  isCurrent: boolean;
  description?: string | null;
}

export interface CandidateSkillDetailDto {
  skillId: number;
  proficiency: string;
  yearsOfExperience: number;
  isPreferred?: boolean;
}

export interface CandidateProfileDto {
  userId?: string;
  fullName?: string;
  email?: string;
  headline: string;
  bio?: string | null;
  gitHubUrl?: string | null;
  location?: string | null;
  experienceLevel?: string;
  yearsOfExperience?: number;
  preferredWorkArrangement?: string;
  preferredJobType?: string;
  skills?: SkillDto[];
  preferredSkills?: SkillDto[];
  skillIds?: number[];
  preferredSkillIds?: number[];
  skillDetails?: CandidateSkillDetailDto[];
  experienceHistory?: CandidateExperienceDto[];
  updatedAt?: string;
  hasDefaultCv?: boolean;
  defaultCvOriginalFileName?: string | null;
  defaultCvFileSizeBytes?: number | null;
  defaultCvUploadedAt?: string | null;
}

export interface UpdateCandidateProfileRequest {
  headline: string;
  bio?: string | null;
  gitHubUrl?: string | null;
  location?: string | null;
  experienceLevel?: string;
  yearsOfExperience?: number;
  preferredWorkArrangement?: string;
  preferredJobType?: string;
  skillIds: number[];
  preferredSkillIds?: number[];
  skillDetails?: CandidateSkillDetailDto[];
  experienceHistory?: CandidateExperienceDto[];
}
