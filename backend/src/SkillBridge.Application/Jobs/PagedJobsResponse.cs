namespace SkillBridge.Application.Jobs;

public sealed record PagedJobsResponse(
    IReadOnlyList<JobDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
