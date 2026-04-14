using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;
using System;
using System.Linq;

namespace OpenHub.Agents.Tests;

public sealed class CopilotTaskSubscriberTests
{
    [Fact]
    public void Update_ThrowsWhenEventIsNull()
    {
        CopilotTaskSubscriber subscriber = new(Guid.NewGuid());

        Assert.Throws<ArgumentNullException>(() => subscriber.Update(null!));
    }

    [Fact]
    public void Update_EmitsReasoningAndContentChunksWithPartBoundaries()
    {
        CopilotTaskSubscriber subscriber = new(Guid.NewGuid());

        subscriber.Update(GitHubCopilotTestEvents.CreateReasoningDeltaEvent("reasoning-1", "step 1"));
        subscriber.Update(GitHubCopilotTestEvents.CreateReasoningDeltaEvent("reasoning-1", "step 2"));
        subscriber.Update(GitHubCopilotTestEvents.CreateMessageDeltaEvent("message-1", "hello"));
        subscriber.Update(GitHubCopilotTestEvents.CreateMessageDeltaEvent("message-1", " world"));

        IReadOnlyList<string> parts = ObservableReplay.Read(subscriber.NewPart);
        IReadOnlyList<TaskReasoningChunkEvent> reasoningChunks = ObservableReplay.Read(subscriber.TaskReasoningChunk);
        IReadOnlyList<TaskContentChunkEvent> contentChunks = ObservableReplay.Read(subscriber.TaskContentChunk);

        Assert.Equal([nameof(TextReasoningContent), nameof(TextContent)], parts);
        Assert.Equal(["step 1", "step 2"], reasoningChunks.Select(chunk => chunk.Content));
        Assert.All(reasoningChunks, chunk => Assert.Equal(1, chunk.ReasoningId));
        Assert.Equal([0, 1], reasoningChunks.Select(chunk => chunk.Seq));
        Assert.Equal(["hello", " world"], contentChunks.Select(chunk => chunk.Content));
        Assert.All(contentChunks, chunk => Assert.Equal(1, chunk.ContentId));
        Assert.Equal([0, 1], contentChunks.Select(chunk => chunk.Seq));
    }

    [Fact]
    public void Update_EmitsToolCallRequestAndResponseUsingTheOriginalToolName()
    {
        CopilotTaskSubscriber subscriber = new(Guid.NewGuid());

        subscriber.Update(GitHubCopilotTestEvents.CreateToolExecutionStartEvent("tool-1", "read_file", new { Path = "Program.cs" }));
        subscriber.Update(GitHubCopilotTestEvents.CreateToolExecutionCompleteEvent("tool-1", "file content"));

        IReadOnlyList<string> parts = ObservableReplay.Read(subscriber.NewPart);
        IReadOnlyList<TaskToolCallRequestEvent> requests = ObservableReplay.Read(subscriber.TaskToolCallRequest);
        IReadOnlyList<TaskToolCallResponseEvent> responses = ObservableReplay.Read(subscriber.TaskToolCallResponse);

        Assert.Equal([nameof(ToolCallContent)], parts);
        TaskToolCallRequestEvent request = Assert.Single(requests);
        Assert.Equal("tool-1", request.ToolCallId);
        Assert.Equal("read_file", request.ToolName);
        Assert.Contains("Program.cs", request.Arguments, StringComparison.Ordinal);

        TaskToolCallResponseEvent response = Assert.Single(responses);
        Assert.Equal("tool-1", response.ToolCallId);
        Assert.Equal("read_file", response.ToolName);
        Assert.Contains("file content", response.Response, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_MapsUsageWithoutDoubleCountingCachedInputTokens()
    {
        CopilotTaskSubscriber subscriber = new(Guid.NewGuid());

        subscriber.Update(GitHubCopilotTestEvents.CreateUsageEvent(inputTokens: 10, outputTokens: 5, cacheReadTokens: 7));

        TaskUsageUpdatedEvent usageEvent = Assert.Single(ObservableReplay.Read(subscriber.TaskUsageUpdated));

        Assert.Equal(10, usageEvent.UsageContent.Details.InputTokenCount);
        Assert.Equal(5, usageEvent.UsageContent.Details.OutputTokenCount);
        Assert.Equal(7, usageEvent.UsageContent.Details.CachedInputTokenCount);
        Assert.Equal(15, usageEvent.UsageContent.Details.TotalTokenCount);
    }
}
