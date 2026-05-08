using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;
using TaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenHub.Agents;

internal abstract class CopilotTaskAgentBase : TaskAgentBase
{
    public override Task<CreateTaskResponse> CreateTaskAsync(
        CreateTaskRequest request,
        IReadOnlyList<TaskHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        string message = ValidateTaskMessage(request);
        TaskHistoryMessage[] validatedHistory = ValidateTaskHistory(history);
        string prompt = BuildCopilotPrompt(message, validatedHistory);

        Guid taskId = Guid.NewGuid();
        CopilotTaskSubscriber subscriber = new(taskId);
        _taskSubscribers[taskId] = subscriber;
        Publisher.PublishTaskStatusChanged(new TaskStatusChangedEvent(taskId, TaskStatus.Pending, DateTime.UtcNow));

        Task execution = ScheduleTaskExecutionAsync(taskId, subscriber, prompt, _disposeCancellationSource.Token);
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

    private static string BuildCopilotPrompt(string message, IReadOnlyList<TaskHistoryMessage> history)
    {
        if (history.Count == 0)
        {
            return message;
        }

        List<SerializedHistoryMessage> serializedHistory = new(history.Count);
        foreach (TaskHistoryMessage historyMessage in history)
        {
            serializedHistory.Add(new SerializedHistoryMessage(MapHistoryRole(historyMessage.Role), historyMessage.Content));
        }

        string historyJson = JsonSerializer.Serialize(serializedHistory, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        return $$"""
            Continue the conversation using the JSON transcript below as prior context.
            Each history item is in chronological order and has a "role" of "user" or "assistant".
            Respond only to the current user message.

            Conversation history JSON:
            {{historyJson}}

            Current user message:
            {{message}}
            """;
    }


    private static string MapHistoryRole(ChatRole role)
    {
        if (role == ChatRole.User)
        {
            return "user";
        }

        if (role == ChatRole.Assistant)
        {
            return "assistant";
        }

        throw new ArgumentOutOfRangeException(nameof(role));
    }

    private readonly record struct SerializedHistoryMessage(string Role, string Content);

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
