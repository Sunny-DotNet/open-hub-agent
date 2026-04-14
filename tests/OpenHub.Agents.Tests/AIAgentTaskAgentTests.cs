using Microsoft.Extensions.AI;
using OpenHub.Agents.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenHub.Agents.Tests;

public sealed class AIAgentTaskAgentTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CreateTaskAsync_RejectsBlankMessagesAndCanceledTokens()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        FakeAIAgent backend = new((_, _) => AIAgentTestStreams.ReturnUpdates([]));
        await using ITaskAgent taskAgent = backend.AsTaskAgent();

        await Assert.ThrowsAsync<ArgumentException>(() => taskAgent.CreateTaskAsync(new CreateTaskRequest(" ")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => taskAgent.CreateTaskAsync(new CreateTaskRequest("prompt"), cancellationTokenSource.Token));
    }

    [Fact]
    public async Task AsTaskAgent_PublishesLifecycleAndMappedEvents()
    {
        FakeAIAgent backend = new((_, _) => AIAgentTestStreams.ReturnUpdates(
        [
            AIAgentTestUpdates.CreateAssistantUpdate(new TextReasoningContent("think")),
            AIAgentTestUpdates.CreateAssistantUpdate(new TextContent("hello")),
            AIAgentTestUpdates.CreateAssistantUpdate(
                new FunctionCallContent("call-1", "read_file", new Dictionary<string, object?> { ["path"] = "Program.cs" }),
                new FunctionResultContent("call-1", "done"),
                new UsageContent(new UsageDetails
                {
                    InputTokenCount = 10,
                    OutputTokenCount = 5,
                    CachedInputTokenCount = 2,
                    TotalTokenCount = 15,
                }))
        ]));

        await using (ITaskAgent taskAgent = backend.AsTaskAgent())
        {
            CreateTaskResponse response = await taskAgent.CreateTaskAsync(new CreateTaskRequest("prompt"));
            Assert.Same(response.Subscriber, taskAgent.GetTaskSubscriber(response.TaskId));

            using TaskStatusRecorder statusRecorder = new(taskAgent.Subscriber, response.TaskId, TaskStatus.Completed);

            await response.Subscriber.WaitForCompletionAsync().WaitAsync(WaitTimeout);
            await statusRecorder.WaitForTerminalStatusAsync().WaitAsync(WaitTimeout);

            Assert.Equal(["prompt"], backend.Prompts);
            Assert.Equal([TaskStatus.Pending, TaskStatus.InProgress, TaskStatus.Completed], statusRecorder.Statuses);
            Assert.Equal(
                [nameof(TextReasoningContent), nameof(TextContent), nameof(ToolCallContent)],
                ObservableReplay.Read(response.Subscriber.NewPart));

            TaskReasoningChunkEvent reasoningChunk = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskReasoningChunk));
            Assert.Equal("think", reasoningChunk.Content);

            TaskContentChunkEvent contentChunk = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskContentChunk));
            Assert.Equal("hello", contentChunk.Content);

            TaskToolCallRequestEvent toolRequest = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskToolCallRequest));
            Assert.Equal("call-1", toolRequest.ToolCallId);
            Assert.Equal("read_file", toolRequest.ToolName);
            Assert.Contains("Program.cs", toolRequest.Arguments, StringComparison.Ordinal);

            TaskToolCallResponseEvent toolResponse = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskToolCallResponse));
            Assert.Equal("call-1", toolResponse.ToolCallId);
            Assert.Equal("read_file", toolResponse.ToolName);
            Assert.Contains("done", toolResponse.Response, StringComparison.Ordinal);

            TaskUsageUpdatedEvent usageEvent = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskUsageUpdated));
            Assert.Equal(10, usageEvent.UsageContent.Details.InputTokenCount);
            Assert.Equal(5, usageEvent.UsageContent.Details.OutputTokenCount);
            Assert.Equal(2, usageEvent.UsageContent.Details.CachedInputTokenCount);
            Assert.Equal(15, usageEvent.UsageContent.Details.TotalTokenCount);
        }

        Assert.True(backend.Disposed);
    }

    [Fact]
    public async Task AsTaskAgent_CancelsRunningTasksDuringDispose()
    {
        TaskCompletionSource sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAIAgent backend = new((_, cancellationToken) => AIAgentTestStreams.WaitForCancellation(sendStarted, cancellationToken));

        ITaskAgent? taskAgent = backend.AsTaskAgent();

        try
        {
            CreateTaskResponse response = await taskAgent.CreateTaskAsync(new CreateTaskRequest("prompt"));
            using TaskStatusRecorder statusRecorder = new(taskAgent.Subscriber, response.TaskId, TaskStatus.Cancelled);

            await sendStarted.Task.WaitAsync(WaitTimeout);

            await taskAgent.DisposeAsync();
            taskAgent = null;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response.Subscriber.WaitForCompletionAsync());
            await statusRecorder.WaitForTerminalStatusAsync().WaitAsync(WaitTimeout);

            Assert.Equal([TaskStatus.Pending, TaskStatus.InProgress, TaskStatus.Cancelled], statusRecorder.Statuses);
            Assert.True(backend.Disposed);
        }
        finally
        {
            if (taskAgent is not null)
            {
                await taskAgent.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task AsTaskAgent_PropagatesFailures()
    {
        InvalidOperationException expected = new("boom");
        FakeAIAgent backend = new((_, cancellationToken) => AIAgentTestStreams.Throw(expected, cancellationToken));

        await using (ITaskAgent taskAgent = backend.AsTaskAgent())
        {
            CreateTaskResponse response = await taskAgent.CreateTaskAsync(new CreateTaskRequest("prompt"));
            using TaskStatusRecorder statusRecorder = new(taskAgent.Subscriber, response.TaskId, TaskStatus.Failed);

            InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => response.Subscriber.WaitForCompletionAsync());
            Assert.Equal(expected.Message, actual.Message);

            await statusRecorder.WaitForTerminalStatusAsync().WaitAsync(WaitTimeout);
            Assert.Equal([TaskStatus.Pending, TaskStatus.InProgress, TaskStatus.Failed], statusRecorder.Statuses);
        }
    }

    [Fact]
    public void ChatClientAgentTaskSubscriber_MapsStreamingUpdates()
    {
        Guid taskId = Guid.NewGuid();
        ChatClientAgentTaskSubscriber subscriber = new(taskId);

        subscriber.Update(AIAgentTestUpdates.CreateAssistantUpdate(
            new TextReasoningContent("think"),
            new TextReasoningContent("again")));
        subscriber.Update(AIAgentTestUpdates.CreateAssistantUpdate(new TextContent("hello")));
        subscriber.Update(AIAgentTestUpdates.CreateAssistantUpdate(
            new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?> { ["location"] = "Paris" }),
            new FunctionResultContent("call-1", "cloudy"),
            new UsageContent(new UsageDetails
            {
                InputTokenCount = 3,
                OutputTokenCount = 4,
                TotalTokenCount = 7,
            })));

        Assert.Equal(
            [nameof(TextReasoningContent), nameof(TextContent), nameof(ToolCallContent)],
            ObservableReplay.Read(subscriber.NewPart));

        Assert.Equal(
            ["think", "again"],
            ObservableReplay.Read(subscriber.TaskReasoningChunk).Select(chunk => chunk.Content).ToArray());

        TaskContentChunkEvent contentChunk = Assert.Single(ObservableReplay.Read(subscriber.TaskContentChunk));
        Assert.Equal("hello", contentChunk.Content);

        TaskToolCallRequestEvent toolRequest = Assert.Single(ObservableReplay.Read(subscriber.TaskToolCallRequest));
        Assert.Equal("get_weather", toolRequest.ToolName);
        Assert.Contains("Paris", toolRequest.Arguments, StringComparison.Ordinal);

        TaskToolCallResponseEvent toolResponse = Assert.Single(ObservableReplay.Read(subscriber.TaskToolCallResponse));
        Assert.Equal("call-1", toolResponse.ToolCallId);
        Assert.Equal("get_weather", toolResponse.ToolName);
        Assert.Contains("cloudy", toolResponse.Response, StringComparison.Ordinal);

        TaskUsageUpdatedEvent usageEvent = Assert.Single(ObservableReplay.Read(subscriber.TaskUsageUpdated));
        Assert.Equal(7, usageEvent.UsageContent.Details.TotalTokenCount);
    }
}
