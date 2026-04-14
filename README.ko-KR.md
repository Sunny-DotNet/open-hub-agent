# OpenHub.Agents

[ English ](README.md) | [ 简体中文 ](README.zh-CN.md) | [ 日本語 ](README.ja-JP.md) | [ **한국어** ](README.ko-KR.md)

---

OpenHub.Agents는 [Microsoft Agents SDK](https://github.com/microsoft/agents)와 [GitHub Copilot SDK](https://github.com/github/copilot-sdk) 위에 구축된 작업 중심 추상화 레이어입니다. 여러 AI 백엔드를 하나의 `ITaskAgent` API 뒤로 감싸서, 작업 생성과 스트리밍 이벤트 구독을 같은 방식으로 처리할 수 있게 합니다.

## 주요 기능

- 작업 생성과 스트리밍 구독을 위한 통합 `ITaskAgent` 추상화
- `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`, `net10.0` 지원
- GitHub Copilot 실행 모드 2종
  - 대화 문맥을 유지하는 공유 session 모드
  - 작업마다 새 session 을 만드는 팩토리 모드
- reasoning, content, tool call, media, usage 를 다루는 Rx 기반 스트리밍 모델
- `Directory.Packages.props` 기반 중앙 패키지 관리

## 설치

```powershell
dotnet add package OpenHub.Agents.Abstractions
dotnet add package OpenHub.Agents.AIAgent
dotnet add package OpenHub.Agents.GitHubCopilot
```

필요한 패키지만 선택해서 설치할 수 있습니다.

- `OpenHub.Agents.Abstractions`: 계약과 모델
- `OpenHub.Agents.AIAgent`: `Microsoft.Agents.AI.AIAgent` 어댑터
- `OpenHub.Agents.GitHubCopilot`: GitHub Copilot 연동

## 프로젝트 구성

| 프로젝트 | 역할 |
| --- | --- |
| `src/OpenHub.Agents.Abstractions` | 핵심 task-agent 계약과 이벤트 모델 |
| `src/OpenHub.Agents.AIAgent` | `Microsoft.Agents.AI.AIAgent`를 `ITaskAgent`로 어댑트 |
| `src/OpenHub.Agents.GitHubCopilot` | 공유 session / 작업별 session 방식의 GitHub Copilot 어댑터 |
| `samples/OpenHub.Agents.Sample.Console` | OpenAI + GitHub Copilot 콘솔 샘플 |
| `tests/OpenHub.Agents.Tests` | xUnit 테스트 프로젝트 |

## 사용 라이브러리 설명

### 런타임 / 핵심 패키지

| 패키지 | 용도 | 저장소 |
| --- | --- | --- |
| [GitHub.Copilot.SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK) | GitHub Copilot session 생성, 시작, 관리에 사용하는 네이티브 .NET SDK입니다. | [github/copilot-sdk](https://github.com/github/copilot-sdk) |
| [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI) | `AIAgent` 파이프라인을 표현하고 실행하는 Microsoft Agents SDK 메인 패키지입니다. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Agents.AI.Abstractions) | 계약 레이어에서 사용하는 Microsoft Agents SDK 공용 추상화입니다. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.GitHub.Copilot](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot) | GitHub Copilot 관련 기능을 위한 Microsoft Agents 통합 패키지입니다. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Agents.AI.OpenAI) | 샘플 애플리케이션에서 사용하는 OpenAI 어댑터 패키지입니다. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Bcl.AsyncInterfaces](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces) | 구형 대상 프레임워크에서 async interface 호환성을 제공하는 패키지입니다. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions) | `ChatMessage`, `UsageContent`, tool-call content 등의 공용 AI 추상화를 제공합니다. | [dotnet/extensions](https://github.com/dotnet/extensions) |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) | AIAgent 통합 레이어에서 사용하는 DI 추상화입니다. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | AIAgent 통합 레이어에서 사용하는 로깅 추상화입니다. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [System.Reactive](https://www.nuget.org/packages/System.Reactive) | 작업 및 이벤트 스트림을 위한 Rx.NET 구현입니다. | [dotnet/reactive](https://github.com/dotnet/reactive) |

### 테스트 / 도구 패키지

| 패키지 | 용도 | 저장소 |
| --- | --- | --- |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) | 테스트 실행 시 코드 커버리지를 수집합니다. | [coverlet-coverage/coverlet](https://github.com/coverlet-coverage/coverlet) |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | `dotnet test` 를 위한 테스트 호스트 및 발견 인프라입니다. | [microsoft/vstest](https://github.com/microsoft/vstest) |
| [xunit](https://www.nuget.org/packages/xunit) | 이 저장소에서 사용하는 단위 테스트 프레임워크입니다. | [xunit/xunit](https://github.com/xunit/xunit) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) | Visual Studio 및 `dotnet test` 용 xUnit 러너 통합입니다. | [xunit/xunit](https://github.com/xunit/xunit) |

## 빌드와 테스트

```powershell
dotnet build OpenHub.Agents.slnx
dotnet test tests\OpenHub.Agents.Tests\OpenHub.Agents.Tests.csproj
```

## 빠른 시작

### `AIAgent` 래핑

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

### GitHub Copilot 래핑

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

GitHub Copilot 는 두 가지 방식으로 사용할 수 있습니다.

- `CopilotSession.AsTaskAgent(...)`: 하나의 session 을 재사용하고 대화 상태를 유지
- `CopilotClient.AsTaskAgent(...)`: 작업마다 새 session 생성

## 참고

- `OpenHub.Agents.GitHubCopilot` 는 현재 프리뷰 패키지 `Microsoft.Agents.AI.GitHub.Copilot` 에 의존합니다.
- 패키지 버전은 `Directory.Packages.props` 로 중앙 관리됩니다.
- 저장소 전반의 빌드 규칙은 `Directory.Build.props` 와 `.editorconfig` 에 정의되어 있습니다.
