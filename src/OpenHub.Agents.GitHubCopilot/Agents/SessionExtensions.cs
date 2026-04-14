using GitHub.Copilot.SDK;
using System;

namespace OpenHub.Agents;

public static class SessionExtensions
{
    /// <summary>
    /// Creates a task agent over a shared Copilot session. Tasks are serialized and share conversation state.
    /// </summary>
    public static ITaskAgent AsTaskAgent(this CopilotSession session, bool ownsSession = false)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        return new SharedCopilotSessionTaskAgent(new CopilotSessionConnection(session), ownsSession);
    }
}
