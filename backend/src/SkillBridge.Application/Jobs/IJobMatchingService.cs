using SkillBridge.Domain.Entities;

namespace SkillBridge.Application.Jobs;

public interface IJobMatchingService
{
    JobMatchDto CalculateMatch(JobMatchCriteria jobCriteria, CandidateMatchProfile candidate);

    JobMatchDto CalculateMatch(Job job, CandidateProfile candidateProfile);
}

