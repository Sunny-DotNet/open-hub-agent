using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenHub.Agents;


public interface ITaskAgent : IAsyncDisposable
{
    IAgentSubscriber Subscriber { get; }
    ITaskSubscriber? GetTaskSubscriber(Guid taskId);
    Task<CreateTaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<CreateTaskResponse> CreateTaskAsync(CreateTaskRequest request, IReadOnlyList<TaskHistoryMessage> history, CancellationToken cancellationToken = default);
}
public abstract class TaskAgentBase : ITaskAgent
{
    protected readonly ConcurrentDictionary<Guid, ITaskSubscriber> _taskSubscribers = new();
    protected readonly ConcurrentDictionary<Guid, Task> _taskExecutions = new();
    protected readonly AgentSubscriber _agentSubscriber = new();
    protected readonly CancellationTokenSource _disposeCancellationSource = new();
    private volatile int _disposed;

    public virtual IAgentSubscriber Subscriber => _agentSubscriber;
    protected IAgentPublisher Publisher => _agentSubscriber;

    protected TaskAgentBase()
    {
    }

    public virtual Task<CreateTaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
        => CreateTaskAsync(request, Array.Empty<TaskHistoryMessage>(), cancellationToken);

    public abstract Task<CreateTaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        IReadOnlyList<TaskHistoryMessage> history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Base disposal: clears subscribers and completes the agent subscriber.
    /// Derived classes should cancel/await tasks and dispose owned resources before calling base.
    /// </summary>
    public virtual ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return default;
        }

        _taskSubscribers.Clear();
        _agentSubscriber.Complete();
        return default;
    }

    public virtual ITaskSubscriber? GetTaskSubscriber(Guid taskId)
        => _taskSubscribers.TryGetValue(taskId, out var subscriber) ? subscriber : null;


    protected void CleanupTask(Guid taskId)
    {
        _taskExecutions.TryRemove(taskId, out _);
        _taskSubscribers.TryRemove(taskId, out _);
    }

    protected static string ValidateTaskMessage(CreateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("A task message is required.", nameof(request));
        }

        return request.Message;
    }

    protected static TaskHistoryMessage[] ValidateTaskHistory(IReadOnlyList<TaskHistoryMessage> history)
    {
        if (history is null)
        {
            throw new ArgumentNullException(nameof(history));
        }

        TaskHistoryMessage[] validatedHistory = [.. history];
        for (int i = 0; i < validatedHistory.Length; i++)
        {
            TaskHistoryMessage historyMessage = validatedHistory[i];
            if (string.IsNullOrWhiteSpace(historyMessage.Content))
            {
                throw new ArgumentException($"History message at index {i} requires content.", nameof(history));
            }

            if (string.IsNullOrWhiteSpace(historyMessage.Role.Value))
            {
                throw new ArgumentException($"History message at index {i} requires a role.", nameof(history));
            }

        }

        return validatedHistory;
    }

    protected void ThrowIfDisposed()
    {
        if (_disposed != 0 || _disposeCancellationSource.IsCancellationRequested)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
