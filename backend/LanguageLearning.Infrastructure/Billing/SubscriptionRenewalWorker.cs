using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LanguageLearning.Infrastructure.Billing;

public sealed class SubscriptionRenewalWorker(IServiceScopeFactory scopes, ILogger<SubscriptionRenewalWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SubscriptionRenewalProcessor>().ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                // Transaction rollback permits safe recovery. Do not log card/account data or connection details.
                logger.LogWarning("Simulated renewal processing temporarily unavailable ({ExceptionType}).", exception.GetType().Name);
            }
        }
    }
}
