using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenHub.Agents;

internal sealed class FactoryCopilotSessionTaskAgent(
    Func<CancellationToken, ValueTask<ICopilotSessionConnection>> sessionFactory,
    object? ownedResource = null) : CopilotTaskAgentBase
{
    protected override async Task ExecuteCoreAsync(
        CopilotTaskSubscriber subscriber,
        string message,
        CancellationToken cancellationToken)
    {
        await using ICopilotSessionConnection session = await sessionFactory(cancellationToken);
        await RunPromptAsync(session, subscriber, message, cancellationToken);
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
            await DisposeOwnedResourceAsync(ownedResource);
        }
        finally
        {
            _disposeCancellationSource.Dispose();
            await base.DisposeAsync();
        }
    }
}
