using Microsoft.Agents.AI;

namespace OpenHub.Agents.Tests;

public sealed class AIAgentExtensionsTests
{
    [Fact]
    public void AsTaskAgent_ThrowsWhenAgentIsNull()
    {
        AIAgent? agent = null;

        Assert.Throws<ArgumentNullException>(() => AIAgentExtensions.AsTaskAgent(agent!));
    }
}
