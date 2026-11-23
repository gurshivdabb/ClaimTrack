using ClaimTrack.Api.Common;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Services;

// Runs inside the API for as long as it's up: checks for overdue claims once at startup, then on a
// timer (every 60 minutes by default). BackgroundService is .NET's base class for this, like a
// Spring @Scheduled method.
public class OverdueClaimsJob(
    IServiceScopeFactory scopeFactory,
    IOptions<OverdueClaimsOptions> options,
    TimeProvider timeProvider,
    ILogger<OverdueClaimsJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Overdue-claims check is disabled.");
            return;
        }

        // The timer uses TimeProvider, so tests can control time instead of waiting an hour.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.CheckIntervalMinutes), timeProvider);
        do
        {
            await RunOnceAsync(settings, stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(OverdueClaimsOptions settings, CancellationToken stoppingToken)
    {
        try
        {
            // This job lives as long as the app, but AppDbContext is created per request (scoped).
            // A fresh scope per run gives each check its own DbContext, disposed afterwards.
            using var scope = scopeFactory.CreateScope();
            var flagged = await scope.ServiceProvider.GetRequiredService<IOverdueClaimsService>()
                .FlagOverdueClaimsAsync(stoppingToken);

            if (flagged > 0)
            {
                // A Warning, so alerting tools watching the logs can notify someone.
                logger.LogWarning(
                    "Flagged {FlaggedCount} claims as overdue (UnderReview for {ThresholdDays}+ days).",
                    flagged, settings.ThresholdDays);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw; // the API is shutting down
        }
        catch (Exception exception)
        {
            // Never let one failed run take the API down: since .NET 8 an unhandled exception in a
            // BackgroundService stops the whole app. Log it and try again at the next tick.
            logger.LogError(exception, "Checking for overdue claims failed; will retry at the next run.");
        }
    }
}
