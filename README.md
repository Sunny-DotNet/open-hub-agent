# OpenHub.Agents

[ **English** ](README.md) | [ 简体中文 ](README.zh-CN.md) | [ 日本語 ](README.ja-JP.md) | [ Français ](README.fr-FR.md)

---

OpenHub.Agents is a task-oriented abstraction layer built on top of the [Microsoft Agents SDK](https://github.com/microsoft/agents) and the [GitHub Copilot SDK](https://github.com/github/copilot-sdk). It wraps different AI backends behind a unified `ITaskAgent` API so callers can create tasks and consume streaming updates in one consistent way.

## Features

- Unified `ITaskAgent` abstraction for task creation and streaming subscriptions
- Multi-targeting support for `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`, and `net10.0`
- Two GitHub Copilot execution modes:
  - shared session with preserved conversation context
  - factory mode with a fresh session per task
- Rx-based streaming model for reasoning, content, tool calls, media, and usage updates
- Central package management with `Directory.Packages.props`

## Installation

```powershell
dotnet add package OpenHub.Agents.Abstractions
dotnet add package OpenHub.Agents.AIAgent
dotnet add package OpenHub.Agents.GitHubCopilot
```

Install only the packages you need:

- `OpenHub.Agents.Abstractions` for contracts and models
- `OpenHub.Agents.AIAgent` when you want to adapt `Microsoft.Agents.AI.AIAgent`
- `OpenHub.Agents.GitHubCopilot` when you want GitHub Copilot integration

## Projects

| Project | Purpose |
| --- | --- |
| `src/OpenHub.Agents.Abstractions` | Core task-agent contracts and event models |
| `src/OpenHub.Agents.AIAgent` | Adapter from `Microsoft.Agents.AI.AIAgent` to `ITaskAgent` |
| `src/OpenHub.Agents.GitHubCopilot` | Adapters for shared-session and per-task GitHub Copilot execution |
| `samples/OpenHub.Agents.Sample.Console` | Console sample covering OpenAI + GitHub Copilot flows |
| `tests/OpenHub.Agents.Tests` | xUnit test suite |

## Dependencies Explained

### Runtime and core packages

| Package | Purpose | Repository |
| --- | --- | --- |
| [GitHub.Copilot.SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK) | Native .NET SDK used to create, start, and manage GitHub Copilot sessions. | [github/copilot-sdk](https://github.com/github/copilot-sdk) |
| [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI) | Main Microsoft Agents SDK package used to represent and run `AIAgent` pipelines. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Agents.AI.Abstractions) | Shared abstractions from the Microsoft Agents SDK used by the contract layer. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.GitHub.Copilot](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot) | Microsoft Agents integration package for GitHub Copilot-related capabilities. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Agents.AI.OpenAI) | OpenAI adapter package used by the sample application. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Bcl.AsyncInterfaces](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces) | Compatibility package for async interfaces on older target frameworks. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions) | Shared AI content abstractions such as `ChatMessage`, `UsageContent`, and tool-call content types. | [dotnet/extensions](https://github.com/dotnet/extensions) |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) | Dependency injection abstractions used by the AIAgent integration layer. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | Logging abstractions used by the AIAgent integration layer. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [System.Reactive](https://www.nuget.org/packages/System.Reactive) | Rx.NET implementation used for observable task and event streams. | [dotnet/reactive](https://github.com/dotnet/reactive) |

### Testing and tooling packages

| Package | Purpose | Repository |
| --- | --- | --- |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) | Code coverage collector for test runs. | [coverlet-coverage/coverlet](https://github.com/coverlet-coverage/coverlet) |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | Test host and discovery infrastructure for `dotnet test`. | [microsoft/vstest](https://github.com/microsoft/vstest) |
| [xunit](https://www.nuget.org/packages/xunit) | Unit testing framework used by the repository. | [xunit/xunit](https://github.com/xunit/xunit) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) | Visual Studio and `dotnet test` runner integration for xUnit. | [xunit/xunit](https://github.com/xunit/xunit) |

## Build and test

```powershell
dotnet build OpenHub.Agents.slnx
dotnet test tests\OpenHub.Agents.Tests\OpenHub.Agents.Tests.csproj
```

## Quick Start

### Wrap an `AIAgent`

```csharp
ITaskAgent taskAgent = chatClient
    .AsAIAgent(options)
    .AsTaskAgent();

CreateTaskResponse response = await taskAgent.CreateTaskAsync(new("Summarize this repo."));

using IDisposable contentSubscription = response.Subscriber.TaskContentChunk.Subscribe(chunk =>
{
    Console.Write(chunk.Content);
});

await response.Subscriber.WaitForCompletionAsync();
```

### Wrap GitHub Copilot

```csharp
await using CopilotClient copilotClient = new();
await copilotClient.StartAsync();

SessionConfig sessionConfig = new()
{
    Streaming = true,
    Model = "claude-sonnet-4.6",
    OnPermissionRequest = PermissionHandler.ApproveAll
};

ITaskAgent factoryAgent = copilotClient.AsTaskAgent(sessionConfig);
```

GitHub Copilot can be used in two ways:

- `CopilotSession.AsTaskAgent(...)`: reuse one session and preserve conversation state
- `CopilotClient.AsTaskAgent(...)`: create a fresh session per task

## Notes

- `OpenHub.Agents.GitHubCopilot` currently depends on preview package `Microsoft.Agents.AI.GitHub.Copilot`.
- Package versions are managed centrally via `Directory.Packages.props`.
- Repository-wide build conventions live in `Directory.Build.props` and `.editorconfig`.
