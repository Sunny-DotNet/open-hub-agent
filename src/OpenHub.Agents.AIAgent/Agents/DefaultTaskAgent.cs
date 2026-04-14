using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenHub.Agents;

internal sealed class DefaultTaskAgent : TaskAgentBase
{
    private readonly AIAgent _agent;

    public DefaultTaskAgent(AIAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

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
        ChatClientAgentTaskSubscriber subscriber = new(taskId);
        _taskSubscribers[taskId] = subscriber;
        Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Pending, DateTime.UtcNow));

        Task execution = Task.Run(() => ExecuteTaskAsync(taskId, subscriber, request.Message, _disposeCancellationSource.Token));
        _taskExecutions[taskId] = execution;
        _ = execution.ContinueWith(
            _ => CleanupTask(taskId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return Task.FromResult(new CreateTaskResponse(taskId, subscriber));
    }

    private async Task ExecuteTaskAsync(
        Guid taskId,
        ChatClientAgentTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.InProgress, DateTime.UtcNow));

            await foreach (AgentResponseUpdate update in _agent.RunStreamingAsync(
                [new ChatMessage(ChatRole.User, message)],
                cancellationToken: cancellationToken).WithCancellation(cancellationToken))
            {
                subscriber.Update(update);
            }

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
            if (_agent is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_agent is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        finally
        {
            _disposeCancellationSource.Dispose();
            await base.DisposeAsync();
        }
    }
}
