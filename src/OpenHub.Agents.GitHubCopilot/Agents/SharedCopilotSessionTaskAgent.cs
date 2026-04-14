using OpenHub.Agents.Models;
using System;
using System.Threading.Tasks;
using TaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenHub.Agents;

internal sealed class SharedCopilotSessionTaskAgent(ICopilotSessionConnection session, bool ownsSession) : CopilotTaskAgentBase
{
    private readonly object _executionSyncRoot = new();
    private Task _executionQueueTail = Task.CompletedTask;

    protected override Task ScheduleTaskExecutionAsync(
        Guid taskId,
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
    {
        Task predecessor;
        TaskCompletionSource executionCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task execution;

        lock (_executionSyncRoot)
        {
            predecessor = _executionQueueTail;
            execution = Task.Run(() => ExecuteQueuedTaskAsync(predecessor, executionCompleted, taskId, subscriber, message, cancellationToken));
            _executionQueueTail = executionCompleted.Task;
        }

        return execution;
    }

    private async Task ExecuteQueuedTaskAsync(
        Task predecessor,
        TaskCompletionSource executionCompleted,
        Guid taskId,
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await predecessor.WaitAsync(cancellationToken);
            await ExecuteTaskAsync(taskId, subscriber, message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            subscriber.Cancel();
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Cancelled, DateTime.UtcNow));
        }
        finally
        {
            executionCompleted.TrySetResult();
        }
    }

    protected override Task ExecuteCoreAsync(
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
        => RunPromptAsync(session, subscriber, message, cancellationToken);

    public override async ValueTask DisposeAsync()
    {
        if (_disposeCancellationSource.IsCancellationRequested)
        {
            return;
        }

        _disposeCancellationSource.Cancel();

        Task[] runningTasks = [.. _taskExecutions.Values];
        if (runningTasks.Length > 0)
        {
            await Task.WhenAll(runningTasks);
        }

        try
        {
            if (ownsSession)
            {
                await DisposeOwnedResourceAsync(session);
            }
        }
        finally
        {
            _disposeCancellationSource.Dispose();
            await base.DisposeAsync();
        }
    }
}
