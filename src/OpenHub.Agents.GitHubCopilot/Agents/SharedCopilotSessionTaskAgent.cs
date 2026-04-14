using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenHub.Agents;

internal sealed class SharedCopilotSessionTaskAgent(ICopilotSessionConnection session, bool ownsSession) : CopilotTaskAgentBase
{
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    protected override async Task ExecuteCoreAsync(
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
    {
        bool entered = false;

        try
        {
            await _executionGate.WaitAsync(cancellationToken);
            entered = true;

            await RunPromptAsync(session, subscriber, message, cancellationToken);
        }
        finally
        {
            if (entered)
            {
                _executionGate.Release();
            }
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposeCancellationSource.IsCancellationRequested)
        {
            return;
        }

        _disposeCancellationSource.Cancel();

        Task[] runningTasks = [.. _taskExecutions.Values];
        if (runningTasks.Length > 0)
        {
            await Task.WhenAll(runningTasks);
        }

        try
        {
            if (ownsSession)
            {
                await DisposeOwnedResourceAsync(session);
            }
        }
        finally
        {
            _executionGate.Dispose();
            _disposeCancellationSource.Dispose();
            await base.DisposeAsync();
        }
    }
}
