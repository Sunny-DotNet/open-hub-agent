# OpenHub.Agents

[ English ](README.md) | [ 简体中文 ](README.zh-CN.md) | [ 日本語 ](README.ja-JP.md) | [ **Français** ](README.fr-FR.md)

---

OpenHub.Agents est une couche d'abstraction orientee taches construite au-dessus du [Microsoft Agents SDK](https://github.com/microsoft/agents) et du [GitHub Copilot SDK](https://github.com/github/copilot-sdk). Elle encapsule differents backends d'IA derriere une API `ITaskAgent` unifiee afin de creer des taches et de consommer des mises a jour en flux de maniere coherente.

## Fonctionnalites

- Abstraction `ITaskAgent` unifiee pour la creation de taches et les abonnements en streaming
- Prise en charge de `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0` et `net10.0`
- Deux modes d'execution GitHub Copilot :
  - session partagee avec conservation du contexte de conversation
  - mode fabrique avec une nouvelle session pour chaque tache
- Modele de streaming base sur Rx pour le raisonnement, le contenu, les appels d'outils, les medias et l'utilisation
- Gestion centralisee des packages avec `Directory.Packages.props`

## Installation

```powershell
dotnet add package OpenHub.Agents.Abstractions
dotnet add package OpenHub.Agents.AIAgent
dotnet add package OpenHub.Agents.GitHubCopilot
```

Installez uniquement les packages dont vous avez besoin :

- `OpenHub.Agents.Abstractions` pour les contrats et les modeles
- `OpenHub.Agents.AIAgent` pour adapter `Microsoft.Agents.AI.AIAgent`
- `OpenHub.Agents.GitHubCopilot` pour l'integration GitHub Copilot

## Projets

| Projet | Raison d'etre |
| --- | --- |
| `src/OpenHub.Agents.Abstractions` | Contrats task-agent de base et modeles d'evenements |
| `src/OpenHub.Agents.AIAgent` | Adaptateur de `Microsoft.Agents.AI.AIAgent` vers `ITaskAgent` |
| `src/OpenHub.Agents.GitHubCopilot` | Adaptateurs GitHub Copilot en mode session partagee ou session par tache |
| `samples/OpenHub.Agents.Sample.Console` | Exemple console couvrant OpenAI + GitHub Copilot |
| `tests/OpenHub.Agents.Tests` | Suite de tests xUnit |

## Explication des dependances

### Packages runtime et coeur

| Package | Utilite | Depot |
| --- | --- | --- |
| [GitHub.Copilot.SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK) | SDK .NET natif utilise pour creer, demarrer et gerer les sessions GitHub Copilot. | [github/copilot-sdk](https://github.com/github/copilot-sdk) |
| [Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI) | Package principal du Microsoft Agents SDK utilise pour representer et executer les pipelines `AIAgent`. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Agents.AI.Abstractions) | Abstractions partagees du Microsoft Agents SDK utilisees par la couche de contrats. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.GitHub.Copilot](https://www.nuget.org/packages/Microsoft.Agents.AI.GitHub.Copilot) | Package d'integration Microsoft Agents pour les fonctionnalites liees a GitHub Copilot. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Agents.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Agents.AI.OpenAI) | Package d'adaptation OpenAI utilise par l'application d'exemple. | [microsoft/agents](https://github.com/microsoft/agents) |
| [Microsoft.Bcl.AsyncInterfaces](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces) | Package de compatibilite pour les interfaces asynchrones sur les frameworks plus anciens. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions) | Abstractions IA partagees comme `ChatMessage`, `UsageContent` et les types de contenu pour les appels d'outils. | [dotnet/extensions](https://github.com/dotnet/extensions) |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) | Abstractions d'injection de dependances utilisees par la couche d'integration AIAgent. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | Abstractions de journalisation utilisees par la couche d'integration AIAgent. | [dotnet/runtime](https://github.com/dotnet/runtime) |
| [System.Reactive](https://www.nuget.org/packages/System.Reactive) | Implementation Rx.NET utilisee pour les flux observables de taches et d'evenements. | [dotnet/reactive](https://github.com/dotnet/reactive) |

### Packages de test et d'outillage

| Package | Utilite | Depot |
| --- | --- | --- |
| [coverlet.collector](https://www.nuget.org/packages/coverlet.collector) | Collecteur de couverture de code pour les executions de tests. | [coverlet-coverage/coverlet](https://github.com/coverlet-coverage/coverlet) |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | Infrastructure d'hebergement et de decouverte des tests pour `dotnet test`. | [microsoft/vstest](https://github.com/microsoft/vstest) |
| [xunit](https://www.nuget.org/packages/xunit) | Framework de test unitaire utilise par le depot. | [xunit/xunit](https://github.com/xunit/xunit) |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio) | Integration du runner xUnit pour Visual Studio et `dotnet test`. | [xunit/xunit](https://github.com/xunit/xunit) |

## Compiler et tester

```powershell
dotnet build OpenHub.Agents.slnx
dotnet test tests\OpenHub.Agents.Tests\OpenHub.Agents.Tests.csproj
```

## Demarrage rapide

### Envelopper un `AIAgent`

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

### Envelopper GitHub Copilot

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

GitHub Copilot peut etre utilise de deux manieres :

- `CopilotSession.AsTaskAgent(...)` : reutiliser une session et conserver l'etat de conversation
- `CopilotClient.AsTaskAgent(...)` : creer une nouvelle session pour chaque tache

## Notes

- `OpenHub.Agents.GitHubCopilot` depend actuellement du package en preversion `Microsoft.Agents.AI.GitHub.Copilot`.
- Les versions de packages sont gerees centralement via `Directory.Packages.props`.
- Les conventions de build a l'echelle du depot se trouvent dans `Directory.Build.props` et `.editorconfig`.
