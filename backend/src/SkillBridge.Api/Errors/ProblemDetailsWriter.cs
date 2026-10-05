using Microsoft.AspNetCore.Mvc;

namespace SkillBridge.Api.Errors;

/// <summary>Writes the contract's error body: ProblemDetails + "code" + "traceId".</summary>
public static class ProblemDetailsWriter
{
    public static Task WriteAsync(HttpContext context, int status, string code, string title, string detail,
        IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (extensions is not null)
            foreach (var (key, value) in extensions)
                problem.Extensions[key] = value;

        return WriteAsync(context, problem);
    }

    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json");
    }
}
