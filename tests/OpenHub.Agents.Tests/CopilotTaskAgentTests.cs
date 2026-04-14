using OpenHub.Agents.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskStatus = OpenHub.Agents.Models.TaskStatus;

namespace OpenHub.Agents.Tests;

public sealed class CopilotTaskAgentTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CreateTaskAsync_RejectsBlankMessagesAndCanceledTokens()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await using SharedCopilotSessionTaskAgent agent = new(new FakeCopilotSessionConnection((_, _, _) => Task.CompletedTask), ownsSession: false);

        await Assert.ThrowsAsync<ArgumentException>(() => agent.CreateTaskAsync(new CreateTaskRequest(" ")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agent.CreateTaskAsync(new CreateTaskRequest("prompt"), cancellationTokenSource.Token));
    }

    [Fact]
    public async Task SharedCopilotSessionTaskAgent_PublishesLifecycleAndMappedTaskEvents()
    {
        FakeCopilotSessionConnection session = new((self, _, _) =>
        {
            self.Publish(GitHubCopilotTestEvents.CreateReasoningDeltaEvent("reasoning-1", "think"));
            self.Publish(GitHubCopilotTestEvents.CreateMessageDeltaEvent("message-1", "hello"));
            self.Publish(GitHubCopilotTestEvents.CreateToolExecutionStartEvent("tool-1", "read_file", new { Path = "Program.cs" }));
            self.Publish(GitHubCopilotTestEvents.CreateToolExecutionCompleteEvent("tool-1", "done"));
            self.Publish(GitHubCopilotTestEvents.CreateUsageEvent(inputTokens: 10, outputTokens: 5, cacheReadTokens: 7));
            self.Publish(GitHubCopilotTestEvents.CreateIdleEvent());
            return Task.CompletedTask;
        });

        await using SharedCopilotSessionTaskAgent agent = new(session, ownsSession: false);

        CreateTaskResponse response = await agent.CreateTaskAsync(new CreateTaskRequest("prompt"));
        Assert.Same(response.Subscriber, agent.GetTaskSubscriber(response.TaskId));

        using TaskStatusRecorder statusRecorder = new(agent.Subscriber, response.TaskId, TaskStatus.Completed);

        await response.Subscriber.WaitForCompletionAsync();
        await statusRecorder.WaitForTerminalStatusAsync().WaitAsync(WaitTimeout);

        Assert.Equal([TaskStatus.Pending, TaskStatus.InProgress, TaskStatus.Completed], statusRecorder.Statuses);
        Assert.Equal(["prompt"], session.SentPrompts);

        Assert.Equal(
            [nameof(Microsoft.Extensions.AI.TextReasoningContent), nameof(Microsoft.Extensions.AI.TextContent), nameof(Microsoft.Extensions.AI.ToolCallContent)],
            ObservableReplay.Read(response.Subscriber.NewPart));

        TaskReasoningChunkEvent reasoningChunk = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskReasoningChunk));
        Assert.Equal("think", reasoningChunk.Content);

        TaskContentChunkEvent contentChunk = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskContentChunk));
        Assert.Equal("hello", contentChunk.Content);

        TaskToolCallRequestEvent toolRequest = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskToolCallRequest));
        Assert.Equal("tool-1", toolRequest.ToolCallId);
        Assert.Equal("read_file", toolRequest.ToolName);
        Assert.Contains("Program.cs", toolRequest.Arguments, StringComparison.Ordinal);

        TaskToolCallResponseEvent toolResponse = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskToolCallResponse));
        Assert.Equal("tool-1", toolResponse.ToolCallId);
        Assert.Equal("read_file", toolResponse.ToolName);
        Assert.Contains("done", toolResponse.Response, StringComparison.Ordinal);

        TaskUsageUpdatedEvent usageEvent = Assert.Single(ObservableReplay.Read(response.Subscriber.TaskUsageUpdated));
        Assert.Equal(10, usageEvent.UsageContent.Details.InputTokenCount);
        Assert.Equal(5, usageEvent.UsageContent.Details.OutputTokenCount);
        Assert.Equal(7, usageEvent.UsageContent.Details.CachedInputTokenCount);
        Assert.Equal(15, usageEvent.UsageContent.Details.TotalTokenCount);
    }

    [Fact]
    public async Task SharedCopilotSessionTaskAgent_SerializesConcurrentTasks()
    {
        TaskCompletionSource firstSendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowFirstTaskToFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondSendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int sendIndex = 0;

        FakeCopilotSessionConnection session = new(async (self, _, cancellationToken) =>
        {
            int currentSend = Interlocked.Increment(ref sendIndex);

            if (currentSend == 1)
            {
                firstSendStarted.TrySetResult();
                await allowFirstTaskToFinish.Task.WaitAsync(cancellationToken);
                self.Publish(GitHubCopilotTestEvents.CreateMessageDeltaEvent("message-1", "first"));
                self.Publish(GitHubCopilotTestEvents.CreateIdleEvent());
                return;
            }

            secondSendStarted.TrySetResult();
            self.Publish(GitHubCopilotTestEvents.CreateMessageDeltaEvent("message-2", "second"));
            self.Publish(GitHubCopilotTestEvents.CreateIdleEvent());
        });

        await using SharedCopilotSessionTaskAgent agent = new(session, ownsSession: false);

        CreateTaskResponse firstTask = await agent.CreateTaskAsync(new CreateTaskRequest("first prompt"));
        CreateTaskResponse secondTask = await agent.CreateTaskAsync(new CreateTaskRequest("second prompt"));

        await firstSendStarted.Task.WaitAsync(WaitTimeout);

        Assert.Equal(1, session.SendCount);
        Assert.False(secondSendStarted.Task.IsCompleted);

        allowFirstTaskToFinish.TrySetResult();

        await Task.WhenAll(
            firstTask.Subscriber.WaitForCompletionAsync(),
            secondTask.Subscriber.WaitForCompletionAsync());

        await secondSendStarted.Task.WaitAsync(WaitTimeout);

        Assert.Equal(2, session.SendCount);
        Assert.Equal(1, session.MaxConcurrentSends);
        Assert.Equal(["first prompt", "second prompt"], session.SentPrompts);
        Assert.Equal(["first"], ObservableReplay.Read(firstTask.Subscriber.TaskContentChunk).Select(evt => evt.Content));
        Assert.Equal(["second"], ObservableReplay.Read(secondTask.Subscriber.TaskContentChunk).Select(evt => evt.Content));
    }

    [Fact]
    public async Task SharedCopilotSessionTaskAgent_FailsTheTaskWhenTheSessionErrors()
    {
        FakeCopilotSessionConnection session = new((self, _, _) =>
        {
            self.Publish(GitHubCopilotTestEvents.CreateErrorEvent(" "));
            return Task.CompletedTask;
        });

        await using SharedCopilotSessionTaskAgent agent = new(session, ownsSession: false);

        CreateTaskResponse response = await agent.CreateTaskAsync(new CreateTaskRequest("prompt"));
        using TaskStatusRecorder statusRecorder = new(agent.Subscriber, response.TaskId, TaskStatus.Failed);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => response.Subscriber.WaitForCompletionAsync());

        await statusRecorder.WaitForTerminalStatusAsync().WaitAsync(WaitTimeout);

        Assert.Equal("The Copilot session reported an error.", exception.Message);
        Assert.Equal([TaskStatus.Pending, TaskStatus.InProgress, TaskStatus.Failed], statusRecorder.Statuses);
    }

    [Fact]
    public async Task SharedCopilotSessionTaskAgent_CancelsRunningTasksDuringDispose()
    {
        TaskCompletionSource sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeCopilotSessionConnection session = new(async (_, _, cancellationToken) =>
        {
            sendStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        });

        SharedCopilotSessionTaskAgent agent = new(session, ownsSession: false);

        try
        {
            CreateTaskResponse response = await agent.CreateTaskAsync(new CreateTaskRequest("prompt"));
            using TaskStatusRecorder statusRecorder = new(agent.Subscriber, response.TaskId, TaskStatus.Cancelled);

            await sendStarted.Task.WaitAsync(WaitTimeout);

            ValueTask disposeTask = agent.DisposeAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response.Subscriber.WaitForCompletionAsync());
            await statusRecorder.WaitForTerminalStatusAsync().WaitAsync(WaitTimeout);
            await disposeTask;

            Assert.Equal([TaskStatus.Pending, TaskStatus.InProgress, TaskStatus.Cancelled], statusRecorder.Statuses);
        }
        finally
        {
            await agent.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedCopilotSessionTaskAgent_Dispose_RespectsSessionOwnership(bool ownsSession)
    {
        FakeCopilotSessionConnection session = new((_, _, _) => Task.CompletedTask);

        await using (SharedCopilotSessionTaskAgent agent = new(session, ownsSession))
        {
        }

        Assert.Equal(ownsSession, session.Disposed);
    }

    [Fact]
    public async Task FactoryCopilotSessionTaskAgent_CreatesIndependentSessionsPerTask()
    {
        TaskCompletionSource allSendsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource allowTasksToFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int createCount = 0;
        int activeSends = 0;
        int maxConcurrentSends = 0;
        int startedSendCount = 0;
        object syncRoot = new();
        List<FakeCopilotSessionConnection> createdSessions = [];

        await using FactoryCopilotSessionTaskAgent agent = new(_ =>
        {
            int currentIndex = Interlocked.Increment(ref createCount);
            FakeCopilotSessionConnection session = new(async (self, _, cancellationToken) =>
            {
                int currentActiveSends = Interlocked.Increment(ref activeSends);
                UpdateMax(ref maxConcurrentSends, currentActiveSends);

                if (Interlocked.Increment(ref startedSendCount) == 2)
                {
                    allSendsStarted.TrySetResult();
                }

                try
                {
                    await allowTasksToFinish.Task.WaitAsync(cancellationToken);
                    self.Publish(GitHubCopilotTestEvents.CreateMessageDeltaEvent($"message-{currentIndex}", $"factory-{currentIndex}"));
                    self.Publish(GitHubCopilotTestEvents.CreateIdleEvent());
                }
                finally
                {
                    Interlocked.Decrement(ref activeSends);
                }
            });

            lock (syncRoot)
            {
                createdSessions.Add(session);
            }

            return ValueTask.FromResult<ICopilotSessionConnection>(session);
        });

        CreateTaskResponse firstTask = await agent.CreateTaskAsync(new CreateTaskRequest("first prompt"));
        CreateTaskResponse secondTask = await agent.CreateTaskAsync(new CreateTaskRequest("second prompt"));

        await allSendsStarted.Task.WaitAsync(WaitTimeout);

        Assert.Equal(2, createCount);
        Assert.Equal(2, maxConcurrentSends);

        allowTasksToFinish.TrySetResult();

        await Task.WhenAll(
            firstTask.Subscriber.WaitForCompletionAsync(),
            secondTask.Subscriber.WaitForCompletionAsync());

        Assert.Equal(2, createdSessions.Count);
        Assert.All(createdSessions, session => Assert.True(session.Disposed));

        string[] allContents =
        [
            .. ObservableReplay.Read(firstTask.Subscriber.TaskContentChunk).Select(evt => evt.Content),
            .. ObservableReplay.Read(secondTask.Subscriber.TaskContentChunk).Select(evt => evt.Content),
        ];

        Assert.Equal(2, allContents.Length);
        Assert.Contains("factory-1", allContents);
        Assert.Contains("factory-2", allContents);
    }

    [Fact]
    public async Task FactoryCopilotSessionTaskAgent_FailsTasksWhenSessionCreationFails()
    {
        InvalidOperationException failure = new("factory boom");

        await using FactoryCopilotSessionTaskAgent agent = new(_ => ValueTask.FromException<ICopilotSessionConnection>(failure));

        CreateTaskResponse response = await agent.CreateTaskAsync(new CreateTaskRequest("prompt"));
        using TaskStatusRecorder statusRecorder = new(agent.Subscriber, response.TaskId, TaskStatus.Failed);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => response.Subscriber.WaitForCompletionAsync());

        await statusRecorder.WaitForTerminalStatusAsync().WaitAsync(WaitTimeout);

        Assert.Same(failure, exception);
        Assert.Equal([TaskStatus.Pending, TaskStatus.InProgress, TaskStatus.Failed], statusRecorder.Statuses);
    }

    [Fact]
    public async Task FactoryCopilotSessionTaskAgent_DisposesOwnedResources()
    {
        TestAsyncDisposable ownedResource = new();

        await using (FactoryCopilotSessionTaskAgent agent = new(
            _ => ValueTask.FromResult<ICopilotSessionConnection>(new FakeCopilotSessionConnection((_, _, _) => Task.CompletedTask)),
            ownedResource))
        {
        }

        Assert.True(ownedResource.Disposed);
    }

    private static void UpdateMax(ref int target, int candidate)
    {
        while (true)
        {
            int current = Volatile.Read(ref target);
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, current) == current)
            {
                return;
            }
        }
    }
}
