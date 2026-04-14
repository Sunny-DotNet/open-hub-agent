using OpenHub.Agents.Models;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace OpenHub.Agents;

public interface IAgentSubscriber
{
    IObservable<TaskStatusChangedEvent> TaskStatusChanged { get; }
}

public interface IAgentPublisher
{
    void PublishTaskStatusChanged(TaskStatusChangedEvent @event);
}

public sealed class AgentSubscriber : IAgentSubscriber, IAgentPublisher
{
    private const int StatusReplayBufferSize = 64;

    private ISubject<TaskStatusChangedEvent> TaskStatusChangedSubject { get; } = new ReplaySubject<TaskStatusChangedEvent>(StatusReplayBufferSize);

    public IObservable<TaskStatusChangedEvent> TaskStatusChanged => TaskStatusChangedSubject.AsObservable();

    public void PublishTaskStatusChanged(TaskStatusChangedEvent @event)
        => TaskStatusChangedSubject.OnNext(@event);

    public void Complete()
        => TaskStatusChangedSubject.OnCompleted();

    public void Throw(Exception exception)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        TaskStatusChangedSubject.OnError(exception);
    }
}
