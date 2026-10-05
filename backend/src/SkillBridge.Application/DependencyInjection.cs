using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Application.Auth;

namespace SkillBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        return services;
    }
}
