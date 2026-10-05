namespace SkillBridge.Domain.Entities;

/// <summary>Join entity: a skill a job requires. Composite key (JobId, SkillId).</summary>
public class JobSkill
{
    private JobSkill() { } // EF

    public JobSkill(Job job, int skillId)
    {
        Job = job;
        SkillId = skillId;
    }

    public int JobId { get; private set; }
    public int SkillId { get; private set; }

    public Job Job { get; private set; } = null!;
    public Skill Skill { get; private set; } = null!;
}
