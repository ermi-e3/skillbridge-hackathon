using SkillBridge.Application.Applications;
using SkillBridge.Application.Jobs;

namespace SkillBridge.Application.Common.Interfaces;

public sealed record ApplicationSubmittedRealtimeEvent(
    int ApplicationId,
    int JobId,
    string JobTitle,
    string EmployerId,
    string CandidateId,
    string CandidateName,
    int MatchPercent,
    JobApplicantItemDto Applicant,
    ApplicantCountsDto Counts);

public sealed record ApplicationWithdrawnRealtimeEvent(
    int ApplicationId,
    int JobId,
    string JobTitle,
    string EmployerId,
    string CandidateId,
    ApplicantCountsDto Counts);

public sealed record ApplicationStatusChangedRealtimeEvent(
    int ApplicationId,
    int JobId,
    string JobTitle,
    string EmployerId,
    string CandidateId,
    string NewStatus,
    ApplicantCountsDto Counts);

public sealed record JobCreatedRealtimeEvent(
    JobDto Job);

public sealed record JobUpdatedRealtimeEvent(
    JobDto Job);

public sealed record JobStatusChangedRealtimeEvent(
    int JobId,
    bool IsOpen,
    DateTime? Deadline,
    JobDto? Job);

public sealed record CandidateProfileUpdatedRealtimeEvent(
    string CandidateId);

public sealed record NotificationRealtimeEvent(
    string Id,
    string Title,
    string Message,
    string Type,
    DateTime Timestamp,
    string? ActionUrl = null);

public interface IRealtimeNotifier
{
    Task NotifyApplicationSubmittedAsync(ApplicationSubmittedRealtimeEvent @event, CancellationToken cancellationToken = default);
    Task NotifyApplicationWithdrawnAsync(ApplicationWithdrawnRealtimeEvent @event, CancellationToken cancellationToken = default);
    Task NotifyApplicationStatusChangedAsync(ApplicationStatusChangedRealtimeEvent @event, CancellationToken cancellationToken = default);
    Task NotifyJobCreatedAsync(JobCreatedRealtimeEvent @event, CancellationToken cancellationToken = default);
    Task NotifyJobUpdatedAsync(JobUpdatedRealtimeEvent @event, CancellationToken cancellationToken = default);
    Task NotifyJobStatusChangedAsync(JobStatusChangedRealtimeEvent @event, CancellationToken cancellationToken = default);
    Task NotifyCandidateProfileUpdatedAsync(CandidateProfileUpdatedRealtimeEvent @event, CancellationToken cancellationToken = default);
    Task SendNotificationAsync(string userId, NotificationRealtimeEvent @event, CancellationToken cancellationToken = default);
}

