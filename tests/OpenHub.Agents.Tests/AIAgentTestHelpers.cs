using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace OpenHub.Agents.Tests;

internal static class AIAgentTestUpdates
{
    public static AgentResponseUpdate CreateAssistantUpdate(params AIContent[] contents)
        => new(ChatRole.Assistant, new List<AIContent>(contents));
}

internal static class AIAgentTestStreams
{
    public static async IAsyncEnumerable<AgentResponseUpdate> ReturnUpdates(
        IEnumerable<AgentResponseUpdate> updates,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (AgentResponseUpdate update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
            await Task.Yield();
        }
    }

    public static async IAsyncEnumerable<AgentResponseUpdate> WaitForCancellation(
        TaskCompletionSource started,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var never = false;
        if (never)
        {
            yield return AIAgentTestUpdates.CreateAssistantUpdate();
        }

        started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        yield break;
    }

    public static async IAsyncEnumerable<AgentResponseUpdate> Throw(
        Exception exception,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var never = false;
        if (never)
        {
            yield return AIAgentTestUpdates.CreateAssistantUpdate();
        }

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw exception;
    }
}

// AIAgent is abstract, so tests use a tiny delegate-backed implementation to
// exercise the task-agent adapter without needing a real backend.
internal sealed class FakeAIAgent(
    Func<IReadOnlyList<ChatMessage>, CancellationToken, IAsyncEnumerable<AgentResponseUpdate>> runStreamingAsync) : AIAgent, IAsyncDisposable
{
    private readonly ConcurrentQueue<string?> _prompts = [];

    public IReadOnlyList<string?> Prompts => [.. _prompts];

    public bool Disposed { get; private set; }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<AgentSession>(new FakeAgentSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession? session,
        JsonSerializerOptions? serializerOptions,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(JsonDocument.Parse("{}").RootElement.Clone());

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement sessionState,
        JsonSerializerOptions? serializerOptions,
        CancellationToken cancellationToken)
        => ValueTask.FromResult<AgentSession>(new FakeAgentSession());

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Use RunStreamingAsync in tests.");

    protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        ChatMessage[] bufferedMessages = [.. messages];
        foreach (ChatMessage message in bufferedMessages)
        {
            _prompts.Enqueue(message.Text);
        }

        return runStreamingAsync(bufferedMessages, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeAgentSession : AgentSession;
