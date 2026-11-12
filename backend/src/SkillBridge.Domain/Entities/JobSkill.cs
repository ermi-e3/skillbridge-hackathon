using SkillBridge.Domain.Enums;

namespace SkillBridge.Domain.Entities;

/// <summary>Join entity: a skill a job requires or prefers. Composite key (JobId, SkillId).</summary>
public class JobSkill
{
    private JobSkill() { } // EF

    public JobSkill(Job job, int skillId, bool isRequired = true, SkillProficiency minProficiency = SkillProficiency.Intermediate)
    {
        Job = job;
        SkillId = skillId;
        IsRequired = isRequired;
        MinProficiency = minProficiency;
    }

    public int JobId { get; private set; }
    public int SkillId { get; private set; }
    public bool IsRequired { get; private set; } = true;
    public SkillProficiency MinProficiency { get; private set; } = SkillProficiency.Intermediate;

    public Job Job { get; private set; } = null!;
    public Skill Skill { get; private set; } = null!;

    public void UpdateRequirement(bool isRequired, SkillProficiency minProficiency)
    {
        IsRequired = isRequired;
        MinProficiency = minProficiency;
    }
}
