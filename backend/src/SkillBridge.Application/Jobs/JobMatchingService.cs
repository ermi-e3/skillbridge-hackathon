using SkillBridge.Application.Skills;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Jobs;

public sealed class JobMatchingService : IJobMatchingService
{
    public JobMatchDto CalculateMatch(Job job, CandidateProfile candidateProfile)
    {
        if (candidateProfile == null || !candidateProfile.HasCompletedProfileSetup)
        {
            var reqSkills = job.RequiredSkills.Where(s => s.Skill != null && s.IsRequired).Select(s => new SkillDto(s.SkillId, s.Skill!.Name)).ToList();
            var prefSkills = job.RequiredSkills.Where(s => s.Skill != null && !s.IsRequired).Select(s => new SkillDto(s.SkillId, s.Skill!.Name)).ToList();
            return new JobMatchDto(
                0,
                new List<SkillDto>(),
                reqSkills,
                new List<SkillDto>(),
                prefSkills,
                new JobMatchBreakdownDto(0, 0, 0, 0, 0, 0, 0, "Profile Incomplete", "Profile Incomplete", "Profile Incomplete", "Profile Incomplete"));
        }

        var jobSkills = job.RequiredSkills
            .Where(js => js.Skill != null)
            .Select(js => new JobSkillRequirement(
                js.SkillId,
                js.Skill.Name,
                js.IsRequired,
                js.MinProficiency))
            .ToList();

        var jobCriteria = new JobMatchCriteria(
            job.Id,
            job.Title,
            job.Location,
            job.JobType,
            job.WorkArrangement,
            job.ExperienceLevel,
            job.MinExperienceYears,
            jobSkills);

        var candidateSkills = candidateProfile.Skills
            .Where(cs => cs.Skill != null)
            .Select(cs => new CandidateSkillProfile(
                cs.SkillId,
                cs.Skill.Name,
                cs.Proficiency,
                cs.YearsOfExperience,
                cs.IsPreferred))
            .ToList();

        var candidateExperience = candidateProfile.ExperienceHistory
            .Select(e =>
            {
                var end = e.IsCurrent || !e.EndDate.HasValue ? DateTime.UtcNow : e.EndDate.Value;
                var months = Math.Max(1, (int)((end - e.StartDate).TotalDays / 30.0));
                return new CandidateExperienceSummary(e.Title, e.Company, months);
            })
            .ToList();

        var candidateMatch = new CandidateMatchProfile(
            candidateProfile.UserId,
            candidateProfile.Location,
            candidateProfile.ExperienceLevel,
            candidateProfile.YearsOfExperience,
            candidateProfile.PreferredWorkArrangement,
            candidateProfile.PreferredJobType,
            candidateSkills,
            candidateExperience);

        return CalculateMatch(jobCriteria, candidateMatch);
    }

    public JobMatchDto CalculateMatch(JobMatchCriteria job, CandidateMatchProfile candidate)
    {
        var candidateSkillMap = candidate.Skills.ToDictionary(s => s.SkillId);

        var allJobSkills = job.Skills;
        var requiredJobSkills = allJobSkills.Where(s => s.IsRequired).ToList();
        var preferredJobSkills = allJobSkills.Where(s => !s.IsRequired).ToList();

        // If no skills are explicitly marked required, treat all as required
        if (requiredJobSkills.Count == 0 && allJobSkills.Count > 0)
        {
            requiredJobSkills = allJobSkills.ToList();
            preferredJobSkills.Clear();
        }

        var matchedRequired = new List<SkillDto>();
        var missingRequired = new List<SkillDto>();
        double requiredPointsSum = 0;

        foreach (var req in requiredJobSkills)
        {
            if (candidateSkillMap.TryGetValue(req.SkillId, out var candSkill))
            {
                matchedRequired.Add(new SkillDto(req.SkillId, req.SkillName));

                // Proficiency weighting
                double proficiencyMultiplier = 1.0;
                if ((int)candSkill.Proficiency >= (int)req.MinProficiency)
                {
                    proficiencyMultiplier = 1.0;
                }
                else if ((int)candSkill.Proficiency == (int)req.MinProficiency - 1)
                {
                    proficiencyMultiplier = 0.85;
                }
                else
                {
                    proficiencyMultiplier = 0.70;
                }

                if (candSkill.IsPreferred)
                {
                    proficiencyMultiplier *= 0.85;
                }

                requiredPointsSum += proficiencyMultiplier;
            }
            else
            {
                missingRequired.Add(new SkillDto(req.SkillId, req.SkillName));
            }
        }

        var hasPreferred = preferredJobSkills.Count > 0;
        var requiredWeight = hasPreferred ? 50.0 : 65.0;
        var requiredScoreRatio = requiredJobSkills.Count == 0 ? 0 : (requiredPointsSum / requiredJobSkills.Count);
        var requiredScore = (int)Math.Round(requiredScoreRatio * requiredWeight, MidpointRounding.AwayFromZero);

        if (matchedRequired.Count == 0 && requiredJobSkills.Count >= 2)
        {
            requiredScore = 0;
        }

        // 2. Preferred Skills (Weight: 15 points)
        var matchedPreferred = new List<SkillDto>();
        var missingPreferred = new List<SkillDto>();
        var preferredScore = 0;

        if (hasPreferred)
        {
            foreach (var pref in preferredJobSkills)
            {
                if (candidateSkillMap.ContainsKey(pref.SkillId))
                {
                    matchedPreferred.Add(new SkillDto(pref.SkillId, pref.SkillName));
                }
                else
                {
                    missingPreferred.Add(new SkillDto(pref.SkillId, pref.SkillName));
                }
            }

            var prefRatio = (double)matchedPreferred.Count / preferredJobSkills.Count;
            preferredScore = (int)Math.Round(prefRatio * 15.0, MidpointRounding.AwayFromZero);
        }

        // 3. Experience Compatibility (Weight: 15 points)
        var (expScore, expLabel) = EvaluateExperience(job, candidate);

        // 4. Work Arrangement Compatibility (Weight: 10 points)
        var (arrScore, arrLabel) = EvaluateWorkArrangement(job, candidate);

        // 5. Location Compatibility (Weight: 5 points)
        var (locScore, locLabel) = EvaluateLocation(job, candidate);

        // 6. Job Type Compatibility (Weight: 5 points)
        var (typeScore, typeLabel) = EvaluateJobType(job, candidate);

        var total = requiredScore + preferredScore + expScore + arrScore + locScore + typeScore;

        // Cap at 25% if 0 required skills matched out of >= 2 required
        if (matchedRequired.Count == 0 && requiredJobSkills.Count >= 2)
        {
            total = Math.Min(total, 25);
        }

        var finalPercent = Math.Clamp(total, 0, 100);

        var breakdown = new JobMatchBreakdownDto(
            requiredScore,
            preferredScore,
            expScore,
            arrScore,
            locScore,
            typeScore,
            finalPercent,
            expLabel,
            arrLabel,
            locLabel,
            typeLabel);

        return new JobMatchDto(
            finalPercent,
            matchedRequired.OrderBy(s => s.Name).ToList(),
            missingRequired.OrderBy(s => s.Name).ToList(),
            matchedPreferred.OrderBy(s => s.Name).ToList(),
            missingPreferred.OrderBy(s => s.Name).ToList(),
            breakdown);
    }

