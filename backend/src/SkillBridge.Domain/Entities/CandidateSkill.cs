using SkillBridge.Domain.Enums;

namespace SkillBridge.Domain.Entities;

/// <summary>Join entity: a skill a candidate has, including proficiency, experience, and whether it is a preferred / nice-to-have skill. Composite key (CandidateId, SkillId).</summary>
public class CandidateSkill
{
    private CandidateSkill() { } // EF

    public CandidateSkill(
        string candidateId,
        int skillId,
        SkillProficiency proficiency = SkillProficiency.Intermediate,
        int yearsOfExperience = 1,
        bool isPreferred = false)
    {
        CandidateId = candidateId;
        SkillId = skillId;
        Proficiency = proficiency;
        YearsOfExperience = yearsOfExperience > 0 ? yearsOfExperience : 1;
        IsPreferred = isPreferred;
    }

    public string CandidateId { get; private set; } = null!;
    public int SkillId { get; private set; }
    public SkillProficiency Proficiency { get; private set; } = SkillProficiency.Intermediate;
    public int YearsOfExperience { get; private set; } = 1;
    public bool IsPreferred { get; private set; } = false;

    public CandidateProfile Candidate { get; private set; } = null!;
    public Skill Skill { get; private set; } = null!;

    public void Update(SkillProficiency proficiency, int yearsOfExperience, bool? isPreferred = null)
    {
        Proficiency = proficiency;
        YearsOfExperience = yearsOfExperience > 0 ? yearsOfExperience : 1;
        if (isPreferred.HasValue)
        {
            IsPreferred = isPreferred.Value;
        }
    }

    public void SetPreferred(bool isPreferred)
    {
        IsPreferred = isPreferred;
    }
}
