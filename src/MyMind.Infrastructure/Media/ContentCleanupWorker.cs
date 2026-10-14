using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyMind.Application.Features.Media;

namespace MyMind.Infrastructure.Media;

/// <summary>Runs <see cref="ContentCleanupService"/> once an hour; <c>Cleanup:Enabled=false</c> turns it off (tests).</summary>
internal sealed class ContentCleanupWorker(
    IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ContentCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Cleanup:Enabled", defaultValue: true))
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ContentCleanupService>().RunAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Content cleanup failed; retrying in {Interval}", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
