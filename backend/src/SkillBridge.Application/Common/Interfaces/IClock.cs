namespace SkillBridge.Application.Common.Interfaces;

/// <summary>Always UTC. Npgsql rejects non-UTC DateTime values for timestamptz columns.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
