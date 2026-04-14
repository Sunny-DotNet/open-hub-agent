using GitHub.Copilot.SDK;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenHub.Agents;

public static class CopilotClientExtensions
{
    /// <summary>
    /// Creates a task agent that opens a fresh Copilot session for each task.
    /// </summary>
    public static ITaskAgent AsTaskAgent(this CopilotClient client, SessionConfig sessionConfig, bool ownsClient = false)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (sessionConfig is null)
        {
            throw new ArgumentNullException(nameof(sessionConfig));
        }

        if (sessionConfig.OnPermissionRequest is null)
        {
            throw new ArgumentException("SessionConfig.OnPermissionRequest is required.", nameof(sessionConfig));
        }

        SessionConfig sessionConfigTemplate = CreateStreamingSessionConfig(sessionConfig);

        return new FactoryCopilotSessionTaskAgent(
            cancellationToken => CreateSessionConnectionAsync(client, sessionConfigTemplate, cancellationToken),
            ownsClient ? client : null);
    }

    private static async ValueTask<ICopilotSessionConnection> CreateSessionConnectionAsync(
        CopilotClient client,
        SessionConfig sessionConfigTemplate,
        CancellationToken cancellationToken)
    {
        CopilotSession session = await client.CreateSessionAsync(sessionConfigTemplate, cancellationToken);
        return new CopilotSessionConnection(session);
    }

    private static SessionConfig CreateStreamingSessionConfig(SessionConfig sessionConfig)
        => new()
        {
            Agent = sessionConfig.Agent,
            AvailableTools = sessionConfig.AvailableTools,
            ClientName = sessionConfig.ClientName,
            Commands = sessionConfig.Commands,
            ConfigDir = sessionConfig.ConfigDir,
            CreateSessionFsHandler = sessionConfig.CreateSessionFsHandler,
            CustomAgents = sessionConfig.CustomAgents,
            DisabledSkills = sessionConfig.DisabledSkills,
            EnableConfigDiscovery = sessionConfig.EnableConfigDiscovery,
            ExcludedTools = sessionConfig.ExcludedTools,
            Hooks = sessionConfig.Hooks,
            InfiniteSessions = sessionConfig.InfiniteSessions,
            McpServers = sessionConfig.McpServers,
            Model = sessionConfig.Model,
            ModelCapabilities = sessionConfig.ModelCapabilities,
            OnElicitationRequest = sessionConfig.OnElicitationRequest,
            OnEvent = sessionConfig.OnEvent,
            OnPermissionRequest = sessionConfig.OnPermissionRequest,
            OnUserInputRequest = sessionConfig.OnUserInputRequest,
            Provider = sessionConfig.Provider,
            ReasoningEffort = sessionConfig.ReasoningEffort,
            SessionId = sessionConfig.SessionId,
            SkillDirectories = sessionConfig.SkillDirectories,
            Streaming = true,
            SystemMessage = sessionConfig.SystemMessage,
            Tools = sessionConfig.Tools,
            WorkingDirectory = sessionConfig.WorkingDirectory,
        };
}
