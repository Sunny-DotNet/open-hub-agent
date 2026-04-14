using Microsoft.Agents.AI;
using System;

namespace OpenHub.Agents;

public static class AIAgentExtensions
{
    public static ITaskAgent AsTaskAgent(this AIAgent agent)
    {
        if (agent is null)
        {
            throw new ArgumentNullException(nameof(agent));
        }

        return new DefaultTaskAgent(agent);
    }
}
