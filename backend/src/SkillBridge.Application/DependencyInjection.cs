using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Application.Auth;
using SkillBridge.Application.CandidateProfiles;
using SkillBridge.Application.Skills;

namespace SkillBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ICandidateProfileService, CandidateProfileService>();
        services.AddScoped<ISkillCatalogService, SkillCatalogService>();
        services.AddScoped<Jobs.JobsService>();
        services.AddScoped<Applications.ApplicationsService>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        return services;
    }
}
