using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LanguageLearning.Infrastructure.Classrooms;

public sealed class AssignmentDeadlineWorker(IServiceScopeFactory scopes, ILogger<AssignmentDeadlineWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AssignmentDeadlineNotices>().SendBatchAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Assignment deadline notices could not be generated ({ExceptionType}).",
                    exception.GetType().Name);
            }
        }
    }
}
