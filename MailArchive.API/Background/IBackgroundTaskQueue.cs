namespace MailArchive.API.Background;

public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(
        Func<IServiceProvider, CancellationToken, ValueTask> workItem);

    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken);
}