# Manuel de migration — Microsoft Agent Framework (.NET)

> Référence commune à tous les exercices du lab. Analyse faite le **2026-09-26**.
> Sources : NuGet.org (versions), dépôt officiel [`microsoft/agent-framework`](https://github.com/microsoft/agent-framework) au tag `dotnet-1.22.0` (samples + sources), release notes GitHub `dotnet-*`, et la documentation Microsoft Learn ([Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp), [Azure OpenAI v1 API](https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle)).

---

## 1. Version cible

| Élément | Version retenue | Justification |
|---|---|---|
| **Microsoft Agent Framework** (`Microsoft.Agents.AI`, `.Abstractions`, `.OpenAI`, `.Workflows`) | **1.22.0** (stable, publiée le 2026-09-18) | Dernière version stable sur NuGet. GA atteinte avec 1.0.0 le 2026-04-02 ; livraisons mineures ~hebdomadaires depuis. |
| **.NET** | **net10.0** (SDK 10.0.100+) | Version LTS actuelle ; c'est la TFM des samples officiels (`<TargetFrameworks>net10.0</TargetFrameworks>`). Le lab cible déjà `net10.0`. |
| `OpenAI` (SDK officiel) | **2.13.0** (transitif via `Microsoft.Agents.AI.OpenAI`) | Version testée par MAF 1.22.0 (`Directory.Packages.props` officiel). Pas de référence explicite nécessaire. |
| `Microsoft.Extensions.AI` / `.Abstractions` / `.OpenAI` | **10.10.0** (transitif) | Dépendance directe de MAF 1.22.0. |
| `Azure.Identity` | **1.21.0** | Stable ; même version que le dépôt officiel. |
| `Microsoft.Extensions.Configuration.*` | **10.0.12** | Stable, alignée sur .NET 10 et sur le dépôt officiel. |
| `ModelContextProtocol` / `.AspNetCore` | **2.2.0** (stable) | Pour Lab04 / Lab10. |
| `Microsoft.Extensions.VectorData.Abstractions` | **10.10.0** (stable) | Pour Lab05 / Lab07 (transitif via MAF). |
| `CommunityToolkit.VectorData.InMemory` | **1.0.1** (stable) | `InMemoryVectorStore` des samples officiels (`Agent_Step04_3rdPartyChatHistoryStorage`) ; Lab05. |
| `MongoDB.Driver` | **3.12.0** (stable) | Driver officiel ; Lab05 le référence directement (voir §8, CommonUtilities). |

### Packages sans version stable (exception documentée)

| Package | Dernière version | Labs concernés | Décision |
|---|---|---|---|
| `Microsoft.Agents.AI.A2A`, `Microsoft.Agents.AI.Hosting.A2A(.AspNetCore)`, `Microsoft.Agents.AI.Hosting` | `1.22.0-preview.260918.1` | Lab06 (client/serveur) | Aucune version stable n'existe : utiliser la préversion **alignée** sur 1.22.0 et le signaler dans le README du lab. **Appliqué (Lab06)** : le client référence `Microsoft.Agents.AI.A2A`, le serveur `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` (qui apporte `.Hosting.A2A`, `.Hosting`, `.Hosting.AspNetCore`, `A2A.AspNetCore` et `Microsoft.Extensions.Configuration.*` 10.0.12). |
| `A2A` (SDK) | `1.0.0-preview2` | Lab06 | Idem (dépendance du précédent). |
| `Microsoft.SemanticKernel.Connectors.InMemory` / `.MongoDB` | `1.74.0-preview` | Lab05, Lab07 | **Remplacés** (Lab05, cf. §4.11) : `CommunityToolkit.VectorData.InMemory` 1.0.1 (stable, samples officiels) et, pour MongoDB, un `ChatHistoryProvider` sur le driver officiel `MongoDB.Driver` 3.12.0 (aucun connecteur `VectorData` MongoDB stable). Lab07 (recherche vectorielle) : à trancher. |
| `Microsoft.Agents.AI.Foundry` | stable 1.5.0 seulement ; 1.22.0 en `-preview` | aucun aujourd'hui | Non utilisé : le lab cible Azure OpenAI, pas Foundry Agent Service. |

---

## 2. État actuel du lab

| Élément | Valeur actuelle |
|---|---|
| TFM | `net10.0` partout (mais les README Lab01 annoncent « .NET 8 ») |
| MAF | `Microsoft.Agents.AI.OpenAI` **1.0.0-preview.251204.1** (majorité), `1.0.0-preview.251002.1` (Lab05, MAS-Lab01/Start), `1.0.0-preview.251219.1` (Lab10/Solution) — **versions incohérentes entre Start et Solution** |
| Autres MAF | `Microsoft.Agents.AI.A2A` / `.Hosting.A2A.AspNetCore` / `.Workflows` en `1.0.0-preview.251219.1` |
| Client Azure OpenAI | `Azure.AI.OpenAI 2.7.0-beta.2` (`AzureOpenAIClient`) — **16/16 labs** |
| Auth | `Azure.Identity 1.18.0-beta.2` (préversion) |
| Configuration | `Microsoft.Extensions.Hosting 9.0.0` (`Host.CreateApplicationBuilder()` uniquement pour lire la config) + `appsettings.json` contenant un champ `APIKey` |
| Composant partagé | `CommonUtilities` (net10.0) : `ColoredConsole`, `ConsoleSpinner`/`WithSpinner`, `MongoDbHealthCheck` (dépend de `MongoDB.Driver 2.30.0`) |
| Tests | Aucun projet de test ; pas de `.sln` ; pas de `Directory.Build.props` / `Directory.Packages.props` |

### APIs utilisées (comptage sur tout le code .cs)

`AzureOpenAIClient` (112), `CreateAIAgent` (44), `AIContextProvider` (44), `AgentRunResponse` (38), `ChatMessageStore` (28), `AIFunctionFactory` (28), `AgentThread` (17), `AsAIFunction` (15), `MongoVectorStore` (13), `GetNewThread` (5), `DeserializeThread` (4), `MapA2A` (4), `ApprovalRequiredAIFunction` (4), `AgentWorkflowBuilder` (4), `InMemoryVectorStore` (3).

---

## 3. Breaking changes (preview.251204 → 1.22.0)

Extraits des release notes officielles `dotnet-*` ; seuls ceux qui touchent le lab sont listés.

| Version | Changement | PR | Labs impactés |
|---|---|---|---|
| preview.251219.1 | Namespaces des classes `Microsoft.Agents.AI.OpenAI` modifiés | #2627 | tous |
| preview.251219.1 | `AIAgent.Id` non-nullable ; suppression de `DisplayName` | #2719, #2758 | tous (si utilisé) |
| preview.260108.1 | Pattern `RunCoreAsync`/`RunCoreStreamingAsync` dans `AIAgent` (agents custom) | #2749 | Lab11 (si agent custom) |
| preview.260108.1 | Refonte des méthodes de `ChatMessageStore` | #2604 | Lab05 |
| preview.260121.1 | **`AgentRunResponse` → `AgentResponse`**, **`AgentRunResponseUpdate` → `AgentResponseUpdate`** | #3197 | tous |
| preview.260121.1 | **`CreateAIAgent` / `GetAIAgent` → `AsAIAgent`** | #3222 | tous |
| preview.260121.1 | `GetNewThread` / `DeserializeThread` deviennent asynchrones | #3152 | Lab05, Lab09, Lab12 |
| preview.260127.1 | **`ChatMessageStore` → `ChatHistoryProvider`** | #3375 | Lab05 |
| preview.260127.1 | **`AgentThread` → `AgentSession`** | #3430 | Lab05, Lab09, Lab12 |
| preview.260205.1 | `GetNewSession` → **`CreateSessionAsync`** ; `AgentSession.Serialize` déplacé sur `AIAgent` | #3501, #3650 | Lab05, Lab09, Lab12 |
| preview.260205.1 | Suppression de `UserInputRequests` | #3682 | Lab09 |
| preview.260205.1 | `ReflectingExecutor` obsolète (remplacé par source generator) | #3380 | MAS-Lab02/03 (à vérifier) |
| preview.260205.1 | Agent et session fournis à `AIContextProvider` / `ChatHistoryProvider` | #3695 | Lab05, Lab12 |
| rc1 | Session `StateBag`, plusieurs providers par agent, composition au lieu d'héritage typé | #3806, #3988 | Lab05, Lab12 |
| rc1 | Événements `AgentResponse[Update]` unifiés en `WorkflowOutputEvent` ; renommages API Workflows | #3441, #4090 | MAS-Lab02/03 |
| rc1 | Améliorations Structured Output | #3761 | Lab02 |
| 1.0.0 | Suppression de `OpenAIAssistantClientExtensions` ; `Microsoft.Agents.AI.AzureAI` → `Microsoft.Agents.AI.Foundry` | #5058, #5042 | aucun |
| 1.19.0 | Support MCP long-running tasks migré | #7774 | aucun (Lab04 n'utilise pas l'extension Tasks ; cité dans « Going further ») |
| **1.21.0** | **Suppression de la dépendance `Azure.AI.OpenAI`** : tous les samples Azure OpenAI utilisent `OpenAIClient` + endpoint `/openai/v1/` | #7986 | **tous** |
| 1.21.0 | Clarification des modes d'exécution de l'agent A2A | #8032 | Lab06 (défaut `AgentRunMode.ReturnMessage` : réponse = un `Message` A2A) |
| preview → 1.x (A2A SDK v1) | **Protocole A2A v0.3 → v1** : SDK `A2A` 1.0.0-preview2, hébergement `AddA2AServer` + `MapA2AJsonRpc`/`MapA2AHttpJson` + `MapWellKnownAgentCard`, `AgentCard.SupportedInterfaces`, `GetAIAgent` → `AsAIAgent`. **Client v1 et serveur v0.3 incompatibles** (vérifié : *"'method' field is not a valid A2A method"*) | guide Learn « A2A SDK v1 Migration Guide » | Lab06 (client **et** serveur, migrés ensemble) |
| 1.22.0 | Sessions MCP adossées à un provider scoppées par invocation | #8425 | aucun — ne concerne que `Microsoft.Agents.AI.Workflows.Declarative.Mcp` (`DefaultMcpToolHandler`), vérifié sur la PR ; Lab04 utilise `McpClient` directement |

---

## 4. Mapping ancien → nouveau

### 4.1 Client Azure OpenAI

**Ancienne approche**
```csharp
using Azure.AI.OpenAI;
AzureOpenAIClient client = new(new Uri(settings.Endpoint), new DefaultAzureCredential());
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);
```

**Nouvelle approche**
```csharp
using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using OpenAI.Chat;

OpenAIClientOptions options = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };

#pragma warning disable OPENAI001 // OpenAIClient(AuthenticationPolicy, ...) is still [Experimental] in OpenAI 2.13
OpenAIClient client = string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), options)
    : new OpenAIClient(new ApiKeyCredential(settings.APIKey), options);
#pragma warning restore OPENAI001

ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);
```

**Explication** — Azure OpenAI expose une API **v1** compatible OpenAI (`https://<ressource>.openai.azure.com/openai/v1/`), sans `api-version`. Microsoft recommande d'utiliser directement le SDK `OpenAI` ; MAF a supprimé `Azure.AI.OpenAI` de ses samples en 1.21.0 (PR #7986). Pour Entra ID, le scope est `https://ai.azure.com/.default`. Le nom de déploiement Azure devient le « model ». `AzureOpenAIEndpoint.ToV1Uri` (dans `CommonUtilities`) ajoute `/openai/v1/` à l'URL racine de la ressource, comme le helper officiel `SampleHelpers.AzureOpenAIEndpoint`.

### 4.2 Création d'un agent

**Ancienne approche** : `ChatClientAgent agent = chatClient.CreateAIAgent(instructions: "...", name: "...");`

**Nouvelle approche** : `AIAgent agent = chatClient.AsAIAgent(instructions: "...", name: "...");`

**Explication** — Renommage (#3222). `AsAIAgent` renvoie toujours un `ChatClientAgent`, mais les samples officiels typent la variable en **`AIAgent`** (abstraction commune à tous les fournisseurs) : c'est ce qu'on enseigne désormais. `AsAIAgent(ChatClientAgentOptions)` reste disponible pour la configuration avancée ; `instructions` et `tools` y vivent dans `ChatOptions` (`options.ChatOptions.Instructions`).

### 4.3 Réponses

**Ancienne approche** : `AgentRunResponse r = await agent.RunAsync("...");` / `AgentRunResponseUpdate`

**Nouvelle approche** : `AgentResponse r = await agent.RunAsync("...");` / `AgentResponseUpdate`

**Explication** — Renommage (#3197). `Text`, `Messages`, `Usage`, `ToString()` sont inchangés. Nouveau : `FinishReason` (rc4).

### 4.4 Streaming

**Nouvelle approche**
```csharp
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("..."))
{
    Console.Write(update.Text);
}
```
**Explication** — Montré dès le premier sample officiel (`01-get-started/01_hello_agent`). Aucun lab ne l'enseignait : ajouté à Lab01.

### 4.5 Conversations multi-tours

**Ancienne approche**
```csharp
AgentThread thread = agent.GetNewThread();
await agent.RunAsync("...", thread);
JsonElement state = thread.Serialize();
AgentThread restored = agent.DeserializeThread(state);
```

**Nouvelle approche**
```csharp
AgentSession session = await agent.CreateSessionAsync();
await agent.RunAsync("...", session);
// sérialisation/désérialisation : via AIAgent (cf. sample Agent_Step03_PersistedConversations)
```

**Explication** — `AgentThread` → `AgentSession` (#3430), création asynchrone (#3152, #3501), sérialisation portée par l'agent (#3650) : `await agent.SerializeSessionAsync(session)` / `await agent.DeserializeSessionAsync(json)`. Validé sur Lab05, voir §4.11.

### 4.6 Stockage de l'historique et contexte

**Ancienne approche** : `ChatMessageStore` + `ChatMessageStoreFactory`, `AIContextProvider` avec `InvokingAsync`/`InvokedAsync`.

**Nouvelle approche** : `ChatHistoryProvider` (option `ChatClientAgentOptions.ChatHistoryProvider`), `AIContextProviders` (collection), état dans `AgentSession.StateBag`.

**Explication** — Refonte complète entre preview.260108 et rc3 (#2604, #3375, #3695, #3806, #3988, #4327, #4395). **Refonte conceptuelle** pour Lab05 et Lab12 ; référence officielle : `02-agents/Agents/Agent_Step04_3rdPartyChatHistoryStorage`, `Agent_Step17_AdditionalAIContext`, `02-agents/AgentWithMemory/*`.

### 4.7 Configuration

**Ancienne approche** : `Host.CreateApplicationBuilder().Configuration` + clé API dans `appsettings.json`.

**Nouvelle approche**
```csharp
new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<AzureOpenAISettings>(optional: true)
    .AddEnvironmentVariables()
    .Build();
```

**Explication** — Un lab console n'utilise ni DI ni host : `Microsoft.Extensions.Hosting` était une dépendance lourde et inutile. `Host.CreateApplicationBuilder()` n'ajoute les *user secrets* qu'en environnement `Development`, qui n'est pas actif par défaut pour une console : la clé API finissait donc dans `appsettings.json` (fichier suivi par git). Désormais : valeurs non secrètes dans `appsettings.json`, secret dans **user-secrets** ou variable d'environnement (`AzureOpenAI__APIKey`).

### 4.8 Structured Output (validé sur Lab02)

**Ancienne approche**
```csharp
ChatClientAgent agent = chatClient.CreateAIAgent(...);           // RunAsync<T> réservé à ChatClientAgent
AgentRunResponse<T> r = await agent.RunAsync<T>("...");
JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(T));
ChatOptions o = new() { ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "Name", "Description") };
T value = response.Deserialize<T>(options);
```

**Nouvelle approche**
```csharp
AgentResponse<T> r = await agent.RunAsync<T>("...");            // sur tout AIAgent ; r.Result
ChatOptions o = new() { ResponseFormat = ChatResponseFormat.ForJsonSchema<T>(jsonOptions, schemaName: "Name") };
AgentRunOptions run = new() { ResponseFormat = ChatResponseFormat.ForJsonSchema<T>() }; // format pour un seul run
T value = JsonSerializer.Deserialize<T>(response.Text, jsonOptions)!;
// streaming : updates.ToAgentResponse() puis désérialisation de Text (pas de RunStreamingAsync<T>)
```

**Explication** — `RunAsync<T>` est porté par `AIAgent` (fichier `AIAgentStructuredOutput.cs`, 1.22.0) et utilise `AgentAbstractionsJsonUtilities.DefaultOptions` (web + enums en chaîne). `AgentResponse.Deserialize<T>()` n'existe plus dans l'API publique. `AgentRunOptions.ResponseFormat` (nouveau) prend le pas sur `ChatOptions.ResponseFormat` de l'agent (`ChatClientAgent`, `??=`). Les `[Description]` du type sont copiés dans le schéma. **Mode strict** : l'adaptateur `Microsoft.Extensions.AI.OpenAI` 10.10 transforme le schéma au format strict mais n'envoie `strict: true` que si `ChatOptions.AdditionalProperties["strict"] = true` ; sans cela, `gpt-4o-mini` ajoute souvent une propriété hors schéma (`description`), ignorée à la désérialisation. Les samples officiels ne l'activent pas → documenté dans « Going further », pas dans le code. Références : sample `02-agents/Agents/Agent_Step02_StructuredOutput`, page Learn « Producing Structured Outputs with agents ».


### 4.9 Function tools, injection de dépendances et middleware (validé sur Lab03)

**Ancienne approche**
```csharp
ChatClientAgent agent = chatClient.CreateAIAgent(instructions: "...",
    tools: [AIFunctionFactory.Create(tools.GetEmployeeInfo, "get_employee_info")],
    services: serviceProvider);                       // ServiceCollection venait de Microsoft.Extensions.Hosting
```

**Nouvelle approche**
```csharp
AIAgent agent = chatClient.AsAIAgent(instructions: "...", name: "...",
    tools: [AIFunctionFactory.Create(tools.GetEmployeeInfo, "get_employee_info")],
    services: serviceProvider);                       // paramètre IServiceProvider des outils rempli par le framework

// Middleware d'appel de fonction (non expérimental en 1.22.0)
AIAgent traced = agent.AsBuilder().Use(FunctionCallMiddleware).Build();
async ValueTask<object?> FunctionCallMiddleware(AIAgent agent, FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken ct)
    => await next(context, ct);
```

**Explication** — `AIFunctionFactory`, `AITool`, `[Description]` et le paramètre `IServiceProvider` des outils viennent de `Microsoft.Extensions.AI` : **inchangés**. Seuls changent la création de l'agent (`AsAIAgent`, #3222) et les types de réponse. `AsAIAgent(ChatClient, …, tools, clientFactory, loggerFactory, services)` est dans `PublicAPI.Shipped.txt` 1.22.0 ; le pattern `tools` + `services` est celui du sample `Agent_Step12_Plugins`. Le middleware (`FunctionInvocationDelegatingAgentBuilderExtensions.Use`) est celui de `Agent_Step11_Middleware` et de la page Learn « Agent Middleware » ; il exige un agent dont le pipeline contient un `FunctionInvokingChatClient` (c'est le cas de `AsAIAgent`).

Points découverts :
- **`Microsoft.Extensions.DependencyInjection` n'est plus transitif** : MAF 1.22.0 n'apporte que `…DependencyInjection.Abstractions` (`GetRequiredService`). `ServiceCollection` venait de `Microsoft.Extensions.Hosting`, retiré du socle → référencer explicitement `Microsoft.Extensions.DependencyInjection` **10.0.12** (version du `Directory.Packages.props` officiel), dans Start **et** Solution.
- **Une exception levée par un outil ne fait pas échouer le run** : `FunctionInvokingChatClient` renvoie l'erreur au modèle, qui répond « an error occurred… ». Exemple vérifié : `services:` omis → le run se termine normalement avec une réponse d'excuse. À documenter dans le dépannage des labs à outils (Lab07, Lab09, Lab10).
- **`Usage` d'un run avec outils** = somme de tous les appels modèle de la boucle (≈ 500 tokens d'entrée pour un seul appel d'outil avec 3 outils déclarés).
- `AITool.Name` / `AITool.Description` permettent d'afficher ce que le modèle reçoit (utile pédagogiquement).

### 4.10 Client MCP (validé sur Lab04)

**Ancienne approche**
```csharp
// ModelContextProtocol 0.5.0-preview.1
await using McpClient mcp = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
{
    TransportMode = HttpTransportMode.StreamableHttp, Endpoint = new Uri(url),
    AdditionalHeaders = new Dictionary<string, string> { { "Authorization", $"Bearer {token}" } }   // envoyé même vide
}));
ChatClientAgent agent = chatClient.CreateAIAgent(instructions: "...", tools: tools.Cast<AITool>().ToList(),
    clientFactory: c => new ConfigureOptionsChatClient(c, o => { o.MaxOutputTokens = 600; o.Temperature = 1; }));
```

**Nouvelle approche**
```csharp
// ModelContextProtocol 2.2.0 (stable)
await using McpClient mcp = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "Hugging Face", Endpoint = new Uri(url), TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = hasToken ? new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" } : null
}));
IList<McpClientTool> mcpTools = await mcp.ListToolsAsync();
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "...",
    ChatOptions = new ChatOptions { Instructions = "...", Tools = [.. mcpTools.Cast<AITool>()], MaxOutputTokens = 1000, Temperature = 0.2f }
});
AgentResponse<T> response = await agent.RunAsync<T>("...");
IEnumerable<FunctionCallContent> calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>();
```

**Explication** — L'API client du SDK MCP C# officiel n'a pas changé entre 0.5.0-preview.1 et 2.2.0 pour ce cas (vérifié dans `ModelContextProtocol.Core.xml` 2.2.0 et le sample `Agent_MCP_Server` au tag `dotnet-1.22.0`) ; `McpClientTool` dérive toujours de `AIFunction`. Le sample officiel référence `ModelContextProtocol` (qui apporte `.Core` et `Microsoft.Extensions.Hosting.Abstractions` en transitif) : même choix. Les options d'agent passent par `ChatClientAgentOptions.ChatOptions` (pattern des samples `01-get-started/04_memory` et `Agent_Step13_Plugins` : `new ChatClientAgentOptions { Name, ChatOptions = new() { Instructions, Tools } }` ; `MaxOutputTokens` et `Temperature` sont des propriétés de ce même `ChatOptions` MEAI) plutôt que par un `clientFactory`.

Points découverts :
- **Le serveur MCP Hugging Face accepte l'accès anonyme** (4 outils : `hf_whoami`, `hub_repo_search`, `hub_repo_details`, `hf_fs`, limites de débit réduites) : le token devient **facultatif**, c'est un **secret** (user-secrets `MCPServers:HuggingFace:BearerToken`), et l'en-tête `Authorization` n'est envoyé que s'il existe. Token invalide → `HttpRequestException … 401` dès `CreateAsync`.
- **La page Learn C# « Using MCP tools » montre encore `McpClientFactory.CreateAsync`** (API 0.x) : le sample au tag fait foi (`McpClient.CreateAsync`).
- **Coût des définitions d'outils** : les 4 outils Hugging Face représentent ~2 500 tokens d'entrée par run (4 435 avec tous les outils, 1 927 avec `hub_repo_search` seul, même réponse). Filtrer la liste `McpClientTool` avant `AsAIAgent` (équivalent C# de `allowed_tools`, documenté côté Python sur la même page Learn) est enseigné en scénario 3.
- **`response.Messages`** d'un `AgentResponse<T>` contient les `FunctionCallContent` / `FunctionResultContent` de la boucle d'outils : permet d'afficher les outils appelés sans middleware.
- **`MaxOutputTokens` trop bas + `RunAsync<T>`** : JSON tronqué → `JsonException` à la lecture de `Result` (vérifié avec 60).
- `McpClient.CreateAsync` renvoie une `Task<McpClient>` : `WithSpinner()` de `CommonUtilities` s'applique.

### 4.11 Sessions et `ChatHistoryProvider` (validé sur Lab05)

**Ancienne approche**
```csharp
var options = new ChatClientAgentOptions
{
    Instructions = "...", Name = "...",
    ChatMessageStoreFactory = ctx => new MyStore(vectorStore, ctx.SerializedState, ctx.JsonSerializerOptions) // un store par thread, clé dans un champ
};
AgentThread thread = agent.GetNewThread();
JsonElement state = thread.Serialize();                       // puis extraction manuelle de "storeState"
AgentThread restored = agent.DeserializeThread(JsonSerializer.SerializeToElement(new { StoreState = id }));
```

**Nouvelle approche**
```csharp
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "...",
    ChatOptions = new() { Instructions = "..." },
    ChatHistoryProvider = new VectorChatHistoryProvider(vectorStore)   // une instance par agent, partagée par toutes les sessions
});
AgentSession session = await agent.CreateSessionAsync();
await agent.RunAsync("...", session);
JsonElement serialized = await agent.SerializeSessionAsync(session);    // enregistrer la session entière
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
agent.GetService<InMemoryChatHistoryProvider>()!.GetMessages(session);  // historique par défaut, dans la session

// Provider personnalisé : état par session dans le StateBag
internal sealed class VectorChatHistoryProvider : ChatHistoryProvider
{
    private readonly ProviderSessionState<State> _sessionState =
        new(_ => new State(Guid.NewGuid().ToString("N")), nameof(VectorChatHistoryProvider));
    public override IReadOnlyList<string> StateKeys => [_sessionState.StateKey];
    protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken ct = default) => ...; // avant le run
    protected override ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken ct = default) => ...;                     // après un run réussi
}
```

**Explication** — `AgentThread` → `AgentSession` (#3430), création et (dé)sérialisation asynchrones portées par l'agent (#3152, #3501, #3650), `ChatMessageStore` → `ChatHistoryProvider` (#3375), état dans `AgentSession.StateBag` (#3806). Changement conceptuel : **le provider est sans état** (une instance par agent) et la clé de stockage vit dans la session, via `ProviderSessionState<T>` ; la documentation demande de **persister la session entière** et de la traiter comme un objet opaque, restauré avec la même configuration d'agent — l'ancienne extraction manuelle de l'id (`storeState`, `AgentThreadState`) n'a plus d'équivalent et ne doit pas être reproduite. Sans provider, `ChatClientAgent` utilise `InMemoryChatHistoryProvider` : la session sérialisée contient alors tous les messages (`stateBag.InMemoryChatHistoryProvider.messages`) ; avec un provider externe, seulement sa clé (`stateBag.<StateKey>.sessionDbKey`). `agent.GetService<TProvider>()` renvoie le provider attaché. Toutes ces API sont dans `PublicAPI.Shipped.txt` 1.22.0 et non expérimentales (seuls les constructeurs de `InvokingContext`/`InvokedContext` le sont, inutiles ici). Références : samples `Agent_Step03_PersistedConversations`, `Agent_Step04_3rdPartyChatHistoryStorage`, pages Learn [Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp) et [Storage](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp).

Points découverts :
- **Coquille de la doc Learn** (2026-09) : les pages Session/Storage écrivent `agent.SerializeSession(session)` ; l'API 1.22.0 est `await agent.SerializeSessionAsync(session)` (sample et `PublicAPI.Shipped.txt`).
- **Ordre des messages** : le sample `Agent_Step04` donne le même `Timestamp` à tous les messages d'un run ; avec un tri décroissant stable puis `Reverse()`, la réponse peut précéder la question. Lab05 ajoute un tick (vector store) ou un champ `Order` (MongoDB, dates à la milliseconde) par message.
- **Vector store** : `CommunityToolkit.VectorData.InMemory` 1.0.1 (stable, propriétaires Microsoft/.NET Foundation, utilisé par 4 samples officiels) remplace `Microsoft.SemanticKernel.Connectors.InMemory` (préversion). `Microsoft.Extensions.VectorData.Abstractions` 10.10.0 arrive en transitif via MAF.
- **MongoDB** : aucun connecteur `VectorData` MongoDB stable (SK `1.74.0-preview` seulement ; `CommunityToolkit.VectorData.CosmosMongoDB` cible Azure Cosmos DB). Un historique de conversation n'a pas besoin de recherche vectorielle : Lab05 écrit un `ChatHistoryProvider` directement sur le driver officiel **`MongoDB.Driver` 3.12.0** (même pattern que les providers intégrés, p. ex. `CosmosChatHistoryProvider`, qui utilisent le SDK natif de leur base).
- **Chat reducers** (`MessageCountingChatReducer`, `InMemoryChatHistoryProviderOptions.ChatReducer`) : encore `[Experimental]` **`MEAI001`** (vérifié à la compilation) → cités dans « Going further » de Lab05, pas enseignés.
- **`ConversationId` et `ChatHistoryProvider` sont exclusifs** : avec un service qui stocke l'historique (Responses API avec réponses stockées), `ChatClientAgent` lève `Only ConversationId or ChatHistoryProvider may be used…` — raison de plus pour Chat Completions (règle 5).

### 4.12 A2A v1 : client et serveur (validé sur Lab06)

**Ancienne approche**
```csharp
// Client (A2A 0.3)
AIAgent remote = new A2A.A2AClient(new Uri(url)).GetAIAgent();
// Serveur (A2A 0.3)
app.MapA2A(agent, path: "/a2a/authAgent", agentCard: card, taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/authAgent"));
new AgentCard { Name = "...", Url = "http://localhost:5000/a2a/authAgent", ... };
```

**Nouvelle approche**
```csharp
// Client : découverte (well-known URI), configuration directe, agent distant comme outil
AgentCard card = await new A2ACardResolver(new Uri($"{agentUrl}/")).GetAgentCardAsync();   // '/' final requis
AIAgent remote = card.AsAIAgent();                                    // ou await resolver.GetAIAgentAsync()
using A2A.A2AClient client = new(new Uri(url));                        // JSON-RPC, sans carte
AIAgent direct = client.AsAIAgent(name: "...", description: "...");
AIAgent local = chatClient.AsAIAgent(instructions: "...", tools: [remote.AsAIFunction()]);

// Serveur : enregistrement, deux liaisons, carte par agent
builder.AddA2AServer(agent);                                           // Microsoft.Extensions.DependencyInjection
var app = builder.Build();
app.MapA2AJsonRpc(agent, "/a2a/authAgent");
app.MapA2AHttpJson(agent, "/a2a/authAgent");
app.MapWellKnownAgentCard(new AgentCard { ..., SupportedInterfaces = [new AgentInterface { Url = url, ProtocolBinding = ProtocolBindingNames.JsonRpc, ProtocolVersion = "1.0" }, ...] }, "/a2a/authAgent");
await app.RunAsync(baseUrl);
```

**Explication** — Le SDK A2A passe en v1 (`1.0.0-preview2`) : nouvelles méthodes JSON-RPC (`SendMessage`), liaison HTTP+JSON (`POST …/message:send`), `AgentCard.Url` remplacé par `SupportedInterfaces` (URL + liaison + version). Côté serveur, `MapA2A` est éclaté en `AddA2AServer` (gestionnaire A2A + task store, **clé = nom de l'agent**), `MapA2AJsonRpc` / `MapA2AHttpJson` et `MapWellKnownAgentCard` (paquet `A2A.AspNetCore`). Côté client, `GetAIAgent` devient `AsAIAgent` (`A2AClientExtensions`, `A2AAgentCardExtensions`) ; `A2ACardResolver.GetAIAgentAsync` est conservé. `AsAIFunction()` (Microsoft.Agents.AI, stable) transforme un agent distant en outil (sample `A2AAgent_AsFunctionTool`). Références : samples `Agent_With_A2A`, `A2AAgent_AsFunctionTool`, `A2AAgent_ProtocolSelection`, `A2AAgent_Skills`, `05-end-to-end/A2AClientServer` ; page Learn « A2A SDK v1 Migration Guide ».

Points découverts :
- **Incompatibilité v0.3 ↔ v1** : aucun mode de compatibilité dans le SDK v1 ; client et serveur doivent être migrés et testés ensemble.
- **`MapWellKnownAgentCard(card, path)`** sert la carte à `<path>/.well-known/agent-card.json` : une carte par agent sur un même hôte (le guide Learn dit « une carte par hôte » pour la racine seulement). `A2ACardResolver(baseUrl)` ajoute `.well-known/agent-card.json` **relativement** à l'URL : sans `/` final, 404.
- **Liaison par défaut** : avec une carte qui liste JSON-RPC puis HTTP+JSON, `AsAIAgent()` a utilisé **JSON-RPC** (vérifié dans les journaux du serveur), alors que le guide Learn annonce « HTTP+JSON preferred ». `A2AClientOptions { PreferredBindings = [ProtocolBindingNames.HttpJson] }` force HTTP+JSON (vérifié).
- **Nom de type ambigu** : `A2A.AgentSkill` et `Microsoft.Agents.AI.AgentSkill` coexistent ; un projet nommé `A2AClient` masque le type `A2A.A2AClient` (écrire le nom complet).
- **Sessions côté serveur** : par défaut, aucune session n'est conservée entre requêtes (sample : `AddKeyedSingleton<AgentSessionStore>(name, new InMemoryAgentSessionStore())`) ; `A2AServerRegistrationOptions` (`AgentRunMode`) est `[Experimental]`.
- **L'hébergement A2A appelle toujours `RunStreamingAsync`** : il est touché par la régression streaming Azure (§8) → contournement temporaire dans Lab06_A2AServer.
- `Microsoft.Agents.Hosting.AspNetCore` (1.4.9-beta, SDK « Agents » M365) et `Microsoft.Extensions.Hosting` étaient inutiles : supprimés.

---

## 5. APIs / packages supprimés ou remplacés

| Nom | Statut | Impact | Remplacement recommandé | Exercices touchés |
|---|---|---|---|---|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | Retiré des samples et des dépendances MAF (1.21.0) ; package encore publié mais sa dernière version stable (2.1.0) est antérieure à la v1 API | Construction du client dans tous les labs | `OpenAI.OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` + `ApiKeyCredential` ou `BearerTokenPolicy` | **tous** |
| `CreateAIAgent` / `GetAIAgent` | Renommés (preview.260121) | Compilation | `AsAIAgent` | tous |
| `AgentRunResponse` / `AgentRunResponseUpdate` | Renommés | Compilation | `AgentResponse` / `AgentResponseUpdate` | tous |
| `AgentThread`, `GetNewThread`, `DeserializeThread`, `thread.Serialize()` | Renommés / déplacés / async | Compilation + concept | `AgentSession`, `CreateSessionAsync`, `SerializeSessionAsync` / `DeserializeSessionAsync` sur `AIAgent` (§4.11) | Lab05 ✅, Lab09, Lab12 |
| `ChatMessageStore`, `ChatMessageStoreFactory` | Renommés + refondus | Refonte | `ChatHistoryProvider` (instance unique, `ChatClientAgentOptions.ChatHistoryProvider`) + `ProviderSessionState<T>` ; `ChatClientAgentOptions.Instructions` → `ChatOptions.Instructions` (§4.11) | Lab05 ✅ |
| `AIContextProvider` (API preview) | Refondu (signature, StateBag, composition) | Refonte | `AIContextProvider` 1.x / `MessageAIContextProvider` | Lab12 |
| `AgentResponse.Deserialize<T>()`, `RunAsync<T>` limité à `ChatClientAgent`, `AIJsonUtilities.CreateJsonSchema` + `ForJsonSchema(schema, …)` | Supprimé / généralisé / simplifié | Compilation (Deserialize) + pédagogie | `JsonSerializer.Deserialize<T>(response.Text, …)`, `AIAgent.RunAsync<T>`, `ChatResponseFormat.ForJsonSchema<T>()` | Lab02 |
| `ModelContextProtocol 0.5.0-preview.1` | Préversion ; 2.2.0 stable | Aucune rupture pour le client HTTP (`McpClient.CreateAsync`, `HttpClientTransport(Options)`, `ListToolsAsync`, `McpClientTool` inchangés) | `ModelContextProtocol` 2.2.0 | Lab04 (Lab10 : côté serveur, à vérifier) |
| `McpClientFactory.CreateAsync` | Supprimé (API 0.x) — encore montré par la page Learn « Using MCP tools » en C# au 2026-09-23 | Compilation | `McpClient.CreateAsync` (sample `Agent_MCP_Server` au tag) | Lab04, Lab10 |
| `clientFactory: c => new ConfigureOptionsChatClient(c, o => …)` pour fixer `MaxOutputTokens` / `Temperature` | Toujours disponible (MEAI) mais détourné : c'est un middleware `IChatClient` | Pédagogie | `AsAIAgent(new ChatClientAgentOptions { ChatOptions = new() { Instructions, Tools, MaxOutputTokens, Temperature } })` | Lab04 |
| `Microsoft.Extensions.DependencyInjection` (transitif via Hosting) | N'est plus apporté une fois Hosting retiré (MAF n'apporte que `.Abstractions`) | Compilation (`ServiceCollection`) | Référence explicite `Microsoft.Extensions.DependencyInjection` 10.0.12 | Lab03 (et tout lab qui construit un `ServiceCollection` : Lab10, MAS) |
| `UserInputRequests` | Supprimé | Compilation | contenu d'approbation dans les messages de réponse (cf. `Agent_Step01_UsingFunctionToolsWithApprovals`) | Lab09 |
| `ReflectingExecutor` | Obsolète | Warning puis suppression | Executors source-générés | MAS-Lab02/03 (à vérifier) |
| `Microsoft.SemanticKernel.Connectors.InMemory` / `.MongoDB` | Toujours en préversion | Viole la règle « stable uniquement » | `CommunityToolkit.VectorData.InMemory` (stable) ; MongoDB : `ChatHistoryProvider` sur `MongoDB.Driver` 3.12.0 (Lab05) | Lab05 ✅, Lab07 |
| `A2AClient.GetAIAgent()` | Renommé (A2A SDK v1) | Compilation | `IA2AClient.AsAIAgent(name, description)` ; `AgentCard.AsAIAgent()` ; `A2ACardResolver.GetAIAgentAsync()` inchangé (§4.12) | Lab06 ✅ |
| `app.MapA2A(agent, path, agentCard, taskManager => …)`, `ITaskManager`, `AgentCard.Url` | Supprimés (A2A SDK v1) | Compilation + protocole | `AddA2AServer` + `MapA2AJsonRpc` / `MapA2AHttpJson` + `MapWellKnownAgentCard(card, path)` ; `AgentCard.SupportedInterfaces` (§4.12) | Lab06 ✅ |
| `Microsoft.Agents.Hosting.AspNetCore` 1.4.9-beta | Inutile (SDK M365 Agents) | Dépendance en préversion | — (supprimé) | Lab06 ✅ |
| `Microsoft.Extensions.Hosting` (usage « config seulement ») | Surdimensionné | Dépendance inutile | `Microsoft.Extensions.Configuration.Json/UserSecrets/EnvironmentVariables/Binder` | tous les labs console |
| `Azure.Identity 1.18.0-beta.2` | Préversion | Règle « stable » | `Azure.Identity 1.21.0` | tous |
| `MongoDB.Driver 2.30.0` (CommonUtilities) | Vulnérabilités transitives (`Snappier` 1.0.0 *high*, `SharpCompress` 0.30.1 *moderate*) | Warnings NU1902/NU1903 dans **tous** les labs | `MongoDB.Driver 3.12.0` — Lab05 le référence directement (son graphe n'a plus de package vulnérable) ; la montée de CommonUtilities reste à faire avec Lab07/Lab12 (cf. §8) | tous (warnings), Lab07/12 (code) |

---

## 6. Bonnes pratiques à appliquer à chaque exercice

1. **Une seule version MAF** pour tout le lab : `1.22.0` (Start **et** Solution identiques ; plus de mélange de previews).
2. **Référencer le package le plus haut niveau utile** (`Microsoft.Agents.AI.OpenAI`) et laisser `OpenAI` / `Microsoft.Extensions.AI` venir en transitif, sauf besoin explicite d'API plus récente.
3. **Programmer contre `AIAgent`**, pas contre `ChatClientAgent`, sauf si une API propre à `ChatClientAgent` est enseignée.
4. **Endpoint v1 + SDK `OpenAI`** pour Azure OpenAI ; auth par clé API **ou** `DefaultAzureCredential` via `BearerTokenPolicy` (scope `https://ai.azure.com/.default`). Garder l'avertissement officiel : en production, préférer une credential spécifique (`ManagedIdentityCredential`).
5. **Chat Completions par défaut** (`GetChatClient`) dans les labs : historique géré localement par MAF, ce qui est indispensable à la pédagogie des labs sessions / persistance (Lab05, Lab12). La Responses API (`GetResponsesClient().AsAIAgent(model: …)`) est présentée comme alternative dans le README (elle stocke l'historique côté service par défaut).
6. **Jamais de secret dans `appsettings.json`** : user-secrets (`UserSecretsId` dans le `.csproj`) ou variables d'environnement ; `appsettings.json` ne contient que des valeurs non sensibles.
7. **Validation de la configuration au démarrage** avec un message d'erreur actionnable (placeholders non remplacés, valeurs manquantes).
8. **Commentaires d'aide et README** : nommer les types et méthodes **actuels** ; aucune mention de `CreateAIAgent`, `AgentRunResponse`, `AgentThread`, `AzureOpenAIClient`, sauf dans un encadré « Si vous venez d'une ancienne version ».
9. **Start et Solution partagent** exactement les mêmes fichiers d'infrastructure (`.csproj`, `ConfigurationHelper.cs`, `AzureOpenAISettings.cs`, `appsettings.json`) — seul `Program.cs` diffère.
10. **Le projet Start doit compiler** tel que livré (TODO en commentaires) pour que l'apprenant puisse lancer `dotnet run` dès le début.
11. **Aucun `NoWarn` global** sur les diagnostics expérimentaux (`MAAI001`, `OPENAI001`) : suppression ciblée et commentée si une API expérimentale est réellement enseignée.
12. **Aucune abstraction ni bibliothèque tierce non nécessaire** : une pratique n'entre dans le lab que si elle est démontrée dans les samples ou la documentation officiels 1.22.0 (ou utilise correctement leurs API sans s'en écarter). Pas de toolkit communautaire, pas de fabrique maison qui masquerait les API enseignées.
13. **`OPENAI001` (voie Entra ID)** : le client est construit **directement dans `Program.cs`**, comme dans les samples officiels, avec un `#pragma warning disable/restore OPENAI001` limité à cette instruction, conformément à la doc Azure « v1 API ». Pas de fabrique dédiée (règle 12), pas de suppression globale (règle 11). La Responses API n'est pas utilisée par défaut : `GetResponsesClient()` / `ResponsesClient` sont eux aussi `[Experimental]` en OpenAI 2.13.

---

## 7. Stratégie de test (par exercice)

| # | Vérification | Commande / méthode | Critère |
|---|---|---|---|
| 1 | Restauration | `dotnet restore Start` et `Solution` | 0 erreur ; aucune préversion sauf exception documentée (§1) |
| 2 | Graphe de dépendances | `dotnet list <proj> package --include-transitive` | MAF = 1.22.0 ; pas de `Azure.AI.OpenAI` |
| 3 | Vulnérabilités | `dotnet list <proj> package --vulnerable --include-transitive` | Aucune introduite par le lab |
| 4 | Compilation | `dotnet build -warnaserror` (hors warnings NU19xx hérités de CommonUtilities tant que la phase 0 n'est pas faite) | 0 erreur, 0 warning C# |
| 5 | APIs obsolètes | `grep` des anciens noms (§5) dans `Start/`, `Solution/`, `README.md` | 0 occurrence hors encadré « ancienne version » |
| 6 | Exécution Solution — clé API | `dotnet run --project Solution` | Chaque scénario produit une réponse cohérente ; usage des tokens affiché |
| 7 | Exécution Solution — Entra ID | idem avec `AzureOpenAI__APIKey=""` après `az login` | Même comportement (ou erreur d'autorisation RBAC explicite, documentée) |
| 8 | Robustesse config | lancement avec config absente / placeholder | Message d'erreur clair, pas de stack trace obscure |
| 9 | Start livré | `dotnet run --project Start` | Compile et s'exécute (affiche les en-têtes de scénarios sans planter) |
| 10 | Start complété | copie temporaire de Start + TODO complétés selon les indices du README | Compile et produit le même comportement que Solution |
| 11 | Cohérence | `diff Start Solution` (hors `Program.cs`) | Aucune différence |
| 12 | Pédagogie | relecture README ↔ TODO ↔ Solution | Chaque TODO a un indice README qui mène exactement au code de la Solution |

---

## 8. Risques et points d'attention

- **Endpoint** : l'API v1 exige le suffixe `/openai/v1/`. Les endpoints `*.openai.azure.com`, `*.cognitiveservices.azure.com` et `*.services.ai.azure.com` sont acceptés.
- **RBAC Entra ID** : la voie `DefaultAzureCredential` exige le rôle *Cognitive Services OpenAI User* sur la ressource ; sans lui, erreur 401/403 — à documenter dans chaque README.
- **Modèles** : les samples officiels utilisent `gpt-5.4-mini`. Le lab reste agnostique (nom de déploiement en configuration) ; certains modèles de raisonnement ignorent `temperature` etc. (labs futurs).
- **Collision `ChatMessage`** : `OpenAI.Chat.ChatMessage` et `Microsoft.Extensions.AI.ChatMessage` coexistent dès qu'on importe `OpenAI.Chat` (nécessaire pour `ChatClient` et `AsAIAgent`). Conserver l'alias `AIExtensions` enseigné depuis Lab01.
- **Cadence de release** : MAF publie ~1 version/semaine avec des `[BREAKING]` dans les zones Skills, Harness, Hosting, A2A, MCP. Figer 1.22.0 pour toute la migration ; ne pas monter de version en cours de route.
- **Lab06 (A2A)** : packages uniquement en préversion → exception à la règle « stable ».
- **Régression streaming Azure OpenAI (constatée le 2026-09-28)** : Azure envoie désormais, en streaming Chat Completions, des annotations de filtre de contenu **sans `delta`** ; `Microsoft.Extensions.AI.OpenAI` 10.10.0 (et 10.10.1, dernière version) lève `InvalidOperationException: The requested operation requires an element of type 'Object', but the target element has type 'Null'` dans `OpenAIChatClient.TryGetReasoningDelta` ([dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790), ouvert). Impact : **tout `RunStreamingAsync`** — Lab01 scénario 5 et Lab02 (streaming) échouent aujourd'hui alors qu'ils étaient validés le 2026-09-26/27 (non corrigés : hors périmètre), et l'hébergement A2A (qui exécute toujours en streaming) renvoie *"Agent handler did not produce any response events"*. Lab06_A2AServer contient un contournement **temporaire** (`StreamingWorkaround.WithNonStreamingResponses()`, middleware `ChatClientBuilder.Use` qui sert les requêtes streaming par un appel non streaming). À retirer, et à re-tester Lab01/Lab02, dès qu'une version corrigée de `Microsoft.Extensions.AI.OpenAI` est publiée.
- **Port 5000 sur macOS** : le récepteur AirPlay écoute sur `*:5000` ; Kestrel peut quand même se lier à `localhost:5000`, mais quand le serveur du lab ne tourne pas, un client reçoit `403 Forbidden` d'AirPlay. Documenté dans les README Lab06 ; le dashboard utilise un port dédié (5071).
- **Lab05 / Lab07** : connecteurs Semantic Kernel en préversion et API mémoire/historique profondément refondues → refonte conceptuelle, pas une adaptation syntaxique.
- **CommonUtilities** : monter `MongoDB.Driver` en 3.x provoquerait `NU1605` (downgrade) dans Lab07/Lab12, qui référencent 2.30.0 directement. Décision Lab05 : ne pas toucher CommonUtilities ; Lab05 référence `MongoDB.Driver` 3.12.0 (la version la plus haute gagne dans son graphe) et **n'utilise pas** `MongoDbHealthCheck` (compilé contre 2.x ; la 3.0 a fusionné `MongoDB.Driver.Core` dans `MongoDB.Driver`, compatibilité binaire non garantie) — il vérifie la connexion dans son `ConfigurationHelper`. La montée de CommonUtilities (ou le déplacement de `MongoDbHealthCheck`) se fera avec Lab07/Lab12.
- **Docker Compose** : un `docker-compose.yml` placé dans un dossier `MongoDB/` prend par défaut le nom de projet `mongodb`, partagé avec tout autre projet local du même nom : `docker compose up` **recrée alors les conteneurs de l'autre projet** et réutilise ses volumes. Toujours déclarer un `name:` de projet propre au lab (Lab05 : `lab05-aiagent-sessions`) et des `container_name` uniques (Lab07, Lab12).
- **Analyse graphify** : l'extracteur AST échoue sur 5 `Program.cs` à top-level statements (Lab01/02/03/04/07 Solution) ; sans impact sur la compilation, mais ces fichiers sont sous-représentés dans la carte.
