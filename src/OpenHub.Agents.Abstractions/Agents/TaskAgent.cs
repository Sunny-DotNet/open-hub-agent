using OpenHub.Agents.Models;
using System;
using System.Collections.Concurrent;

namespace OpenHub.Agents;


public interface ITaskAgent : IAsyncDisposable
{
    IAgentSubscriber Subscriber { get; }
    ITaskSubscriber? GetTaskSubscriber(Guid taskId);
    Task<CreateTaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
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

    public abstract Task<CreateTaskResponse> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

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
    protected void ThrowIfDisposed()
    {
        if (_disposed != 0 || _disposeCancellationSource.IsCancellationRequested)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
