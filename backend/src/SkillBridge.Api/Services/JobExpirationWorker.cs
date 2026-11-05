using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Api.Services;

public sealed class JobExpirationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<JobExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow startup and database migrations to complete before first check
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndCloseExpiredJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                try
                {
                    logger.LogError(ex, "Error occurred while auto-closing expired jobs.");
                }
                catch
                {
                    // Suppress any secondary logger provider issues
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckAndCloseExpiredJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var notifier = scope.ServiceProvider.GetService<IRealtimeNotifier>();

        var expiredJobs = await dbContext.Jobs
            .Where(j => j.IsOpen && j.Deadline != null && j.Deadline <= clock.UtcNow)
            .ToListAsync(stoppingToken);

        if (expiredJobs.Count > 0)
        {
            foreach (var job in expiredJobs)
            {
                job.Close();
            }

            await dbContext.SaveChangesAsync(stoppingToken);

            if (notifier != null)
            {
                foreach (var job in expiredJobs)
                {
                    await notifier.NotifyJobStatusChangedAsync(
                        new JobStatusChangedRealtimeEvent(job.Id, false, job.Deadline, null),
                        stoppingToken);
                }
            }
        }
    }
}

