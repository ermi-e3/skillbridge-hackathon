namespace SkillBridge.Application.Common.Interfaces;

public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task NotifyApplicationSubmittedAsync(ApplicationSubmittedRealtimeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyApplicationWithdrawnAsync(ApplicationWithdrawnRealtimeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyApplicationStatusChangedAsync(ApplicationStatusChangedRealtimeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyJobCreatedAsync(JobCreatedRealtimeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyJobStatusChangedAsync(JobStatusChangedRealtimeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyCandidateProfileUpdatedAsync(CandidateProfileUpdatedRealtimeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendNotificationAsync(string userId, NotificationRealtimeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

