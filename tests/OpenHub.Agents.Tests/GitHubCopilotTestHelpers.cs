using GitHub.Copilot.SDK;
using OpenHub.Agents.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentTaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenHub.Agents.Tests;

internal static class GitHubCopilotTestEvents
{
    public static AssistantReasoningDeltaEvent CreateReasoningDeltaEvent(string reasoningId, string deltaContent)
        => new()
        {
            Data = new AssistantReasoningDeltaData
            {
                ReasoningId = reasoningId,
                DeltaContent = deltaContent,
            },
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        };

    public static AssistantMessageDeltaEvent CreateMessageDeltaEvent(string messageId, string deltaContent)
        => new()
        {
            Data = new AssistantMessageDeltaData
            {
                MessageId = messageId,
                DeltaContent = deltaContent,
            },
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        };

    public static ToolExecutionStartEvent CreateToolExecutionStartEvent(string toolCallId, string toolName, object? arguments = null)
        => new()
        {
            Data = new ToolExecutionStartData
            {
                ToolCallId = toolCallId,
                ToolName = toolName,
                Arguments = arguments,
            },
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        };

    public static ToolExecutionCompleteEvent CreateToolExecutionCompleteEvent(string toolCallId, string content)
        => new()
        {
            Data = new ToolExecutionCompleteData
            {
                ToolCallId = toolCallId,
                Success = true,
                Result = new ToolExecutionCompleteDataResult
                {
                    Content = content,
                },
            },
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        };

    public static AssistantUsageEvent CreateUsageEvent(long inputTokens, long outputTokens, long cacheReadTokens)
        => new()
        {
            Data = new AssistantUsageData
            {
                Model = "gpt-4",
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CacheReadTokens = cacheReadTokens,
            },
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        };

    public static SessionIdleEvent CreateIdleEvent()
        => new()
        {
            Data = new SessionIdleData(),
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        };

    public static SessionErrorEvent CreateErrorEvent(string? message)
        => new()
        {
            Data = new SessionErrorData
            {
                ErrorType = "session_error",
                Message = message ?? string.Empty,
            },
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        };
}

internal static class ObservableReplay
{
    public static IReadOnlyList<T> Read<T>(IObservable<T> source)
    {
        List<T> values = [];

        using IDisposable subscription = source.Subscribe(values.Add);

        return values;
    }
}

internal sealed class TaskStatusRecorder : IDisposable
{
    private readonly ConcurrentQueue<AgentTaskStatus> _statuses = new();
    private readonly TaskCompletionSource _terminalStatusObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IDisposable _subscription;

    public TaskStatusRecorder(IAgentSubscriber subscriber, Guid taskId, AgentTaskStatus terminalStatus)
    {
        _subscription = subscriber.TaskStatusChanged.Subscribe(@event =>
        {
            if (@event.TaskId != taskId)
            {
                return;
            }

            _statuses.Enqueue(@event.Status);
            if (@event.Status == terminalStatus)
            {
                _terminalStatusObserved.TrySetResult();
            }
        });
    }

    public IReadOnlyList<AgentTaskStatus> Statuses => [.. _statuses];

    public Task WaitForTerminalStatusAsync()
        => _terminalStatusObserved.Task;

    public void Dispose()
        => _subscription.Dispose();
}

internal sealed class FakeCopilotSessionConnection(
    Func<FakeCopilotSessionConnection, MessageOptions, CancellationToken, Task> sendAsync) : ICopilotSessionConnection
{
    private readonly object _syncRoot = new();
    private readonly List<SessionEventHandler> _handlers = [];
    private readonly ConcurrentQueue<string?> _sentPrompts = new();
    private int _activeSends;
    private int _maxConcurrentSends;
    private int _sendCount;

    public int SendCount => Volatile.Read(ref _sendCount);

    public int MaxConcurrentSends => Volatile.Read(ref _maxConcurrentSends);

    public IReadOnlyList<string?> SentPrompts => [.. _sentPrompts];

    public bool Disposed { get; private set; }

    public IDisposable On(SessionEventHandler handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (_syncRoot)
        {
            _handlers.Add(handler);
        }

        return new Subscription(this, handler);
    }

    public async Task SendAsync(MessageOptions options, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _sendCount);
        _sentPrompts.Enqueue(options.Prompt);

        int activeSends = Interlocked.Increment(ref _activeSends);
        UpdateMaxConcurrentSends(activeSends);

        try
        {
            await sendAsync(this, options, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _activeSends);
        }
    }

    public void Publish(SessionEvent @event)
    {
        SessionEventHandler[] handlers;

        lock (_syncRoot)
        {
            handlers = [.. _handlers];
        }

        foreach (SessionEventHandler handler in handlers)
        {
            handler(@event);
        }
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private void RemoveHandler(SessionEventHandler handler)
    {
        lock (_syncRoot)
        {
            _handlers.Remove(handler);
        }
    }

    private void UpdateMaxConcurrentSends(int candidate)
    {
        while (true)
        {
            int currentMax = Volatile.Read(ref _maxConcurrentSends);
            if (candidate <= currentMax)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _maxConcurrentSends, candidate, currentMax) == currentMax)
            {
                return;
            }
        }
    }

    private sealed class Subscription(FakeCopilotSessionConnection owner, SessionEventHandler handler) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.RemoveHandler(handler);
        }
    }
}

internal sealed class TestAsyncDisposable : IAsyncDisposable
{
    public bool Disposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
