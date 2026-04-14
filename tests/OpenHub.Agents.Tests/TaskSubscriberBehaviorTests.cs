using OpenHub.Agents.Models;

namespace OpenHub.Agents.Tests;

public sealed class TaskSubscriberBehaviorTests
{
    [Fact]
    public async Task WaitForCompletionAsync_RespectsCancellation()
    {
        TestTaskSubscriber subscriber = new(Guid.NewGuid());
        using CancellationTokenSource cancellationTokenSource = new();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => subscriber.WaitForCompletionAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task Complete_CompletesPublishedStreamsAndWaiters()
    {
        TestTaskSubscriber subscriber = new(Guid.NewGuid());
        bool newPartCompleted = false;
        bool contentCompleted = false;

        using IDisposable newPartSubscription = subscriber.NewPart.Subscribe(_ => { }, () => newPartCompleted = true);
        using IDisposable contentSubscription = subscriber.TaskContentChunk.Subscribe(_ => { }, () => contentCompleted = true);

        subscriber.PublishContent("hello");
        subscriber.MarkCompleted();

        await subscriber.WaitForCompletionAsync();

        Assert.True(newPartCompleted);
        Assert.True(contentCompleted);
    }

    [Fact]
    public async Task Cancel_FaultsStreamsAndCancelsWaiters()
    {
        TestTaskSubscriber subscriber = new(Guid.NewGuid());
        Exception? observedException = null;

        using IDisposable subscription = subscriber.TaskReasoningChunk.Subscribe(_ => { }, ex => observedException = ex);

        subscriber.MarkCancelled();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => subscriber.WaitForCompletionAsync());
        Assert.IsType<TaskCanceledException>(observedException);
    }

    [Fact]
    public async Task Fail_FaultsStreamsAndWaiters()
    {
        TestTaskSubscriber subscriber = new(Guid.NewGuid());
        InvalidOperationException failure = new("boom");
        Exception? observedException = null;

        using IDisposable subscription = subscriber.TaskUsageUpdated.Subscribe(_ => { }, ex => observedException = ex);

        subscriber.MarkFailed(failure);

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => subscriber.WaitForCompletionAsync());
        Assert.Same(failure, thrown);
        Assert.Same(failure, observedException);
    }

    private sealed class TestTaskSubscriber : TaskSubscriberBase
    {
        public TestTaskSubscriber(Guid taskId)
            : base(taskId)
        {
        }

        public void PublishContent(string content)
        {
            NewPartSubject.OnNext(nameof(TaskContentChunkEvent));
            TaskContentChunkSubject.OnNext(new TaskContentChunkEvent(TaskId, 1, 0, content));
        }

        public void MarkCompleted()
            => CompleteSubscribers();

        public void MarkCancelled()
            => CancelSubscribers();

        public void MarkFailed(Exception exception)
            => FailSubscribers(exception);
    }
}
