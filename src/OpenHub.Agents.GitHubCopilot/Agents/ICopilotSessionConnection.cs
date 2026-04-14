using GitHub.Copilot.SDK;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenHub.Agents;

internal interface ICopilotSessionConnection : IAsyncDisposable
{
    IDisposable On(SessionEventHandler handler);
    Task SendAsync(MessageOptions options, CancellationToken cancellationToken = default);
}
