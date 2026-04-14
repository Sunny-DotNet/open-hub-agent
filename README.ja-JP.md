# OpenHub.Agents

[ English ](README.md) | [ 简体中文 ](README.zh-CN.md) | [ **日本語** ](README.ja-JP.md) | [ 한국어 ](README.ko-KR.md)

---

OpenHub.Agents は、[Microsoft Agents SDK](https://github.com/microsoft/agents) と [GitHub Copilot SDK](https://github.com/github/copilot-sdk) の上に構築された、タスク指向の抽象レイヤーです。複数の AI バックエンドを統一された `ITaskAgent` API で包み、タスク作成とストリーミングイベント購読を同じ方法で扱えるようにします。

## 特徴

- タスク作成とストリーミング購読を統一する `ITaskAgent` 抽象
- `netstandard2.0`、`netstandard2.1`、`net8.0`、`net9.0`、`net10.0` をサポート
- GitHub Copilot の 2 つの実行モード
  - セッション共有モード（会話コンテキストを保持）
  - ファクトリーモード（タスクごとに新しいセッションを作成）
- reasoning、content、tool call、media、usage を扱う Rx ベースのストリーミングモデル
- `Directory.Packages.props` による集中パッケージ管理

## インストール

```powershell
dotnet add package OpenHub.Agents.Abstractions
dotnet add package OpenHub.Agents.AIAgent
dotnet add package OpenHub.Agents.GitHubCopilot
```

必要なものだけを導入できます。

- `OpenHub.Agents.Abstractions`：契約とモデル
- `OpenHub.Agents.AIAgent`：`Microsoft.Agents.AI.AIAgent` のアダプター
- `OpenHub.Agents.GitHubCopilot`：GitHub Copilot 連携

## プロジェクト構成

| プロジェクト | 役割 |
| --- | --- |
| `src/OpenHub.Agents.Abstractions` | task-agent のコア契約とイベントモデル |
| `src/OpenHub.Agents.AIAgent` | `Microsoft.Agents.AI.AIAgent` を `ITaskAgent` に適応 |
| `src/OpenHub.Agents.GitHubCopilot` | 共有 session / タスク単位 session の GitHub Copilot アダプター |
| `samples/OpenHub.Agents.Sample.Console` | OpenAI + GitHub Copilot のコンソールサンプル |
| `tests/OpenHub.Agents.Tests` | xUnit テストスイート |

## 依存ライブラリ

### ランタイム / コアパッケージ

| パッケージ | 用途 | リポジトリ |
| --- | --- | --- |
| [GitHub.Copilot.SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK) | GitHub Copilot の session を作成・開始・管理するためのネイティブ .NET SDK。 | [github/copilot-sdk](https://github.com/github/copilot-sdk) |
| [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI) | `AIAgent` パイプラインの表現と実行に使う Microsoft Agents SDK のメインパッケージ。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Agents.AI.Abstractions) | 契約レイヤーで使う Microsoft Agents SDK の共通抽象。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.GitHub.Copilot](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot) | GitHub Copilot 関連機能のための Microsoft Agents 統合パッケージ。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Agents.AI.OpenAI) | サンプルで使う OpenAI アダプターパッケージ。 | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Bcl.AsyncInterfaces](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces) | 古いターゲットフレームワーク向けの async interface 互換パッケージ。 | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions) | `ChatMessage`、`UsageContent`、tool-call content などの共通 AI 抽象。 | [dotnet/extensions](https://github.com/dotnet/extensions) |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) | AIAgent 統合レイヤーで使う DI 抽象。 | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | AIAgent 統合レイヤーで使うロギング抽象。 | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [System.Reactive](https://www.nuget.org/packages/System.Reactive) | タスクおよびイベントストリームに使う Rx.NET 実装。 | [dotnet/reactive](https://github.com/dotnet/reactive) |

### テスト / ツール用パッケージ

| パッケージ | 用途 | リポジトリ |
| --- | --- | --- |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) | テスト実行時のコードカバレッジ収集。 | [coverlet-coverage/coverlet](https://github.com/coverlet-coverage/coverlet) |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | `dotnet test` のテストホストと検出基盤。 | [microsoft/vstest](https://github.com/microsoft/vstest) |
| [xunit](https://www.nuget.org/packages/xunit) | リポジトリで採用しているユニットテストフレームワーク。 | [xunit/xunit](https://github.com/xunit/xunit) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) | Visual Studio と `dotnet test` 向けの xUnit ランナー統合。 | [xunit/xunit](https://github.com/xunit/xunit) |

## ビルドとテスト

```powershell
dotnet build OpenHub.Agents.slnx
dotnet test tests\OpenHub.Agents.Tests\OpenHub.Agents.Tests.csproj
```

## クイックスタート

### `AIAgent` をラップする

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

### GitHub Copilot をラップする

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

GitHub Copilot には 2 つの使い方があります。

- `CopilotSession.AsTaskAgent(...)`：1 つの session を再利用し、会話状態を維持
- `CopilotClient.AsTaskAgent(...)`：タスクごとに新しい session を作成

## メモ

- `OpenHub.Agents.GitHubCopilot` は現在、プレビュー版の `Microsoft.Agents.AI.GitHub.Copilot` に依存しています。
- パッケージバージョンは `Directory.Packages.props` で一元管理されています。
- リポジトリ全体のビルド規約は `Directory.Build.props` と `.editorconfig` にあります。
