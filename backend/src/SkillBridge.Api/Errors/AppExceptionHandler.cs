using Microsoft.AspNetCore.Diagnostics;
using SkillBridge.Domain.Common;

namespace SkillBridge.Api.Errors;

/// <summary>Turns every exception into ProblemDetails + "code". Controllers never catch.</summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is AppException app)
        {
            await ProblemDetailsWriter.WriteAsync(context, app.Status, app.Code, app.Title, app.Message);
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        await ProblemDetailsWriter.WriteAsync(context, StatusCodes.Status500InternalServerError, "server.error",
            "Something went wrong", "An unexpected error occurred. Please try again.");
        return true;
    }
}
