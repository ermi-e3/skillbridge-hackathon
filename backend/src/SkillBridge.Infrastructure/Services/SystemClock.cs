using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
