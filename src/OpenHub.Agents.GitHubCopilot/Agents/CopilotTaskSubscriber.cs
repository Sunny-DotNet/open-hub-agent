using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace OpenHub.Agents;


public sealed class CopilotTaskSubscriber : TaskSubscriberBase
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, string> _toolCallNames = new(StringComparer.Ordinal);
    private string _lastType = string.Empty;

    public CopilotTaskSubscriber(Guid taskId) : base(taskId)
    {
    }

    private static string GetReasoningDisplayText(TextReasoningContent reasoning)
        => !string.IsNullOrWhiteSpace(reasoning.Text)
            ? reasoning.Text
            : !string.IsNullOrWhiteSpace(reasoning.ProtectedData)
                ? "[protected reasoning]"
                : string.Empty;

    public void Update(SessionEvent evt)
    {
        if (evt is null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        lock (_syncRoot)
        {

            switch (evt)
            {
                case AssistantReasoningDeltaEvent assistantReasoningDeltaEvent:
                    if (!_lastType.Equals(nameof(TextReasoningContent), StringComparison.Ordinal))
                    {
                        _lastType = nameof(TextReasoningContent);
                        _reasoningNumber++;
                        _reasoningSeq = 0;
                        NewPartSubject.OnNext(_lastType);
                    }

                    TaskReasoningChunkSubject.OnNext(new TaskReasoningChunkEvent(TaskId, _reasoningNumber, _reasoningSeq++, assistantReasoningDeltaEvent.Data.DeltaContent));
                    break;

                case AssistantMessageDeltaEvent assistantMessage:
                    if (!_lastType.Equals(nameof(TextContent), StringComparison.Ordinal))
                    {
                        _lastType = nameof(TextContent);
                        _contentNumber++;
                        _contentSeq = 0;
                        NewPartSubject.OnNext(_lastType);
                    }

                    TaskContentChunkSubject.OnNext(new TaskContentChunkEvent(TaskId, _contentNumber, _contentSeq++, assistantMessage.Data.DeltaContent));
                    break;
                case ToolExecutionStartEvent toolExecutionStartEvent:
                    if (!_lastType.Equals(nameof(ToolCallContent), StringComparison.Ordinal))
                    {
                        _lastType = nameof(ToolCallContent);
                        _toolCallNumber++;
                        NewPartSubject.OnNext(_lastType);
                    }

                    _toolCallNames[toolExecutionStartEvent.Data.ToolCallId] = toolExecutionStartEvent.Data.ToolName;
                    TaskToolCallRequestSubject.OnNext(
                        new TaskToolCallRequestEvent(
                            TaskId,
                            toolExecutionStartEvent.Data.ToolCallId,
                            toolExecutionStartEvent.Data.ToolName,
                            JsonSerializer.Serialize(toolExecutionStartEvent.Data.Arguments)));

                    break;

                case ToolExecutionCompleteEvent toolExecutionCompleteEvent:

                    _toolCallNames.TryGetValue(toolExecutionCompleteEvent.Data.ToolCallId, out string? toolName);
                    _toolCallNames.Remove(toolExecutionCompleteEvent.Data.ToolCallId);

                    TaskToolCallResponseSubject.OnNext(
                        new TaskToolCallResponseEvent(
                            TaskId,
                            toolExecutionCompleteEvent.Data.ToolCallId,
                            toolName ?? string.Empty,
                            JsonSerializer.Serialize(toolExecutionCompleteEvent.Data.Result)));
                    break;

                case AssistantUsageEvent usageContent:
                    _lastType = nameof(UsageContent);
                    TaskUsageUpdatedSubject.OnNext(new TaskUsageUpdatedEvent(TaskId, MapTo(usageContent)));
                    break;
                //case AssistantStreamingDeltaEvent streamingDeltaEvent:
                //    Console.WriteLine();
                //    break;
                default:
                    Debug.WriteLine($"[CopilotTaskSubscriber] Unhandled event type: {evt.GetType().Name}");
                    Debug.WriteLine(JsonSerializer.Serialize(evt));
                    break;
            }
        }
    }

    private UsageContent MapTo(AssistantUsageEvent usageContent)
    {
        var details = new UsageDetails()
        {
            CachedInputTokenCount = (long?)usageContent.Data.CacheReadTokens,
            InputTokenCount = (long?)usageContent.Data.InputTokens,
            OutputTokenCount = (long?)usageContent.Data.OutputTokens,
        };
        details.TotalTokenCount = (details.InputTokenCount ?? 0) + (details.OutputTokenCount ?? 0);
        return new UsageContent(details) ;
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
