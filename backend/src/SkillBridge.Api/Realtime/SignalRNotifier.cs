using Microsoft.AspNetCore.SignalR;
using SkillBridge.Api.Hubs;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Api.Realtime;

public sealed class SignalRNotifier(IHubContext<SkillBridgeHub> hubContext) : IRealtimeNotifier
{
    public async Task NotifyApplicationSubmittedAsync(
        ApplicationSubmittedRealtimeEvent @event,
        CancellationToken cancellationToken = default)
    {
        // Direct to employer user
        await hubContext.Clients.User(@event.EmployerId).SendAsync("ApplicationSubmitted", @event, cancellationToken);

        // Direct to candidate user (for other tabs/devices)
        await hubContext.Clients.User(@event.CandidateId).SendAsync("ApplicationSubmitted", @event, cancellationToken);

        // Job room subscribers (e.g. employer viewing applicants page)
        await hubContext.Clients.Group($"job-{@event.JobId}").SendAsync("ApplicationSubmitted", @event, cancellationToken);

        // Push toast notification to the employer
        await hubContext.Clients.User(@event.EmployerId).SendAsync("NotificationReceived", new NotificationRealtimeEvent(
            Guid.NewGuid().ToString(),
            "New Application Received",
            $"{@event.CandidateName} applied for '{@event.JobTitle}' (Match: {@event.MatchPercent}%)",
            "info",
            DateTime.UtcNow,
            $"/employer/jobs/{@event.JobId}/applicants"
        ), cancellationToken);
    }

    public async Task NotifyApplicationWithdrawnAsync(
        ApplicationWithdrawnRealtimeEvent @event,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.User(@event.EmployerId).SendAsync("ApplicationWithdrawn", @event, cancellationToken);
        await hubContext.Clients.User(@event.CandidateId).SendAsync("ApplicationWithdrawn", @event, cancellationToken);
        await hubContext.Clients.Group($"job-{@event.JobId}").SendAsync("ApplicationWithdrawn", @event, cancellationToken);

        // Push toast notification to the employer
        await hubContext.Clients.User(@event.EmployerId).SendAsync("NotificationReceived", new NotificationRealtimeEvent(
            Guid.NewGuid().ToString(),
            "Application Withdrawn",
            $"An application for '{@event.JobTitle}' was withdrawn.",
            "warning",
            DateTime.UtcNow,
            $"/employer/jobs/{@event.JobId}/applicants"
        ), cancellationToken);
    }

    public async Task NotifyApplicationStatusChangedAsync(
        ApplicationStatusChangedRealtimeEvent @event,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.User(@event.CandidateId).SendAsync("ApplicationStatusChanged", @event, cancellationToken);
        await hubContext.Clients.User(@event.EmployerId).SendAsync("ApplicationStatusChanged", @event, cancellationToken);
        await hubContext.Clients.Group($"job-{@event.JobId}").SendAsync("ApplicationStatusChanged", @event, cancellationToken);

        var isShortlisted = string.Equals(@event.NewStatus, "Shortlisted", StringComparison.OrdinalIgnoreCase);
        var title = isShortlisted ? "Application Shortlisted!" : "Application Status Update";
        var type = isShortlisted ? "success" : "info";
        var message = isShortlisted
            ? $"Congratulations! You've been shortlisted for '{@event.JobTitle}'."
            : $"Your application status for '{@event.JobTitle}' was updated to {@event.NewStatus}.";

        await hubContext.Clients.User(@event.CandidateId).SendAsync("NotificationReceived", new NotificationRealtimeEvent(
            Guid.NewGuid().ToString(),
            title,
            message,
            type,
            DateTime.UtcNow,
            $"/jobs/{@event.JobId}"
        ), cancellationToken);
    }

    public async Task NotifyJobCreatedAsync(
        JobCreatedRealtimeEvent @event,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.All.SendAsync("JobCreated", @event, cancellationToken);
    }

    public async Task NotifyJobStatusChangedAsync(
        JobStatusChangedRealtimeEvent @event,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.All.SendAsync("JobStatusChanged", @event, cancellationToken);
    }

    public async Task NotifyCandidateProfileUpdatedAsync(
        CandidateProfileUpdatedRealtimeEvent @event,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.User(@event.CandidateId).SendAsync("CandidateProfileUpdated", @event, cancellationToken);
    }

    public async Task SendNotificationAsync(
        string userId,
        NotificationRealtimeEvent @event,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.User(userId).SendAsync("NotificationReceived", @event, cancellationToken);
    }
}

