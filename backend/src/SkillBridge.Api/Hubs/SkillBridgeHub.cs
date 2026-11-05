using Microsoft.AspNetCore.SignalR;
using SkillBridge.Domain.Common;

namespace SkillBridge.Api.Hubs;

public sealed class SkillBridgeHub : Hub
{
    public const string HubUrl = "/hubs/app";
    public const string CandidatesGroup = "Candidates";
    public const string EmployersGroup = "Employers";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            if (user.IsInRole(Roles.Candidate))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, CandidatesGroup);
            }

            if (user.IsInRole(Roles.Employer))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, EmployersGroup);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinJobRoom(int jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
    }

    public async Task LeaveJobRoom(int jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
    }
}

