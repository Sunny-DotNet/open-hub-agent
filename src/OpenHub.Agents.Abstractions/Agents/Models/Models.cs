using Microsoft.Extensions.AI;
using System;

namespace OpenHub.Agents.Models;


public enum TaskStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled,
    Failed
}

public record struct TaskStatusChangedEvent(Guid TaskId, TaskStatus Status, DateTime Timestamp);
public record struct TaskReasoningChunkEvent(Guid TaskId, int ReasoningId, int Seq, string Content);
public record struct TaskContentChunkEvent(Guid TaskId, int ContentId, int Seq, string Content);
public record struct TaskToolCallRequestEvent(Guid TaskId, string ToolCallId, string ToolName, string Arguments);
public record struct TaskToolCallResponseEvent(Guid TaskId, string ToolCallId, string ToolName, string Response);
public record struct TaskMediaCreatedEvent(Guid TaskId, int MediaId, string MediaUrl, string MediaType);
public record struct TaskUsageUpdatedEvent(Guid TaskId, UsageContent UsageContent);
public record struct CreateTaskRequest(string Message);
public record struct CreateTaskResponse(Guid TaskId, ITaskSubscriber Subscriber);