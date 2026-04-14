using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OpenHub.Agents;


public sealed class ChatClientAgentTaskSubscriber : TaskSubscriberBase
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, string> _toolCallNames = new(StringComparer.Ordinal);
    private string _lastType = string.Empty;

    public ChatClientAgentTaskSubscriber(Guid taskId) : base(taskId)
    {
    }

    private static string GetReasoningDisplayText(TextReasoningContent reasoning)
        => !string.IsNullOrWhiteSpace(reasoning.Text)
            ? reasoning.Text
            : !string.IsNullOrWhiteSpace(reasoning.ProtectedData)
                ? "[protected reasoning]"
                : string.Empty;

    public void Update(AgentResponseUpdate update)
    {
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        lock (_syncRoot)
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent textReasoningContent:
                        if (!_lastType.Equals(nameof(TextReasoningContent), StringComparison.Ordinal))
                        {
                            _lastType = nameof(TextReasoningContent);
                            _reasoningNumber++;
                            _reasoningSeq = 0;
                            NewPartSubject.OnNext(_lastType);
                        }

                        TaskReasoningChunkSubject.OnNext(new TaskReasoningChunkEvent(TaskId, _reasoningNumber, _reasoningSeq++, GetReasoningDisplayText(textReasoningContent)));
                        break;

                    case TextContent textContent:
                        if (!_lastType.Equals(nameof(TextContent), StringComparison.Ordinal))
                        {
                            _lastType = nameof(TextContent);
                            _contentNumber++;
                            _contentSeq = 0;
                            NewPartSubject.OnNext(_lastType);
                        }

                        TaskContentChunkSubject.OnNext(new TaskContentChunkEvent(TaskId, _contentNumber, _contentSeq++, textContent.Text));
                        break;

                    case ToolCallContent toolCallContent:
                        if (!_lastType.Equals(nameof(ToolCallContent), StringComparison.Ordinal))
                        {
                            _lastType = nameof(ToolCallContent);
                            _toolCallNumber++;
                            NewPartSubject.OnNext(_lastType);
                        }

                        if (toolCallContent is FunctionCallContent functionCallContent)
                        {
                            _toolCallNames[toolCallContent.CallId] = functionCallContent.Name;
                            TaskToolCallRequestSubject.OnNext(
                                new TaskToolCallRequestEvent(
                                    TaskId,
                                    toolCallContent.CallId,
                                    functionCallContent.Name,
                                    JsonSerializer.Serialize(functionCallContent.Arguments)));
                        }

                        break;

                    case ToolResultContent toolResultContent:
                        if (toolResultContent is FunctionResultContent functionResultContent)
                        {
                            _toolCallNames.TryGetValue(functionResultContent.CallId, out string? toolName);
                            _toolCallNames.Remove(functionResultContent.CallId);

                            TaskToolCallResponseSubject.OnNext(
                                new TaskToolCallResponseEvent(
                                    TaskId,
                                    functionResultContent.CallId,
                                    toolName ?? string.Empty,
                                    JsonSerializer.Serialize(functionResultContent.Result)));
                        }

                        break;

                    case UsageContent usageContent:
                        _lastType = nameof(UsageContent);
                        TaskUsageUpdatedSubject.OnNext(new TaskUsageUpdatedEvent(TaskId, usageContent));
                        break;
                }
            }
        }
    }

    internal void Complete()
    {
        lock (_syncRoot) { CompleteSubscribers(); }
    }

    internal void Cancel()
    {
        lock (_syncRoot) { CancelSubscribers(); }
    }

    internal void Throw(Exception exception)
    {
        lock (_syncRoot) { FailSubscribers(exception); }
    }
}
