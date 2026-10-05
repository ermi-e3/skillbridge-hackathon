using SkillBridge.Domain.Common;

namespace SkillBridge.Application.Common.Exceptions;

public sealed class RequestValidationException(Dictionary<string, string[]> errors)
    : AppException("validation.failed", "Invalid request", "One or more fields are invalid.", 400)
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
