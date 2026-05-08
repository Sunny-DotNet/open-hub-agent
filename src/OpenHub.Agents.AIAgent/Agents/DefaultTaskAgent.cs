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
        IReadOnlyList<TaskHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string message = ValidateTaskMessage(request);
        TaskHistoryMessage[] validatedHistory = ValidateTaskHistory(history);
        ChatMessage[] messages = CreateChatMessages(message, validatedHistory);

        Guid taskId = Guid.NewGuid();
        ChatClientAgentTaskSubscriber subscriber = new(taskId);
        _taskSubscribers[taskId] = subscriber;
        Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Pending, DateTime.UtcNow));

        Task execution = Task.Run(() => ExecuteTaskAsync(taskId, subscriber, messages, _disposeCancellationSource.Token));
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
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        try
        {
            Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.InProgress, DateTime.UtcNow));

            await foreach (AgentResponseUpdate update in _agent.RunStreamingAsync(
                messages,
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

    private static ChatMessage[] CreateChatMessages(string message, IReadOnlyList<TaskHistoryMessage> history)
    {
        ChatMessage[] chatMessages = new ChatMessage[history.Count + 1];
        for (int i = 0; i < history.Count; i++)
        {
            TaskHistoryMessage historyMessage = history[i];
            chatMessages[i] = new ChatMessage(historyMessage.Role, historyMessage.Content);
        }

        chatMessages[^1] = new ChatMessage(ChatRole.User, message);
        return chatMessages;
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
