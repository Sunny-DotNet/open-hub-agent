using OpenHub.Agents.Models;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace OpenHub.Agents;

public interface ITaskSubscriber
{
    IObservable<string> NewPart { get; }
    IObservable<TaskReasoningChunkEvent> TaskReasoningChunk { get; }
    IObservable<TaskContentChunkEvent> TaskContentChunk { get; }
    IObservable<TaskToolCallRequestEvent> TaskToolCallRequest { get; }
    IObservable<TaskToolCallResponseEvent> TaskToolCallResponse { get; }
    IObservable<TaskUsageUpdatedEvent> TaskUsageUpdated { get; }
    IObservable<TaskMediaCreatedEvent> TaskMediaCreated { get; }

    Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
}

public abstract class TaskSubscriberBase : ITaskSubscriber
{
    private const int DefaultReplayBufferSize = 1024;

    protected ISubject<string> NewPartSubject { get; } = new ReplaySubject<string>(DefaultReplayBufferSize);
    protected ISubject<TaskReasoningChunkEvent> TaskReasoningChunkSubject { get; } = new ReplaySubject<TaskReasoningChunkEvent>(DefaultReplayBufferSize);
    protected ISubject<TaskContentChunkEvent> TaskContentChunkSubject { get; } = new ReplaySubject<TaskContentChunkEvent>(DefaultReplayBufferSize);
    protected ISubject<TaskToolCallRequestEvent> TaskToolCallRequestSubject { get; } = new ReplaySubject<TaskToolCallRequestEvent>(DefaultReplayBufferSize);
    protected ISubject<TaskToolCallResponseEvent> TaskToolCallResponseSubject { get; } = new ReplaySubject<TaskToolCallResponseEvent>(DefaultReplayBufferSize);
    protected ISubject<TaskUsageUpdatedEvent> TaskUsageUpdatedSubject { get; } = new ReplaySubject<TaskUsageUpdatedEvent>(DefaultReplayBufferSize);
    protected ISubject<TaskMediaCreatedEvent> TaskMediaCreatedSubject { get; } = new ReplaySubject<TaskMediaCreatedEvent>(DefaultReplayBufferSize);
    protected TaskCompletionSource<int> TaskCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IObservable<string> NewPart { get; }
    public IObservable<TaskReasoningChunkEvent> TaskReasoningChunk { get; }
    public IObservable<TaskContentChunkEvent> TaskContentChunk { get; }
    public IObservable<TaskToolCallRequestEvent> TaskToolCallRequest { get; }
    public IObservable<TaskToolCallResponseEvent> TaskToolCallResponse { get; }
    public IObservable<TaskUsageUpdatedEvent> TaskUsageUpdated { get; }
    public IObservable<TaskMediaCreatedEvent> TaskMediaCreated { get; }

    public Guid TaskId { get; }

    protected int _reasoningNumber = 0;
    protected int _contentNumber = 0;
    protected int _toolCallNumber = 0;
    protected int _mediaNumber = 0;
    protected int _reasoningSeq = 0;
    protected int _contentSeq = 0;
    protected int _mediaSeq = 0;

    protected TaskSubscriberBase(Guid taskId)
    {
        TaskId = taskId;
        NewPart = NewPartSubject.AsObservable();
        TaskReasoningChunk = TaskReasoningChunkSubject.AsObservable();
        TaskContentChunk = TaskContentChunkSubject.AsObservable();
        TaskToolCallRequest = TaskToolCallRequestSubject.AsObservable();
        TaskToolCallResponse = TaskToolCallResponseSubject.AsObservable();
        TaskUsageUpdated = TaskUsageUpdatedSubject.AsObservable();
        TaskMediaCreated = TaskMediaCreatedSubject.AsObservable();
    }

    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
        => TaskCompletionSource.Task.WaitAsyncCompat(cancellationToken);

    protected void CompleteSubscribers()
    {
        if (!TaskCompletionSource.TrySetResult(0))
        {
            return;
        }

        CompleteSubject(NewPartSubject);
        CompleteSubject(TaskReasoningChunkSubject);
        CompleteSubject(TaskContentChunkSubject);
        CompleteSubject(TaskToolCallRequestSubject);
        CompleteSubject(TaskToolCallResponseSubject);
        CompleteSubject(TaskUsageUpdatedSubject);
        CompleteSubject(TaskMediaCreatedSubject);
    }

    protected void CancelSubscribers()
    {
        if (!TaskCompletionSource.TrySetCanceled())
        {
            return;
        }

        TaskCanceledException exception = new();
        FailSubject(NewPartSubject, exception);
        FailSubject(TaskReasoningChunkSubject, exception);
        FailSubject(TaskContentChunkSubject, exception);
        FailSubject(TaskToolCallRequestSubject, exception);
        FailSubject(TaskToolCallResponseSubject, exception);
        FailSubject(TaskUsageUpdatedSubject, exception);
        FailSubject(TaskMediaCreatedSubject, exception);
    }

    protected void FailSubscribers(Exception exception)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        if (!TaskCompletionSource.TrySetException(exception))
        {
            return;
        }

        FailSubject(NewPartSubject, exception);
        FailSubject(TaskReasoningChunkSubject, exception);
        FailSubject(TaskContentChunkSubject, exception);
        FailSubject(TaskToolCallRequestSubject, exception);
        FailSubject(TaskToolCallResponseSubject, exception);
        FailSubject(TaskUsageUpdatedSubject, exception);
        FailSubject(TaskMediaCreatedSubject, exception);
    }

    private static void CompleteSubject<T>(ISubject<T> subject)
        => subject.OnCompleted();

    private static void FailSubject<T>(ISubject<T> subject, Exception exception)
        => subject.OnError(exception);
}