    private static (int Score, string Label) EvaluateExperience(JobMatchCriteria job, CandidateMatchProfile candidate)
    {
        if (!candidate.ExperienceLevel.HasValue)
        {
            return (0, "Experience Level Not Set");
        }

        var candYears = candidate.YearsOfExperience ?? 0;
        var meetsYears = candYears >= job.MinExperienceYears;
        var meetsLevel = (int)candidate.ExperienceLevel.Value >= (int)job.ExperienceLevel;

        if (meetsYears && meetsLevel)
        {
            var label = (int)candidate.ExperienceLevel.Value > (int)job.ExperienceLevel || candYears > job.MinExperienceYears + 2
                ? "Exceeds Requirements"
                : "Meets Requirements";
            return (15, label);
        }

        if (meetsYears || (int)candidate.ExperienceLevel.Value >= (int)job.ExperienceLevel - 1)
        {
            return (10, "Strong Growth Opportunity");
        }

        if ((int)candidate.ExperienceLevel.Value >= (int)job.ExperienceLevel - 2)
        {
            return (5, "Below Preferred Experience");
        }

        return (2, "Significant Experience Gap");
    }

    private static (int Score, string Label) EvaluateWorkArrangement(JobMatchCriteria job, CandidateMatchProfile candidate)
    {
        if (job.WorkArrangement == WorkArrangement.Remote)
        {
            return (10, "Remote (Full Compatibility)");
        }

        if (!candidate.PreferredWorkArrangement.HasValue)
        {
            return (0, "Work Arrangement Not Set");
        }

        var pref = candidate.PreferredWorkArrangement.Value;

        if (job.WorkArrangement == pref)
        {
            return (10, "Direct Arrangement Match");
        }

        if (job.WorkArrangement == WorkArrangement.Hybrid &&
            (pref == WorkArrangement.Remote || pref == WorkArrangement.OnSite))
        {
            return (7, "Hybrid Compatible");
        }

        if (job.WorkArrangement == WorkArrangement.OnSite && pref == WorkArrangement.Hybrid)
        {
            return (6, "Flexible");
        }

        if (job.WorkArrangement == WorkArrangement.OnSite && pref == WorkArrangement.Remote)
        {
            return (3, "Arrangement Mismatch (Requires On-Site)");
        }

        return (5, "Flexible");
    }

    private static (int Score, string Label) EvaluateLocation(JobMatchCriteria job, CandidateMatchProfile candidate)
    {
        if (job.WorkArrangement == WorkArrangement.Remote)
        {
            return (5, "Remote (Any Location)");
        }

        if (string.IsNullOrWhiteSpace(job.Location) || string.IsNullOrWhiteSpace(candidate.Location))
        {
            return (4, "Location Compatible");
        }

        var jobLoc = job.Location.Trim().ToLowerInvariant();
        var candLoc = candidate.Location.Trim().ToLowerInvariant();

        if (jobLoc.Contains(candLoc) || candLoc.Contains(jobLoc))
        {
            return (5, "Same Location");
        }

        var jobTokens = jobLoc.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var candTokens = candLoc.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);

        if (jobTokens.Intersect(candTokens).Any())
        {
            return (5, "Same Location");
        }

        return (1, "Different Location (May Require Relocation)");
    }

    private static (int Score, string Label) EvaluateJobType(JobMatchCriteria job, CandidateMatchProfile candidate)
    {
        if (!candidate.PreferredJobType.HasValue)
        {
            return (0, "Job Type Not Set");
        }

        var pref = candidate.PreferredJobType.Value;

        if (job.JobType == pref)
        {
            return (5, "Matches Preference");
        }

        if (job.JobType == JobType.Internship && candidate.ExperienceLevel == ExperienceLevel.EntryLevel)
        {
            return (5, "Ideal for Entry-Level");
        }

        if ((job.JobType == JobType.Contract && pref == JobType.Freelance) ||
            (job.JobType == JobType.Freelance && pref == JobType.Contract))
        {
            return (4, "Flexible Terms");
        }

        return (2, "Different Job Type");
    }
}

