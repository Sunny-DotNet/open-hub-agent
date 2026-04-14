# Copilot Instructions — OpenHub.Agents

## Overview

OpenHub.Agents 是一个 .NET 类库，基于 [microsoft/agent-framework](https://github.com/microsoft/agent-framework) 封装统一的 Task-Agent 抽象层。用户只需调用 `AsTaskAgent()` 扩展方法即可将不同 AI 后端（Microsoft.Agents.AI、GitHub Copilot SDK）包装为统一的 `ITaskAgent` 接口，通过 Rx.NET `IObservable<T>` 消费流式事件。

## Build & Test

```bash
# 构建整个解决方案
dotnet build OpenHub.Agents.slnx

# 运行单元测试
dotnet test tests/OpenHub.Agents.Tests/OpenHub.Agents.Tests.csproj
```

- .NET SDK 10+ required（libraries multi-target netstandard2.0/2.1 + net8.0/9.0/10.0；GitHubCopilot 仅 net8.0+）
- NuGet 包版本由 `Directory.Packages.props` 集中管理，不要在 .csproj 里写 `Version`
- CI 环境下 `ContinuousIntegrationBuild` 自动启用

## Solution Structure

```
OpenHub.Agents.slnx                 # 解决方案
Directory.Build.props                # 全局构建属性
Directory.Packages.props             # 集中包版本管理
src/
  OpenHub.Agents.Abstractions/       # 核心抽象：ITaskAgent, ITaskSubscriber, Models
  OpenHub.Agents.AIAgent/            # microsoft/agent-framework AIAgent 适配器
  OpenHub.Agents.GitHubCopilot/      # GitHub Copilot SDK 适配器（shared session / factory）
samples/
  OpenHub.Agents.Sample.Console/     # 控制台演示（OpenAI + Copilot 两种模式）
tests/
  OpenHub.Agents.Tests/              # xUnit 单元测试
```

### Dependency Chain

```
Abstractions  ← AIAgent  ← GitHubCopilot
                          ← Tests (references all three)
                          ← Sample.Console
```

## Coding Conventions

### Namespace

- `RootNamespace` 为 `OpenHub`（在 Directory.Build.props 中设置）。
- 所有公开类型统一位于 `namespace OpenHub.Agents;`，不按程序集分子命名空间。

### Language & Analysis

- 始终使用最新 C# 语言版本（`LangVersion=latest`）。
- 启用 nullable reference types，所有新代码都应标注 nullability。
- `TreatWarningsAsErrors=true`：所有警告视为错误，不允许残留 warnings。
- `EnforceCodeStyleInBuild=true` + `EnableNETAnalyzers=true`（AnalysisLevel=latest）。
- 仅豁免 CS1591（缺少 XML 文档注释）。

### Naming

| 类型 | 后缀 | 示例 |
|------|------|------|
| 接口 | `I` 前缀 | `ITaskAgent`, `ITaskSubscriber` |
| 事件 record | `Event` | `TaskContentChunkEvent`, `TaskStatusChangedEvent` |
| 请求/响应 record | `Request` / `Response` | `CreateTaskRequest`, `CreateTaskResponse` |
| 订阅者 | `Subscriber` | `CopilotTaskSubscriber`, `ChatClientAgentTaskSubscriber` |
| Agent 实现 | `Agent` | `DefaultTaskAgent`, `SharedCopilotSessionTaskAgent` |
| 扩展方法类 | `Extensions` | `AIAgentExtensions`, `SessionExtensions` |

### Async & Disposal

- 所有 agent 实现 `IAsyncDisposable`。
- 使用 `CancellationTokenSource` 管理优雅取消——`DisposeAsync()` 先取消再 await 运行中任务。
- 区分 "owns" / "does not own" 语义（如 `ownsSession`），非拥有者不在 Dispose 时释放外部资源。
- netstandard2.0 兼容：低于 net8.0 时用自定义 `WaitAsync()` polyfill（见 `TaskWaitExtensions`）。

### Reactive (Rx.NET)

- 每个 `ITaskSubscriber` 暴露 7 个 `IObservable<T>` 流。
- 使用 `ReplaySubject<T>` 保证晚订阅者也能收到历史事件。
- `IAgentSubscriber` 使用 `ReplaySubject<TaskStatusChangedEvent>(bufferSize: 64)`。

### Thread Safety

- `TaskSubscriberBase` 子类（`ChatClientAgentTaskSubscriber`, `CopilotTaskSubscriber`）用 `lock(_syncRoot)` 保护内部状态。
- `SharedCopilotSessionTaskAgent` 通过 FIFO 异步队列串行化共享 session 上的任务执行，避免并发调用打乱会话顺序。
- Agent 基类的 `_tasks` / `_subscribers` 使用 `ConcurrentDictionary`。

### Extension Methods

- 公开入口全部通过扩展方法：`agent.AsTaskAgent()`, `session.AsTaskAgent()`, `client.AsTaskAgent(config)`。
- 扩展方法必须对 `null` 参数抛出 `ArgumentNullException`。
- Factory 入口（`CopilotClient.AsTaskAgent`）自动克隆 `SessionConfig` 并强制 `Streaming=true`，不修改调用方传入的原始配置。

## Architecture Decisions

### Dual Copilot Entry Points

| 模式 | 入口 | 行为 |
|------|------|------|
| Shared Session | `CopilotSession.AsTaskAgent(ownsSession)` | 复用同一 session，串行执行 task，保留会话上下文 |
| Factory (Per-Task) | `CopilotClient.AsTaskAgent(config, ownsClient)` | 每个 task 创建独立 session，支持并行 |

### Testability

- `ICopilotSessionConnection` 抽象了 `CopilotSession`（sealed 不可 mock）。
- `InternalsVisibleTo("OpenHub.Agents.Tests")` 允许测试访问 internal 类型。
- 测试使用 `FakeCopilotSessionConnection` 模拟事件发布和消息录制。

## Testing Conventions

- 框架：**xUnit 2.9.3** + **Microsoft.NET.Test.Sdk 18.4.0**。
- 测试项目仅面向 `net10.0`。
- 异步方法断言使用 `Assert.ThrowsAsync`（xUnit 3.x 的 `xUnit2014` 规则要求）。
- 测试帮助类集中在 `GitHubCopilotTestHelpers.cs`：`FakeCopilotSessionConnection`, `GitHubCopilotTestEvents`, `TaskStatusRecorder`, `ObservableReplay`。
- `TaskStatus` 与 `System.Threading.Tasks.TaskStatus` 有歧义时，在测试中用 `using AgentTaskStatus = OpenHub.Agents.Models.TaskStatus;` 别名。

## Key SDK Gotchas

- `GitHub.Copilot.SDK` 的 `SessionConfig` 构造函数为 `internal`——克隆时须逐属性复制。
- `AssistantUsageData.Model` 和 `SessionErrorData.ErrorType` 是 `required` 成员，创建测试实例时必须赋值。
- `Microsoft.Agents.AI.GitHub.Copilot` 版本为 preview，API 可能变动。
