# OpenHub.Agents

[ English ](README.md) | [ **简体中文** ](README.zh-CN.md) | [ 日本語 ](README.ja-JP.md) | [ Français ](README.fr-FR.md)

---

OpenHub.Agents 是一个构建在 [Microsoft Agents SDK](https://github.com/microsoft/agents) 和 [GitHub Copilot SDK](https://github.com/github/copilot-sdk) 之上的任务型抽象层。它把不同 AI 后端统一包装成 `ITaskAgent` API，让调用方可以用一致的方式创建任务并消费流式事件。

## 特性

- 统一的 `ITaskAgent` 抽象，负责任务创建和流式订阅
- 支持 `netstandard2.0`、`netstandard2.1`、`net8.0`、`net9.0`、`net10.0`
- 两种 GitHub Copilot 执行模式：
  - 共享 session，保留会话上下文
  - 工厂模式，每个任务创建独立 session
- 基于 Rx 的流式模型，覆盖 reasoning、content、tool call、media、usage
- 使用 `Directory.Packages.props` 做集中包管理

## 安装

```powershell
dotnet add package OpenHub.Agents.Abstractions
dotnet add package OpenHub.Agents.AIAgent
dotnet add package OpenHub.Agents.GitHubCopilot
```

按需安装：

- `OpenHub.Agents.Abstractions`：契约与事件模型
- `OpenHub.Agents.AIAgent`：适配 `Microsoft.Agents.AI.AIAgent`
- `OpenHub.Agents.GitHubCopilot`：接入 GitHub Copilot

## 项目结构

| 项目 | 作用 |
| --- | --- |
| `src/OpenHub.Agents.Abstractions` | 核心 task-agent 契约与事件模型 |
| `src/OpenHub.Agents.AIAgent` | 将 `Microsoft.Agents.AI.AIAgent` 适配成 `ITaskAgent` |
| `src/OpenHub.Agents.GitHubCopilot` | GitHub Copilot 的共享 session / 每任务 session 两种适配方式 |
| `samples/OpenHub.Agents.Sample.Console` | OpenAI + GitHub Copilot 控制台示例 |
| `tests/OpenHub.Agents.Tests` | xUnit 测试项目 |

## 依赖说明

### 运行时与核心依赖

| 包 | 用途 | 仓库 |
| --- | --- | --- |
| [GitHub.Copilot.SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK) | 原生 .NET SDK，用于创建、启动和管理 GitHub Copilot session。 | [github/copilot-sdk](https://github.com/github/copilot-sdk) |
| [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI) | Microsoft Agents SDK 主包，用来表示和运行 `AIAgent` 管线。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Agents.AI.Abstractions) | Microsoft Agents SDK 的共享抽象层，供契约层使用。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.GitHub.Copilot](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot) | 与 GitHub Copilot 相关的 Microsoft Agents 集成包。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Agents.AI.OpenAI) | OpenAI 适配包，主要供 sample 使用。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Bcl.AsyncInterfaces](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces) | 为老目标框架补充异步接口兼容能力。 | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions) | `ChatMessage`、`UsageContent`、tool-call content 等共享 AI 抽象。 | [dotnet/extensions](https://github.com/dotnet/extensions) |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) | AIAgent 集成层使用的依赖注入抽象。 | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | AIAgent 集成层使用的日志抽象。 | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [System.Reactive](https://www.nuget.org/packages/System.Reactive) | Rx.NET 实现，用于任务与事件流的 observable 模型。 | [dotnet/reactive](https://github.com/dotnet/reactive) |

### 测试与工具依赖

| 包 | 用途 | 仓库 |
| --- | --- | --- |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) | 测试覆盖率采集器。 | [coverlet-coverage/coverlet](https://github.com/coverlet-coverage/coverlet) |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | `dotnet test` 所需的测试宿主与发现基础设施。 | [microsoft/vstest](https://github.com/microsoft/vstest) |
| [xunit](https://www.nuget.org/packages/xunit) | 仓库使用的单元测试框架。 | [xunit/xunit](https://github.com/xunit/xunit) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) | xUnit 在 Visual Studio 和 `dotnet test` 中的运行器集成。 | [xunit/xunit](https://github.com/xunit/xunit) |

## 构建与测试

```powershell
dotnet build OpenHub.Agents.slnx
dotnet test tests\OpenHub.Agents.Tests\OpenHub.Agents.Tests.csproj
```

## 快速开始

### 包装一个 `AIAgent`

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

### 包装 GitHub Copilot

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

GitHub Copilot 支持两种用法：

- `CopilotSession.AsTaskAgent(...)`：复用同一个 session，并保留会话状态
- `CopilotClient.AsTaskAgent(...)`：每个任务创建一个全新 session

## 备注

- `OpenHub.Agents.GitHubCopilot` 当前依赖预览版包 `Microsoft.Agents.AI.GitHub.Copilot`。
- 包版本统一由 `Directory.Packages.props` 管理。
- 仓库级构建约定位于 `Directory.Build.props` 与 `.editorconfig`。
