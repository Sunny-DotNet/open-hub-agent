using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenHub.Agents;
using System.ClientModel;
using System.ComponentModel;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not set.");

var baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o";

var key = new ApiKeyCredential(apiKey);
OpenAIClientOptions? options = null;
if (!string.IsNullOrWhiteSpace(baseUrl))
{
    baseUrl = baseUrl.TrimEnd('/').EndsWith("/v1", StringComparison.Ordinal) ? baseUrl : baseUrl + "/v1";
    options = new OpenAIClientOptions
    {
        Endpoint = new Uri(baseUrl),
    };
}

Console.Title = "Type 'exit' to quit.";

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";
ChatClientAgentOptions chatClientAgentOptions = new ChatClientAgentOptions
{
    ChatOptions = new()
    {
        Reasoning = new()
        {
            Effort = ReasoningEffort.Medium,
            Output = ReasoningOutput.Full
        },
        Instructions = "you are a great assistant.",
        Tools = new List<AITool>
        {
            AIFunctionFactory.Create(GetWeather)
        }
    },
    Name = "Monica"
};

var split = new string('-', 32);

await RunOpenAIDemoAsync();
await RunCopilotDemosAsync();

async Task RunOpenAIDemoAsync()
{
#pragma warning disable OPENAI001
    ITaskAgent agent = new OpenAIClient(key, options)
        .GetChatClient(model)
        .AsAIAgent(chatClientAgentOptions)
        .AsTaskAgent();
#pragma warning restore OPENAI001

    try
    {
        await RunInteractiveLoopAsync(agent, $"To AI Agent({model})：");
    }
    finally
    {
        await agent.DisposeAsync();
    }
}

async Task RunCopilotDemosAsync()
{
    await using CopilotClient copilotClient = new();
    await copilotClient.StartAsync();

    SessionConfig sessionConfig = new()
    {
        Streaming = true,
        ReasoningEffort = "medium",
        Model = "claude-sonnet-4.6",
        OnPermissionRequest = PermissionHandler.ApproveAll
    };

    // Shared-session mode keeps Copilot conversation state across tasks.
    await using CopilotSession session = await copilotClient.CreateSessionAsync(sessionConfig);
    ITaskAgent sharedSessionAgent = session.AsTaskAgent();
    try
    {
        await RunInteractiveLoopAsync(sharedSessionAgent, $"To copilot with session({sessionConfig.Model})：");
    }
    finally
    {
        await sharedSessionAgent.DisposeAsync();
        await copilotClient.DeleteSessionAsync(session.SessionId);
    }

    // Factory mode opens a fresh Copilot session for each task.
    ITaskAgent factoryAgent = copilotClient.AsTaskAgent(sessionConfig);
    try
    {
        await RunInteractiveLoopAsync(factoryAgent, $"To copilot({sessionConfig.Model})：");
    }
    finally
    {
        await factoryAgent.DisposeAsync();
    }
}

async Task RunInteractiveLoopAsync(ITaskAgent taskAgent, string prompt)
{
    var input = string.Empty;
    CancellationTokenSource? activeTaskCancellationSource = null;
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        if (activeTaskCancellationSource is null)
        {
            return;
        }

        // When a task is running, Ctrl+C cancels that task instead of killing the whole sample.
        eventArgs.Cancel = true;
        activeTaskCancellationSource.Cancel();
    };

    Console.CancelKeyPress += cancelHandler;

    try
    {
        do
        {
            Console.Write(prompt);
            input = Console.ReadLine() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(input) && !input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                using CancellationTokenSource taskCancellationSource = new();
                activeTaskCancellationSource = taskCancellationSource;

                try
                {
                    var response = await taskAgent.CreateTaskAsync(new(input), taskCancellationSource.Token);

                    using var newPartSubscription = response.Subscriber.NewPart.Subscribe(part =>
                    {
                        Console.WriteLine();
                        Console.WriteLine($"[New Part] {part}:");
                    });
                    using var reasoningChunkSubscription = response.Subscriber.TaskReasoningChunk.Subscribe(part =>
                    {
                        Console.Write(part.Content);
                    });

                    using var contentChunkSubscription = response.Subscriber.TaskContentChunk.Subscribe(part =>
                    {
                        Console.Write(part.Content);
                    });

                    using var toolCallRequestSubscription = response.Subscriber.TaskToolCallRequest.Subscribe(part =>
                    {
                        Console.Write(part.ToolName);
                    });
                    using var usageUpdatedSubscription = response.Subscriber.TaskUsageUpdated.Subscribe(part =>
                    {
                        Console.WriteLine();
                        Console.WriteLine($"{split}INPUT\t{part.UsageContent.Details.InputTokenCount}{split}");
                        Console.WriteLine($"{split}OUTPUT\t{part.UsageContent.Details.OutputTokenCount}{split}");
                        Console.WriteLine($"{split}Cached\t{part.UsageContent.Details.CachedInputTokenCount}{split}");
                        Console.WriteLine($"{split}TOTAL\t{part.UsageContent.Details.TotalTokenCount}{split}");
                    });

                    await response.Subscriber.WaitForCompletionAsync(taskCancellationSource.Token);
                    Console.WriteLine();
                    Console.WriteLine($"{split}END{split}");
                }
                catch (OperationCanceledException) when (taskCancellationSource.IsCancellationRequested)
                {
                    Console.WriteLine();
                    Console.WriteLine($"{split}CANCELLED{split}");
                }
                finally
                {
                    activeTaskCancellationSource = null;
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine();
                }
            }
        } while (!input.Equals("exit", StringComparison.OrdinalIgnoreCase));
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
}
