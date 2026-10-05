export interface SkillDto {
  id: number;
  name: string;
}

export interface CandidateProfileDto {
  userId?: string;
  fullName?: string;
  email?: string;
  headline: string;
  bio?: string | null;
  gitHubUrl?: string | null;
  skills?: SkillDto[];
  skillIds?: number[];
  updatedAt?: string;
}

export interface UpdateCandidateProfileRequest {
  headline: string;
  bio?: string | null;
  gitHubUrl?: string | null;
  skillIds: number[];
}
