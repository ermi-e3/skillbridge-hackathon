using Microsoft.Extensions.DependencyInjection;

namespace SkillBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register one service per feature folder here, e.g. services.AddScoped<JobService>();
        return services;
    }
}
