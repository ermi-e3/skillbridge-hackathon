using SkillBridge.Domain.Common;

namespace SkillBridge.Application.Common.Exceptions;

// BusinessRuleException (400) lives in Domain because entities throw it.

public sealed class NotFoundException(string code, string title, string detail)
    : AppException(code, title, detail, 404);

public sealed class ForbiddenException(string code, string title, string detail)
    : AppException(code, title, detail, 403);

public sealed class UnauthorizedException(string code, string title, string detail)
    : AppException(code, title, detail, 401);
