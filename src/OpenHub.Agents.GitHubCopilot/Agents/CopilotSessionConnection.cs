using GitHub.Copilot.SDK;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenHub.Agents;

internal sealed class CopilotSessionConnection(CopilotSession session) : ICopilotSessionConnection
{
    public IDisposable On(SessionEventHandler handler)
        => session.On(handler);

    public async Task SendAsync(MessageOptions options, CancellationToken cancellationToken = default)
        => _ = await session.SendAsync(options, cancellationToken);

    public ValueTask DisposeAsync()
        => session.DisposeAsync();
}
