using SkillBridge.Application.Jobs;

namespace SkillBridge.Application.Applications;

public sealed record ApplicationDto(
    int Id,
    int JobId,
    string JobTitle,
    string? CompanyName,
    string Status,
    DateTime AppliedAt,
    DateTime? StatusChangedAt,
    string? CoverNote,
    int MatchPercent,
    JobMatchDto? Match,
    bool HasCv = false,
    string? CvOriginalFileName = null,
    long? CvFileSizeBytes = null,
    DateTime? CvUploadedAt = null,
    string? CvDownloadUrl = null);
