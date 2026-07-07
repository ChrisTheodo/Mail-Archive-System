namespace MailArchive.API.Background;

public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(
        IBackgroundTaskQueue taskQueue,
        IServiceProvider serviceProvider,
        ILogger<QueuedHostedService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background import queue service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, ValueTask> workItem;

            try
            {
                workItem = await _taskQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await workItem(_serviceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing a background queued task.");
            }
        }

        _logger.LogInformation("Background import queue service stopped.");
    }
}