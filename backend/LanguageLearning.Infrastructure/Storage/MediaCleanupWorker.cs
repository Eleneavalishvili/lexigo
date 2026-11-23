using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanguageLearning.Infrastructure.Storage;

public sealed class MediaCleanupWorker(IServiceScopeFactory scopes, IOptions<ObjectStorageOptions> options,
    ILogger<MediaCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !options.Value.CleanupEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MediaCleanupService>().ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                // SDK errors may contain signed requests; never log the exception body.
                logger.LogWarning("Private audio cleanup will retry ({ErrorType}).", error.GetType().Name);
            }
        }
    }
}
