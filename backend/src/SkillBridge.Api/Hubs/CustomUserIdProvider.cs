using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace SkillBridge.Api.Hubs;

public sealed class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value;
    }
}

