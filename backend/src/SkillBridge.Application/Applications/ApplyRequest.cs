namespace SkillBridge.Application.Applications;

public sealed record ApplyRequest(string? CoverNote = null, bool UseDefaultCv = false);
