using GitHub.Copilot.SDK;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace OpenHub.Agents.Tests;

public sealed class GitHubCopilotExtensionsTests
{
    [Fact]
    public void Session_AsTaskAgent_ThrowsWhenSessionIsNull()
    {
        CopilotSession? session = null;

        Assert.Throws<ArgumentNullException>(() => SessionExtensions.AsTaskAgent(session!));
    }

    [Fact]
    public async Task Session_AsTaskAgent_ReturnsSharedSessionAgent()
    {
        CopilotSession session = (CopilotSession)RuntimeHelpers.GetUninitializedObject(typeof(CopilotSession));

        await using ITaskAgent taskAgent = session.AsTaskAgent();

        Assert.IsType<SharedCopilotSessionTaskAgent>(taskAgent);
    }

    [Fact]
    public void Client_AsTaskAgent_ThrowsWhenClientIsNull()
    {
        CopilotClient? client = null;
        SessionConfig sessionConfig = new() { OnPermissionRequest = PermissionHandler.ApproveAll };

        Assert.Throws<ArgumentNullException>(() => CopilotClientExtensions.AsTaskAgent(client!, sessionConfig));
    }

    [Fact]
    public async Task Client_AsTaskAgent_ThrowsWhenSessionConfigIsNull()
    {
        await using CopilotClient client = new();

        Assert.Throws<ArgumentNullException>(() => client.AsTaskAgent(sessionConfig: null!));
    }

    [Fact]
    public async Task Client_AsTaskAgent_ThrowsWhenPermissionHandlerIsMissing()
    {
        await using CopilotClient client = new();

        Assert.Throws<ArgumentException>(() => client.AsTaskAgent(new SessionConfig()));
    }

    [Fact]
    public async Task Client_AsTaskAgent_ReturnsFactoryAgentWithoutMutatingTheInputConfig()
    {
        await using CopilotClient client = new();
        SessionConfig sessionConfig = new()
        {
            Model = "gpt-4",
            Streaming = false,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        };

        await using ITaskAgent taskAgent = client.AsTaskAgent(sessionConfig);

        Assert.IsType<FactoryCopilotSessionTaskAgent>(taskAgent);
        Assert.False(sessionConfig.Streaming);
        Assert.Equal("gpt-4", sessionConfig.Model);
    }
}
