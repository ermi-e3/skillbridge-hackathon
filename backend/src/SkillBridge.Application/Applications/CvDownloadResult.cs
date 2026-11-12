namespace SkillBridge.Application.Applications;

public sealed record CvDownloadResult(
    Stream Stream,
    string ContentType,
    string FileName);

