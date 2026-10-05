namespace SkillBridge.Domain.Entities;

/// <summary>Join entity: a skill a candidate has. Composite key (CandidateId, SkillId).</summary>
public class CandidateSkill
{
    private CandidateSkill() { } // EF

    public CandidateSkill(string candidateId, int skillId)
    {
        CandidateId = candidateId;
        SkillId = skillId;
    }

    public string CandidateId { get; private set; } = null!;
    public int SkillId { get; private set; }

    public CandidateProfile Candidate { get; private set; } = null!;
    public Skill Skill { get; private set; } = null!;
}
