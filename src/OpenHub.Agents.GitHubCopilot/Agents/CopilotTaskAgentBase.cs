using GitHub.Copilot.SDK;
using OpenHub.Agents.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenHub.Agents;

internal abstract class CopilotTaskAgentBase : TaskAgentBase
{
    public override Task<CreateTaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("A task message is required.", nameof(request));
        }

        Guid taskId = Guid.NewGuid();
        CopilotTaskSubscriber subscriber = new(taskId);
        _taskSubscribers[taskId] = subscriber;
        Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Pending, DateTime.UtcNow));

        Task execution = ScheduleTaskExecutionAsync(taskId, subscriber, request.Message, _disposeCancellationSource.Token);
        _taskExecutions[taskId] = execution;
        _ = execution.ContinueWith(
            _ => CleanupTask(taskId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return Task.FromResult(new CreateTaskResponse(taskId, subscriber));
    }

    protected virtual Task ScheduleTaskExecutionAsync(
        Guid taskId,
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
        => Task.Run(() => ExecuteTaskAsync(taskId, subscriber, message, cancellationToken));

    protected async Task ExecuteTaskAsync(
        Guid taskId,
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.InProgress, DateTime.UtcNow));

            await ExecuteCoreAsync(subscriber, message, cancellationToken);

            subscriber.Complete();
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Completed, DateTime.UtcNow));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            subscriber.Cancel();
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Cancelled, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            subscriber.Throw(ex);
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Failed, DateTime.UtcNow));
        }
    }

    protected abstract Task ExecuteCoreAsync(
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken);

    protected static async Task RunPromptAsync(
        ICopilotSessionConnection session,
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
    {
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = session.On(evt =>
        {
            try
            {
                switch (evt)
                {
                    case SessionErrorEvent sessionErrorEvent:
                        string errorMessage = string.IsNullOrWhiteSpace(sessionErrorEvent.Data.Message)
                            ? "The Copilot session reported an error."
                            : sessionErrorEvent.Data.Message;
                        completionSource.TrySetException(new InvalidOperationException(errorMessage));
                        break;

                    case SessionIdleEvent:
                        completionSource.TrySetResult();
                        break;

                    default:
                        subscriber.Update(evt);
                        break;
                }
            }
            catch (Exception ex)
            {
                completionSource.TrySetException(ex);
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = message }, cancellationToken);
        await completionSource.Task.WaitAsync(cancellationToken);
    }

    protected static async ValueTask DisposeOwnedResourceAsync(object? resource)
    {
        switch (resource)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;

            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
