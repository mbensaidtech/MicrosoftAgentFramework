# Audit de pertinence, de fraîcheur et de couverture pédagogique des labs Microsoft Agent Framework

| | |
|---|---|
| **Date de l'audit** | 2026-10-25 |
| **Périmètre** | Les 16 labs de `LearningLabs/` : 13 labs de la piste *AzureOpenAI* (Lab01 → Lab12, Lab06 en deux projets serveur/client) et 3 labs de la piste *MultiAgentSystem* |
| **Version de référence du framework** | Microsoft Agent Framework .NET **1.26.0** (tag `dotnet-1.26.0`, publié le 2026-10-23), dernière version publiée au jour de l'audit |
| **Version utilisée par les labs** | **1.22.0** (piste AzureOpenAI) et **1.0.0-preview.251204.1 / Workflows 1.0.0-preview.251219.1** (piste MultiAgentSystem) |
| **Nature du livrable** | Diagnostic et recommandations. **Aucun lab n'a été modifié.** |

---

## Sommaire

1. [Résumé exécutif](#1-résumé-exécutif)
2. [Méthodologie et sources](#2-méthodologie-et-sources)
3. [Inventaire des labs existants](#3-inventaire-des-labs-existants)
4. [Évaluation individuelle des labs](#4-évaluation-individuelle-des-labs)
5. [Concepts importants manquants](#5-concepts-importants-manquants)
6. [Matrice de couverture pédagogique](#6-matrice-de-couverture-pédagogique)
7. [Plan d'action priorisé](#7-plan-daction-priorisé)
8. [Parcours pédagogique recommandé](#8-parcours-pédagogique-recommandé)
9. [Références officielles](#9-références-officielles)
10. [Annexes](#10-annexes)

---

## 1. Résumé exécutif

### 1.1 État général

Le parcours tient en deux pistes de maturité très différente.

**La piste AzureOpenAI (13 labs) est techniquement à jour et pédagogiquement solide sur son périmètre.** Ce périmètre est l'agent individuel. Les labs utilisent la version stable 1.22.0. Elle a quatre versions mineures de retard sur la 1.26.0, et ce gel est volontaire et documenté dans `Migration/Migration-Manual.md`. Le client Azure OpenAI est construit exactement comme dans l'exemple officiel `Agent_With_AzureOpenAIChatCompletion` : `OpenAIClient`, endpoint `/openai/v1/` et `BearerTokenPolicy`. Les README sont précis et à jour, avec de nombreuses références officielles.

**La piste MultiAgentSystem (3 labs) est fortement obsolète.** Elle n'a pas été migrée. Elle utilise des API qui **n'existent plus** dans le code source actuel du framework : `ChatClient.CreateAIAgent`, `AgentRunResponse<T>`, `InProcessExecution.StreamAsync`, `AzureOpenAIClient`. Elle n'est pas enregistrée dans le dashboard. Elle aborde les orchestrations sans enseigner le modèle de workflow (executors, edges, événements) sur lequel elles reposent.

**Surtout, le parcours ne couvre qu'une partie des fondamentaux que Microsoft met aujourd'hui en avant.** La documentation officielle présente le framework en trois piliers : *Agents*, *Workflows* et *Harness Agents*. Elle cite comme « foundational building blocks » les clients de modèle (Chat Completions **et** Responses), la session, les context providers, le **middleware** et les clients MCP. Plusieurs de ces piliers ne sont enseignés par aucun lab, ou seulement avec une API obsolète :
- les workflows ;
- le middleware d'agent ;
- l'observabilité ;
- l'hébergement ;
- l'Agent Harness ;
- l'évaluation.

### 1.2 Principaux constats

| # | Constat | Nature |
|---|---|---|
| 1 | Les 13 labs AzureOpenAI sont alignés sur les API 1.x (`AsAIAgent`, `AgentResponse`, `AgentSession`, `ChatHistoryProvider`, `AIContextProvider`, `ToolApprovalRequestContent`, MCP 2.2, A2A v1). Aucun n'enseigne d'API supprimée. | Fait vérifié (code + release notes) |
| 2 | Les 3 labs MultiAgentSystem utilisent des API supprimées ou renommées, des paquets en préversion de décembre 2025 et `Azure.AI.OpenAI`, dont le framework ne dépend plus depuis la 1.21.0 (PR #7986). | Fait vérifié |
| 3 | **Les workflows ne sont enseignés nulle part avec l'API actuelle.** C'est l'étape 5 du *Get Started* officiel et l'un des trois piliers du framework. | Fait vérifié + interprétation (priorité P0) |
| 4 | **Le middleware n'est enseigné que pour les appels de fonction** (Lab03, scénario 4). Ni le middleware d'exécution d'agent, ni le middleware de chat client, ni les garde-fous (termination, result overrides, gestion d'exceptions) ne sont traités. | Fait vérifié |
| 5 | La documentation Azure OpenAI indique : *« Responses is the recommended primary client »*. Tous les labs utilisent Chat Completions, un choix pédagogique justifié par le manuel de migration (historique géré localement). Pourtant, l'apprenant n'exécute jamais un agent sur l'API Responses : elle n'apparaît que dans les sections « Going Further ». | Fait vérifié + interprétation |
| 6 | Deux labs de la piste principale portent sur des sujets périphériques au framework : le format des données (Lab08) et le transport HTTP du SDK OpenAI (Lab11). L'observabilité OpenTelemetry, approche officielle pour « voir ce que fait l'agent », n'est enseignée nulle part. | Fait vérifié + interprétation |
| 7 | Le concept « agent comme outil » (`AsAIFunction`) est enseigné trois fois : Lab06 client (scénario 3), Lab10 et MAS-Lab01. La seule version dédiée (MAS-Lab01) utilise l'API obsolète. | Fait vérifié |
| 8 | Le contournement temporaire de streaming de Lab06_A2AServer peut sans doute être retiré. Le bug amont (dotnet/extensions#7790) est fermé depuis le 2026-10-12 et cité dans les notes de `Microsoft.Extensions.AI` 10.11.0, mais MAF 1.26.0 référence encore 10.10.1. À re-tester : Lab01 scénario 5, Lab02 scénario 4 et Lab06. | Fait vérifié + action à valider par test |
| 9 | De la 1.23 à la 1.26, plusieurs changements *[BREAKING]* touchent les approbations d'outils (Lab09) et A2A (Lab06, SDK 1.0.0-preview3). Une montée de version doit être planifiée et testée, pas faite à la volée. | Fait vérifié (release notes) |

### 1.3 Synthèse des verdicts

| Classement | Labs |
|---|---|
| **À conserver** (6) | Lab02 Structured Output · Lab04 MCP Client · Lab06 A2A Client · Lab07 Agentic RAG · Lab09 Human Approval · Lab12 AI Context Provider |
| **À améliorer** (6) | Lab01 First Agent · Lab03 Function Tools · Lab05 Sessions · Lab06 A2A Server · Lab10 Agent as MCP Tool · Lab11 Custom HTTP Transport |
| **Partiellement obsolète** (0) | — |
| **Fortement obsolète** (3) | MAS-Lab01 Use Agent as Tool · MAS-Lab02 Orchestration Sequential · MAS-Lab03 Orchestration Concurrent |
| **À supprimer ou remplacer** (1) | Lab08 Data Format Comparison (remplacement par fusion dans un lab « Contexte et coûts ») |

### 1.4 Priorités

- **P0 — critique**
  - Refondre les 3 labs MultiAgentSystem.
  - Créer un lab **Workflows fondamentaux** (executors, edges, événements) avant les orchestrations.
  - Créer un lab **Middleware et garde-fous**.
  - Re-tester le streaming et retirer le contournement de Lab06.
- **P1 — élevée**
  - Créer des labs **Observabilité (OpenTelemetry)**, **Évaluation**, **Human-in-the-loop et checkpoints dans les workflows**, **Agent Harness** et **Hébergement et injection de dépendances**.
  - Introduire l'API **Responses** dans Lab01 et Lab05.
  - Renforcer la **sécurité** dans les labs existants, à partir de la page *Agent Safety*.
- **P2 — moyenne**
  - Créer des labs **Gestion du contexte (réduction et compaction)**, qui remplace Lab08, **Handoff et Group Chat**, **Agent Skills**.
  - Ajouter le multimodal.
  - Monter de version vers 1.26.x.
- **P3 — faible** : Magentic, agents et workflows déclaratifs, AG-UI et DevUI, hébergement managé (Durable, Foundry Hosted Agents), capacités expérimentales (looping, background agents, CodeAct), agents personnalisés et routage multi-modèles.

---

## 2. Méthodologie et sources

### 2.1 Démarche

1. **Inventaire du projet.** J'ai lu les README, les `Program.cs` et les classes des projets `Solution`, les `.csproj` (versions de paquets), le registre du dashboard (`Dashboard/LabDashboard/labs.json`), le `README.md` racine (parcours d'apprentissage) et les documents de migration (`Migration/Migration-Manual.md`, `Migration-Plan.md`, rapports par lab).
2. **Cartographie des API utilisées.** J'ai recherché par motif, dans le code `Solution` de chaque lab, les API du framework (`AsAIAgent`, `RunStreamingAsync`, `RunAsync<T>`, `ApprovalRequiredAIFunction`, `UseOpenTelemetry`, `ChatHistoryProvider`, `AIContextProvider`, `AgentWorkflowBuilder`, `AddAIAgent`, etc.). La matrice de couverture (§6) part de ce relevé, complété par les sections « Going Further » des README.
3. **Sources officielles**
   - **Microsoft Learn.** J'ai téléchargé le HTML brut d'une centaine de pages (environ 105) avec le pivot `?pivots=programming-language-csharp`. Seul le contenu C# et le contenu commun ont été conservés. La lecture a été répartie entre trois sous-agents de recherche, et j'ai revérifié moi-même les citations décisives.
   - **Dépôt GitHub `microsoft/agent-framework`**
     - l'arborescence de `dotnet/samples` sur `main` et au tag `dotnet-1.22.0` ;
     - les notes de version `dotnet-1.23.0` à `dotnet-1.26.0` ;
     - `dotnet/Directory.Packages.props` au tag `dotnet-1.26.0` ;
     - le code source au commit `67ba500` (2026-10-24), pour confirmer la disparition des anciennes API ;
     - les PR citées.
   - **NuGet.** J'ai relevé les versions publiées et le statut stable, préversion ou alpha de chaque paquet.
4. **Comparaison.** Pour chaque lab, j'ai comparé les objectifs annoncés et le code réel aux recommandations officielles. J'ai distingué l'obsolescence **technique** (API, paquets, versions) de l'obsolescence **pédagogique** (concept devenu secondaire, approche non recommandée, ordre de progression).

### 2.2 Version de référence

| Élément | Valeur constatée le 2026-10-25 | Source |
|---|---|---|
| Dernière version .NET du framework | `dotnet-1.26.0` (2026-10-23). Les précédentes sont 1.25.0 (10-14), 1.24.0 (10-07), 1.23.0 (10-01) et 1.22.0 (09-18). | [GitHub Releases](https://github.com/microsoft/agent-framework/releases) |
| GA du framework | `1.0.0` le 2026-04-02 | `Migration/Migration-Manual.md` §1 ; release notes |
| Paquets **stables** 1.26.0 | `Microsoft.Agents.AI`, `.Abstractions`, `.OpenAI`, `.Workflows`, `.Workflows.Generators`, `.Workflows.Declarative`, `.Workflows.Declarative.Mcp`, `.Harness` (stable depuis 1.14.0), `.Anthropic`, `.GitHub.Copilot` | NuGet ; `.csproj` (`<IsReleased>true`) |
| Paquets **préversion** | `.A2A`, `.Hosting`, `.Hosting.AspNetCore`, `.Hosting.A2A(.AspNetCore)`, `.Hosting.AGUI.AspNetCore`, `.DevUI`, `.Foundry` (dernière stable : 1.5.0), `.Foundry.Hosting`, `.CosmosNoSql`, `.LocalCodeAct`, `.Hyperlight` | NuGet |
| Paquets **RC / alpha** | RC : `.Declarative`, `.Purview`. Alpha : `.Hosting.OpenAI`, `.Mcp`, `.AgentHooks`, `.Valkey` | NuGet |
| Dépendances de MAF 1.26.0 | `OpenAI` 2.14.0 · `Microsoft.Extensions.AI.OpenAI` 10.10.1 · `ModelContextProtocol` 2.2.0 · `A2A` 1.0.0-preview3 · `Microsoft.Extensions.VectorData.Abstractions` 10.10.0 | [`Directory.Packages.props` @ dotnet-1.26.0](https://github.com/microsoft/agent-framework/blob/dotnet-1.26.0/dotnet/Directory.Packages.props) |
| Échantillons officiels | Tous les dossiers de `dotnet/samples` cités dans ce rapport existent déjà au tag `dotnet-1.22.0`. Entre 1.22.0 et `main`, aucun dossier d'exemple n'a été ajouté ni retiré. Les nouveaux labs proposés peuvent donc être construits sur la version actuelle des labs. | Comparaison des arbres Git |

### 2.3 Conventions de ce rapport

- **Fait vérifié** : constaté dans le code du projet, sur Learn, dans le dépôt GitHub, sur NuGet ou dans les notes de version, avec la source citée.
- **Interprétation** : jugement pédagogique de l'auditeur, argumenté.
- **Recommandation** : action proposée.
- Abréviations : **L01…L12** pour les labs AzureOpenAI, **L06S / L06C** pour le serveur et le client A2A, **M01…M03** pour les labs MultiAgentSystem, **MAF** pour Microsoft Agent Framework.
- URL Learn : toutes les pages sont citées avec le pivot C#. La base est `https://learn.microsoft.com/en-us/agent-framework/`, suivie du chemin et de `?pivots=programming-language-csharp`.

### 2.4 Limites

- **Aucune exécution.** Aucun lab n'a été exécuté pendant l'audit, qui est une analyse statique. Les constats sur le streaming s'appuient sur `Migration/Migration-Manual.md` §8 et sur l'état du bug amont, et doivent être validés par test.
- **Incohérences entre Learn et le dépôt.** La documentation Learn n'est pas toujours cohérente avec le dépôt, et ce rapport s'appuie en priorité sur le dépôt pour les détails d'API.
  - Plusieurs extraits C# de Learn utilisent encore `AzureOpenAIClient` (paquet `Azure.AI.OpenAI --prerelease`). Le dépôt a supprimé cette dépendance en 1.21.0, et l'exemple officiel `Agent_With_AzureOpenAIChatCompletion` utilise `OpenAIClient` + `/openai/v1/`, comme les labs. La page d'ensemble des fournisseurs de modèles mentionne bien la ligne « Azure OpenAI through OpenAI v1 ».
  - Learn recommande `--prerelease` pour des paquets désormais stables.
  - Certaines pages mélangent des identifiants Python dans la vue C#. C'est le cas de *Storage*, pour la persistance par appel de service.
- **Versions non précisées par Learn.** Learn n'indique pas dans quelle version une API est apparue. La disponibilité en 1.22.0 a été vérifiée par la présence des exemples au tag `dotnet-1.22.0` et par l'historique NuGet. Les signatures exactes n'ont pas été compilées.
- **Points non vérifiés.**
  - Le nom des paquets NuGet qui portent les types d'évaluation, de Skills et de Looping n'est pas donné par Learn.
  - La liaison par défaut du client A2A diffère entre la page Learn (HTTP+JSON par défaut) et l'observation consignée dans le README de L06C (JSON-RPC par défaut avec les cartes du serveur du lab). L'écart peut venir de la version du SDK A2A (preview2 contre preview3).
- **Python ignoré.** Les fonctionnalités Python ne sont pas prises en compte dans les recommandations. Les pages réservées à Python sont signalées comme telles : Agent Hooks, FIDES, Functional Workflow API, Controlling tool availability, Computer use.

---

## 3. Inventaire des labs existants

| Id | Lab (dossier) | Objectif pédagogique déclaré | Ce que le code enseigne réellement | MAF | Scénarios | Dashboard |
|---|---|---|---|---|---|---|
| L01 | `AzureOpenAI/Lab01-FirstBasicAIAgent` | Créer un premier agent sur Azure OpenAI | `OpenAIClient` v1 (clé API ou Entra ID), `GetChatClient().AsAIAgent()`, `RunAsync`, instructions et nom, messages `System`/`User`, `Usage` (y compris les tokens de raisonnement), `RunStreamingAsync` + `ToAgentResponse()` | 1.22.0 | 5 | ✅ |
| L02 | `AzureOpenAI/Lab02-AIAgentWithSO` | Sortie structurée | JSON décrit dans les instructions (anti-modèle montré en contraste), `RunAsync<T>`, `ChatResponseFormat.ForJsonSchema<T>` au niveau de l'agent (`ChatClientAgentOptions`) et d'une exécution (`AgentRunOptions`), avec streaming | 1.22.0 | 4 | ✅ |
| L03 | `AzureOpenAI/Lab03-AIAgentWithFunctionTools` | Function tools | `AIFunctionFactory.Create`, découverte par réflexion (`GetMethods`), services injectés (`IServiceProvider`), middleware d'appel de fonction (`AsBuilder().Use(...)`) | 1.22.0 | 4 | ✅ |
| L04 | `AzureOpenAI/Lab04-AIAgentWithMCPClient` | Client MCP | `McpClient` + `HttpClientTransport` (Streamable HTTP) vers le serveur MCP public de Hugging Face, `ListToolsAsync`, `McpClientTool` en tant qu'`AIFunction`, `RunAsync<T>`, sélection d'outils pour réduire les tokens | 1.22.0 · MCP 2.2.0 | 3 | ✅ |
| L05 | `AzureOpenAI/Lab05-AIAgentWithThreads` | Conversations multi-tours et persistance | `AgentSession`, `CreateSessionAsync`, `SerializeSessionAsync`/`DeserializeSessionAsync`, `InMemoryChatHistoryProvider`, `ChatHistoryProvider` personnalisé (vector store en mémoire, MongoDB), `StateBag`/`ProviderSessionState<T>` | 1.22.0 | 3 | ✅ |
| L06S | `AzureOpenAI/Lab06_A2AServer` | Exposer des agents en A2A v1 | ASP.NET Core, `AddA2AServer`, `MapA2AJsonRpc`/`MapA2AHttpJson`, `AgentCard`/`AgentSkill`, `MapWellKnownAgentCard`, outils HMAC, contournement de streaming (`StreamingWorkaround`) | 1.22.0-preview (A2A) | 1 | ✅ |
| L06C | `AzureOpenAI/Lab06_A2AClient` | Appeler des agents distants en A2A v1 | `A2ACardResolver`, `AgentCard.AsAIAgent()`, `A2AClient.AsAIAgent()`, agent distant utilisé comme outil (`AsAIFunction`) | 1.22.0-preview (A2A) | 3 | ✅ |
| L07 | `AzureOpenAI/Lab07-AgenticRAG-VectorStore` | RAG avec vector store | `IEmbeddingGenerator`, `InMemoryVectorStore`, attributs `[VectorStore*]`, `SearchAsync`, RAG agentique (outil de recherche) comparé à `TextSearchProvider` (context provider), filtre de stockage d'historique | 1.22.0 | 4 | ✅ |
| L08 | `AzureOpenAI/Lab08-DataFormatComparison` | Impact du format des données sur les tokens | Outil renvoyant du JSON comparé à un outil renvoyant du CSV, `RunAsync<List<Hotel>>`, `FunctionResultContent`, comparaison de `Usage` | 1.22.0 | 2 | ✅ |
| L09 | `AzureOpenAI/Lab09-AIAgentWithFunctionToolsHumanApproval` | Approbation humaine d'outils | `ApprovalRequiredAIFunction`, `ToolApprovalRequestContent`, `CreateResponse(approved, reason)`, boucle d'approbation dans la même session, décision par une politique | 1.22.0 | 2 | ✅ |
| L10 | `AzureOpenAI/Lab10_ExposeAIAgentAsMCPTool` | Exposer un agent comme outil MCP | `AsAIFunction`, `McpServerTool.Create`, `AddMcpServer().WithHttpTransport()/WithStdioServerTransport()`, `MapMcp`, consommation par un client MCP et par un autre agent | 1.22.0 · MCP 2.2.0 | 3 | ✅ |
| L11 | `AzureOpenAI/Lab11-AIAgentWithCustomHttpTransport` | Transport HTTP personnalisé | `DelegatingHandler`, `HttpClientPipelineTransport`, `OpenAIClientOptions.Transport`, journalisation masquée, plusieurs requêtes HTTP par exécution avec function calling | 1.22.0 | 2 | ✅ |
| L12 | `AzureOpenAI/Lab12-AIAgentWithAIContextProvider` | Mémoire long terme de l'utilisateur | `AIContextProvider` personnalisé (`ProvideAIContextAsync`/`StoreAIContextAsync`), `AIContext`, `ProviderSessionState<T>`, extraction de faits avec `RunAsync<T>` par un second agent, stockage MongoDB | 1.22.0 | 2 | ✅ |
| M01 | `MultiAgentSystem/Lab01-UseAgentAsTool` | Agent comme outil, orchestrateur | `AzureOpenAIClient`, fabrique et builder maison (`AgentFactory`, `AgentBuilder`), `CreateAIAgent`, `AsAIFunction`, `RunAsync<OrchestratorResponse>`, agents « juges » | **1.0.0-preview.251204.1** | 1 (2 annoncés) | ❌ |
| M02 | `MultiAgentSystem/Lab02_OrchestrationSequential` | Orchestration séquentielle | `AgentWorkflowBuilder.BuildSequential`, `InProcessExecution.StreamAsync`, `TurnToken`, `WorkflowOutputEvent` | **preview.251204.1 / Workflows preview.251219.1** | 1 | ❌ |
| M03 | `MultiAgentSystem/Lab03_OrchestrationConcurrent` | Orchestration concurrente | `AgentWorkflowBuilder.BuildConcurrent`, mêmes API d'exécution que M02 | **idem** | 1 | ❌ |

Le dashboard référence 13 labs dans `Dashboard/LabDashboard/labs.json`, tous de la piste AzureOpenAI. Les labs MultiAgentSystem n'y figurent pas et n'ont pas d'auto-tests.

Tous les projets ciblent `net10.0`. Les projets `Start` de la piste AzureOpenAI contiennent de 12 à 27 marqueurs `TODO` alignés sur les README. Les projets `Start` de M02 et M03 n'ont pas de `TODO` numérotés, seulement des commentaires `Hint`, et ces indices citent `CreateAIAgent()`.

---

## 4. Évaluation individuelle des labs

Chaque fiche suit la même structure : objectif, ce qui reste pertinent, ce qui est devenu secondaire ou obsolète, justification officielle, obsolescence technique et pédagogique, conséquences, action recommandée.

Niveaux de valeur pour le développeur : **Essentielle**, **Utile**, **Secondaire**, **Marginale**.

---

### L01 — First Basic AI Agent · **À améliorer** · Valeur : Essentielle

**Objectif actuel.** Connecter le framework à Azure OpenAI, créer un agent, l'exécuter, lire l'usage en tokens et streamer une réponse.

**Constats vérifiés**
- Le client est construit comme dans l'exemple officiel [`Agent_With_AzureOpenAIChatCompletion`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentProviders/azure/Agent_With_AzureOpenAIChatCompletion) : `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), new OpenAIClientOptions { Endpoint = … /openai/v1 })`.
  - Le choix de l'API se fait à `client.GetChatClient(...)` (`LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Solution/Program.cs:44`).
  - L'avertissement officiel sur `DefaultAzureCredential` en production est repris.
- Les concepts enseignés correspondent à l'étape 1 du *Get Started* : `AsAIAgent`, `RunAsync`, `RunStreamingAsync` ([get-started/your-first-agent](https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent?pivots=programming-language-csharp)).
- La page fournisseur Azure OpenAI dit : *« Responses is the recommended primary client: it supports the full set of hosted tools »* et *« Use Chat Completion when you need broad model compatibility or have an existing Chat Completions integration to keep »*. Elle ajoute que *« The Azure OpenAI Assistants API is deprecated »* ([model-providers/azure-openai](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/azure-openai?pivots=programming-language-csharp)).
- Le *Get Started* officiel et les exemples `02-agents/Agents` utilisent par défaut un projet Microsoft Foundry (`AIProjectClient.AsAIAgent`). La page fournisseur Foundry le présente comme *« the recommended starting point for most scenarios »*.
- Le scénario 5 (streaming) échouait depuis le 2026-09-28 à cause du bug [dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790). Ce bug est fermé depuis le 2026-10-12 ; voir le §7 P0 et `Migration/Migration-Manual.md` §8.

**Concepts toujours pertinents.** Abstraction `AIAgent`, `ChatClientAgent`, instructions, `RunAsync`/`RunStreamingAsync`, `AgentResponse`/`AgentResponseUpdate`, `Usage`, authentification Entra ID.

**Concepts devenus secondaires.** Aucun n'est obsolète. Le choix exclusif de Chat Completions est **pédagogiquement incomplet** : l'API que Microsoft recommande pour les nouvelles applications Azure OpenAI n'est jamais exécutée.

**Analyse (interprétation)**
- **Obsolescence technique : nulle.**
- **Obsolescence pédagogique : faible.** Le choix de Chat Completions est cohérent pour la suite du parcours : les sessions, la persistance et les context providers exigent un historique géré localement (manuel de migration, règle 5).
- **Conséquence.** Un développeur qui suit le lab aujourd'hui sait créer un agent, mais ignore :
  - que Responses est le client recommandé ;
  - que certaines capacités en dépendent : background responses, outils hébergés, historique géré par le service ;
  - que les exemples officiels partent de Foundry.

**Action recommandée : corriger (enrichir)**
- **Conserver** les scénarios 1 à 5.
- **Ajouter** un scénario 6 « Le même agent sur l'API Responses » : `client.GetResponsesClient().AsAIAgent(model: …)`.
  - Le présenter comme un *choix d'API*, avec un tableau comparatif Chat Completions / Responses (historique, outils hébergés, background responses), d'après la [page d'ensemble des fournisseurs](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/?pivots=programming-language-csharp).
  - Expliquer pourquoi la suite du parcours reste sur Chat Completions.
- **Ajouter** dans le README une note « Foundry » : le Get Started officiel utilise `AIProjectClient`, et le reste du code est identique grâce à `AIAgent`.
- **Option P2** : un scénario multimodal (`UriContent`/`DataContent`), voir G13.
- **Re-tester** le scénario 5 (P0).

---

### L02 — AI Agent with Structured Output · **À conserver** · Valeur : Essentielle

**Objectif actuel.** Obtenir une réponse typée et comparer quatre approches.

**Constats vérifiés.** Les approches enseignées sont exactement celles de la page [agents/structured-outputs](https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs?pivots=programming-language-csharp) :
- `RunAsync<T>` et `AgentResponse<T>.Result` ;
- `ChatResponseFormat.ForJsonSchema<T>()` au niveau de l'agent (`ChatClientAgentOptions.ChatOptions`) et d'une exécution (`AgentRunOptions`) ;
- streaming puis désérialisation.

Le scénario 1 (format JSON décrit dans les instructions) est volontairement montré comme fragile. Le README cite la limite officielle : *« Primitives and arrays are not supported by the ResponseFormat approach »*. Il mentionne aussi le décorateur `StructuredOutputAgent`, qui figure dans l'exemple `Agent_Step02_StructuredOutput`.

**Analyse.**
- **Obsolescence technique : nulle.**
- **Obsolescence pédagogique : nulle.**
- Le contraste entre le JSON décrit dans le prompt et le schéma imposé est une bonne pratique pédagogique.

**Redondance.** La sortie structurée est réutilisée dans L04, L08, L12 et M01. C'est un renforcement utile, pas une redondance.

**Action recommandée : conserver.**
- Re-tester le scénario 4 (streaming, P0).
- Option : ajouter dans le README une ligne sur `strict` (déjà présente) et sur la gestion d'une réponse tronquée (`MaxOutputTokens` provoque une `JsonException`), aujourd'hui traitée dans L08.

---

### L03 — AI Agent with Function Tools · **À améliorer** · Valeur : Essentielle

**Objectif actuel.** Donner des outils .NET à un agent, avec injection de services et middleware d'appel de fonction.

**Constats vérifiés**
- `AIFunctionFactory.Create` et `[Description]` sont conformes à [get-started/add-tools](https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools?pivots=programming-language-csharp) et à [agents/tools/function-tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp).
- Le scénario 2 expose **toutes** les méthodes publiques d'une classe par réflexion (`Program.cs:101`, `GetMethods(BindingFlags.Public | …)`). L'exemple officiel le plus proche, [`Agent_Step12_Plugins`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step12_Plugins), fait exposer explicitement les outils par la classe. Le README le cite en « Going Further ».
- La page *Add tools* recommande : *« Before you add tools, review the tool approval security best practices to decide which operations need human confirmation. »*
- La page [Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) demande de traiter les arguments produits par le modèle comme non fiables : listes blanches, contrôles de type et de plage, longueurs, traversée de chemin, requêtes paramétrées. Le README de L03 n'aborde pas ce point.
- Le middleware enseigné (scénario 4) est le seul des trois types documentés ([concepts/agents/middleware](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/?pivots=programming-language-csharp)) : *agent run*, *function calling*, *IChatClient*.

**Concepts toujours pertinents.** Function tools, services injectés, middleware de fonction, coût en tokens de la boucle d'outils.

**Concepts devenus secondaires.** La découverte par réflexion n'est pas déconseillée par Microsoft. Elle est **non standard**, et elle va à l'encontre du principe de moindre privilège : toute nouvelle méthode publique devient un outil sans décision explicite (interprétation).

**Analyse**
- **Obsolescence technique : nulle.**
- **Obsolescence pédagogique : faible.** Le scénario par réflexion enseigne un réflexe discutable, et la sécurité des arguments d'outil n'est pas abordée.
- **Conséquence.** Le développeur sait brancher des outils, mais pas les concevoir de façon sûre.

**Action recommandée : corriger**
- **Remplacer** le scénario 2 par le pattern « plugin » (classe qui expose ses outils, résolue depuis le conteneur), ou le rendre optionnel avec un avertissement.
- **Ajouter** une section « Concevoir des outils sûrs » (validation des arguments, effets de bord, outils d'écriture → approbation, avec renvoi vers L09) à partir de la page *Agent Safety*.
- **Garder** le scénario 4 en pont vers le nouveau lab *Middleware et garde-fous* (G2).

---

### L04 — AI Agent with MCP Client · **À conserver** · Valeur : Essentielle

**Objectif actuel.** Consommer les outils d'un serveur MCP distant.

**Constats vérifiés**
- Le lab suit le pattern officiel de [agents/tools/local-mcp-tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/local-mcp-tools?pivots=programming-language-csharp) : `McpClient.CreateAsync`, `ListToolsAsync`, `mcpTools.Cast<AITool>()`, `await using`. Le transport est Streamable HTTP plutôt que stdio ; le stdio est cité en « Going Further ».
- Le README contient une section *Security: third-party MCP servers* qui reprend les recommandations officielles : serveurs hébergés par le fournisseur, pas de proxy, revue des données partagées, jetons.
- Les clients MCP font partie des *foundational building blocks* cités dans la [vue d'ensemble](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp). L'étape 3 du [Journey](https://learn.microsoft.com/en-us/agent-framework/journey/?pivots=programming-language-csharp) regroupe function tools et MCP.
- Le scénario 3 (ne donner que les outils utiles) est une bonne pratique.

**Analyse**
- **Obsolescence technique : nulle.** La version MCP 2.2.0 est celle de MAF 1.26.0 ; la 2.3.0 existe.
- **Obsolescence pédagogique : nulle.**
- **Risque.** Le lab dépend d'un service public tiers (`https://huggingface.co/mcp`), dont la disponibilité et la liste d'outils peuvent changer.

**Action recommandée : conserver.**
- Option : un scénario stdio (serveur local), pour ne pas dépendre d'un service externe pendant une formation.
- Option : mentionner qu'un `McpClientTool` peut être enveloppé dans `ApprovalRequiredAIFunction`. C'est une interprétation : Learn ne le décrit pas explicitement pour MCP, mais précise que l'approbation *« works with providers whose clients invoke tools locally »*.

---

### L05 — AI Agent with Sessions (dossier `Lab05-AIAgentWithThreads`) · **À améliorer** · Valeur : Essentielle

**Objectif actuel.** Conversations multi-tours, sérialisation et reprise de session, stockage personnalisé de l'historique.

**Constats vérifiés**
- Le lab couvre les étapes 3 et 4 du *Get Started* :
  - [multi-turn](https://learn.microsoft.com/en-us/agent-framework/get-started/multi-turn?pivots=programming-language-csharp) : `AgentSession`, `CreateSessionAsync` ;
  - [memory](https://learn.microsoft.com/en-us/agent-framework/get-started/memory?pivots=programming-language-csharp) : `ChatHistoryProvider` personnalisé ;
  - la page [Storage](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp).
- Le scénario 2 stocke l'historique dans un vector store en mémoire (`Program.cs:117`), comme l'exemple officiel `Agent_Step04_3rdPartyChatHistoryStorage`.
- Le nom du dossier (`AIAgentWithThreads`) et le libellé « Threads » du `README.md` racine (ligne 576) reprennent l'ancien concept `AgentThread`, renommé `AgentSession` (PR [#3430](https://github.com/microsoft/agent-framework/pull/3430)).
- La réduction d'historique (`InMemoryChatHistoryProviderOptions.ChatReducer`, `MessageCountingChatReducer`) et l'historique géré par le service (Responses, `ConversationId`) n'apparaissent qu'en « Going Further ».
- Learn insiste sur la sécurité des sessions : *« Treat `AgentSession` as an opaque state object … bind each stored session to the authenticated user or tenant »* (Storage). La page [Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) ajoute que restaurer une session depuis une source non fiable revient à accepter une entrée non fiable. Le README le mentionne en une ligne (« Production »).

**Concepts toujours pertinents.** Tous : session opaque, sérialisation par l'agent, `ChatHistoryProvider`, `StateBag`, `ProviderSessionState<T>`.

**Concepts secondaires.** Le stockage de l'historique dans un *vector store* (scénario 2) entretient une confusion possible avec la **mémoire sémantique** (`ChatHistoryMemoryProvider`, recherche par similarité). Le vector store n'y sert que de base clé-valeur (interprétation).

**Analyse**
- **Obsolescence technique : nulle**, hors le nom du dossier.
- **Obsolescence pédagogique : faible.**
- **Conséquence.** Le développeur ne sait pas borner un historique qui grandit, et ne voit pas l'alternative « historique côté service ».

**Action recommandée : corriger**
- **Remplacer ou compléter** le scénario 2 par un scénario « borner l'historique » avec `ChatReducer`, qui est stable. Si le scénario vector store est conservé, préciser explicitement la différence avec la mémoire sémantique (renvoi vers L12 et `ChatHistoryMemoryProvider`).
- **Ajouter** un encadré « Historique géré par le service » : agent Responses, `ChatClientAgentSession.ConversationId`, limites (la compaction ne s'applique pas, le routage de modèles n'est pas possible), d'après [runtime-model-routing](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/runtime-model-routing?pivots=programming-language-csharp) et [compaction](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp).
- **Renommer** le dossier en `Lab05-AIAgentWithSessions` et le libellé « Threads » du README racine. C'est une action P2 qui impacte `labs.json`, les tests du dashboard et les liens.
- **Renforcer** la section sécurité des sessions.

---

### L06S — A2A Server · **À améliorer** · Valeur : Utile

**Objectif actuel.** Héberger deux agents derrière le protocole A2A v1.

**Constats vérifiés**
- Le lab est conforme au guide [A2A SDK v1 migration](https://learn.microsoft.com/en-us/agent-framework/migration-guide/agent-to-agent-sdk-v1?pivots=programming-language-csharp) et à [hosting/self-hosting/a2a/dotnet](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/a2a/dotnet?pivots=programming-language-csharp) : `AddA2AServer`, `MapA2AJsonRpc`/`MapA2AHttpJson`, `MapWellKnownAgentCard`. L'ancien `MapA2A` a été supprimé.
- Les paquets sont en **préversion** (`1.22.0-preview.260918.1`, SDK `A2A` 1.0.0-preview2), et le README le signale. MAF 1.25.0 a monté le SDK A2A en **1.0.0-preview3** (PR [#9386](https://github.com/microsoft/agent-framework/pull/9386)). En 1.26.0, l'exemple ajoute la sélection du protocole gRPC (PR [#9563](https://github.com/microsoft/agent-framework/pull/9563)).
- `Program.cs:54` applique `StreamingWorkaround.WithNonStreamingResponses()`, un contournement temporaire de dotnet/extensions#7790. Ce bug est fermé le 2026-10-12, et la release [v10.11.0](https://github.com/dotnet/extensions/releases) de `Microsoft.Extensions.AI` le référence. MAF 1.26.0 référence toujours `Microsoft.Extensions.AI.OpenAI` 10.10.1 : le correctif ne s'obtient donc que par une référence explicite à 10.11.0 (interprétation à valider par test). Le manuel (§8) note aussi qu'un streaming court a réussi le 2026-10-25.
- Le README contient une section *Security* : pas d'authentification, `UseClaimsBasedAgentIsolation` cité. Le README `04-hosting` officiel applique `.RequireAuthorization()` aux endpoints. La 1.24.0 a documenté l'authentification A2A (PR [#8491](https://github.com/microsoft/agent-framework/pull/8491)).
- Le lab n'utilise pas le builder d'hébergement du framework (`builder.AddAIAgent(...)` puis `agentBuilder.AddA2AServer()`), qui n'est cité qu'en « Going Further ».

**Analyse**
- **Obsolescence technique : faible.** Le contournement est à retirer, et le SDK préversion est déjà dépassé d'une version.
- **Obsolescence pédagogique : faible.** Le contenu A2A est juste.
- **Position dans le parcours discutable.** A2A arrive en 6ᵉ position, avant le RAG, les approbations et l'« agent comme outil ». Le [Journey](https://learn.microsoft.com/en-us/agent-framework/journey/?pivots=programming-language-csharp) le place à l'étape 8, après *Agents as Tools* (étape 7).
- **Conséquence.** L'apprenant découvre l'hébergement d'agents par un protocole spécialisé, en préversion, avant d'avoir vu l'hébergement générique (`AddAIAgent`), qui n'est enseigné nulle part.

**Action recommandée : corriger et repositionner**
- Retirer le contournement après re-test (P0).
- Déplacer L06S et L06C après « Agent comme outil » (§8).
- Ajouter un scénario ou une variante « avec le builder d'hébergement » (`AddAIAgent` puis `AddA2AServer`), qui sert de pont vers le futur lab Hébergement (G5).
- Lors de la montée de version, passer au SDK A2A preview3 et re-tester la compatibilité client/serveur.

---

### L06C — A2A Client · **À conserver** (à repositionner) · Valeur : Utile

**Objectif actuel.** Découvrir et appeler des agents distants en A2A v1, et utiliser un agent distant comme outil.

**Constats vérifiés**
- Le lab est conforme à [integrations/by-component/agent-services/a2a](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/a2a?pivots=programming-language-csharp) : `A2ACardResolver.GetAIAgentAsync()`, `AgentCard.AsAIAgent()`, `A2AClient.AsAIAgent(...)`, `A2AClientOptions.PreferredBindings`.
- Écart non résolu : Learn indique HTTP+JSON comme liaison par défaut, alors que le README du lab a observé JSON-RPC par défaut avec les cartes du serveur en 1.22.0.
- Le scénario 3 (`AsAIFunction`) recoupe M01 et L10.

**Analyse**
- **Obsolescence technique : faible**, du fait de la préversion.
- **Obsolescence pédagogique : nulle.**
- **Redondance.** `AsAIFunction` est enseigné ici avant le lab dédié.

**Action recommandée : conserver.**
- Déplacer après M01 refondu (§8). Le scénario 3 devient alors une application de M01 plutôt qu'une introduction du concept.
- Vérifier la liaison par défaut après la montée en A2A preview3.

---

### L07 — Agentic RAG with a Vector Store · **À conserver** · Valeur : Essentielle

**Objectif actuel.** Construire un RAG avec embeddings et vector store, puis comparer le RAG agentique (outil de recherche) et `TextSearchProvider`.

**Constats vérifiés**
- La page [agents/rag](https://learn.microsoft.com/en-us/agent-framework/agents/rag?pivots=programming-language-csharp) décrit exactement les deux approches enseignées : *« context providers that add retrieved content before model invocation and search tools that let the model retrieve grounding data on demand »*.
- Les abstractions sont celles de [vector-stores](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/vector-stores/?pivots=programming-language-csharp) : `Microsoft.Extensions.VectorData`, `IEmbeddingGenerator`, `InMemoryVectorStore` (CommunityToolkit, paquet utilisé par les exemples officiels).
- Le filtre `StorageInputRequestMessageFilter`, qui évite de stocker le contexte injecté dans l'historique, est une subtilité bien traitée.
- La page *Agent Safety* et la page *Chat History Memory Provider* signalent le risque d'**injection de prompt indirecte** via le contenu récupéré. Le README de L07 ne l'aborde pas (recherche « injection » sans résultat).

**Analyse**
- **Obsolescence technique : nulle.**
- **Obsolescence pédagogique : nulle.** C'est l'un des meilleurs labs du parcours : il compare deux architectures et mesure leur coût.

**Action recommandée : conserver.**
- Ajouter une section « Sécurité du RAG » : contenu récupéré non fiable, injection indirecte, citations (`CitationsPrompt`).
- Option : un scénario `TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling`, qui est la version « framework » du RAG agentique ; le README le cite déjà.

---

### L08 — Data Format Comparison · **À supprimer ou remplacer (par fusion)** · Valeur : Marginale

**Objectif actuel.** Mesurer l'effet du format JSON ou CSV des résultats d'outil et des réponses sur la consommation de tokens.

**Constats vérifiés**
- Le lab réutilise des concepts déjà enseignés : function tools (L03), `RunAsync<T>` (L02), `FunctionResultContent` et `Usage`. Son seul apport propre est la comparaison de formats de sérialisation.
- La comparaison JSON/CSV est une question d'ingénierie de prompt. Aucune page Learn ni aucun exemple officiel de MAF ne la traite.
- Le framework propose aujourd'hui des mécanismes dédiés à la maîtrise du contexte et des coûts, qui ne sont enseignés nulle part :
  - `IChatReducer` ;
  - la compaction, avec `ToolResultCompactionStrategy` (*« Collapses old tool groups »*), `SlidingWindowCompactionStrategy`, `SummarizationCompactionStrategy` et `PipelineCompactionStrategy` ([compaction](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp), expérimental `MAAI001`) ;
  - les exemples `Agent_Step13_ChatReduction` et `Agent_Step18_CompactionPipeline`.
- Le scénario CSV repose sur un format demandé dans les instructions, sans garantie de schéma. Le README le reconnaît, et c'est précisément l'approche que L02 présente comme fragile.

**Analyse**
- **Obsolescence technique : nulle.** Le lab fonctionne.
- **Obsolescence pédagogique : réelle, au sens de la valeur.**
  - Il occupe une place dans la piste principale pour un sujet périphérique au framework.
  - La mesure du coût d'un résultat d'outil est utile, mais elle gagnerait à être enseignée avec les outils du framework (réduction, compaction des résultats d'outils) plutôt qu'avec un changement de format manuel.
- **Conséquence.** Un développeur qui suit ce lab peut en retenir qu'il faut « passer en CSV » pour économiser des tokens. Il ignore les mécanismes officiels et perd les garanties de schéma.

**Action recommandée : remplacer**
- Créer le lab **« Contexte et coûts : réduction et compaction »** (G10).
- Y conserver la comparaison JSON/CSV comme scénario secondaire (« mesurer le coût d'un résultat d'outil »).
- Ou garder L08 tel quel en lab **optionnel** hors parcours principal.

---

### L09 — Function Tools with Human Approval · **À conserver** · Valeur : Essentielle

**Objectif actuel.** Faire valider par un humain, ou par une politique, l'exécution des outils sensibles.

**Constats vérifiés**
- Le flux enseigné est identique à celui de [agents/tools/tool-approval](https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval?pivots=programming-language-csharp) :
  1. `new ApprovalRequiredAIFunction(...)` ;
  2. `ToolApprovalRequestContent` dans `response.Messages` ;
  3. `request.CreateResponse(bool)` dans un `ChatMessage` utilisateur ;
  4. `RunAsync(approvalMessages, session)` avec **la même session**.
- La liaison des approbations à la session (binding) est expliquée. Le README cite `UseToolApproval()`/`ToolApprovalAgent` (« don't ask again ») et `AsHarnessAgent`.
- Les versions 1.23 à 1.26 modifient le comportement des approbations :
  - [BREAKING] *Enforce approval response binding consistently* (PR [#8641](https://github.com/microsoft/agent-framework/pull/8641)) ;
  - *a bug where invoke function tool could bypass approval* (PR [#8403](https://github.com/microsoft/agent-framework/pull/8403)) ;
  - *Automatically reject tool-approval requests that never receive a response* (PR [#9152](https://github.com/microsoft/agent-framework/pull/9152)) ;
  - *Reject duplicate tool approval request IDs* (PR [#9542](https://github.com/microsoft/agent-framework/pull/9542)).

**Analyse**
- **Obsolescence technique : nulle en 1.22.0**, mais lab sensible à la montée de version.
- **Obsolescence pédagogique : nulle.** Le lab enseigne une pratique de sécurité centrale.
- **Point de progression.** Le lab arrive tard (9ᵉ) alors qu'il prolonge directement L03 et L05.

**Action recommandée : conserver.**
- Le rapprocher de L05 dans le parcours (§8).
- Lors de la montée en 1.26.x, re-tester les deux scénarios et documenter le nouveau comportement des demandes sans réponse.
- Option : un scénario `UseToolApproval` avec `AutoApprovalRules`, aujourd'hui en « Going Further ».

---

### L10 — Expose an AI Agent as an MCP Tool · **À améliorer** · Valeur : Utile

**Objectif actuel.** Exposer un agent comme outil MCP, en Streamable HTTP et en stdio, et le faire consommer par un autre agent.

**Constats vérifiés**
- Le lab est conforme à l'exemple officiel [`Agent_Step07_AsMcpTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step07_AsMcpTool) et à la page *Local MCP tools*, qui montre `McpServerTool.Create(agent.AsAIFunction())` avec `WithStdioServerTransport()`.
- Le README contient une section *Security* : pas d'authentification, `RequireAuthorization()` à ajouter avant toute exposition réseau.
- La page [hosting/self-hosting/mcp](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/mcp?pivots=programming-language-csharp) annonce : *« Self-hosting MCP tool support in .NET is coming soon »*. Une API d'hébergement MCP propre au framework pourrait donc remplacer l'assemblage manuel avec `ModelContextProtocol.AspNetCore`.
- `AsAIFunction` est déjà enseigné dans L06C et M01.

**Analyse**
- **Obsolescence technique : nulle**, avec un risque de supersession à moyen terme.
- **Obsolescence pédagogique : faible.**
- **Recouvrement avec L04** : client contre serveur. Il est complémentaire.

**Action recommandée : corriger (léger)**
- Présenter `AsAIFunction` comme un acquis de M01 plutôt que comme un concept nouveau.
- Ajouter un encadré « Évolution prévue » qui renvoie vers la page Learn *self-hosting/mcp*.
- Option : un scénario d'authentification du serveur HTTP (`RequireAuthorization`).
- Surveiller l'arrivée du support d'hébergement MCP en .NET.

---

### L11 — AI Agent with Custom HTTP Transport · **À améliorer** (à repositionner en module avancé) · Valeur : Secondaire

**Objectif actuel.** Intercepter le trafic HTTP du SDK OpenAI (`DelegatingHandler`, `HttpClientPipelineTransport`) pour ajouter un en-tête, journaliser sans fuite de secret et compter les requêtes.

**Constats vérifiés**
- Les API enseignées appartiennent au SDK `OpenAI` et à `System.ClientModel`, pas au framework. Elles restent valides.
- L'approche officielle pour observer un agent est **OpenTelemetry**. Selon [agents/observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp) :
  - *« Agent Framework emits traces, logs, and metrics according to the OpenTelemetry GenAI Semantic Conventions »* ;
  - les API sont `AsBuilder().UseOpenTelemetry(sourceName, …)` sur l'agent ou le chat client, avec les exportateurs Azure Monitor ou OTLP/Aspire Dashboard.
  
  Le README de L11 cite lui-même `UseOpenTelemetry` en « Going Further » (ligne 421), comme alternative *« instead of raw HTTP »*.
- La page *Agent Safety* rappelle que les journaux de niveau `Trace` contiennent les messages complets et *« should never be enabled in production »*, et qu'il ne faut pas activer `EnableSensitiveData` en production. Le lab masque correctement les secrets.

**Analyse**
- **Obsolescence technique : nulle.**
- **Obsolescence pédagogique : faible**, mais un problème de **positionnement**.
  - Le lab est le seul du parcours qui montre « ce que fait l'agent », et il le fait à un niveau bas (HTTP) que Microsoft ne présente pas comme l'outil d'observabilité.
  - Il reste utile pour comprendre la boucle de function calling (plusieurs requêtes par exécution) et pour des besoins réels : proxy, en-têtes, authentification par requête.
- **Conséquence.** Sans lab OpenTelemetry, un développeur risque de reproduire en production une journalisation HTTP maison au lieu d'une télémétrie standardisée.

**Action recommandée : repositionner et compléter**
- Créer le lab **Observabilité** (G3, P1).
- Déplacer L11 en module avancé ou optionnel, après ce lab.
- Option : fusionner le scénario 2 (plusieurs requêtes HTTP par exécution) dans le lab Observabilité, sous la forme « la même exécution vue en spans `invoke_agent` / `chat` / `execute_tool` ».

---

### L12 — AI Agent with an AI Context Provider · **À conserver** · Valeur : Essentielle

**Objectif actuel.** Donner à l'agent une mémoire long terme de l'utilisateur grâce à un `AIContextProvider` personnalisé.

**Constats vérifiés**
- Le lab est conforme à [concepts/agents/conversations/context-providers](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/context-providers?pivots=programming-language-csharp) :
  - `ProvideAIContextAsync`/`StoreAIContextAsync`, `AIContext { Instructions, Messages, Tools }` ;
  - `ProviderSessionState<T>` ;
  - la bonne pratique officielle : un provider *« should not store any session specific state in the provider instance »*.
- Les context providers sont un *foundational building block* (vue d'ensemble), l'étape 4 du *Get Started* et l'étape 6 du *Journey*.
- Le README cite les alternatives intégrées (`ChatHistoryMemoryProvider`) et l'exemple `01-get-started/04_memory`.

**Analyse**
- **Obsolescence technique : nulle.**
- **Obsolescence pédagogique : nulle.**
- **Point d'attention.** Le provider appelle un modèle à chaque exécution pour extraire des faits, ce qui double les appels (manuel §8). C'est assumé et expliqué.

**Action recommandée : conserver.**
- Option : comparer avec `ChatHistoryMemoryProvider` (mémoire sémantique intégrée, [page dédiée](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/chat-history-memory-provider?pivots=programming-language-csharp)) pour clarifier « mémoire faite main » contre « mémoire fournie ».
- Ajouter une note sécurité : une mémoire persistante peut être empoisonnée par un utilisateur malveillant, à partir de la page *Agent Safety* et de la mise en garde de la page *Chat History Memory Provider* sur l'injection indirecte.

---

### M01 — Use Agent as Tool · **Fortement obsolète** · Valeur du concept : Utile

**Objectif actuel.** Un orchestrateur coordonne des agents spécialisés exposés comme outils, avec des agents « juges » qui évaluent la qualité.

**Constats vérifiés**
- **Paquets** :
  - `Microsoft.Agents.AI.OpenAI` **1.0.0-preview.251204.1** ;
  - `Azure.AI.OpenAI` 2.7.0-beta.2 (le framework a supprimé cette dépendance en 1.21.0, PR [#7986](https://github.com/microsoft/agent-framework/pull/7986)) ;
  - `Azure.Identity` 1.18.0-beta.2 ;
  - `Microsoft.Extensions.Hosting` 9.0.0.
- **API absentes du code source actuel** (vérifié au commit `67ba500` du 2026-10-24) :
  - `ChatClient.CreateAIAgent(...)` (`AgentCreation/AgentBuilder.cs:65,71`), renommé `AsAIAgent` par la PR [#3222](https://github.com/microsoft/agent-framework/pull/3222) ;
  - `AgentRunResponse<T>` (`Program.cs:97`), renommé `AgentResponse<T>` par la PR [#3197](https://github.com/microsoft/agent-framework/pull/3197).
- **Abstractions maison** : `AgentFactory`/`AgentBuilder` masquent les API du framework. C'est contraire à la règle 12 du manuel de migration du projet (« pas de fabrique maison qui masquerait les API enseignées »).
- **Évaluation ad hoc** : les agents « juges » sont des outils appelés par l'orchestrateur, qui s'évalue donc lui-même dans la même exécution. Le framework fournit désormais une API d'évaluation (`agent.EvaluateAsync(...)`, `LocalEvaluator`, `EvalChecks`, évaluateurs `Microsoft.Extensions.AI.Evaluation` : `RelevanceEvaluator`, `GroundednessEvaluator`…), décrite sur [agents/evaluation](https://learn.microsoft.com/en-us/agent-framework/agents/evaluation?pivots=programming-language-csharp).
- **Inexactitude du README** : `DefaultAzureCredential` y est décrit comme *« Managed identity authentication (no API keys!) »* (ligne 156). En réalité, c'est une chaîne de credentials, et l'avertissement officiel recommande une credential spécifique en production.
- Le README annonce deux scénarios, mais un seul est implémenté (`scenariosToRun = [1]`).
- Le concept reste au programme officiel : *Agents as Tools* est l'étape 7 du [Journey](https://learn.microsoft.com/en-us/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp), avec l'exemple [`Agent_Step09_AsFunctionTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step09_AsFunctionTool).

**Analyse**
- **Obsolescence technique : forte.** Le code ne correspond plus à aucune API publiée en 1.x.
- **Obsolescence pédagogique : partielle.**
  - Le concept d'agent comme outil est d'actualité.
  - La fabrique maison et l'auto-évaluation par des juges-outils ne sont pas des pratiques recommandées.
- **Conséquence.** Un développeur qui suit ce lab apprend des noms d'API qui n'existent plus, et une méthode d'évaluation non standard.

**Action recommandée : refondre**
- **Migrer** vers les API actuelles, sur la même version que le reste du parcours (1.22.0, puis 1.26.x).
- **Retirer** `AgentFactory`/`AgentBuilder` au profit d'appels `AsAIAgent` explicites.
- **Recentrer** sur l'agent comme outil :
  - `AsAIFunction` avec `AIFunctionFactoryOptions` (nom, description) ;
  - orchestrateur et spécialistes ;
  - coût en tokens ;
  - limites (pas de session partagée par défaut).
- **Déplacer** les juges vers le futur lab **Évaluation** (G7).
- **Corriger** le README et ajouter le lab au dashboard.

---

### M02 — Orchestration Sequential · **Fortement obsolète** · Valeur du concept : Essentielle

**Objectif actuel.** Enchaîner deux agents (résumé, puis extraction d'actions) avec une orchestration séquentielle.

**Constats vérifiés**
- **Paquets** : préversions `1.0.0-preview.251204.1` et `Microsoft.Agents.AI.Workflows` `1.0.0-preview.251219.1`, plus `Azure.AI.OpenAI` en bêta.
- **API renommées ou supprimées.** Toutes sont absentes du code source actuel, à l'exception de `TurnToken` :
  - `InProcessExecution.StreamAsync` (`Program.cs:55`) devient `InProcessExecution.RunStreamingAsync` (PR [#4090](https://github.com/microsoft/agent-framework/pull/4090), rc1) ;
  - `CreateAIAgent` (`Program.cs:42-43`) devient `AsAIAgent` ;
  - `RunId` devient `SessionId`.
- **Événements.** La page Learn [orchestrations/sequential](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/sequential?pivots=programming-language-csharp) consomme `AgentResponseUpdateEvent` pour le streaming et `WorkflowOutputEvent.As<List<ChatMessage>>()` pour le résultat. Le lab caste `(List<ChatMessage>)outputEvt.Data!`.
  - Depuis la PR [#3441](https://github.com/microsoft/agent-framework/pull/3441), les événements d'agent **dérivent de `WorkflowOutputEvent`**, si bien que l'ordre des tests `is` compte.
  - Le motif du lab (`is WorkflowOutputEvent` puis `break`) peut donc capturer un événement d'agent intermédiaire au lieu du résultat final. C'est une interprétation à confirmer par test lors de la refonte.
- **`TurnToken` toujours requis.** *« Must send the turn token to trigger the agents »* (exemples `_StartHere`).
- **Pas de fondamentaux.** Le lab n'enseigne ni executor, ni edge, ni `WorkflowBuilder`. Le *Get Started* officiel enseigne d'abord ces fondamentaux ([get-started/workflows](https://learn.microsoft.com/en-us/agent-framework/get-started/workflows?pivots=programming-language-csharp), exemple `05_first_workflow`) avant les orchestrations (`03-workflows/_StartHere/03_AgentWorkflowPatterns`).
- **Projet `Start` minimal** : des `Hint` sans `TODO` numérotés, qui citent `CreateAIAgent()`. Le lab ne figure pas au dashboard.

**Analyse**
- **Obsolescence technique : forte.**
- **Obsolescence pédagogique : partielle.**
  - L'orchestration séquentielle reste documentée et utile.
  - Le lab saute la couche conceptuelle des workflows. Il présente la séquence comme une « boîte noire » sans montrer le streaming des agents intermédiaires, `BuildSequential(chainOnlyAgentResponses: true, …)` ni l'approbation d'outils intégrée (*« BuildSequential() supports tool approval out of the box »*).
- **Conséquence.** Un développeur ne peut pas réutiliser ce code sur une version actuelle, et n'a aucune base pour concevoir un workflow personnalisé.

**Action recommandée : refondre, fusionner avec M03 et faire précéder d'un lab Workflows fondamentaux (G1)**
- Nouveau lab **« Orchestrations séquentielle et concurrente »** :
  - `RunStreamingAsync` + `TurnToken` ;
  - `AgentResponseUpdateEvent` pour le streaming par agent ;
  - `WorkflowOutputEvent.As<T>()` ;
  - `chainOnlyAgentResponses` ;
  - agrégateur concurrent.
- Conserver le scénario « transcription de réunion », qui est pertinent.

---

### M03 — Orchestration Concurrent · **Fortement obsolète** · Valeur du concept : Utile

**Objectif actuel.** Exécuter trois agents en parallèle sur le même message client.

**Constats vérifiés**
- Les problèmes techniques sont les mêmes que pour M02 : `StreamAsync` (`Program.cs:52`), `CreateAIAgent` (`Program.cs:38-40`), préversions.
- **Défaut de conception du scénario.** Le « Follow-up Agent » (`Program.cs:40`) doit *« create a polite reply summarizing actions »*. Or, en orchestration concurrente, chaque agent reçoit la même entrée **sans voir la sortie des autres**. Il ne peut donc pas résumer les décisions de l'agent de retour et de l'agent de remboursement.
- La page [orchestrations/concurrent](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/concurrent?pivots=programming-language-csharp) et le code source proposent `BuildConcurrent(agents, aggregator)` pour personnaliser la fusion des résultats. L'agrégateur par défaut renvoie le dernier message de chaque agent.
- `TurnToken(emitEvents: false)` ici, contre `true` dans M02 : l'incohérence n'est pas expliquée.

**Analyse**
- **Obsolescence technique : forte.**
- **Obsolescence pédagogique : partielle.**
  - Le concept de fan-out est juste.
  - Le scénario enseigne implicitement un usage incorrect : une synthèse dans une branche parallèle au lieu d'une étape d'agrégation.

**Action recommandée : refondre et fusionner avec M02**
- Corriger le scénario : deux ou trois agents d'analyse en parallèle, puis un **agrégateur** (fonction `aggregator` ou agent de synthèse après fan-in) qui produit la réponse au client.
- Montrer la différence entre `BuildConcurrent` et un graphe `AddFanOutEdge`/`AddFanInBarrierEdge`, ce qui fait le lien avec G1.

---

## 5. Concepts importants manquants

### 5.1 Vue d'ensemble

| Id | Concept | Priorité | Nouveau lab ou enrichissement | Labs concernés |
|---|---|---|---|---|
| G1 | Workflows graph-based : executors, edges, événements, `WorkflowBuilder`, `[MessageHandler]` | **P0** | Nouveau lab | précède M02+M03 refondus |
| G2 | Middleware d'agent (run, function, chat client) et garde-fous | **P0** | Nouveau lab | prolonge L03 (scénario 4) |
| G3 | Observabilité OpenTelemetry | **P1** | Nouveau lab | repositionne L11 |
| G4 | API Responses, historique géré par le service, background responses | **P1** | Enrichissement | L01, L05 |
| G5 | Hébergement et injection de dépendances (`AddAIAgent`, endpoints de protocole, session stores, authentification) | **P1** | Nouveau lab | L06S, L10 (ponts) |
| G6 | Human-in-the-loop et checkpoints dans les workflows | **P1** | Nouveau lab | après G1 |
| G7 | Évaluation des agents | **P1** | Nouveau lab | remplace les juges de M01 |
| G8 | Sécurité des agents (*Agent Safety*) de façon transversale | **P1** | Enrichissement | L03, L05, L07, L12 (+ L04, L06S, L10 déjà partiels) |
| G9 | Agent Harness | **P1** | Nouveau lab | après workflows |
| G10 | Gestion du contexte : réduction et compaction | **P2** | Nouveau lab (remplace L08) | L05, L08 |
| G11 | Orchestrations avancées : Handoff, Group Chat, workflow comme agent | **P2** | Nouveau lab | après G1/G6 |
| G12 | Agent Skills | **P2** | Nouveau lab | après G9 |
| G13 | Multimodal | **P2** | Enrichissement | L01 ou L02 |
| G14 | Orchestration Magentic | P3 | Nouveau lab optionnel | — |
| G15 | Agents et workflows déclaratifs (YAML) | P3 | Nouveau lab optionnel | — |
| G16 | Interfaces : AG-UI et DevUI | P3 | Nouveau lab optionnel ou outil de formation | — |
| G17 | Hébergement managé : Durable Extension, Foundry Hosted Agents | P3 | Nouveau lab optionnel | — |
| G18 | Capacités autonomes expérimentales : Looping, Background agents, Todos et modes, CodeAct | P3 | Lab optionnel ou extension de G9 | — |
| G19 | Agents personnalisés (`AIAgent`) et routage multi-modèles | P3 | Lab optionnel | — |

> **Pourquoi ces priorités.** Le classement s'appuie sur la place du concept dans les deux parcours officiels, le *Get Started* (7 étapes) et le *Journey* (9 étapes). Il tient compte aussi de son statut : stable, préversion ou expérimental. Une fonctionnalité **nouvelle** n'est pas jugée prioritaire pour cette raison : Skills, CodeAct, Looping et Magentic restent en P2 ou P3 malgré leur visibilité dans la documentation récente.

**Parcours officiels de référence** (faits vérifiés) :
- **Get Started** ([get-started](https://learn.microsoft.com/en-us/agent-framework/get-started/?pivots=programming-language-csharp)) : 1. First agent · 2. Tools · 3. Multi-turn · 4. Memory & persistence · 5. **Workflows** · 6. **Agent Harness** · 7. **Host your agent**.
- **Journey** ([journey](https://learn.microsoft.com/en-us/agent-framework/journey/?pivots=programming-language-csharp)) : LLM fundamentals · From LLMs to agents · Tools (+MCP) · **Skills** · **Middleware** · Context providers · **Agents as tools** · A2A · **Workflows**.
- **Concepts** ([concepts](https://learn.microsoft.com/en-us/agent-framework/concepts/?pivots=programming-language-csharp)) : *« Agent Framework is built around a couple of foundational concepts, Agents and building on that: Workflows and Harness Agents. »*
- **Vue d'ensemble** ([overview](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)) : les *foundational building blocks* sont *« model clients (chat completions and responses), an agent session for state management, context providers for agent memory, middleware for intercepting agent actions, and MCP clients for tool integration »*.

### 5.2 Fiches détaillées

#### G1 — Workflows graph-based (executors, edges, événements) · **P0**

- **Description.** Modèle d'exécution des workflows .NET.
  - Un graphe d'`Executor` reliés par des edges : `AddEdge`, conditions, `AddSwitch`, `AddFanOutEdge`/`AddFanInBarrierEdge`.
  - Construit avec `WorkflowBuilder` et exécuté par `InProcessExecution.RunAsync`/`RunStreamingAsync`, en supersteps.
  - Observé par des événements : `ExecutorCompletedEvent`, `WorkflowOutputEvent`, `AgentResponseUpdateEvent`…
  - La manière recommandée de déclarer les handlers en C# est l'attribut `[MessageHandler]` sur une classe `partial`, avec le paquet `Microsoft.Agents.AI.Workflows.Generators`.
- **Importance.** Les workflows sont l'un des trois piliers du framework et l'étape 5 du *Get Started*. Les orchestrations (séquentielle, concurrente, handoff, group chat) sont construites sur ce modèle.
- **Pourquoi l'enseigner.** C'est le pendant déterministe des agents. La page Skills le résume : *« if you need to guarantee what steps execute … use a workflow »*. Sans lui, l'apprenant ne peut ni comprendre les orchestrations, ni écrire un processus métier fiable mêlant code et agents.
- **Compétences visées**
  - Écrire un executor avec `[MessageHandler]` et `Executor<TIn,TOut>`.
  - Relier des executors par des edges, y compris conditionnels.
  - Exécuter et streamer un workflow, et lire ses événements.
  - Intégrer un agent comme executor : comprendre `TurnToken` et `AIAgentHostOptions`.
  - Visualiser un workflow avec `ToMermaidString()`.
- **Lab existant extensible.** Aucun. M02/M03 sont trop haut niveau et obsolètes.
- **Choix : nouveau lab**, placé avant les orchestrations refondues.
- **Prérequis.** L01, L03 ; L05 utile.
- **Sources**
  - [concepts/workflows](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/?pivots=programming-language-csharp)
  - [executors](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/executors?pivots=programming-language-csharp) (*« The recommended way to define executor message handlers in C# is to use the [MessageHandler] attribute »*)
  - [edges](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/edges?pivots=programming-language-csharp)
  - [events](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/events?pivots=programming-language-csharp)
  - [builder-and-execution](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/builder-and-execution?pivots=programming-language-csharp)
  - exemples [`01-get-started/05_first_workflow`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/01-get-started/05_first_workflow) et [`03-workflows/_StartHere`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/_StartHere) (01 → 07)
- **Statut.** `Microsoft.Agents.AI.Workflows` et `.Workflows.Generators` sont **stables** (1.26.0). L'API fonctionnelle (*Functional Workflow API*) n'existe pas en C# : *« The functional workflow API is not available for C# at this time »*.

#### G2 — Middleware d'agent et garde-fous · **P0**

- **Description.** Trois niveaux d'interception (page [agent-pipeline](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/agent-pipeline?pivots=programming-language-csharp)) :
  - **agent run** : `AsBuilder().Use(runFunc:, runStreamingFunc:)` ;
  - **function calling** : `Use(funcMiddleware)` ;
  - **IChatClient** : `chatClient.AsBuilder().Use(...)` ou `clientFactory:`.
  
  Des patterns documentés s'appuient dessus :
  - garde-fous et arrêt anticipé ([termination](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/termination?pivots=programming-language-csharp)) ;
  - réécriture de résultat ([result-overrides](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/result-overrides?pivots=programming-language-csharp)) ;
  - gestion d'exceptions ([exception-handling](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/exception-handling?pivots=programming-language-csharp)) ;
  - portée agent ou exécution ([agent-vs-run-scope](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/agent-vs-run-scope?pivots=programming-language-csharp)) ;
  - contexte d'exécution (`AgentRunOptions.AdditionalProperties`, `AgentSession.StateBag`, `AIAgent.CurrentRunContext`, voir [runtime-context](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/runtime-context?pivots=programming-language-csharp)).
- **Importance.** Le middleware figure parmi les *foundational building blocks* et constitue l'étape 5 du *Journey*. C'est le point d'insertion des contrôles de sécurité en .NET. La page Agent Hooks (réservée à Python) indique d'ailleurs : *« Use agent middleware, tool approval, and agent safety to add runtime controls to .NET agents. »*
- **Pourquoi l'enseigner.** C'est le moyen standard d'ajouter, sans toucher à l'agent, la modération d'entrée, la validation de sortie, la journalisation, la limitation et le repli sur erreur. Le parcours ne montre aujourd'hui qu'un middleware de trace d'appels de fonction.
- **Compétences visées**
  - Écrire un middleware d'exécution avec ses deux variantes (non streaming et streaming). Learn précise : *« Ideally both runFunc and runStreamingFunc should be provided »*.
  - Court-circuiter une exécution en renvoyant une `AgentResponse` (garde-fou).
  - Filtrer ou modifier une réponse.
  - Rattraper une exception.
  - Écrire un middleware `IChatClient`.
  - Choisir le bon niveau d'interception.
  - Connaître les pièges : *« setting FunctionInvocationContext.Terminate might result in your chat history being left in an inconsistent state »*.
- **Labs extensibles.** L03 (scénario 4) reste l'introduction. Ajouter tous les types de middleware à L03 le surchargerait.
- **Choix : nouveau lab** *Middleware et garde-fous*, avec renvoi depuis L03.
- **Prérequis.** L01, L03, L05.
- **Sources.** Celles citées ci-dessus, plus l'exemple [`Agent_Step11_Middleware`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step11_Middleware).

#### G3 — Observabilité OpenTelemetry · **P1**

- **Description.**
  - `UseOpenTelemetry(sourceName, configure)` sur l'agent ou le chat client.
  - Traces, métriques et logs selon les conventions sémantiques OpenTelemetry GenAI.
  - Exportateurs OTLP (Aspire Dashboard), Azure Monitor ou console.
  - Pour les workflows : `WithOpenTelemetry`, `WorkflowTelemetryOptions`.
- **Importance.** C'est l'outil de diagnostic et de suivi des coûts en production. Le Harness l'active par défaut.
- **Pourquoi l'enseigner.** C'est indispensable pour des applications fiables et maintenables. Le parcours ne l'enseigne qu'au niveau HTTP (L11).
- **Compétences visées**
  - Instrumenter un agent et un chat client sans dupliquer les spans. Learn : *« you might choose to enable observability only on the chat client or only on the agent »*.
  - Lire une trace : `invoke_agent`, `chat`, `execute_tool`. Ces noms ne sont documentés que dans le pivot Python ; il faut les vérifier en .NET lors de la conception.
  - Lire les métriques de tokens.
  - Protéger les données sensibles : `EnableSensitiveData` uniquement en développement.
- **Labs extensibles.** L11 (scénario 2 réinterprété).
- **Choix : nouveau lab** ; L11 passe en avancé.
- **Prérequis.** L01, L03.
- **Sources**
  - [agents/observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp)
  - [workflows/observability](https://learn.microsoft.com/en-us/agent-framework/workflows/observability?pivots=programming-language-csharp)
  - exemples [`Agent_Step05_Observability`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step05_Observability) et [`AgentOpenTelemetry`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentOpenTelemetry)
- **Statut.** La source par défaut s'appelle `Experimental.Microsoft.Agents.AI`. Le nom l'indique comme expérimental, mais aucun avertissement de compilation n'est documenté.

#### G4 — API Responses, historique géré par le service, background responses · **P1**

- **Description.**
  - `GetResponsesClient().AsAIAgent(model: …)`.
  - Historique stocké par le service (`ConversationId`), avec option `store=false` (`AsIChatClientWithStoredOutputDisabled`).
  - Outils hébergés (code interpreter, file search, MCP hébergé) disponibles seulement avec Responses ou Foundry.
  - **Background responses** : `AllowBackgroundResponses`, `ContinuationToken`. *« Currently, only agents that use the OpenAI Responses API support background responses »*.
- **Importance.** Responses est le client recommandé pour Azure OpenAI. Plusieurs capacités en dépendent.
- **Pourquoi l'enseigner.** Le développeur doit savoir **choisir** son API, en connaissant les conséquences du choix :
  - sur l'historique ;
  - sur la compaction (*« no effect … Responses API with store enabled (the default) »*) ;
  - sur le routage multi-modèles ;
  - sur la portabilité des sessions.
- **Compétences visées**
  - Créer un agent Responses.
  - Comparer historique local et historique côté service.
  - Reprendre une conversation par son identifiant.
  - Lancer et reprendre une réponse en arrière-plan.
- **Labs extensibles.** L01 (scénario Responses), L05 (encadré « historique côté service »). Background responses : scénario optionnel en L05, ou lab P2 dédié.
- **Choix : enrichir L01 et L05.**
- **Prérequis.** L01.
- **Sources**
  - [model-providers/azure-openai](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/azure-openai?pivots=programming-language-csharp)
  - [model-providers](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/?pivots=programming-language-csharp)
  - [background-responses](https://learn.microsoft.com/en-us/agent-framework/agents/background-responses?pivots=programming-language-csharp)
  - [agents/tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/?pivots=programming-language-csharp) (matrice outils / API)
  - exemples [`Agent_With_AzureOpenAIResponses`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentProviders/azure/Agent_With_AzureOpenAIResponses) et [`Agent_Step14_BackgroundResponses`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step14_BackgroundResponses)
- **Point d'attention.** `GetResponsesClient()` est marqué `[Experimental]` (`OPENAI001`) dans le SDK OpenAI 2.13 (manuel de migration, règle 13). Il faut un `#pragma` ciblé et commenté, comme pour la voie Entra ID.

#### G5 — Hébergement et injection de dépendances · **P1**

- **Description.**
  - Enregistrer des agents dans le conteneur : `builder.AddAIAgent("name", …)` qui renvoie un `IHostedAgentBuilder` (`.WithAITool`, `.WithInMemorySessionStore()`).
  - Enregistrer des workflows comme agents : `AddWorkflow(...).AddAsAIAgent()`.
  - Les exposer par des adaptateurs de protocole : `MapOpenAIResponses`, `MapOpenAIConversations`, `MapOpenAIChatCompletions`, `MapA2AHttpJson`/`MapA2AJsonRpc`, `MapAGUIServer`.
  - Ajouter l'isolation et l'autorisation : `UseClaimsBasedAgentIsolation()`, `.RequireAuthorization()`.
- **Importance.** C'est l'étape 7 du *Get Started*, et le passage obligé de la console à une application réelle.
- **Pourquoi l'enseigner.** Aujourd'hui, l'hébergement n'apparaît qu'à travers deux protocoles spécialisés (A2A en L06S, MCP en L10), assemblés à la main.
- **Compétences visées**
  - Enregistrer un agent et ses outils dans l'injection de dépendances, en suivant l'exemple [`Agent_Step06_DependencyInjection`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step06_DependencyInjection) (`Host.CreateApplicationBuilder` + `builder.Services.AddSingleton(agent)`). L'exemple utilise Foundry, qui est en préversion. Transposé à Azure OpenAI, ce patron ne demande que des paquets stables.
  - Exposer un agent derrière un endpoint.
  - Choisir un session store : *« MAF doesn't include a general-purpose durable session store »*.
  - Sécuriser l'endpoint.
- **Labs extensibles.** L06S (variante `AddAIAgent`), L10.
- **Choix : nouveau lab.**
- **Statut — point d'attention fort.**
  - `Microsoft.Agents.AI.Hosting` et `.Hosting.AspNetCore` sont en **préversion**.
  - `Microsoft.Agents.AI.Hosting.OpenAI` est en **alpha**.
  - La page [self-hosting/responses](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/responses?pivots=programming-language-csharp) indique *« coming soon »* pour .NET.
  
  Le lab doit séparer la partie stable (DI d'agents, ASP.NET Core) de la partie en préversion, et le signaler comme le fait déjà L06.
- **Prérequis.** L01, L03, L05.
- **Sources**
  - [get-started/hosting](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp)
  - [hosting/self-hosting](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/?pivots=programming-language-csharp)
  - [openai-endpoints](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/openai-endpoints?pivots=programming-language-csharp)
  - [`04-hosting/README.md`](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/04-hosting/README.md)
  - exemple [`AgentWebChat`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/AgentWebChat)

#### G6 — Human-in-the-loop et checkpoints dans les workflows · **P1**

- **Description.**
  - **Demandes externes** : `RequestPort.Create<TReq,TResp>("id")`, `RequestInfoEvent`, `ExternalRequest.TryGetDataAs<T>`, `CreateResponse`, `StreamingRun.SendResponseAsync`.
  - **Approbations d'outils dans une orchestration** : `RequestInfoEvent` porteur de `ToolApprovalRequestContent`.
  - **Checkpoints** : `CheckpointManager`, `SuperStepCompletedEvent.CompletionInfo.Checkpoint`, `RestoreCheckpointAsync`, `ResumeStreamingAsync`, `OnCheckpointingAsync`/`OnCheckpointRestoredAsync`.
- **Importance.** Ce sont les processus longs avec attente humaine et la reprise après incident. Learn précise : *« Sequential, concurrent, and group chat orchestrations do not pause for free-form user input on their own; pair them with a RequestPort »*.
- **Compétences visées**
  - Suspendre un workflow sur une demande et le reprendre avec la réponse.
  - Sauvegarder et restaurer un workflow.
  - Donner un `Id` stable aux agents pour que la reprise fonctionne.
  - Traiter le stockage des checkpoints comme une frontière de confiance.
- **Choix : nouveau lab**, qui prolonge L09 au niveau workflow.
- **Prérequis.** G1, L09.
- **Sources**
  - [workflows/human-in-the-loop](https://learn.microsoft.com/en-us/agent-framework/workflows/human-in-the-loop?pivots=programming-language-csharp)
  - [workflows/checkpoints](https://learn.microsoft.com/en-us/agent-framework/workflows/checkpoints?pivots=programming-language-csharp)
  - exemples [`HumanInTheLoopBasic`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/HumanInTheLoop/HumanInTheLoopBasic) et [`Checkpoint/*`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Checkpoint)

#### G7 — Évaluation des agents · **P1**

- **Description.** Cadre d'évaluation .NET bâti sur `Microsoft.Extensions.AI.Evaluation` :
  - `agent.EvaluateAsync(queries, evaluator, …)` ;
  - `LocalEvaluator` et `EvalChecks.KeywordCheck`/`ToolCalledCheck` ;
  - `FunctionEvaluator` ;
  - `ExpectedToolCall` et `ConversationSplitters` ;
  - les évaluateurs `RelevanceEvaluator`, `CoherenceEvaluator`, `GroundednessEvaluator`, utilisables directement ;
  - l'évaluation de workflows.
- **Importance.** Pour qu'une application soit fiable, il faut pouvoir vérifier qu'une modification du prompt, de l'outil ou du modèle ne dégrade pas le comportement. C'est le pendant des tests unitaires pour un agent.
- **Pourquoi l'enseigner.** Le seul contenu proche (les juges de M01) est ad hoc et mélange orchestration et évaluation.
- **Compétences visées**
  - Constituer un jeu de requêtes avec sorties et appels d'outils attendus.
  - Combiner des contrôles déterministes et des évaluateurs LLM.
  - Lire et assertir les résultats (`AssertAllPassed`).
  - Évaluer par tour ou sur la conversation entière.
- **Labs extensibles.** M01 (juges) : à déplacer ici.
- **Choix : nouveau lab.**
- **Prérequis.** L01, L02, L03.
- **Sources**
  - [agents/evaluation](https://learn.microsoft.com/en-us/agent-framework/agents/evaluation?pivots=programming-language-csharp)
  - exemples [`02-agents/Evaluation`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Evaluation) et [`03-workflows/Evaluation`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Evaluation)
- **Limite.** Learn ne nomme pas le paquet NuGet des types `LocalEvaluator`/`EvalChecks`, et son espace de noms pour `FoundryEvals` est incohérent. Il faut le vérifier dans les `.csproj` des exemples lors de la conception. `Microsoft.Extensions.AI.Evaluation` est stable (10.11.0).

#### G8 — Sécurité des agents, de façon transversale · **P1**

- **Description.** Recommandations de la page [Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) :
  - cartographier les frontières de confiance ;
  - valider les entrées des fonctions ;
  - exiger une approbation pour les outils à risque (*« By default, all tools … are invoked without user approval »*) ;
  - garder les messages `system` sous le contrôle du développeur ;
  - vérifier les providers d'extension ;
  - valider la sortie du modèle (XSS, SQL, shell) ;
  - protéger les journaux ;
  - sécuriser les données de session ;
  - fixer des limites de ressources.
- **Importance.** C'est une responsabilité partagée, et le framework *« does not manage authentication, encryption, or connection details »*. FIDES (contrôle de flux d'information) est réservé à Python : *« FIDES is currently Python-only »*.
- **État actuel.** La sécurité est couverte de façon dispersée, mais réelle :
  - L01 : message système ;
  - L04, L06S, L10 : sections *Security* ;
  - L09 : approbations ;
  - L11 : masquage des secrets.
  
  Elle est absente de L03 (arguments d'outils), L07 (injection indirecte) et L12 (mémoire empoisonnée), et très brève dans L05 (sessions).
- **Choix : enrichir** les labs concernés. Le lab G2 (*Middleware et garde-fous*) sert de lieu de synthèse. Un lab de sécurité séparé n'est pas recommandé tant que FIDES n'existe pas en .NET (interprétation).
- **Prérequis.** Aucun ; la sécurité est transversale.

#### G9 — Agent Harness · **P1**

- **Description.** `chatClient.AsHarnessAgent()` / `new HarnessAgent(chatClient, HarnessAgentOptions)` assemble les briques du framework dans un agent prêt pour les tâches longues :
  - modes plan/exécution et liste de tâches ;
  - mémoire de fichiers ;
  - approbations permanentes (« don't ask again ») ;
  - Skills, OpenTelemetry, recherche web ;
  - compaction optionnelle ;
  - agents en arrière-plan et boucles optionnels.
- **Importance.** C'est l'un des trois piliers (*« Workflows and Harness Agents »*) et l'étape 6 du *Get Started*. Le paquet `Microsoft.Agents.AI.Harness` est **stable depuis la 1.14.0**, donc disponible en 1.22.0.
- **Pourquoi l'enseigner.** C'est la manière recommandée de construire un agent « opérationnel » sans recâbler chaque brique. Il réutilise tout le parcours : approbations, context providers, compaction, observabilité.
- **Compétences visées**
  - Créer un harness agent.
  - Comprendre et désactiver ses composants par défaut (`Disable*`).
  - Ajouter des outils et des context providers.
  - Activer la compaction (`MaxContextWindowTokens` + `MaxOutputTokens`).
  - Persister une session de harness.
- **Labs extensibles.** L09 le cite déjà en « Going Further ».
- **Choix : nouveau lab**, placé après les fondamentaux et les workflows, comme dans le *Get Started*.
- **Prérequis.** L03, L05, L09, L12 ; G10 utile.
- **Sources**
  - [concepts/harness](https://learn.microsoft.com/en-us/agent-framework/concepts/harness?pivots=programming-language-csharp)
  - [get-started/harness](https://learn.microsoft.com/en-us/agent-framework/get-started/harness?pivots=programming-language-csharp)
  - exemples [`02-agents/Harness`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness) (`Harness_Step01` → `Step05`)
- **Point d'attention.** Les sous-capacités Looping et Background agents sont **expérimentales**.

#### G10 — Gestion du contexte : réduction et compaction · **P2**

- **Description.**
  - `IChatReducer` sur `InMemoryChatHistoryProvider` (stable) réduit l'historique stocké.
  - `CompactionProvider` + `CompactionStrategy` (expérimental, `MAAI001`) compacte le contexte envoyé au modèle, y compris dans la boucle d'outils. Stratégies disponibles : `Truncation`, `SlidingWindow`, `ToolResult`, `Summarization`, `Pipeline`. Déclencheurs : `CompactionTriggers`.
- **Importance.** Maîtriser la fenêtre de contexte et le coût des conversations longues et des boucles d'outils.
- **Pourquoi P2 et pas P1.** La compaction est expérimentale en C# (*« The compaction framework is currently experimental »*). La réduction, plus simple et stable, peut entrer dès maintenant dans L05 (G4/L05).
- **Compétences visées**
  - Borner un historique.
  - Compacter les résultats d'outils anciens.
  - Résumer avec un modèle plus petit.
  - Distinguer réduction de l'historique stocké et compaction du contexte de requête.
  - Mesurer les tokens économisés, en réutilisant la démarche de L08.
- **Choix : nouveau lab qui remplace L08**, avec la comparaison JSON/CSV en scénario secondaire.
- **Prérequis.** L03, L05.
- **Sources**
  - [compaction](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp)
  - [storage](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)
  - exemples [`Agent_Step13_ChatReduction`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step13_ChatReduction) et [`Agent_Step18_CompactionPipeline`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step18_CompactionPipeline)

#### G11 — Orchestrations avancées : Handoff, Group Chat, workflow comme agent · **P2**

- **Description.**
  - **Handoff** : `AgentWorkflowBuilder.CreateHandoffBuilderWith(triage).WithHandoffs(...)`, mode autonome, conditions de fin.
  - **Group Chat** : `CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(agents){…})`, manager personnalisé.
  - **Workflow comme agent** : `workflow.AsAIAgent(...)`.
  - **Sous-workflows** : `BindAsExecutor`.
- **Importance.** Ce sont les patterns multi-agents courants : triage et escalade, débat ou revue.
- **Pourquoi P2.** Ces patterns sont utiles mais spécialisés, et ils supposent G1 maîtrisé.
- **Statut.** Le paquet est stable. Les exemples suppriment encore l'avertissement `MAAIW001` (« Handoff Orchestrations are Experimental »), alors que cet identifiant n'est plus émis dans le code source actuel. À vérifier à la compilation.
- **Choix : nouveau lab.**
- **Prérequis.** G1, M02+M03 refondus.
- **Sources**
  - [orchestrations/handoff](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/handoff?pivots=programming-language-csharp)
  - [group-chat](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/group-chat?pivots=programming-language-csharp)
  - [workflows/as-agents](https://learn.microsoft.com/en-us/agent-framework/workflows/as-agents?pivots=programming-language-csharp)
  - [sub-workflows](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/advanced/sub-workflows?pivots=programming-language-csharp)
  - exemple [`03_AgentWorkflowPatterns`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/_StartHere/03_AgentWorkflowPatterns)

#### G12 — Agent Skills · **P2**

- **Description.**
  - Paquets portables `SKILL.md`, chargés par divulgation progressive : annonce, puis `load_skill`, puis `read_skill_resource`/`run_skill_script`.
  - API C# : `AgentSkillsProvider`, `AgentSkillsProviderBuilder`, `AgentInlineSkill`, `AgentClassSkill<T>`.
  - Approbation requise par défaut sur les outils de skill.
- **Importance.** C'est l'étape 4 du *Journey*, et le Harness l'active par défaut. La page donne une règle utile pour l'enseigner : skill quand l'IA choisit *comment* faire, workflow quand il faut *garantir* les étapes.
- **Pourquoi P2.** Le pattern est récent et orienté agents autonomes ; il n'est pas nécessaire pour la majorité des applications. Les skills via MCP sont **expérimentales**.
- **Choix : nouveau lab**, après le Harness.
- **Prérequis.** L03, L09, G9.
- **Sources**
  - [agents/skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills?pivots=programming-language-csharp)
  - exemples [`02-agents/AgentSkills`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentSkills) (Step01 → Step07)

#### G13 — Multimodal · **P2**

- **Description.** Envoyer une image dans un `ChatMessage` (`UriContent`, `DataContent`).
- **Pourquoi P2.** La capacité est utile, simple et documentée, mais elle n'est pas structurante.
- **Choix : enrichir L01 ou L02**, avec une image extraite en sortie structurée.
- **Sources**
  - [agents/multimodal](https://learn.microsoft.com/en-us/agent-framework/agents/multimodal?pivots=programming-language-csharp)
  - exemple [`Agent_Step08_UsingImages`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step08_UsingImages)

#### G14 à G19 — Sujets avancés · **P3**

| Id | Sujet | Raison du classement P3 | Sources |
|---|---|---|---|
| G14 | Orchestration **Magentic** | Les types sont expérimentaux (*« Magentic types are experimental »* dans le code Learn). Le pattern est coûteux et spécialisé. | [magentic](https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/magentic?pivots=programming-language-csharp), [`Orchestration/Magentic`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Orchestration/Magentic) |
| G15 | **Agents et workflows déclaratifs** (YAML) | `Microsoft.Agents.AI.Declarative` est en RC. Les workflows déclaratifs sont surtout orientés Foundry. | [agents/declarative](https://learn.microsoft.com/en-us/agent-framework/agents/declarative?pivots=programming-language-csharp), [workflows/declarative](https://learn.microsoft.com/en-us/agent-framework/workflows/declarative?pivots=programming-language-csharp) |
| G16 | **AG-UI** et **DevUI** | Paquets en préversion. *« DevUI is a sample app… not intended for production use »*. DevUI peut toutefois servir d'**outil de formation** pour visualiser agents et workflows. | [ag-ui](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/ui/ag-ui/?pivots=programming-language-csharp), [devui](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/ui/devui/?pivots=programming-language-csharp) |
| G17 | **Durable Extension**, **Foundry Hosted Agents** | Ces sujets dépendent d'Azure Functions ou de Foundry. La Durable Extension est en préversion et a été déplacée dans un dépôt séparé. | [durable-extension](https://learn.microsoft.com/en-us/agent-framework/hosting/durable-extension?pivots=programming-language-csharp), [foundry-hosted-agent](https://learn.microsoft.com/en-us/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp) |
| G18 | **Looping**, **Background agents**, **Todos et modes**, **CodeAct** | Looping et background agents sont expérimentaux (*« Agent looping is experimental »*, *« Background agents are experimental »*). Le paquet .NET de CodeAct dépend d'un NuGet non publié (Hyperlight), et Local CodeAct *« is not a security sandbox »*. | [looping](https://learn.microsoft.com/en-us/agent-framework/agents/looping?pivots=programming-language-csharp), [background-agents](https://learn.microsoft.com/en-us/agent-framework/agents/background-agents?pivots=programming-language-csharp), [planning-and-todos](https://learn.microsoft.com/en-us/agent-framework/agents/planning-and-todos?pivots=programming-language-csharp), [code-act](https://learn.microsoft.com/en-us/agent-framework/agents/code-act?pivots=programming-language-csharp) |
| G19 | **Agents personnalisés** (`AIAgent`, `RunCoreAsync`) et **routage multi-modèles** | Ce sont des points d'extension avancés. `RoutePersistingRoutingChatClient` est expérimental. | [custom-agents](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/custom-agents?pivots=programming-language-csharp), [runtime-model-routing](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/runtime-model-routing?pivots=programming-language-csharp) |

**Sujets volontairement exclus**, car réservés à Python en .NET au 2026-10-25 : Agent Hooks, FIDES, Functional Workflow API, *Controlling tool availability*, *Computer use*.

---

## 6. Matrice de couverture pédagogique

**Légende**
- ● : enseigné, c'est un objectif du lab.
- ◐ : utilisé ou traité partiellement.
- ○ : seulement mentionné (README, « Going Further »).
- † : enseigné avec une API obsolète.
- Case vide : absent.

| Concept (priorité si lacune) | L01 | L02 | L03 | L04 | L05 | L06S | L06C | L07 | L08 | L09 | L10 | L11 | L12 | M01 | M02 | M03 | Couverture |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Création et exécution d'agent (`AsAIAgent`, `RunAsync`, `AgentResponse`) | ● | ◐ | ◐ | ◐ | ◐ | ◐ | ◐ | ◐ | ◐ | ◐ | ◐ | ◐ | ◐ | † | † | † | **Bonne** |
| Streaming (`RunStreamingAsync`) | ● | ◐ | | | | | | | | | | | | | | | Correcte |
| Choix Chat Completions / Responses, historique côté service (G4, P1) | ○ | | | | ○ | | | | | | | | | | | | **Absente** |
| Multimodal (G13, P2) | | | | | | | | | | | | | | | | | **Absente** |
| Sortie structurée | | ● | | ◐ | | | | | ◐ | | | | ◐ | † | | | **Bonne** |
| Function tools | | | ● | | | ◐ | | ◐ | ◐ | ◐ | ◐ | ◐ | | | | | **Bonne** |
| Outils avec services (DI), pattern plugin | | | ◐ | | | | | | | | | | | | | | Partielle |
| Approbation d'outils (HITL, niveau agent) | | | ○ | | | | | | | ● | | | | | | | **Bonne** |
| Middleware d'appel de fonction | | | ◐ | | | | | | | | | | | | | | Partielle |
| Middleware d'exécution et de chat client, garde-fous (G2, P0) | | | ○ | | | ◐ | | | | | | ○ | | | | | **Absente** |
| Client MCP | | | | ● | | | | | | | ◐ | | | | | | **Bonne** |
| Agent exposé en MCP | | | | | | | | | | | ● | | | | | | Bonne |
| Agent comme outil (`AsAIFunction`) | | | ○ | | | | ◐ | | | | ◐ | | | † | | | Redondante, version dédiée obsolète |
| A2A serveur et client | | | | | | ● | ● | | | | | | | | | | Bonne (préversion) |
| Sessions et sérialisation | | | | | ● | | | ◐ | | ◐ | | | ◐ | | | | **Bonne** |
| `ChatHistoryProvider` personnalisé | | | | | ● | | | ◐ | | | | | | | | | Bonne |
| Réduction et compaction de l'historique (G10, P2) | | | | | ○ | | | | | | | | | | | | **Absente** |
| `AIContextProvider` personnalisé | | | | | | | | ◐ | | | | | ● | | | | Bonne |
| RAG (embeddings, VectorData, `TextSearchProvider`) | | | | | | | | ● | | | | | | | | | Bonne |
| Mémoire intégrée (`ChatHistoryMemoryProvider`…) | | | | | | | | | | | | | ○ | | | | Absente |
| Transport HTTP, pipeline du SDK | | | | | | | | | | | | ● | | | | | Bonne (périphérique) |
| Observabilité OpenTelemetry (G3, P1) | | | | | | | | | | | | ○ | | | | | **Absente** |
| Évaluation (G7, P1) | | | | | | | | | | | | | | † | | | **Absente** (version ad hoc) |
| Sécurité (*Agent Safety*) (G8, P1) | ◐ | | | ◐ | ○ | ◐ | | | | ◐ | ◐ | ◐ | | | | | Partielle, dispersée |
| Coût et usage en tokens | ◐ | | ◐ | ◐ | ◐ | | | ◐ | ● | ◐ | | ◐ | ◐ | ◐ | | | Sur-représentée |
| Workflows : executors, edges, événements (G1, P0) | | | | | | | | | | | | | | | † | † | **Absente** |
| Orchestration séquentielle | | | | | | | | | | | | | | | † | | Obsolète |
| Orchestration concurrente | | | | | | | | | | | | | | | | † | Obsolète |
| Handoff, Group Chat (G11, P2) | | | | | | | | | | | | | | | | | **Absente** |
| HITL et checkpoints de workflow (G6, P1) | | | | | | | | | | | | | | | | | **Absente** |
| Workflow comme agent, sous-workflows (G11, P2) | | | | | | | | | | | | | | | | | **Absente** |
| Hébergement ASP.NET Core et DI des agents (G5, P1) | | | | | | ◐ | | | | | ◐ | | | | | | Partielle (protocoles seulement) |
| Agent Harness (G9, P1) | | | | | | | | | | ○ | | | | | | | **Absente** |
| Agent Skills (G12, P2) | | | | | | | | | | | | | | | | | **Absente** |
| Background responses, tâches longues (G4) | | | | | | ○ | ○ | | | | | | | | | | Absente |
| Agents et workflows déclaratifs (G15, P3) | | | | | | | | | | | | | | | | | Absente |
| AG-UI, DevUI (G16, P3) | | | | | | | | | | | | | | | | | Absente |
| Agents personnalisés, routage de modèles (G19, P3) | | | | | | | | | | | | | | | | | Absente |

**Lecture de la matrice.**
- Le **socle de l'agent individuel** est bien couvert, souvent deux fois, ce qui est un renforcement positif : création, sortie structurée, outils, MCP, sessions, context providers, RAG, approbations.
- Le **coût en tokens** est présent dans presque tous les labs, et dédié dans L08. Il est sur-représenté par rapport à son poids dans la documentation officielle.
- Plusieurs blocs entiers sont **absents** ou **seulement obsolètes** : contrôle (middleware), exploitation (observabilité, évaluation), workflows, production (hébergement) et Harness.

---

## 7. Plan d'action priorisé

Effort indicatif : **S** < ½ jour · **M** ≈ 1 à 2 jours · **L** ≈ 3 jours ou plus, pour un lab complet (Start + Solution + README + auto-tests du dashboard), d'après la taille des labs existants. C'est une estimation de l'auditeur.

### P0 — Critique

| # | Action | Type | Cible | Effort | Justification |
|---|---|---|---|---|---|
| P0-1 | **Re-tester le streaming** (L01 scénario 5, L02 scénario 4, L06 sans `StreamingWorkaround`) avec une référence explicite à `Microsoft.Extensions.AI.OpenAI` **10.11.0**. Puis retirer le contournement de L06S et mettre à jour le manuel §8. | Corriger | L01, L02, L06S | S | Bug amont dotnet/extensions#7790 fermé le 2026-10-12 et cité dans la release 10.11.0 ; MAF 1.26.0 référence encore 10.10.1. Des labs peuvent échouer chez l'apprenant. |
| P0-2 | **Créer le lab « Workflows fondamentaux »** (G1) : executors `[MessageHandler]`, edges (dont conditionnels), `RunStreamingAsync`, événements, agent dans un workflow (`TurnToken`), visualisation. | Créer | nouveau | L | Pilier du framework, étape 5 du Get Started, absent du parcours. |
| P0-3 | **Refondre M02 + M03** en un lab « Orchestrations séquentielle et concurrente » sur l'API actuelle. Corriger le scénario concurrent (agrégateur) et streamer `AgentResponseUpdateEvent`. Ajouter `chainOnlyAgentResponses`. Ajouter au dashboard. | Refondre et fusionner | M02, M03 | M | API supprimées (`StreamAsync`, `CreateAIAgent`) ; défaut de conception du scénario concurrent. |
| P0-4 | **Refondre M01** « Agent comme outil » sur l'API actuelle : retirer `AgentFactory`/`AgentBuilder`, sortir les juges vers G7, corriger le README (`DefaultAzureCredential`), ajouter au dashboard. | Refondre | M01 | M | API supprimées ; abstraction maison contraire à la règle 12 du manuel du projet. |
| P0-5 | **Créer le lab « Middleware et garde-fous »** (G2) : middleware run (streaming et non streaming), function, chat client ; court-circuit, réécriture, exceptions, contexte d'exécution ; synthèse sécurité. | Créer | nouveau (lien depuis L03) | M–L | *Foundational building block*, étape 5 du Journey ; point d'insertion des contrôles .NET. |

> Pour P0-3 et P0-4, refondre **sur la même version que le reste du parcours** (1.22.0) respecte la règle 1 du manuel de migration (« une seule version MAF »). La montée de version globale est traitée en P2-6.

### P1 — Élevée

| # | Action | Type | Cible | Effort | Justification |
|---|---|---|---|---|---|
| P1-1 | **Créer le lab « Observabilité OpenTelemetry »** (G3) ; repositionner L11 en module avancé. | Créer, repositionner | nouveau, L11 | M | Approche officielle de télémétrie ; seul L11 traite le sujet, au niveau HTTP. |
| P1-2 | **Introduire l'API Responses** : scénario 6 dans L01 ; encadré « historique géré par le service » et background responses dans L05. | Enrichir | L01, L05 | S–M | *« Responses is the recommended primary client »*. |
| P1-3 | **Créer le lab « Évaluation des agents »** (G7), qui reprend les juges de M01. | Créer | nouveau | M | Fiabilité ; API officielle désormais disponible. |
| P1-4 | **Créer le lab « Human-in-the-loop et checkpoints de workflow »** (G6). | Créer | nouveau | M | Processus longs, reprise ; dépend de P0-2. |
| P1-5 | **Créer le lab « Agent Harness »** (G9). | Créer | nouveau | M | Pilier et étape 6 du Get Started ; paquet stable depuis 1.14.0. |
| P1-6 | **Créer le lab « Hébergement et DI »** (G5), en séparant clairement la partie stable de la partie en préversion ou alpha. Ajouter à L06S une variante `AddAIAgent` + `AddA2AServer`. | Créer, enrichir | nouveau, L06S | M–L | Étape 7 du Get Started ; aujourd'hui seulement via A2A et MCP assemblés à la main. |
| P1-7 | **Renforcer la sécurité** (G8) : arguments d'outils (L03), injection indirecte (L07), sessions restaurées (L05), mémoire empoisonnée (L12). | Enrichir | L03, L05, L07, L12 | S | Page Agent Safety ; lacunes vérifiées dans les README. |
| P1-8 | **Corriger L03** : remplacer ou rendre optionnel le scénario réflexion au profit du pattern plugin (`Agent_Step12_Plugins`). | Corriger | L03 | S | Moindre privilège ; pattern officiel. |
| P1-9 | **Réordonner le parcours** (§8) et mettre à jour le schéma « Learning Path » du `README.md` racine et l'ordre du dashboard. | Réorganiser | README, `labs.json` | S | Progression alignée sur le Get Started et le Journey. |

### P2 — Moyenne

| # | Action | Type | Cible | Effort |
|---|---|---|---|---|
| P2-1 | **Créer le lab « Contexte et coûts : réduction et compaction »** (G10), qui remplace L08 ; garder JSON/CSV en scénario secondaire, ou L08 en optionnel. | Remplacer | L08 | M |
| P2-2 | Ajouter à L05 un scénario « borner l'historique » (`ChatReducer`), et clarifier le scénario vector store par rapport à la mémoire sémantique. | Enrichir | L05 | S |
| P2-3 | **Créer le lab « Handoff, Group Chat, workflow comme agent »** (G11). | Créer | nouveau | M |
| P2-4 | **Créer le lab « Agent Skills »** (G12). | Créer | nouveau | M |
| P2-5 | Ajouter un scénario **multimodal** (G13). | Enrichir | L01 ou L02 | S |
| P2-6 | **Monter le parcours de 1.22.0 vers 1.26.x**, dans une vague dédiée et testée (voir §10.2). Points de vigilance : approbations (L09), A2A preview3 (L06), `OpenAI` 2.14.0 / MEAI 10.10.1 (tous). | Mettre à jour | tous | M |
| P2-7 | **Renommer** `Lab05-AIAgentWithThreads` en `…WithSessions`, et le libellé « Threads » du README racine (ligne 576). Mettre à jour `labs.json` et les tests du dashboard. | Corriger | L05, README, dashboard | S |
| P2-8 | L10 : présenter `AsAIFunction` comme acquis de M01 ; encadré « hébergement MCP natif .NET à venir ». L06C : déplacer après M01 ; vérifier la liaison par défaut. | Corriger | L10, L06C | S |
| P2-9 | L12 : comparer avec `ChatHistoryMemoryProvider`. L07 : scénario `OnDemandFunctionCalling`. L09 : scénario `UseToolApproval` + `AutoApprovalRules`. | Enrichir | L07, L09, L12 | S chacun |

### P3 — Faible

| # | Action | Cible |
|---|---|---|
| P3-1 | Lab optionnel **Magentic** (G14), une fois les types stabilisés. | nouveau |
| P3-2 | Lab optionnel **agents et workflows déclaratifs** (G15). | nouveau |
| P3-3 | **DevUI** comme outil de visualisation dans les labs workflows ; lab optionnel **AG-UI** (G16). | nouveau / outillage |
| P3-4 | Lab optionnel **hébergement managé** (Durable Extension ou Foundry Hosted Agents) (G17). | nouveau |
| P3-5 | Extension du lab Harness : **Looping, Background agents, Todos et modes** (G18), à suivre jusqu'à leur stabilisation ; **CodeAct** quand le paquet Hyperlight sera publié. | extension G9 |
| P3-6 | Lab optionnel **agents personnalisés et routage multi-modèles** (G19). | nouveau |
| P3-7 | Annexe « Fournisseurs » : le même agent sur Foundry (`AIProjectClient`), qui est le point de départ des exemples officiels. | README L01 ou annexe |

---

## 8. Parcours pédagogique recommandé

### 8.1 Principes

1. **S'aligner sur les deux parcours officiels.** Le *Get Started* va de l'agent aux outils, à la mémoire, aux workflows, au Harness puis à l'hébergement. Le *Journey* va des outils aux skills, au middleware, aux context providers, aux agents comme outils, à A2A puis aux workflows.
2. **Contrôle et exploitation avant la composition.** Le middleware, l'observabilité et l'évaluation viennent avant les systèmes multi-agents, pour que l'apprenant sache diagnostiquer ce qu'il construit ensuite.
3. **Fondamentaux de workflow avant orchestrations prêtes à l'emploi.**
4. **Sujets périphériques en annexe** : transport HTTP, formats de données. **Sujets expérimentaux en option.**

### 8.2 Ordre proposé

| Ordre | Module | Lab | Statut | Priorité | Prérequis |
|---|---|---|---|---|---|
| **A** | **Fondamentaux de l'agent** | | | | |
| A1 | | L01 Premier agent (+ Responses ; multimodal en option) | Amélioré | P1 | — |
| A2 | | L02 Sortie structurée | Conservé | — | A1 |
| A3 | | L03 Function tools (plugin, outils sûrs) | Amélioré | P1 | A1 |
| A4 | | L04 Client MCP | Conservé | — | A3 |
| **B** | **Conversation, mémoire, connaissances** | | | | |
| B1 | | L05 Sessions et historique (+ réduction, + historique côté service) | Amélioré | P1/P2 | A1 |
| B2 | | L07 RAG (+ sécurité du RAG) | Conservé | — | A3, B1 |
| B3 | | L12 Context provider et mémoire long terme | Conservé | — | A2, B1 |
| **C** | **Contrôle, sécurité, qualité** | | | | |
| C1 | | L09 Approbation humaine | Conservé (avancé dans le parcours) | — | A3, B1 |
| C2 | | **Middleware et garde-fous** | Nouveau | **P0** | A3, B1 |
| C3 | | **Observabilité OpenTelemetry** | Nouveau | P1 | A3 |
| C4 | | **Évaluation des agents** | Nouveau | P1 | A2, A3 |
| C5 | | **Contexte et coûts : réduction et compaction** (remplace L08) | Nouveau | P2 | B1 |
| **D** | **Composition et interopérabilité** | | | | |
| D1 | | M01 Agent comme outil | Refondu | **P0** | A3 |
| D2 | | L10 Agent exposé en MCP | Amélioré | P2 | A4, D1 |
| D3 | | L06S + L06C A2A serveur et client | Amélioré / Conservé | P0/P2 | D1 |
| **E** | **Workflows** | | | | |
| E1 | | **Workflows fondamentaux** | Nouveau | **P0** | A1, A3 |
| E2 | | M02 + M03 Orchestrations séquentielle et concurrente | Refondu et fusionné | **P0** | E1 |
| E3 | | **Human-in-the-loop et checkpoints** | Nouveau | P1 | E1, C1 |
| E4 | | **Handoff, Group Chat, workflow comme agent** | Nouveau | P2 | E2 |
| **F** | **Agents autonomes et mise en production** | | | | |
| F1 | | **Agent Harness** | Nouveau | P1 | C1, B3, C5 utile |
| F2 | | **Agent Skills** | Nouveau | P2 | F1 |
| F3 | | **Hébergement et injection de dépendances** | Nouveau | P1 | B1, C2 ; D3 utile |
| **Annexes** | **Optionnel / avancé** | L11 Transport HTTP personnalisé · L08 (si conservé) · G14 à G19 (Magentic, déclaratif, AG-UI/DevUI, hébergement managé, capacités expérimentales, agents personnalisés) | Repositionné / P3 | — | selon le sujet |

### 8.3 Différences avec le parcours actuel

| Changement | Raison |
|---|---|
| L09 (approbations) passe de la 9ᵉ place au début du module C, juste après les sessions | Il prolonge directement L03 et L05 ; c'est une pratique de sécurité de base. |
| L06 (A2A) passe de la 6ᵉ place au module D, après « Agent comme outil » | Ordre du Journey (étapes 7 puis 8) ; la notion d'agent-outil est acquise avant l'agent distant. |
| L08 et L11 quittent la piste principale | Sujets périphériques au framework, remplacés par la compaction (C5) et l'observabilité (C3). |
| Les labs MultiAgentSystem sont éclatés en modules D (agent comme outil) et E (workflows) | La notion de « multi-agent » recouvre deux mécanismes distincts : la composition par outils et les workflows. |
| Ajout d'un module F, fin de parcours | Reflète les étapes 6 et 7 du Get Started (Harness, Hosting). |

**Taille du parcours.** La piste principale passe de 16 à 23 projets de lab : 10 nouveaux, 3 refondus en 2, et L08 et L11 sortis en annexe. S'y ajoutent les annexes. Si cette taille est excessive pour la formation, voici un **socle minimal** :
- les P0 : C2, D1, E1, E2 ;
- les P1 : C3, C4, F1, F3 et les enrichissements de L01 et L05.

E3 (HITL et checkpoints) peut être fusionné dans E2.

---

## 9. Références officielles

### 9.1 Microsoft Learn — Agent Framework (pivot C#)

Toutes les URL ci-dessous sont à compléter par `?pivots=programming-language-csharp` ; elles ont été consultées le 2026-10-25.

**Vue d'ensemble et parcours**
- https://learn.microsoft.com/en-us/agent-framework/overview/
- https://learn.microsoft.com/en-us/agent-framework/get-started/ · `your-first-agent` · `add-tools` · `multi-turn` · `memory` · `workflows` · `harness` · `hosting`
- https://learn.microsoft.com/en-us/agent-framework/journey/ (et `journey/agents-as-tools`)
- https://learn.microsoft.com/en-us/agent-framework/concepts/

**Agents : concepts**
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/running-agents
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/agent-pipeline
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/runtime-model-routing
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/custom-agents
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/ · `session` · `context-providers` · `storage` · `compaction` · `chat-history-memory-provider`
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/ · `defining-middleware` · `chat-middleware` · `agent-vs-run-scope` · `termination` · `result-overrides` · `exception-handling` · `shared-state` · `runtime-context`
- https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety
- https://learn.microsoft.com/en-us/agent-framework/concepts/harness

**Agents : capacités et outils**
- https://learn.microsoft.com/en-us/agent-framework/agents/ · `multimodal` · `structured-outputs` · `background-responses` · `rag` · `declarative` · `observability` · `evaluation` · `agent-hooks` · `skills` · `code-act` · `security` · `looping` · `background-agents` · `planning-and-todos`
- https://learn.microsoft.com/en-us/agent-framework/agents/tools/ · `function-tools` · `controlling-tool-availability` · `tool-approval` · `code-interpreter` · `file-search` · `web-search` · `hosted-mcp-tools` · `local-mcp-tools`

**Workflows**
- https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/ · `functional` · `builder-and-execution` · `executors` · `edges` · `events` · `state` · `advanced/agent-executor` · `advanced/execution-modes` · `advanced/resettable-executors` · `advanced/sub-workflows`
- https://learn.microsoft.com/en-us/agent-framework/workflows/ · `agents-in-workflows` · `human-in-the-loop` · `checkpoints` · `declarative` · `observability` · `as-agents` · `visualization`
- https://learn.microsoft.com/en-us/agent-framework/workflows/orchestrations/ · `sequential` · `concurrent` · `handoff` · `group-chat` · `magentic`

**Intégrations et hébergement**
- https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/ · `azure-openai` · `openai` · `microsoft-foundry`
- https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/context-providers/ · `local`
- https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/vector-stores/
- https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/a2a
- https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/ui/ag-ui/ · `ui/devui/`
- https://learn.microsoft.com/en-us/agent-framework/integrations/by-provider/microsoft-azure
- https://learn.microsoft.com/en-us/agent-framework/hosting/ · `foundry-hosted-agent` · `durable-extension` · `self-hosting/` · `self-hosting/responses` · `self-hosting/openai-endpoints` · `self-hosting/a2a/dotnet` · `self-hosting/mcp`
- https://learn.microsoft.com/en-us/agent-framework/migration-guide/ · `from-semantic-kernel/` · `agent-to-agent-sdk-v1`
- https://learn.microsoft.com/en-us/agent-framework/support/faq · `support/upgrade/`

### 9.2 GitHub — `microsoft/agent-framework`

- Dépôt : https://github.com/microsoft/agent-framework
- Notes de version : https://github.com/microsoft/agent-framework/releases (tags `dotnet-1.22.0` → `dotnet-1.26.0`)
- Dépendances 1.26.0 : https://github.com/microsoft/agent-framework/blob/dotnet-1.26.0/dotnet/Directory.Packages.props
- Exemples .NET : https://github.com/microsoft/agent-framework/tree/main/dotnet/samples (README : ordre `01-get-started` → `05-end-to-end`)
  - `01-get-started/01_hello_agent` … `05_first_workflow`
  - `02-agents/Agents/README.md`, avec les exemples `Agent_Step01` à `Agent_Step24`, notamment :
    - `Step01_UsingFunctionToolsWithApprovals` · `Step02_StructuredOutput` · `Step04_3rdPartyChatHistoryStorage` · `Step05_Observability` · `Step06_DependencyInjection`
    - `Step07_AsMcpTool` · `Step08_UsingImages` · `Step09_AsFunctionTool` · `Step11_Middleware` · `Step12_Plugins`
    - `Step13_ChatReduction` · `Step14_BackgroundResponses` · `Step17_AdditionalAIContext` · `Step18_CompactionPipeline` · `Step24_MultiModelRouting`
  - `02-agents/AgentProviders/azure/Agent_With_AzureOpenAIChatCompletion`, `Agent_With_AzureOpenAIResponses`
  - `02-agents/A2A/*`, `02-agents/ModelContextProtocol/*`, `02-agents/AgentWithRAG/*`, `02-agents/AgentWithMemory/*`
  - `02-agents/Harness/*`, `02-agents/AgentSkills/*`, `02-agents/Evaluation/*`, `02-agents/AgentOpenTelemetry`
  - `03-workflows/README.md`, `03-workflows/_StartHere/01_Streaming` … `07_WriterCriticWorkflow`, `HumanInTheLoop/HumanInTheLoopBasic`, `Checkpoint/*`, `Orchestration/Handoff`, `Orchestration/Magentic`
  - `04-hosting/README.md`, `05-end-to-end/AgentWebChat`, `05-end-to-end/A2AClientServer`
- PR citées :
  - [#3197](https://github.com/microsoft/agent-framework/pull/3197) (`AgentRunResponse` → `AgentResponse`)
  - [#3214](https://github.com/microsoft/agent-framework/pull/3214) (événements d'agent des workflows)
  - [#3222](https://github.com/microsoft/agent-framework/pull/3222) (`CreateAIAgent` → `AsAIAgent`)
  - [#3380](https://github.com/microsoft/agent-framework/pull/3380) (`ReflectingExecutor` obsolète)
  - [#3430](https://github.com/microsoft/agent-framework/pull/3430) (`AgentThread` → `AgentSession`)
  - [#3441](https://github.com/microsoft/agent-framework/pull/3441) (événements unifiés sous `WorkflowOutputEvent`)
  - [#4090](https://github.com/microsoft/agent-framework/pull/4090) (`StreamAsync` → `RunStreamingAsync`, `RunId` → `SessionId`)
  - [#7986](https://github.com/microsoft/agent-framework/pull/7986) (suppression de la dépendance `Azure.AI.OpenAI`)
  - [#8200](https://github.com/microsoft/agent-framework/pull/8200), [#8403](https://github.com/microsoft/agent-framework/pull/8403), [#8491](https://github.com/microsoft/agent-framework/pull/8491), [#8641](https://github.com/microsoft/agent-framework/pull/8641), [#8730](https://github.com/microsoft/agent-framework/pull/8730), [#8754](https://github.com/microsoft/agent-framework/pull/8754), [#9152](https://github.com/microsoft/agent-framework/pull/9152), [#9386](https://github.com/microsoft/agent-framework/pull/9386), [#9542](https://github.com/microsoft/agent-framework/pull/9542), [#9563](https://github.com/microsoft/agent-framework/pull/9563)
- Bug amont du streaming : https://github.com/dotnet/extensions/issues/7790 (fermé le 2026-10-12) ; release https://github.com/dotnet/extensions/releases (v10.11.0, 2026-10-15)

### 9.3 NuGet (versions au 2026-10-25)

- https://www.nuget.org/packages/Microsoft.Agents.AI · `Microsoft.Agents.AI.OpenAI` · `Microsoft.Agents.AI.Workflows` · `Microsoft.Agents.AI.Workflows.Generators` · `Microsoft.Agents.AI.Harness` (stables 1.26.0)
- `Microsoft.Agents.AI.Hosting`, `Microsoft.Agents.AI.A2A`, `Microsoft.Agents.AI.Hosting.A2A.AspNetCore`, `Microsoft.Agents.AI.DevUI`, `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` (1.26.0-preview.261023.1)
- `Microsoft.Agents.AI.Hosting.OpenAI`, `Microsoft.Agents.AI.Mcp` (1.26.0-alpha.261023.1) · `Microsoft.Agents.AI.Declarative`, `Microsoft.Agents.AI.Purview` (1.26.0-rc1)
- `Microsoft.Extensions.AI.OpenAI` 10.11.0 · `Microsoft.Extensions.AI.Evaluation` 10.11.0 · `ModelContextProtocol` 2.3.0

### 9.4 Documents du projet utilisés

- `README.md` (racine) : objectifs et parcours « Learning Path »
- `Migration/Migration-Manual.md`, `Migration/Migration-Plan.md`, `Migration/Lab01…Lab12-Migration-Report.md`
- `Dashboard/LabDashboard/labs.json`
- `LearningLabs/**/README.md`, `LearningLabs/**/Solution/*.cs`, `*.csproj`

---

## 10. Annexes

### 10.1 Correspondance des API à appliquer lors de la refonte des labs MultiAgentSystem

Faits vérifiés : notes de version, PR citées, comparaison de la documentation XML du paquet Workflows preview.251219.1 avec la 1.26.0, et code source au commit `67ba500`.

| Utilisé dans M01–M03 | API actuelle | Référence |
|---|---|---|
| `AzureOpenAIClient` (`Azure.AI.OpenAI` 2.7.0-beta.2) | `OpenAIClient` + endpoint `/openai/v1/` (`CommonUtilities/AzureOpenAIEndpoint.ToV1Uri`) | PR #7986, exemple `Agent_With_AzureOpenAIChatCompletion` |
| `chatClient.CreateAIAgent(...)` | `chatClient.AsAIAgent(...)` | PR #3222 |
| `AgentRunResponse<T>` | `AgentResponse<T>` | PR #3197 |
| `InProcessExecution.StreamAsync(workflow, messages)` | `InProcessExecution.RunStreamingAsync(workflow, messages)` | PR #4090 |
| `StreamingRun.RunId` | `StreamingRun.SessionId` | PR #4090 |
| `(List<ChatMessage>)outputEvt.Data!` | `outputEvt.As<List<ChatMessage>>()`. Tester d'abord `AgentResponseUpdateEvent`, qui dérive de `WorkflowOutputEvent`. | PR #3441, page *Sequential* |
| `AgentRunUpdateEvent` / `AgentRunResponseEvent` | `AgentResponseUpdateEvent` / `AgentResponseEvent` | PR #3214 |
| `AddFanInEdge` | `AddFanInBarrierEdge` | PR #4090 |
| `ReflectingExecutor<T>` / `IMessageHandler<T>` | `[MessageHandler]` sur une classe `partial` dérivée d'`Executor` + paquet `Microsoft.Agents.AI.Workflows.Generators` | PR #3380, page *Executors* |
| `TurnToken(emitEvents: …)` + `TrySendMessageAsync` | **Inchangé**, toujours requis pour déclencher les agents | exemples `_StartHere` |
| `Azure.Identity` 1.18.0-beta.2, `Microsoft.Extensions.Hosting` 9.0.0 | `Azure.Identity` 1.21.0 ; plus de `Hosting` pour une application console (configuration via `ConfigurationBuilder`, comme Lab01) | `Migration/Migration-Manual.md` §1 et §5 |

### 10.2 Changements 1.23.0 → 1.26.0 à prendre en compte lors d'une montée de version

| Version | Changement | Labs concernés |
|---|---|---|
| 1.23.0 | [BREAKING] *Better support tool changes between runs* (#8754) | L03, L09 (à re-tester) |
| 1.23.0 | [BREAKING] Bump `OpenAI` 2.14.0, MEAI 10.10.1, `Azure.AI.Projects` 3.0.0-beta.3 (#8730) | tous |
| 1.23.0 | [BREAKING] *use allow list for configuration keys* (#8200) | à analyser (portée non déterminée lors de l'audit) |
| 1.23.0 | [BREAKING] *Enforce approval response binding consistently* (#8641) ; *fix … invoke function tool could bypass approval* (#8403) ; *Only consume stored approval state when the run succeeds* (#8692) | L09 |
| 1.23.0 | *Add function replace support for function middleware* (#8615) | L03 (fonctionnalité nouvelle, optionnelle) |
| 1.24.0 | *document A2A authentication and tool authorization* (#8491) ; *Allow compaction for per-service-call local chat history* (#8845) | L06S, G10 |
| 1.25.0 | *Upgrade A2A packages to 1.0.0-preview3* (#9386) ; *Automatically reject tool-approval requests that never receive a response* (#9152) | L06S/L06C, L09 |
| 1.26.0 | *Reject duplicate tool approval request IDs* (#9542) ; *Add AgentSessionStore.DeleteSessionAsync* (#8801) ; *Preserve A2A task status messages* (#9534) ; *Add gRPC protocol selection to the A2A sample* (#9563) | L09, L06, G5 |

### 10.3 Incohérences relevées dans la documentation officielle (pour mémoire)

Ces écarts expliquent pourquoi les labs suivent parfois le dépôt GitHub plutôt que Learn. Ils sont à surveiller lors des prochaines révisions.

- **Client Azure OpenAI.** Les extraits C# de la page fournisseur Azure OpenAI utilisent `AzureOpenAIClient` (`Azure.AI.OpenAI --prerelease`). Les exemples du dépôt utilisent `OpenAIClient` + `/openai/v1/` depuis 1.21.0, tout comme les labs.
- **Installation.** Plusieurs pages demandent `--prerelease` pour des paquets stables : `Microsoft.Agents.AI.Workflows`, `.Generators`, `.OpenAI`.
- **Pages incomplètes ou mélangées.**
  - *Storage* : identifiants Python dans la vue C#.
  - *State* : exemple C# « Coming soon ».
  - *Executors* : `BindExecutor` sur un `Func`, alors que le dépôt utilise `BindAsExecutor`.
- **Self-hosting Responses.** La page annonce « coming soon » pour .NET, alors que le dépôt contient déjà un exemple `04-hosting/af-hosting/local_responses`, basé sur un paquet alpha.
- **Observabilité.** Les noms de spans et de métriques ne sont documentés que dans le pivot Python.
- **Évaluation.** L'espace de noms de `FoundryEvals` est incohérent : `Microsoft.Agents.AI.AzureAI` contre `.Foundry`.
