# Manuel de migration — Microsoft Agent Framework (.NET)

> Référence commune à tous les exercices du lab. Analyse faite le **2026-09-26**. **Mise à jour du 2026-10-25 : montée de 1.22.0 vers 1.26.0** (§1, §3 bis).
> Sources : NuGet.org (versions), dépôt officiel [`microsoft/agent-framework`](https://github.com/microsoft/agent-framework) au tag `dotnet-1.22.0` (samples + sources), release notes GitHub `dotnet-*`, et la documentation Microsoft Learn ([Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp), [Azure OpenAI v1 API](https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle)).

---

## 1. Version cible

| Élément | Version retenue | Justification |
|---|---|---|
| **Microsoft Agent Framework** (`Microsoft.Agents.AI`, `.Abstractions`, `.OpenAI`, `.Workflows`) | **1.26.0** (stable, publiée le 2026-10-23) — 1.22.0 jusqu'au 2026-10-25 | Dernière version stable sur NuGet. GA atteinte avec 1.0.0 le 2026-04-02 ; livraisons mineures ~hebdomadaires depuis. Montée 1.22.0 → 1.26.0 : §3 bis. |
| **.NET** | **net10.0** (SDK 10.0.100+) | Version LTS actuelle ; c'est la TFM des samples officiels (`<TargetFrameworks>net10.0</TargetFrameworks>`). Le lab cible déjà `net10.0`. |
| `OpenAI` (SDK officiel) | **2.14.0** (transitif via `Microsoft.Agents.AI.OpenAI` 1.26.0) | Version testée par MAF 1.26.0 (`Directory.Packages.props` officiel, #8730). Pas de référence explicite nécessaire. (2.13.0 avec MAF 1.22.0.) |
| `Microsoft.Extensions.AI` / `.Abstractions` / `.OpenAI` | **10.10.0** / **10.10.1** / **10.10.1** (transitif) ; `Microsoft.Extensions.AI.OpenAI` **10.11.0** **explicite** dans Lab01, Lab02 et Lab06_A2AServer | Dépendances directes de MAF 1.26.0. Exception : correctif du streaming Azure (#7790), absent de 10.10.x et non apporté par MAF 1.26.0 (§8). |
| `Azure.Identity` | **1.21.0** | Stable ; même version que le dépôt officiel. |
| `Microsoft.Extensions.Configuration.*` | **10.0.12** | Stable, alignée sur .NET 10 et sur le dépôt officiel. |
| `ModelContextProtocol` / `.AspNetCore` | **2.2.0** (stable) | Pour Lab04 (`ModelContextProtocol`) / Lab10 (`ModelContextProtocol.AspNetCore`, qui apporte `ModelContextProtocol` et le framework partagé ASP.NET Core). Version du `Directory.Packages.props` 1.22.0 ; une 2.3.0 est publiée depuis (constaté le 2026-10-25), non retenue : version figée. |
| `Microsoft.Extensions.VectorData.Abstractions` | **10.10.0** (stable) | Pour Lab05 / Lab07 (transitif via MAF). |
| `CommunityToolkit.VectorData.InMemory` | **1.0.1** (stable) | `InMemoryVectorStore` des samples officiels (`Agent_Step04_3rdPartyChatHistoryStorage`) ; Lab05. |
| `MongoDB.Driver` | **3.12.0** (stable) | Driver officiel ; Lab05 et Lab12 le référencent directement. `CommonUtilities` n'en dépend plus depuis la phase 0 (Lab12, voir §8). |

### Packages sans version stable (exception documentée)

| Package | Dernière version | Labs concernés | Décision |
|---|---|---|---|
| `Microsoft.Agents.AI.A2A`, `Microsoft.Agents.AI.Hosting.A2A(.AspNetCore)`, `Microsoft.Agents.AI.Hosting` | `1.26.0-preview.261023.1` (`1.22.0-preview.260918.1` jusqu'au 2026-10-25) | Lab06 (client/serveur) | Aucune version stable n'existe : utiliser la préversion **alignée** sur la version cible et le signaler dans le README du lab. **Appliqué (Lab06)** : le client référence `Microsoft.Agents.AI.A2A`, le serveur `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` (qui apporte `.Hosting.A2A`, `.Hosting`, `.Hosting.AspNetCore`, `A2A.AspNetCore` et `Microsoft.Extensions.Configuration.*` 10.0.12). |
| `A2A` (SDK) | `1.0.0-preview3` (`1.0.0-preview2` avec MAF 1.22.0, #9386) | Lab06 | Idem (dépendance du précédent). |
| `Microsoft.SemanticKernel.Connectors.InMemory` / `.MongoDB` | `1.74.0-preview` | Lab05, Lab07 | **Remplacés** (Lab05, cf. §4.11) : `CommunityToolkit.VectorData.InMemory` 1.0.1 (stable, samples officiels) et, pour MongoDB, un `ChatHistoryProvider` sur le driver officiel `MongoDB.Driver` 3.12.0 (aucun connecteur `VectorData` MongoDB stable). **Lab07 (recherche vectorielle) : `InMemoryVectorStore`** (cf. §4.13) — le vector store des samples RAG officiels ; MongoDB Atlas abandonné (aucun connecteur `VectorData` MongoDB stable, et le lab reste exécutable avec la seule CLI `dotnet`). |
| `Microsoft.Agents.AI.Foundry` | stable 1.5.0 seulement ; 1.26.0 en `-preview` | aucun aujourd'hui | Non utilisé : le lab cible Azure OpenAI, pas Foundry Agent Service. |

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
| preview.260108.1 | Pattern `RunCoreAsync`/`RunCoreStreamingAsync` dans `AIAgent` (agents custom) | #2749 | aucun (Lab11 n'a pas d'agent personnalisé : son « custom » est le transport HTTP, vérifié) |
| preview.260108.1 | Refonte des méthodes de `ChatMessageStore` | #2604 | Lab05 |
| preview.260121.1 | **`AgentRunResponse` → `AgentResponse`**, **`AgentRunResponseUpdate` → `AgentResponseUpdate`** | #3197 | tous |
| preview.260121.1 | **`CreateAIAgent` / `GetAIAgent` → `AsAIAgent`** | #3222 | tous |
| preview.260121.1 | `GetNewThread` / `DeserializeThread` deviennent asynchrones | #3152 | Lab05, Lab09, Lab12 ✅ |
| preview.260127.1 | **`ChatMessageStore` → `ChatHistoryProvider`** | #3375 | Lab05 |
| preview.260127.1 | **`AgentThread` → `AgentSession`** | #3430 | Lab05, Lab09, Lab12 ✅ |
| preview.260205.1 | `GetNewSession` → **`CreateSessionAsync`** ; `AgentSession.Serialize` déplacé sur `AIAgent` | #3501, #3650 | Lab05, Lab09, Lab12 ✅ |
| preview.260205.1 | Suppression de `UserInputRequests` | #3682 | Lab09 ✅ |
| 1.14.0 | **Liaison des réponses d'approbation** aux demandes surfacées (`[BREAKING]`) ; `ToolApprovalAgent` sorti d'expérimental | #7111, #7107 | Lab09 ✅ (session obligatoire) |
| **1.22.0** | **Liaison des approbations renforcée** (`[BREAKING]`, replay) : seules les demandes enregistrées par le framework dans la session font foi ; liaison aussi pour les réponses « always approve » | #8375, #8432 | Lab09 ✅ |
| preview.260205.1 | `ReflectingExecutor` obsolète (remplacé par source generator) | #3380 | MAS-Lab02/03 (à vérifier) |
| preview.260205.1 | Agent et session fournis à `AIContextProvider` / `ChatHistoryProvider` | #3695 | Lab05, Lab12 ✅ |
| rc1 | Session `StateBag`, plusieurs providers par agent, composition au lieu d'héritage typé ; `AIContextProviderFactory` → `AIContextProviders` ; `InvokingAsync` / `InvokedAsync` deviennent non virtuels (on surcharge `ProvideAIContextAsync` / `StoreAIContextAsync`) | #3806, #3988 | Lab05, Lab12 ✅ (§4.18) |
| rc1 | Événements `AgentResponse[Update]` unifiés en `WorkflowOutputEvent` ; renommages API Workflows | #3441, #4090 | MAS-Lab02/03 |
| rc1 | Améliorations Structured Output | #3761 | Lab02 |
| 1.0.0 | Suppression de `OpenAIAssistantClientExtensions` ; `Microsoft.Agents.AI.AzureAI` → `Microsoft.Agents.AI.Foundry` | #5058, #5042 | aucun |
| 1.19.0 | Support MCP long-running tasks migré | #7774 | aucun (Lab04 n'utilise pas l'extension Tasks ; cité dans « Going further ») |
| **1.21.0** | **Suppression de la dépendance `Azure.AI.OpenAI`** : tous les samples Azure OpenAI utilisent `OpenAIClient` + endpoint `/openai/v1/` | #7986 | **tous** |
| 1.21.0 | Clarification des modes d'exécution de l'agent A2A | #8032 | Lab06 (défaut `AgentRunMode.ReturnMessage` : réponse = un `Message` A2A) |
| preview → 1.x (A2A SDK v1) | **Protocole A2A v0.3 → v1** : SDK `A2A` 1.0.0-preview2, hébergement `AddA2AServer` + `MapA2AJsonRpc`/`MapA2AHttpJson` + `MapWellKnownAgentCard`, `AgentCard.SupportedInterfaces`, `GetAIAgent` → `AsAIAgent`. **Client v1 et serveur v0.3 incompatibles** (vérifié : *"'method' field is not a valid A2A method"*) | guide Learn « A2A SDK v1 Migration Guide » | Lab06 (client **et** serveur, migrés ensemble) |
| 1.22.0 | Sessions MCP adossées à un provider scoppées par invocation | #8425 | aucun — ne concerne que `Microsoft.Agents.AI.Workflows.Declarative.Mcp` (`DefaultMcpToolHandler`), vérifié sur la PR ; Lab04 utilise `McpClient` directement |
| SDK MCP C# 0.5 → 2.2 (serveur) | **Streamable HTTP sans état par défaut** (`HttpServerTransportOptions.SessionMode = Stateless`, révision de protocole 2026-07-28, SEP-2567) et **SSE legacy désactivé par défaut** (`EnableLegacySse = false` ; incompatible avec le mode sans état) : `MapMcp` ne sert plus `/sse` ni `/message` | docs XML `ModelContextProtocol.AspNetCore` 2.2.0 | Lab10 ✅ (vérifié : client SSE → `405`, `GET …/sse` → `404`) |


### 3 bis. Montée de version 1.22.0 → 1.26.0 (2026-10-25)

Vague unique sur les 13 labs AzureOpenAI (Start **et** Solution), sans changement de code lié à l'API : aucune API utilisée par les labs n'a été supprimée ni renommée entre 1.22.0 et 1.26.0. Rapport : [Upgrade-1.26.0-Report.md](Upgrade-1.26.0-Report.md).

| Paquet | Avant | Après |
|---|---|---|
| `Microsoft.Agents.AI.OpenAI` (26 projets) | 1.22.0 | **1.26.0** |
| `Microsoft.Agents.AI.A2A`, `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` (Lab06) | 1.22.0-preview.260918.1 | **1.26.0-preview.261023.1** (SDK `A2A` 1.0.0-preview3) |
| `OpenAI` (transitif) | 2.13.0 | **2.14.0** |
| `Microsoft.Extensions.AI.OpenAI` | 10.10.0 (transitif) ; 10.11.0 explicite (Lab01, Lab02, Lab06_A2AServer) | 10.10.1 (transitif) ; **10.11.0 explicite conservé** : MAF 1.26.0 n'apporte pas le correctif du streaming (§8) |
| Autres (`Azure.Identity` 1.21.0, `Microsoft.Extensions.Configuration.*` 10.0.12, `ModelContextProtocol` 2.2.0, `MongoDB.Driver` 3.12.0, `CommunityToolkit.VectorData.InMemory` 1.0.1) | — | Inchangés : ce sont les versions du `Directory.Packages.props` de MAF 1.26.0 |

Changements des notes de version `dotnet-1.23.0` → `dotnet-1.26.0` qui concernent les labs :

| Version | Changement | PR | Labs | Constat |
|---|---|---|---|---|
| 1.23.0 | [BREAKING] `OpenAI` 2.14.0, MEAI 10.10.1, `Azure.AI.Projects` 3.0.0-beta.3 | #8730 | tous | Compile sans warning ; `OPENAI001` toujours requis (constructeur `OpenAIClient(AuthenticationPolicy, …)`) |
| 1.23.0 | [BREAKING] Better support tool changes between runs | #8754 | Lab03, Lab09 | Aucun changement de code ; attentes du dashboard OK |
| 1.23.0 | [BREAKING] Enforce approval response binding consistently ; Only consume stored approval state when the run succeeds | #8641, #8692 | Lab09 | OK : approbation à la console et par politique, dans la même session |
| 1.23.0 | Function replace support for function middleware | #8615 | Lab03 | Nouveauté, non utilisée |
| 1.23.0 | [BREAKING] Allow list for configuration keys ; invoke function tool could bypass approval | #8200, #8403 | aucun | Workflows déclaratifs uniquement (fichiers modifiés vérifiés) |
| 1.24.0 | Document A2A authentication and tool authorization | #8491 | Lab06 | Documentation (`A2AClientServer/AUTHENTICATION.md`) |
| 1.25.0 | Upgrade A2A packages to 1.0.0-preview3 | #9386 | Lab06 | Aucun changement de code ; le client utilise toujours JSON-RPC (ordre des interfaces de la carte), vérifié dans les journaux du serveur (`POST /a2a/authAgent`) |
| 1.25.0 | Automatically reject tool-approval requests that never receive a response | #9152 | Lab09 | OK : les labs répondent à toutes les demandes |
| 1.26.0 | Reject duplicate tool approval request IDs ; `AgentSessionStore.DeleteSessionAsync` | #9542, #8801 | Lab09 / aucun | OK |

**Vérifié le 2026-10-25** : build des 26 projets sans erreur ni warning (hors 4 `CS0219` de Lab05/Start, déjà présents en 1.22.0) ; graphe : MAF 1.26.0 partout, pas d'`Azure.AI.OpenAI`, aucune vulnérabilité ; exécution des 13 Solutions avec clé API **et** Entra ID contre les attentes du dashboard (Entra ID 127/127 ; clé API 124/127 au premier passage, puis 100 % après correction de Lab07 et re-run de Lab08, voir le rapport) ; les 13 Start livrés s'exécutent sans exception ; `dotnet test` du dashboard 207/207.

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

**Explication** — Refonte complète entre preview.260108 et rc3 (#2604, #3375, #3695, #3806, #3988, #4327, #4395). **Refonte conceptuelle** pour Lab05 et Lab12 ; référence officielle : `02-agents/Agents/Agent_Step04_3rdPartyChatHistoryStorage`, `Agent_Step17_AdditionalAIContext`, `02-agents/AgentWithMemory/*`. Validé sur Lab05 (§4.11) et Lab12 (§4.18).

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
- **L'hébergement A2A appelle toujours `RunStreamingAsync`** : il était touché par la régression streaming Azure (§8). Résolu le 2026-10-25 par `Microsoft.Extensions.AI.OpenAI` **10.11.0** ; le contournement temporaire de Lab06_A2AServer est supprimé.
- `Microsoft.Agents.Hosting.AspNetCore` (1.4.9-beta, SDK « Agents » M365) et `Microsoft.Extensions.Hosting` étaient inutiles : supprimés.

### 4.13 RAG : vector store, embeddings et `TextSearchProvider` (validé sur Lab07)

**Ancienne approche**
```csharp
// Microsoft.SemanticKernel.Connectors.MongoDB 1.66.0-preview + Azure.AI.OpenAI
IEmbeddingGenerator<string, Embedding<float>> gen = azureClient.GetEmbeddingClient(dep).AsIEmbeddingGenerator();
var store = new MongoVectorStore(mongoDatabase, new MongoVectorStoreOptions { EmbeddingGenerator = gen });
var collection = store.GetCollection<string, FaqRecord>("sav-faq");
var results = await collection.SearchAsync(query, topK, new VectorSearchOptions<FaqRecord> { IncludeVectors = false }).ToListAsync();
ChatClientAgent agent = chatClient.CreateAIAgent(instructions: "...", tools: [AIFunctionFactory.Create(tools.SearchFaqAsync, "search_faq")]);
```

**Nouvelle approche**
```csharp
// CommunityToolkit.VectorData.InMemory 1.0.1 (stable) + OpenAI SDK (endpoint v1) ; Microsoft.Extensions.VectorData.Abstractions 10.10.0 en transitif
IEmbeddingGenerator<string, Embedding<float>> gen = client.GetEmbeddingClient(settings.EmbeddingDeploymentName).AsIEmbeddingGenerator();
VectorStore store = new InMemoryVectorStore(new InMemoryVectorStoreOptions { EmbeddingGenerator = gen });
VectorStoreCollection<string, FaqRecord> collection = store.GetCollection<string, FaqRecord>("sav-faq");
await collection.EnsureCollectionExistsAsync();
await collection.UpsertAsync(records);                                   // le store vectorise la propriété string [VectorStoreVector]
await foreach (VectorSearchResult<FaqRecord> r in collection.SearchAsync(question, top: 3)) { r.Record; r.Score; }

// RAG agentique : la recherche est un function tool (inchangé, Lab03)
AIAgent faqAgent = chatClient.AsAIAgent(instructions: "...", name: "FaqAgent", tools: [AIFunctionFactory.Create(tool.SearchFaqAsync, "search_faq")]);

// RAG « classique » : TextSearchProvider (Microsoft.Agents.AI), recherche avant chaque appel du modèle
async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(string text, CancellationToken ct) => ...; // adaptateur
AIAgent ragAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    ChatOptions = new() { Instructions = "..." },
    AIContextProviders = [new TextSearchProvider(SearchAsync, new TextSearchProviderOptions { SearchTime = BeforeAIInvoke, RecentMessageMemoryLimit = 2 })],
    ChatHistoryProvider = new InMemoryChatHistoryProvider(new() { StorageInputRequestMessageFilter = m => m.Where(x =>
        x.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.AIContextProvider && x.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory) })
});
```

**Explication** — Le connecteur MongoDB de Semantic Kernel n'a toujours pas de version stable, et aucun connecteur `Microsoft.Extensions.VectorData` MongoDB stable n'existe (`CommunityToolkit.VectorData.CosmosMongoDB` cible Azure Cosmos DB). Les samples RAG officiels (`02-agents/AgentWithRAG/*`) utilisent `InMemoryVectorStore` (`CommunityToolkit.VectorData.InMemory`, Step01) ou Qdrant (Step02) : Lab07 passe sur `InMemoryVectorStore`, avec la même API `VectorStore` / `VectorStoreCollection` / `SearchAsync`, ce qui le rend exécutable sans base de données. L'API `VectorData` 10.10 n'a pas changé pour ce cas (attributs `[VectorStoreKey]`/`[VectorStoreData]`/`[VectorStoreVector]`, propriété vectorielle `string` vectorisée par le store, `SearchAsync(value, top)` → `IAsyncEnumerable<VectorSearchResult<T>>`, `IncludeVectors` faux par défaut). **Changement conceptuel** : MAF 1.22.0 fournit un composant RAG intégré, **`TextSearchProvider`** (`MessageAIContextProvider`, stable, dans `PublicAPI.Shipped.txt` de `Microsoft.Agents.AI`), branché par `ChatClientAgentOptions.AIContextProviders` ; il appelle une fonction de recherche avant chaque appel du modèle (`BeforeAIInvoke`) et injecte les résultats sous forme de message (`ContextPrompt`, `CitationsPrompt`), ou expose la recherche comme outil (`OnDemandFunctionCalling`, l'équivalent intégré du RAG agentique). `RecentMessageMemoryLimit` ajoute les derniers messages utilisateur de la session à l'entrée de recherche (état dans `AgentSession.StateBag`, clé `TextSearchProvider`). Les samples filtrent l'historique (`InMemoryChatHistoryProviderOptions.StorageInputRequestMessageFilter` + `GetAgentRequestMessageSourceType()`) pour ne pas stocker les messages produits par le provider. Références : samples `AgentWithRAG_Step01_BasicTextRAG`, `AgentWithRAG_Step02_CustomVectorStoreRAG`, `AgentWithRAG_Step03_CustomRAGDataSource` ; sources `TextSearchProvider.cs` / `TextSearchProviderOptions.cs` au tag.

Points découverts :
- **`Dimensions` de `[VectorStoreVector]` n'est pas transmis au générateur d'embeddings** (vérifié dans `VectorPropertyModel.GenerateEmbeddingsCoreAsync`, MEVD 10.10) : la taille réelle du vecteur est celle du modèle de déploiement ; la valeur de l'attribut n'est qu'une métadonnée pour les stores qui créent un index. Lab07 la garde en constante (`FaqRecord.EmbeddingDimensions = 1536`) et le documente.
- **`AsIEmbeddingGenerator()`** (`Microsoft.Extensions.AI.OpenAI` 10.10.0) s'applique à l'`EmbeddingClient` du SDK `OpenAI` obtenu par `OpenAIClient.GetEmbeddingClient(deployment)` : même client v1 que pour le chat, pas de dépendance supplémentaire.
- Le `TextSearchProvider` **avale les exceptions** de la fonction de recherche (journalisées, résultat vide) : sans logger, une recherche qui échoue passe inaperçue → afficher les résultats depuis l'adaptateur (Lab07) ou passer un `ILoggerFactory`.
- Un lab RAG a besoin d'un **déploiement d'embedding** : nouvelle propriété `AzureOpenAISettings.EmbeddingDeploymentName` (validée au démarrage), hors du formulaire du dashboard.

### 4.14 Formats de données et résultats d'outils (validé sur Lab08)

**Ancienne approche**
```csharp
// Packages tiers ToonNet 1.0.4 (Solution) / ToonNetSerializer 1.0.0 (Start, introuvable sur NuGet : le Start ne restaurait plus)
public static string GetAllHotelsUsingJsonFormat() => File.ReadAllText(HotelsFilePath);          // texte brut du fichier JSON
public static string GetAllHotelsUsingToonFormat() => ToonNetSerializer.ToonNet.Encode(hotels);  // « Toon », en fait du CSV dans les instructions
AgentRunResponse<List<Hotel>> r = await agent.RunAsync<List<Hotel>>("...");                      // ChatClientAgent seulement
```

**Nouvelle approche**
```csharp
// Aucun package de sérialisation : l'outil renvoie des objets (JSON par le framework) ou du texte (CSV encodé par un helper fourni)
public IReadOnlyList<Hotel> GetAllHotelsAsJson() => hotels;                 // sérialisé en JSON (indenté, camelCase) par AIFunctionFactory
public string GetAllHotelsAsCsv() => HotelCsv.Serialize(hotels);            // envoyé tel quel (comme chaîne JSON)
AgentResponse<List<Hotel>> json = await jsonAgent.RunAsync<List<Hotel>>(question);   // tout AIAgent ; schéma non-objet enveloppé automatiquement
AgentResponse csv = await csvAgent.RunAsync(question);                               // format demandé dans les instructions, HotelCsv.Deserialize + FormatException
int sent = response.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()          // ce que le modèle a reçu
    .Sum(r => (r.Result as string ?? JsonSerializer.Serialize(r.Result, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object)))).Length);
```

**Explication** — Le lab comparait JSON et « Toon » avec des packages tiers non officiels, différents entre Start et Solution, dont l'un n'existe plus sur NuGet (règle 12 : aucune bibliothèque tierce non nécessaire). Le concept enseigné — le coût en tokens du format des données échangées avec le modèle — ne dépend d'aucune bibliothèque : le lab compare désormais **JSON** (objets renvoyés par l'outil, sortie structurée `RunAsync<List<Hotel>>`) et **CSV** (texte renvoyé par l'outil, réponse en CSV demandée dans les instructions et relue par un helper fourni de quelques lignes). Ce que le modèle reçoit est mesuré et non supposé : `AIFunctionFactory` transforme **toujours** la valeur de retour d'un outil en `JsonElement` (vérifié avec MEAI 10.10.0), puis `OpenAIChatClient` envoie une chaîne telle quelle et tout autre résultat sérialisé avec `AIJsonUtilities.DefaultOptions` (source `OpenAIChatClient.ToOpenAIChatMessages`, vérifié empiriquement avec `AsOpenAIChatMessages()`) : une liste d'objets devient du **JSON indenté** (10 348 caractères pour 50 hôtels), une chaîne devient une **chaîne JSON** (guillemets, `\n` échappés : 2 765 caractères). `RunAsync<T>` fonctionne avec des outils (le format de réponse s'applique au run entier) et accepte `List<T>` : `StructuredOutputSchemaUtilities.WrapNonObjectSchema` enveloppe le schéma non-objet pour le service (`AIAgentStructuredOutput.cs`, 1.22.0) — ce que `ChatResponseFormat.ForJsonSchema<T>()` ne fait pas (Lab02). Références : samples `Agent_Step02_StructuredOutput`, `01-get-started/02_add_tools` ; sources MEAI `AIFunctionFactory` (marshalling du retour) et `OpenAIChatClient` ; page Learn « Using function tools with an agent ».

Points découverts :
- **`AIFunctionFactoryOptions.SerializerOptions`** change le sérialiseur du résultat d'outil (p. ex. `WriteIndented = false`) : cité dans « Going further » de Lab08, non enseigné.
- **Culture** : l'affichage des `decimal`/`double` (`{hotel.PricePerNight}`) suit la culture du poste (`25,5` en fr-FR). Les helpers d'affichage utilisent `string.Create(CultureInfo.InvariantCulture, $"...")` pour que les checks du dashboard (`25 USD/night`) soient portables ; `System.Text.Json` est déjà invariant.
- **Le format demandé par instructions n'est pas garanti** : `HotelCsv.Deserialize` tolère les clôtures de code Markdown, mais lève `FormatException` (attrapée et affichée) sur un en-tête ou un nombre de colonnes différent — l'équivalent du scénario 1 de Lab02.
- **Ordre des scénarios et comparaison** : un bloc de comparaison affiché après le dernier scénario ne doit pas écrire de lignes `Input tokens: N` (le dashboard les lirait comme un usage) ; Lab08 écrit un tableau sans deux-points (`Input tokens  3180  1421  -55%`).

### 4.15 Approbations humaines sur les function tools (validé sur Lab09)

**Ancienne approche**
```csharp
List<AITool> tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SensitiveTools.DeleteEmployeeData, "delete_employee_data"))];
ChatClientAgent agent = chatClient.CreateAIAgent(instructions: "...", tools: tools);
var thread = agent.GetNewThread();
AgentRunResponse response = await agent.RunAsync("delete ...", thread);
List<UserInputRequestContent> requests = response.UserInputRequests.ToList();                 // supprimé
if (requests.Any())
{
    var answers = requests.OfType<FunctionApprovalRequestContent>()                              // renommé
        .Select(r => new ChatMessage(ChatRole.User, [r.CreateResponse(approved)])).ToList();   // r.FunctionCall
    response = await agent.RunAsync(answers, thread);
}
```

**Nouvelle approche**
```csharp
AIFunction delete = AIFunctionFactory.Create(HrTools.DeleteEmployeeData, "delete_employee_data");
AIAgent agent = chatClient.AsAIAgent(instructions: "...", name: "...", tools: [new ApprovalRequiredAIFunction(delete)]);
AgentSession session = await agent.CreateSessionAsync();                                        // obligatoire (liaison)
AgentResponse response = await agent.RunAsync(prompt, session);
List<ToolApprovalRequestContent> requests = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
while (requests.Count > 0)                                                                      // l'agent peut redemander
{
    List<ChatMessage> answers = requests.ConvertAll(r =>
    {
        FunctionCallContent call = (FunctionCallContent)r.ToolCall;                              // nom + arguments choisis par le modèle
        return new ChatMessage(ChatRole.User, [r.CreateResponse(approved, reason)]);           // reason transmis au modèle si rejet
    });
    response = await agent.RunAsync(answers, session);
    requests = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
}
// Ce que les outils ont renvoyé (ou le rejet reçu par le modèle) : FunctionCallContent / FunctionResultContent (même CallId) dans response.Messages
```

**Explication** — `ApprovalRequiredAIFunction` et `AIFunctionFactory` (MEAI) sont inchangés. `UserInputRequests` a été supprimé (#3682) : les demandes sont des contenus **`ToolApprovalRequestContent`** (MEAI 10.10, ex-`FunctionApprovalRequestContent`) dans `response.Messages`, dont `ToolCall` est le `FunctionCallContent` à approuver ; `CreateResponse(approved, reason)` crée le `ToolApprovalResponseContent` corrélé (`RequestId`). **Changement conceptuel : la session est obligatoire.** Depuis 1.14 (#7111) et surtout 1.22 (#8375, `[BREAKING]`), `ChatClientAgent` injecte par défaut un `ApprovalResponseBindingChatClient` (`ChatClientAgentOptions.DisableApprovalResponseBinding = false`) qui enregistre dans le `StateBag` de la session chaque demande surfacée et ne laisse passer qu'une réponse liée à une demande enregistrée (appel rebasé sur le nom et les arguments d'origine, consommé une seule fois) — **vérifié** : sans session, la réponse d'approbation est ignorée en silence, l'outil n'est jamais exécuté et le modèle répond sans résultat. Deuxième comportement par défaut : `FunctionInvokingChatClient` est « tout ou rien » (un seul outil à approuver transforme tous les appels du tour en demandes), mais `ChatClientAgent` ajoute `ApprovalNotRequiredFunctionBypassingChatClient` (`DisableApprovalNotRequiredFunctionBypassing = false`), qui exécute lui-même les outils non sensibles et ne surface que les vraies demandes — **vérifié** (outil `get_employee_info` + `delete_employee_data` dans le même tour : une seule demande). Sur rejet, le modèle reçoit un `FunctionResultContent` texte `Tool call invocation rejected.` suivi du motif ; sur approbation, la réponse de la seconde exécution contient le `FunctionCallContent` réémis et le `FunctionResultContent` (`JsonElement` pour une chaîne renvoyée par l'outil). Références : page Learn « Using function tools with human in the loop approvals » (`agents/tools/tool-approval`), sample `Agent_Step01_UsingFunctionToolsWithApprovals`, docs XML 1.22.0 de `ChatClientAgentOptions`, `ApprovalResponseBindingChatClient`, `ApprovalNotRequiredFunctionBypassingChatClient`.

Points découverts :
- **`ToolApprovalRequestContent.RequiresConfirmation` est `[Experimental]` (`MEAI001`)** : cité dans « Going further » de Lab09, pas enseigné.
- **« Don't ask again »** : `agent.AsBuilder().UseToolApproval()` (`ToolApprovalAgent`, stable depuis 1.14) + `request.CreateAlwaysApproveToolResponse()` / `CreateAlwaysApproveToolWithArgumentsResponse()` enregistrent des règles dans la session ; non enseigné (Going further).
- **Décision par du code** : la doc décrit la confirmation comme « a user prompt, a policy decision, or any other approver » ; Lab09 scénario 2 prend la décision dans une politique (`DeletionPolicy`) avec motif, mêmes contenus et même `CreateResponse`.
- **Usage** : chaque `RunAsync` a son propre `Usage` ; un flux d'approbation coûte au moins deux appels modèle, un rejet aussi (`UsageDetails.Add` pour cumuler).
- **Lab interactif** : `Console.ReadLine()` → `"interactive": true` dans le catalogue du dashboard (premier lab à l'utiliser) ; `null` (entrée fermée) = rejet.
- Le `Start` doit **utiliser** ses constantes partagées (`AgentInstructions`) hors des TODO, sinon `CS0219` en `-warnaserror` (Lab09 les affiche dans le bloc Setup) ; le Start de **Lab05** présente aujourd'hui ce défaut (4 `CS0219`), à corriger lors d'un passage ultérieur.

### 4.16 Agent exposé comme outil MCP (validé sur Lab10)

**Ancienne approche**
```csharp
// ModelContextProtocol.AspNetCore 0.5.0-preview.1 (Solution seulement : le Start ne compilait pas, CS0246) ; Microsoft.NET.Sdk.Web
ChatClientAgent agent = chatClient.CreateAIAgent(agentOptions);              // outils trouvés par réflexion (GetMethods)
McpServerTool tool = McpServerTool.Create(agent.AsAIFunction());
// Scénario « HTTP » (enum Scenario, un seul serveur bloquant par exécution)
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer().WithHttpTransport().WithTools([tool]);
var app = builder.Build(); app.MapMcp("/mcp/companyassistant"); app.UseHttpsRedirection(); app.Run();   // SSE + Streamable HTTP
// Scénario « stdio » : ColoredConsole écrivait sur stdout avant le serveur (8 lignes non JSON avant le premier message MCP)
var host = Host.CreateEmptyApplicationBuilder(settings: null);
host.Services.AddMcpServer().WithStdioServerTransport().WithTools([tool]); await host.Build().RunAsync();
```

**Nouvelle approche**
```csharp
// ModelContextProtocol.AspNetCore 2.2.0 (Start = Solution), Microsoft.NET.Sdk + usings explicites
if (args.Contains("--stdio")) Console.SetOut(Console.Error);                  // stdout réservé aux messages MCP
AIAgent companyAssistant = chatClient.AsAIAgent(instructions: "...", name: "CompanyAssistant", description: "...", tools: [...]);
McpServerTool tool = McpServerTool.Create(companyAssistant.AsAIFunction());   // nom/description de l'agent, paramètre "query"
// stdio (serveur lancé par un hôte MCP avec --stdio)
HostApplicationBuilder stdio = Host.CreateEmptyApplicationBuilder(settings: null);
stdio.Services.AddMcpServer().WithStdioServerTransport().WithTools([tool]); await stdio.Build().RunAsync();
// Streamable HTTP (sans état par défaut, pas de SSE)
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer().WithHttpTransport().WithTools([tool]);
WebApplication app = builder.Build(); app.MapMcp("/mcp/companyassistant"); app.Urls.Add(baseUrl); await app.StartAsync();
// Le lab se teste lui-même : client HTTP, puis hôte stdio qui se relance avec --stdio, puis agent client
await using McpClient http = await McpClient.CreateAsync(new HttpClientTransport(new() { Endpoint = new Uri(url), TransportMode = HttpTransportMode.StreamableHttp }));
CallToolResult result = await http.CallToolAsync("CompanyAssistant", new Dictionary<string, object?> { ["query"] = "..." });
await using McpClient stdioClient = await McpClient.CreateAsync(new StdioClientTransport(new() { Command = "dotnet",
    Arguments = [Assembly.GetEntryAssembly()!.Location, "--stdio"], StandardErrorLines = line => ... }));
AIAgent office = chatClient.AsAIAgent(instructions: "...", name: "OfficeAssistant", tools: [.. (await stdioClient.ListToolsAsync()).Cast<AITool>()]);
```

**Explication** — Les API serveur du SDK MCP C# n'ont pas changé de forme entre 0.5.0-preview.1 et 2.2.0 (`McpServerTool.Create(AIFunction)`, `AddMcpServer`, `WithHttpTransport`, `WithStdioServerTransport`, `WithTools`, `MapMcp`), mais **leurs valeurs par défaut** ont changé : le transport HTTP est **sans état** (aucune session MCP, chaque requête est indépendante) et le **SSE legacy n'est plus mappé** (§3). Côté MAF, `AsAIFunction(options, session)` (`PublicAPI.Shipped.txt` 1.22.0, source `AgentExtensions.cs`) nomme la fonction d'après l'agent (caractères non alphanumériques → `_`), reprend sa description, expose un seul paramètre `query` et renvoie `response.Text` ; **sans session, chaque appel crée une nouvelle session** (outil sans état ; une session fournie serait partagée par tous les appelants, ce que la doc XML déconseille). Référence officielle : sample `02-agents/Agents/Agent_Step07_AsMcpTool` (stdio, `Host.CreateEmptyApplicationBuilder`, packages `Microsoft.Extensions.Hosting` + `ModelContextProtocol`) ; transport HTTP : quickstart Learn .NET « Build a minimal MCP server » (template `mcpserver`, options stdio/http, `WithHttpTransport` + `MapMcp`) et docs XML de `ModelContextProtocol.AspNetCore` 2.2.0 ; hôte stdio : quickstart Learn .NET « Build a minimal MCP client » et sample `Agent_MCP_Server` (`StdioClientTransport`). **Changement conceptuel** : le lab n'exige plus MCP Inspector (Node.js) — il se teste lui-même avec la CLI `dotnet` (client MCP HTTP, hôte stdio qui relance le programme avec `--stdio`, agent client) ; `--http` / `--stdio` gardent un serveur pour MCP Inspector ou VS Code.

Points découverts :
- **Page Learn obsolète** : l'URL `agent-framework/tutorials/agents/agent-as-mcp-tool` sert désormais la page « Hosted MCP Tools » (sans rapport : outils MCP hébergés par Foundry) ; la page « Self-host agents as MCP tools » indique pour .NET *« coming soon »* (le package Python `agent-framework-hosting-mcp` n'a pas d'équivalent .NET). Le sample au tag fait foi.
- **stdout d'un serveur stdio** : toute ligne non JSON-RPC sur stdout est un message invalide. `Console.SetOut(Console.Error)` en mode `--stdio` **vérifié** (le transport écrit sur le flux stdout brut : stdout ne contient que les réponses JSON-RPC, le journal du lab part sur stderr) ; `Host.CreateEmptyApplicationBuilder(settings: null)` n'ajoute aucun fournisseur de log. Le client 2.2.0 tolérait les 8 lignes parasites de l'ancien serveur (ignorées), mais c'était contraire au protocole.
- **`dotnet run` avec build écrit sur stdout** : les warnings de build (NU1902/NU1903 hérités de `CommonUtilities`) précèdent le premier message MCP → un hôte MCP doit lancer `dotnet run --no-build -- --stdio` ou `dotnet <dll> --stdio` (vérifié). Le lab, hôte de ses scénarios 2-3, lance `dotnet <Assembly.GetEntryAssembly().Location> --stdio`.
- **Résultat de l'outil = chaîne JSON** : `AIFunctionFactory` sérialise la valeur de retour, donc le bloc texte MCP contient `"...\n..."` (déjà le cas avant la migration). `McpClientTool` le renvoie comme `TextContent`. Le helper d'affichage le décode ; `AIFunctionFactoryOptions.MarshalResult = (r, _, _) => new ValueTask<object?>(r)` passé à `AsAIFunction` donne du texte brut (**vérifié**, API stable) → « Going further ».
- **Erreur dans l'agent exposé** : le serveur MCP renvoie `IsError = true` et un message générique (`An error occurred invoking 'CompanyAssistant'.`), sans détail ; le serveur HTTP journalise l'exception (niveau warning). Un serveur stdio qui s'arrête → `ClientTransportClosedException: MCP server process exited unexpectedly (exit code: N)` + fin du stderr du serveur.
- **Package** : `ModelContextProtocol.AspNetCore` apporte un `FrameworkReference` `Microsoft.AspNetCore.App` : `WebApplication`, `Host`, l'injection de dépendances et les fournisseurs de configuration du `ConfigurationHelper` viennent du framework partagé → ni `Microsoft.Extensions.Configuration.*`, ni `Microsoft.Extensions.Hosting`, ni `Microsoft.Extensions.DependencyInjection` en référence explicite (pas de `NU1510` constaté avec eux, mais inutiles). `Microsoft.NET.Sdk` conservé (comme Lab06_A2AServer) : `using Microsoft.AspNetCore.Builder;`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection` explicites.
- **Journalisation ASP.NET Core** : en `Information`, chaque requête MCP produit ~8 lignes (Kestrel, routage, `McpServer`). `appsettings.json` → `Logging:LogLevel:Default = Warning` ; lu depuis le *content root* (dossier courant), donc effectif avec `dotnet run` depuis le dossier du projet et dans le dashboard (répertoire de travail = dossier du projet).
- **Port** : `McpServer:BaseUrl` = `http://localhost:5100` (5000 évité : AirPlay sur macOS, cf. Lab06) ; port occupé → `IOException: Failed to bind to address … address already in use`.
- **Usage** : seules les consommations de l'agent client (scénario 3) sont visibles ; celles de l'agent exposé sont faites dans le processus serveur et ne remontent pas par MCP.

### 4.17 Transport HTTP personnalisé (validé sur Lab11)

**Ancienne approche**
```csharp
// Azure.AI.OpenAI 2.7.0-beta.2 ; handler dérivé de HttpClientHandler dans son propre fichier (à compléter dans le Start)
var httpClient = new HttpClient(new CustomHttpClientHandler());            // CustomHttpClientHandler : HttpClientHandler
AzureOpenAIClient client = new(new Uri(settings.Endpoint), new ApiKeyCredential(settings.APIKey),
    new AzureOpenAIClientOptions { Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout = TimeSpan.FromSeconds(30) });
// Dans le handler : Authorization affiché sur ses 20 premiers caractères, tous les autres en-têtes en clair (dont api-key)
```

**Nouvelle approche**
```csharp
// Handler = DelegatingHandler (pattern des samples officiels), InnerHandler = le handler réseau
HttpLoggingHandler loggingHandler = new() { InnerHandler = new HttpClientHandler() };
using HttpClient httpClient = new(loggingHandler);
OpenAIClientOptions clientOptions = new()
{
    Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint),
    Transport = new HttpClientPipelineTransport(httpClient),                 // System.ClientModel.Primitives
    NetworkTimeout = TimeSpan.FromSeconds(30)                                // par tentative
};
OpenAIClient client = new(new ApiKeyCredential(settings.APIKey), clientOptions);   // ou BearerTokenPolicy (Entra ID)

internal sealed class HttpLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-agent-name", "...");                         // modifier la requête
        // journaliser : méthode, URL, en-têtes (Authorization / api-key masqués entièrement), corps
        long start = Stopwatch.GetTimestamp();
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        TimeSpan duration = Stopwatch.GetElapsedTime(start);
        // journaliser : statut, durée, en-têtes, corps (sauf text/event-stream)
        return response;
    }
}
```

**Explication** — Le concept est inchangé : `Transport` et `NetworkTimeout` sont des propriétés de `ClientPipelineOptions` (System.ClientModel), dont héritaient `AzureOpenAIClientOptions` et dont hérite `OpenAIClientOptions` ; `HttpClientPipelineTransport(HttpClient)` est stable (System.ClientModel 1.15.0, transitif via OpenAI 2.13.0). Seuls changent le client (endpoint v1) et le handler : **tous les samples officiels 1.22.0 qui injectent un `HttpClient` utilisent un `DelegatingHandler` avec `InnerHandler = new HttpClientHandler()`** (`Agent_Step25_FoundryToolboxMcp`, `Agent_Step26_FoundryToolboxMcpSkills`, `Agent_MCP_PerRun_AuthHeaders`, `LocalHttpSchemeRewriteHandler` des samples d'hébergement Foundry ; un seul dérive de `HttpClientHandler`) et `HttpClientPipelineTransport` (`AgentWebChat`, `Using-E2E-Resilience`). Le handler est placé dans le `HttpClient` : il voit la requête **finale** (après les politiques du SDK : en-têtes, retry, authentification), une fois **par tentative**, et la réponse avant le SDK. Références : samples cités au tag `dotnet-1.22.0`, docs XML `System.ClientModel` 1.15.0 (`ClientPipelineOptions`, `HttpClientPipelineTransport`, `PipelinePosition`), page Learn [System.ClientModel library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/system.clientmodel-readme).

Points découverts :
- **Le SDK `OpenAI` envoie la clé API dans `Authorization: Bearer <clé>`** (le SDK Azure l'envoyait dans `api-key`). Tout journal d'en-têtes doit masquer la valeur **entière** (schéma seul : `Bearer [MASKED]`). **Faille de l'ancien lab vérifiée** : avec une clé API, l'ancienne Solution affichait l'en-tête `api-key` **en clair** (compté sans être affiché : 1 ligne contenant la clé complète, 0 masquée) ; et son masquage d'`Authorization` (20 premiers caractères) aurait exposé 13 caractères de la clé avec le SDK `OpenAI`. Lab11 masque dans un helper fourni (`HttpConsole.MaskIfSensitive`) et un check du dashboard échoue si un en-tête d'identifiant apparaît en clair.
- **Une exécution = une ou plusieurs requêtes HTTP** : avec un outil, la 1ʳᵉ réponse a `"finish_reason": "tool_calls"`, la 2ᵉ requête rejoue la conversation + `tool_calls` + un message `"role": "tool"` (résultat d'outil sérialisé en chaîne JSON, cf. §4.14). `AgentResponse.Usage` = somme des `usage` des réponses (vérifié : 74 + 111 = 185 en entrée, 14 + 17 = 31 en sortie).
- **Chaque tentative passe par le handler** (vérifié hors ligne : `503` puis `200` → 2 passages pour un seul `RunAsync`). Délai dépassé : `ClientRetryPolicy` par défaut → 4 tentatives puis `AggregateException: Retry failed after 4 tries. (The operation was cancelled because it exceeded the configured timeout of …)` (vérifié) ; sans retry, `TaskCanceledException` avec le même message. `InnerHandler` absent : `InvalidOperationException: The inner handler has not been assigned.` (vérifié).
- **Lire les corps** : `ReadAsStringAsync` met le contenu en mémoire tampon, le SDK le relit ensuite (requête `MessageBodyAdapter` et réponse, vérifié). Ne pas lire un corps `text/event-stream` (streaming) : le handler attendrait la fin du flux.
- **Le jeton Entra ID n'est pas obtenu par ce `HttpClient`** (`DefaultAzureCredential` a ses propres moyens) : seules les requêtes Azure OpenAI sont journalisées.
- **`User-Agent`** : `OpenAI/2.14.0 (…),MEAI/10.10.1 agent-framework-dotnet/1.26.0 (…)` (1.26.0 ; `OpenAI/2.13.0 … MEAI/10.10.0 … 1.22.0` avant) — chaque couche s'identifie (utile pédagogiquement).
- **Alternatives vérifiées, citées en « Going further »** : journalisation intégrée `ClientPipelineOptions.ClientLoggingOptions` (`LoggerFactory`, `EnableMessageContentLogging`, stable ; en-têtes hors `AllowedHeaderNames` écrits `REDACTED`, `Authorization` compris — vérifié avec `Microsoft.Extensions.Logging.Console`) ; `PipelinePolicy` + `AddPolicy(policy, PipelinePosition.BeforeTransport)` (samples d'hébergement Foundry ; voit déjà `Authorization` — vérifié) ; `IHttpClientFactory` + `AddHttpMessageHandler` ; `UseOpenTelemetry` (`Agent_Step05_Observability`).
- **Écho de `x-ms-client-request-id`** : Azure OpenAI renvoie en réponse la valeur envoyée (observé), mais ce comportement n'est pas documenté pour Azure OpenAI : non enseigné.
- **Spinner** : pas de `WithSpinner()` dans un lab dont le handler écrit sur la console (les deux s'entremêleraient).

### 4.18 `AIContextProvider` : mémoire à long terme de l'utilisateur (validé sur Lab12)

**Ancienne approche**
```csharp
// Un provider par thread (fabrique), état dans des champs, méthodes publiques surchargées
ChatClientAgent agent = chatClient.CreateAIAgent(new ChatClientAgentOptions
{
    ChatOptions = chatOptions,
    AIContextProviderFactory = _ => new UserMemoryAIContextProvider(extractorAgent, userId, repository)
});
public override async ValueTask<AIContext> InvokingAsync(InvokingContext context, CancellationToken ct)
{
    _userMemories = await _repository.GetUserMemoriesAsync(_userId);          // champ relu dans InvokedAsync
    return new AIContext { Instructions = string.Join("\n", _userMemories.Select(x => x.Memory)) };
}
public override async ValueTask InvokedAsync(InvokedContext context, CancellationToken ct) { /* extraction, userId écrit par le modèle */ }
```

**Nouvelle approche**
```csharp
AIAgent memoryAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "...",
    ChatOptions = new() { Instructions = "..." },
    AIContextProviders = [new UserMemoryAIContextProvider(extractor, repository, stateInitializer: _ => new UserMemoryAIContextProvider.State(userId))]
});
AgentResponse greeting = await memoryAgent.RunAsync(await memoryAgent.CreateSessionAsync());   // l'agent parle en premier : le provider est appelé

internal sealed class UserMemoryAIContextProvider : AIContextProvider
{
    private readonly ProviderSessionState<State> _sessionState;        // new(stateInitializer, stateKey: nameof(UserMemoryAIContextProvider))
    public override IReadOnlyList<string> StateKeys => [_sessionState.StateKey];
    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken ct = default)
    {
        string userId = _sessionState.GetOrInitializeState(context.Session).UserId;
        List<string> memories = await _repository.GetMemoriesAsync(userId, ct);
        return memories.Count == 0 ? new AIContext() : new AIContext { Instructions = "What you remember about the user:\n" + ... };
    }
    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken ct = default)
    {
        List<ChatMessage> userMessages = context.RequestMessages.Where(m => m.Role == ChatRole.User).ToList();   // messages passés à RunAsync seulement
        AgentResponse<ExtractedMemories> extraction = await _extractor.RunAsync<ExtractedMemories>(
            [new(ChatRole.System, $"Facts already known ...:\n{string.Join("\n", known.DefaultIfEmpty("(none)"))}"), .. userMessages], cancellationToken: ct);
        // stockage avec le userId de la session, jamais celui du modèle
    }
    public sealed class State(string userId) { public string UserId { get; } = userId; }
}
```

**Explication** — Les méthodes publiques `InvokingAsync` / `InvokedAsync` d'`AIContextProvider` ne sont plus virtuelles (l'ancien code ne compile plus : **`CS0506`**, vérifié) : elles appellent `InvokingCoreAsync` / `InvokedCoreAsync`, dont l'implémentation de base filtre les messages (par défaut seulement `AgentRequestMessageSourceType.External`, c'est-à-dire ceux passés à `RunAsync`), appelle **`ProvideAIContextAsync`** / **`StoreAIContextAsync`** (les deux méthodes à surcharger dans le cas courant), marque les messages fournis comme venant du provider et fusionne l'`AIContext` (instructions concaténées avec `\n`, messages et outils ajoutés) ; `StoreAIContextAsync` n'est **pas appelé si le run a échoué** (`InvokeException`). `ChatClientAgentOptions.AIContextProviderFactory` n'existe plus (**`CS0117`**, vérifié) : `AIContextProviders` reçoit des **instances** (une par agent, partagées par toutes ses sessions, appelées dans l'ordre). **Changement conceptuel** : le provider ne garde aucune donnée de session dans ses champs (doc Learn « Context Providers » : *« shouldn't have an id for the specific set of memories in a field »*) ; la portée (ici l'id de l'utilisateur) vit dans `AgentSession.StateBag` via `ProviderSessionState<T>`, initialisée par un **`stateInitializer`** fourni par l'application — pattern de `Mem0Provider` et `ChatHistoryMemoryProvider` (portées de stockage / de recherche par utilisateur). Les mémoires appartiennent à l'utilisateur (MongoDB) et non à la session : une nouvelle session (historique vide) les retrouve. Toutes ces API sont dans `PublicAPI.Shipped.txt` 1.22.0 et non expérimentales (seuls les constructeurs de `InvokingContext` / `InvokedContext` le sont, `MAAI001`, inutiles ici). Références : samples `01-get-started/04_memory` (`UserInfoMemory` : `ProvideAIContextAsync` + `StoreAIContextAsync` avec extraction en sortie structurée), `02-agents/Agents/Agent_Step17_AdditionalAIContext` (deux providers, `MessageAIContextProvider`, outils fournis par un provider, filtre de l'historique), `02-agents/AgentWithMemory/AgentWithMemory_Step02_MemoryUsingMem0` et `_Step08_MemoryUsingCosmosNoSql` (`stateInitializer` + portée utilisateur) ; sources `AIContextProvider.cs`, `ProviderSessionState{TState}.cs`, `ChatClientAgent.cs` au tag ; pages Learn [Context Providers](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/context-providers?pivots=programming-language-csharp) (2026-09-09) et [Context provider integrations](https://learn.microsoft.com/agent-framework/integrations/by-component/context-providers/?pivots=programming-language-csharp).

Points découverts :
- **Vérifié hors ligne** (`IChatClient` factice) : `ProvideAIContextAsync` ne reçoit dans `context.AIContext.Messages` que le message du run, même quand l'historique en contient d'autres ; `StoreAIContextAsync` ne reçoit que la nouvelle requête ; l'état `{"userId":"ada"}` est sérialisé dans `stateBag.UserMemoryAIContextProvider` par `SerializeSessionAsync` et relu après `DeserializeSessionAsync` ; deux providers avec la même clé d'état → `InvalidOperationException: Multiple providers use the same state key '…'` à la création de l'agent.
- **Le provider s'exécute dans `RunAsync`** (`StoreAIContextAsync` attendu avant le retour, source `ChatClientAgent.RunCoreAsync`) : une extraction par message ajoute un appel modèle (~250 tokens) et de la latence ; une exception du provider remonte à l'appelant (les providers intégrés, eux, journalisent et continuent).
- **`RunAsync(session)` sans message** fonctionne avec Chat Completions (requête avec le seul message `system`) : le provider fournit le contexte, `StoreAIContextAsync` ne voit aucun message utilisateur.
- **Coût mesuré** (`gpt-4o-mini`) : la question de la nouvelle session coûte 119 tokens d'entrée avec 5 souvenirs dans les instructions, 71 sans.
- **Prompt d'extraction** : avec un message système *« Facts already known about the user …: »* suivi d'une liste **vide**, `gpt-4o-mini` n'a rien extrait de la présentation (4 runs sur 4) ; avec `(none)` (`DefaultIfEmpty("(none)")`), les 5 faits sont extraits (3/3) et rien n'est ré-extrait quand ils sont connus (3/3). Un exemple d'instruction qui reprend la donnée de test (*« The user's name is Ada. »*) est aussi à éviter.
- **Coquille Learn** (2026-09) : la page « Context Providers » écrit `public override string StateKey => …` ; l'API 1.22.0 est `public override IReadOnlyList<string> StateKeys` (comme les samples).
- Un provider qui écrit sur la console pendant le run : pas de `WithSpinner()` (cf. §4.17).
- **Sécurité** : ce que renvoie un provider est fusionné tel quel (instructions = confiance maximale) — injection indirecte possible depuis le stockage ; la portée doit venir de l'application (l'ancien lab demandait au modèle de recopier le `userId` dans sa sortie structurée).

---

## 5. APIs / packages supprimés ou remplacés

| Nom | Statut | Impact | Remplacement recommandé | Exercices touchés |
|---|---|---|---|---|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | Retiré des samples et des dépendances MAF (1.21.0) ; package encore publié mais sa dernière version stable (2.1.0) est antérieure à la v1 API | Construction du client dans tous les labs | `OpenAI.OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` + `ApiKeyCredential` ou `BearerTokenPolicy` | **tous** |
| `CreateAIAgent` / `GetAIAgent` | Renommés (preview.260121) | Compilation | `AsAIAgent` | tous |
| `AgentRunResponse` / `AgentRunResponseUpdate` | Renommés | Compilation | `AgentResponse` / `AgentResponseUpdate` | tous |
| `AgentThread`, `GetNewThread`, `DeserializeThread`, `thread.Serialize()` | Renommés / déplacés / async | Compilation + concept | `AgentSession`, `CreateSessionAsync`, `SerializeSessionAsync` / `DeserializeSessionAsync` sur `AIAgent` (§4.11) | Lab05 ✅, Lab09 ✅, Lab12 ✅ |
| `ChatMessageStore`, `ChatMessageStoreFactory` | Renommés + refondus | Refonte | `ChatHistoryProvider` (instance unique, `ChatClientAgentOptions.ChatHistoryProvider`) + `ProviderSessionState<T>` ; `ChatClientAgentOptions.Instructions` → `ChatOptions.Instructions` (§4.11) | Lab05 ✅ |
| `AIContextProvider` (API preview) : `public override InvokingAsync` / `InvokedAsync`, état dans des champs | Refondu (méthodes non virtuelles, StateBag, composition) | Compilation (`CS0506`) + concept | `protected override ProvideAIContextAsync` / `StoreAIContextAsync` ; provider sans état, état par session via `ProviderSessionState<T>` + `stateInitializer` ; `MessageAIContextProvider` pour un provider qui n'ajoute que des messages (§4.18) | Lab12 ✅ |
| `ChatClientAgentOptions.AIContextProviderFactory` (un provider par thread) | Supprimé | Compilation | `ChatClientAgentOptions.AIContextProviders = [...]` (une instance par agent, partagée par ses sessions ; plusieurs providers appelés dans l'ordre) (§4.18) | Lab12 ✅ |
| `CommonUtilities.MongoDbHealthCheck` (+ `Environment.Exit(1)`) | Supprimé (phase 0) : plus aucun lab ne l'utilisait après Lab12 | Compilation (labs non migrés : aucun usage constaté) | `ConfigurationHelper.ConnectToMongoDbAsync()` du lab (ping, `ServerSelectionTimeout` 5 s, message actionnable) | Lab05 ✅, Lab12 ✅ |
| `AgentResponse.Deserialize<T>()`, `RunAsync<T>` limité à `ChatClientAgent`, `AIJsonUtilities.CreateJsonSchema` + `ForJsonSchema(schema, …)` | Supprimé / généralisé / simplifié | Compilation (Deserialize) + pédagogie | `JsonSerializer.Deserialize<T>(response.Text, …)`, `AIAgent.RunAsync<T>`, `ChatResponseFormat.ForJsonSchema<T>()` | Lab02 |
| `ModelContextProtocol 0.5.0-preview.1` | Préversion ; 2.2.0 stable | Aucune rupture pour le client HTTP (`McpClient.CreateAsync`, `HttpClientTransport(Options)`, `ListToolsAsync`, `McpClientTool` inchangés) ; côté serveur, API inchangées mais valeurs par défaut changées (§3, §4.16) | `ModelContextProtocol` 2.2.0 / `ModelContextProtocol.AspNetCore` 2.2.0 | Lab04 ✅, Lab10 ✅ |
| SSE legacy de `MapMcp` (`…/sse`, `…/message`) | Désactivé par défaut (`EnableLegacySse = false`), impossible en mode sans état | Clients SSE : `405` / `404` | Streamable HTTP (`HttpTransportMode.StreamableHttp` côté client) | Lab10 ✅ |
| `app.UseHttpsRedirection()` (Lab10) | Sans effet : aucun endpoint HTTPS | Pédagogie | — (supprimé ; serveur sur `http://localhost` seulement) | Lab10 ✅ |
| `Microsoft.NET.Sdk.Web` (Solution) / `Microsoft.Extensions.Hosting` (Start) pour un serveur MCP | Start ≠ Solution | Socle | `Microsoft.NET.Sdk` + `ModelContextProtocol.AspNetCore` (framework partagé ASP.NET Core) | Lab10 ✅ |
| `McpClientFactory.CreateAsync` | Supprimé (API 0.x) — encore montré par la page Learn « Using MCP tools » en C# au 2026-09-23 | Compilation | `McpClient.CreateAsync` (sample `Agent_MCP_Server` au tag) | Lab04, Lab10 |
| `clientFactory: c => new ConfigureOptionsChatClient(c, o => …)` pour fixer `MaxOutputTokens` / `Temperature` | Toujours disponible (MEAI) mais détourné : c'est un middleware `IChatClient` | Pédagogie | `AsAIAgent(new ChatClientAgentOptions { ChatOptions = new() { Instructions, Tools, MaxOutputTokens, Temperature } })` | Lab04 |
| `Microsoft.Extensions.DependencyInjection` (transitif via Hosting) | N'est plus apporté une fois Hosting retiré (MAF n'apporte que `.Abstractions`) | Compilation (`ServiceCollection`) | Référence explicite `Microsoft.Extensions.DependencyInjection` 10.0.12 | Lab03 (et tout lab console qui construit un `ServiceCollection` : MAS) ; inutile pour Lab10 (framework partagé ASP.NET Core via `ModelContextProtocol.AspNetCore`) |
| `UserInputRequests`, `UserInputRequestContent` | Supprimés | Compilation + concept | `response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>()` + boucle `while` ; session obligatoire (§4.15) | Lab09 ✅ |
| `FunctionApprovalRequestContent` (`.FunctionCall`) | Renommé (MEAI 10.x) | Compilation | `ToolApprovalRequestContent` (`.ToolCall` à caster en `FunctionCallContent`), `CreateResponse(approved, reason)` (§4.15) | Lab09 ✅ |
| `ReflectingExecutor` | Obsolète | Warning puis suppression | Executors source-générés | MAS-Lab02/03 (à vérifier) |
| `Microsoft.SemanticKernel.Connectors.InMemory` / `.MongoDB` | Toujours en préversion | Viole la règle « stable uniquement » | `CommunityToolkit.VectorData.InMemory` (stable) ; MongoDB : `ChatHistoryProvider` sur `MongoDB.Driver` 3.12.0 (Lab05) ; recherche vectorielle : `InMemoryVectorStore` (Lab07, §4.13) | Lab05 ✅, Lab07 ✅ |
| `MongoVectorStore` (SK) + MongoDB Atlas Vector Search | Préversion, service externe | Lab07 non exécutable sans compte Atlas | `InMemoryVectorStore` ; tout connecteur `Microsoft.Extensions.VectorData` stable en « Going further » (§4.13) | Lab07 ✅ |
| `Microsoft.Extensions.VectorData.Abstractions` 9.7.0 (référence explicite) | Remplacé | Version | 10.10.0 transitif via MAF 1.22.0 | Lab05 ✅, Lab07 ✅ |
| `VectorSearchOptions<T> { IncludeVectors = false }` + `SearchAsync(...).ToListAsync()` | Toujours disponible mais inutile | Pédagogie | `await foreach (VectorSearchResult<T> r in collection.SearchAsync(value, top))` (vecteurs exclus par défaut) | Lab07 ✅ |
| `A2AClient.GetAIAgent()` | Renommé (A2A SDK v1) | Compilation | `IA2AClient.AsAIAgent(name, description)` ; `AgentCard.AsAIAgent()` ; `A2ACardResolver.GetAIAgentAsync()` inchangé (§4.12) | Lab06 ✅ |
| `app.MapA2A(agent, path, agentCard, taskManager => …)`, `ITaskManager`, `AgentCard.Url` | Supprimés (A2A SDK v1) | Compilation + protocole | `AddA2AServer` + `MapA2AJsonRpc` / `MapA2AHttpJson` + `MapWellKnownAgentCard(card, path)` ; `AgentCard.SupportedInterfaces` (§4.12) | Lab06 ✅ |
| `Microsoft.Agents.Hosting.AspNetCore` 1.4.9-beta | Inutile (SDK M365 Agents) | Dépendance en préversion | — (supprimé) | Lab06 ✅ |
| `AzureOpenAIClientOptions { Transport, NetworkTimeout }` | Remplacé avec `AzureOpenAIClient` | Construction du client | `OpenAIClientOptions { Endpoint, Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout }` (mêmes propriétés héritées de `ClientPipelineOptions`, §4.17) | Lab11 ✅ |
| Handler d'interception dérivé de `HttpClientHandler` | Toujours disponible, mais les samples officiels utilisent `DelegatingHandler` | Pédagogie | `DelegatingHandler` + `InnerHandler = new HttpClientHandler()` (§4.17) | Lab11 ✅ |
| Journal d'en-têtes masquant `Authorization` après 20 caractères, `api-key` en clair | Fuite de secret (la clé API complète s'affichait avec le SDK Azure ; avec le SDK `OpenAI`, la clé voyage dans `Authorization: Bearer`) | **Sécurité** | Masquage complet des en-têtes d'identifiant (schéma seul), helper fourni `HttpConsole.MaskIfSensitive` + check du dashboard (§4.17) | Lab11 ✅ |
| `Microsoft.Extensions.Hosting` (usage « config seulement ») | Surdimensionné | Dépendance inutile | `Microsoft.Extensions.Configuration.Json/UserSecrets/EnvironmentVariables/Binder` | tous les labs console |
| `Azure.Identity 1.18.0-beta.2` | Préversion | Règle « stable » | `Azure.Identity 1.21.0` | tous |
| `ToonNet` 1.0.4 / `ToonNetSerializer` 1.0.0 (packages tiers « TOON », Lab08) | Non officiels ; `ToonNetSerializer` n'existe plus sur NuGet (`NU1101`, le Start ne restaurait plus) | Restauration du Start impossible ; règle 12 | Aucun package : outil renvoyant des objets (JSON par le framework) ou du CSV encodé par `HotelCsv.cs` fourni (§4.14) | Lab08 ✅ |
| `MongoDB.Driver 2.30.0` (CommonUtilities) | Vulnérabilités transitives (`Snappier` 1.0.0 *high*, `SharpCompress` 0.30.1 *moderate*) | Warnings NU1902/NU1903 dans **tous** les labs | **Retiré de CommonUtilities (phase 0, Lab12)** : `CommonUtilities` n'a plus aucune dépendance ; Lab05 et Lab12 référencent `MongoDB.Driver 3.12.0` directement ; Lab07 ne référence plus MongoDB. Plus aucun NU1902/NU1903 dans les labs AzureOpenAI (non-régression : 34 projets recompilés, cf. rapport Lab12) | ✅ tous |

---

## 6. Bonnes pratiques à appliquer à chaque exercice

1. **Une seule version MAF** pour tout le lab : la version cible du §1, `1.26.0` (Start **et** Solution identiques ; plus de mélange de previews).
2. **Référencer le package le plus haut niveau utile** (`Microsoft.Agents.AI.OpenAI`) et laisser `OpenAI` / `Microsoft.Extensions.AI` venir en transitif, sauf besoin explicite d'API plus récente ou de correctif (cas de `Microsoft.Extensions.AI.OpenAI` 10.11.0, §8).
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
| 2 | Graphe de dépendances | `dotnet list <proj> package --include-transitive` | MAF = version cible (§1) ; pas de `Azure.AI.OpenAI` |
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
- **Cadence de release** : MAF publie ~1 version/semaine avec des `[BREAKING]` dans les zones Skills, Harness, Hosting, A2A, MCP. Figer la version cible (1.22.0 pendant la migration, 1.26.0 depuis le 2026-10-25) ; ne monter de version que dans une vague dédiée, testée sur tous les labs (§3 bis).
- **Lab06 (A2A)** : packages uniquement en préversion → exception à la règle « stable ».
- **Régression streaming Azure OpenAI (constatée le 2026-09-28, résolue le 2026-10-25)** : quand le déploiement utilise le filtre de contenu **asynchrone**, Azure envoie en streaming Chat Completions des annotations **sans `delta`** ; `Microsoft.Extensions.AI.OpenAI` 10.10.x lève alors `InvalidOperationException: The requested operation requires an element of type 'Object', but the target element has type 'Null'` ([dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790)). Impact : tout `RunStreamingAsync` (Lab01 scénario 5, Lab02 scénario 4) et l'hébergement A2A, qui exécute toujours en streaming (*"Agent handler did not produce any response events"*). **Correctif** : #7790 fermé le 2026-10-12 par [dotnet/extensions#7815](https://github.com/dotnet/extensions/pull/7815), qui s'appuie sur `OpenAI` 2.14.0, publié dans `Microsoft.Extensions.AI.OpenAI` **10.11.0** (2026-10-15). MAF 1.22.0 (et encore 1.26.0) n'apporte que 10.10.x : **Lab01, Lab02 et Lab06_A2AServer référencent explicitement 10.11.0** (Start et Solution), qui apporte `OpenAI` 2.14.0 et `Microsoft.Extensions.AI.Abstractions` 10.11.0. `StreamingWorkaround.cs` et le TODO 4 de Lab06_A2AServer sont supprimés ; les agents sont créés avec `chatClient.AsAIAgent(...)`. **Vérifié le 2026-10-25** (CLI, attentes du dashboard) avec clé API **et** Entra ID : Lab01 7/7, Lab02 9/9, Lab06 serveur 10/10 et client 9/9, aucune exception côté serveur. À cette date, le déploiement de test n'émettait plus de chunks sans `delta` (capture SSE : 0 sur 106) : la référence 10.11.0 protège les déploiements qui utilisent le filtre asynchrone, en particulier ceux des apprenants. Lors de la montée de version de MAF, la garder tant que MAF n'apporte pas 10.11.0 ou plus.
- **Analyse graphify** : l'extracteur AST échoue sur 5 `Program.cs` à top-level statements (Lab01/02/03/04/07 Solution) ; sans impact sur la compilation, mais ces fichiers sont sous-représentés dans la carte.
