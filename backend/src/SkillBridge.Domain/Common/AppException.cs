namespace SkillBridge.Domain.Common;

/// <summary>Base for every expected failure. The Api's exception handler turns it into ProblemDetails.</summary>
public abstract class AppException(string code, string title, string detail, int status) : Exception(detail)
{
    public string Code { get; } = code;
    public string Title { get; } = title;
    public int Status { get; } = status;
}

/// <summary>A business rule was broken (thrown by entities and services). Always 400.</summary>
public sealed class BusinessRuleException(string code, string title, string detail)
    : AppException(code, title, detail, 400);
