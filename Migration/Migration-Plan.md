# Plan de migration du lab — MAF 1.22.0 / .NET 10

> Complète [Migration-Manual.md](Migration-Manual.md) (règles, mapping, stratégie de test).
> Statut au 2026-09-27 : **Phase pilote (Lab01) terminée** — voir [Lab01-Migration-Report.md](Lab01-Migration-Report.md). Vague 1 en cours : **Lab02** ([rapport](Lab02-Migration-Report.md)) et **Lab03** ([rapport](Lab03-Migration-Report.md)) migrés ; **Lab08** migré le 2026-09-30 ([rapport](Lab08-Migration-Report.md), tests d'exécution bloqués par les identifiants Azure, comme Lab07) ; reste Lab11. Vague 2 : **Lab04** migré ([rapport](Lab04-Migration-Report.md)) ; reste Lab10. Vague 3 : **Lab05** migré ([rapport](Lab05-Migration-Report.md)) et **Lab07** migré le 2026-09-29 ([rapport](Lab07-Migration-Report.md), tests d'exécution bloqués par les identifiants Azure de l'environnement, cf. manuel §8) ; reste la phase 0 CommonUtilities (avec Lab12). Vague 4 : **Lab09** migré le 2026-10-02 ([rapport](Lab09-Migration-Report.md), exécution réelle validée clé API **et** Entra ID — les identifiants fonctionnent de nouveau) ; reste Lab12. Vague 5 : **Lab06** (client **et** serveur) migré le 2026-09-28 ([rapport](Lab06-Migration-Report.md)) ; restent MAS-Lab01/02/03. ⚠️ Régression streaming Azure OpenAI (manuel §8) : Lab01/Lab02 à re-tester.

---

## 1. Stratégie globale

1. **Pilote** : Lab01 valide la version cible, la construction du client, la configuration et le format Start/Solution/README.
2. **Phase 0 — composants communs**, en même temps que le premier lab qui en a besoin (et non avant, pour ne pas casser les labs non migrés).
3. **Migration par vagues**, dans l'ordre pédagogique, en regroupant les labs qui partagent un même changement conceptuel.
4. Chaque lab n'est « migré » qu'après la checklist §5 et la stratégie de test du manuel (§7).
5. Version **figée à 1.22.0** pendant toute la migration (voir le risque « cadence de release » dans le manuel).

## 2. Composants communs

| Composant | Utilisé par | Action | Quand |
|---|---|---|---|
| `CommonUtilities/AzureOpenAIEndpoint.cs` | tous | **Ajouté pendant le pilote** (ajout pur, sans impact sur les labs non migrés) | ✅ fait |
| `CommonUtilities` → `MongoDB.Driver 2.30.0` | tous (warnings NU1902/NU1903), code : Lab12 (`MongoDbHealthCheck`) | Monter en `3.12.0` **et** retirer en même temps la référence directe `MongoDB.Driver 2.30.0` de Lab12 (sinon `NU1605`). Option à trancher : sortir `MongoDbHealthCheck` dans un projet `CommonUtilities.MongoDb` pour que les labs sans Mongo n'héritent plus de cette dépendance. **Lab05 ✅ découplé** : il référence directement `MongoDB.Driver` 3.12.0 et n'utilise pas `MongoDbHealthCheck` (cf. [rapport Lab05](Lab05-Migration-Report.md) §6). **Lab07 ✅ sans MongoDB** (vector store en mémoire) | Vague 4 (avec Lab12) |
| `ConfigurationHelper.cs` / `AzureOpenAISettings.cs` (copiés dans chaque lab) | tous | Reprendre la version Lab01 (ConfigurationBuilder + user-secrets + validation) ; conserver les propriétés propres à chaque lab | chaque lab |
| `.csproj` | tous | Même socle que Lab01 + packages spécifiques justifiés | chaque lab |
| (optionnel) `Directory.Packages.props` à la racine | tous | Centraliser les versions (comme le dépôt officiel) une fois tous les labs migrés, pour éviter les écarts Start/Solution observés aujourd'hui | après la vague 5 |

## 3. Dépendances entre exercices

- **Aucune référence de projet entre labs** : chaque lab ne dépend que de `CommonUtilities`.
- **Dépendances d'exécution** :
  - `Lab06_A2AClient` appelle `Lab06_A2AServer` (`http://localhost:5000/a2a/...`) → migrer et tester **ensemble**.
  - `Lab04` consomme un serveur MCP externe (Hugging Face) ; `Lab10` expose un agent comme serveur MCP → même version `ModelContextProtocol 2.2.0`, à migrer dans la même vague.
- **Dépendances pédagogiques** : Lab05 (sessions) est prérequis de Lab09 (approbations, session) et Lab12 (context providers) ; Lab03 (function tools) est prérequis de Lab09, Lab10 et MAS-Lab01.

## 4. Ordre de migration

| Vague | Labs | Thème commun | Nature |
|---|---|---|---|
| **Pilote** | Lab01 | client v1, `AsAIAgent`, `AgentResponse`, streaming, config | ✅ terminé |
| 1 | Lab02 ✅, Lab03 ✅, Lab08 ✅, Lab11 | changements mécaniques (client, renommages) + Structured Output (Lab02) + middleware d'outils (Lab03) | adaptation |
| 2 | Lab04 ✅, Lab10 | MCP 0.5.0-preview → **2.2.0 stable** | adaptation significative |
| 3 | Phase 0 CommonUtilities (reportée à Lab12), Lab05 ✅, Lab07 ✅ | `AgentSession`, `ChatHistoryProvider`, vector stores (remplacement SK connectors), `TextSearchProvider` | **refonte conceptuelle** |
| 4 | Lab09 ✅, Lab12 | approbations (sans `UserInputRequests`), `AIContextProvider` 1.x | **refonte conceptuelle** |
| 5 | Lab06 ✅ (client + serveur), MAS-Lab01, MAS-Lab02, MAS-Lab03 | A2A v1 (préversion), agent-as-tool, Workflows 1.22 | adaptation significative |

## 5. Checklist par exercice

Commune à tous (cocher dans la PR du lab) :

- [ ] `.csproj` Start = Solution ; MAF 1.22.0 ; aucune préversion hors exception documentée ; `UserSecretsId` ; plus de `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting` (si console)
- [ ] `ConfigurationHelper.cs`, `AzureOpenAISettings.cs`, `appsettings.json` identiques Start/Solution, sans secret
- [ ] Client : `OpenAIClient` + `AzureOpenAIEndpoint.ToV1Uri` + clé API **ou** `BearerTokenPolicy`
- [ ] `AsAIAgent`, `AIAgent`, `AgentResponse`, `AgentSession` (si applicable)
- [ ] TODO numérotés et indices alignés sur le README ; Start compile et s'exécute
- [ ] README : prérequis .NET 10, configuration user-secrets, tableau des TODO, concepts clés, liens officiels (`microsoft/agent-framework`)
- [ ] grep des anciens noms = 0
- [ ] Tests §7 du manuel exécutés et consignés

Spécifique :

| Lab | Changements attendus | Point d'attention | Refonte ? |
|---|---|---|---|
| **Lab02** Structured Output | client ; `RunAsync<T>` / options SO (rc1 #3761) | vérifier `AgentResponse<T>` et `ChatResponseFormat` avec `Agent_Step02_StructuredOutput` | non |
| **Lab03** Function Tools | client ; `AIFunctionFactory` inchangé (MEAI) ; supprimer `ModelContextProtocol` et `Microsoft.Extensions.DependencyInjection` de la Solution s'ils ne servent pas (absents du Start) | écart Start/Solution actuel — ✅ fait : `ModelContextProtocol` supprimé (inutilisé) ; `Microsoft.Extensions.DependencyInjection` **conservé** (scénario 3) et ajouté aussi au Start ; `CompanyTools.cs` aligné | non |
| **Lab04** MCP Client | `ModelContextProtocol` 0.5.0-preview → 2.2.0 ; `McpClient`/`HttpClientTransport` | API MCP 1.x/2.x ; sample `02-agents/ModelContextProtocol/*` — ✅ fait : API client MCP inchangée ; `clientFactory` + `ConfigureOptionsChatClient` → `ChatClientAgentOptions.ChatOptions` ; token Hugging Face facultatif (accès anonyme) et en user-secrets ; 2 scénarios ajoutés (découverte des outils, sélection des outils) | partielle |
| **Lab05** Threads | `AgentThread`→`AgentSession`, `CreateSessionAsync`, sérialisation via agent, `ChatMessageStore`→`ChatHistoryProvider`, remplacement SK connectors, MongoDB 3.x | samples `Agent_Step03_PersistedConversations`, `Agent_Step04_3rdPartyChatHistoryStorage` ; renommer le lab (« Sessions ») — ✅ fait : 3 scénarios (historique par défaut dans la session, vector store `CommunityToolkit.VectorData.InMemory`, MongoDB via `MongoDB.Driver` 3.12.0) ; titre « Sessions », dossier et projet inchangés (décision à valider) | **oui** |
| **Lab06** A2A | A2A SDK v1 (`1.0.0-preview2`), hosting A2A remanié ; `Microsoft.Agents.Hosting.AspNetCore` 1.4.9-beta à supprimer si inutile | pas de version stable → exception ; guide Learn « A2A SDK v1 Migration Guide » — ✅ fait : client + serveur migrés ensemble (v0.3 et v1 incompatibles) ; serveur `AddA2AServer`/`MapA2AJsonRpc`/`MapA2AHttpJson`/`MapWellKnownAgentCard` ; client 3 scénarios (découverte, URL directe, agent distant comme outil `AsAIFunction`) ; contournement streaming temporaire côté serveur ; dashboard : mécanisme « companion » | partielle |
| **Lab07** Agentic RAG | vector store : SK connectors (preview) → `Microsoft.Extensions.VectorData` 10.10 + connecteur stable ; `TextSearchProvider` (namespace `Microsoft.Agents.AI`) | pas de connecteur MongoDB stable identifié → **`InMemoryVectorStore`** retenu (samples RAG officiels) — ✅ fait : 4 scénarios (remplissage du store, recherche sémantique, RAG agentique par function tool, RAG par `TextSearchProvider` avec `RecentMessageMemoryLimit`) ; MongoDB Atlas et `MongoDB.Driver` retirés ; `EmbeddingDeploymentName` validé au démarrage ; ⚠️ exécution réelle à refaire quand les identifiants Azure de l'environnement seront rétablis | **oui** |
| **Lab08** Data formats | client ; les deux projets utilisaient des packages tiers différents (`ToonNet` vs `ToonNetSerializer`, ce dernier introuvable sur NuGet) | aligner Start/Solution — ✅ fait : packages tiers supprimés, comparaison **JSON** (objets + `RunAsync<List<Hotel>>`) vs **CSV** (helper fourni) sur la même question, mesure du résultat d'outil reçu par le modèle et tableau de comparaison ; ⚠️ exécution réelle à refaire quand les identifiants Azure seront rétablis (cf. §4.14 du manuel) | partielle |
| **Lab09** Human approval | `ApprovalRequiredAIFunction` ; suppression de `UserInputRequests` ; binding des approbations (1.14, 1.22 `[BREAKING]`) ; `CreateSessionAsync` | sample `Agent_Step01_UsingFunctionToolsWithApprovals` — ✅ fait : `ToolApprovalRequestContent` lus dans `response.Messages`, boucle `while`, `CreateResponse(approved, reason)`, session obligatoire (liaison des approbations, vérifiée : sans session l'approbation est ignorée) ; 2 scénarios (approbation humaine à la console ; plusieurs demandes décidées par une politique avec motif, outil non sensible exécuté sans demande) ; premier lab **interactif** du dashboard (cf. §4.15 du manuel) | **oui** |
| **Lab10** Agent as MCP tool | `AsAIFunction` ; `ModelContextProtocol.AspNetCore` 2.2.0 ; Start sans le package MCP ≠ Solution | sample `Agent_Step07_AsMcpTool` | partielle |
| **Lab11** Custom HTTP transport | `AzureOpenAIClientOptions.Transport` → `OpenAIClientOptions.Transport` (`HttpClientPipelineTransport`, System.ClientModel) | concept inchangé | non |
| **Lab12** AIContextProvider | nouvelle API provider (agent + session fournis, StateBag, `AIContextProviders` multiples) | sample `Agent_Step17_AdditionalAIContext` | **oui** |
| **MAS-Lab01** Agent as tool | `AsAIFunction` ; Start en preview.251002 ≠ Solution | sample `Agent_Step09_AsFunctionTool` | non |
| **MAS-Lab02/03** Orchestrations | `Microsoft.Agents.AI.Workflows` 1.22.0 ; `AgentWorkflowBuilder`, `InProcessExecution`, événements unifiés `WorkflowOutputEvent`, renommages rc1 | samples `03-workflows/Orchestration`, `Concurrent` | partielle |

## 5 bis. Pratiques retenues à appliquer lors de la migration

Chacune est démontrée par un sample officiel 1.22.0.

| Lab | Pratique | Référence officielle | Statut |
|---|---|---|---|
| Lab01 | `updates.ToAgentResponse().Usage` après un streaming ; `Usage.ReasoningTokenCount` | `AgentResponseExtensions.ToAgentResponse`, `Agent_Step02_StructuredOutput` | ✅ appliqué |
| Lab02 | `RunAsync<T>` / `AgentResponse<T>.Result` comme approche principale (`ForJsonSchema` en scénario « sous le capot ») | `Agent_Step02_StructuredOutput` | ✅ appliqué (cf. [Lab02-Migration-Report.md](Lab02-Migration-Report.md)) |
| Lab03 | Middleware de traçage des appels d'outils `.AsBuilder().Use(FunctionCallMiddleware)` (scénario 4) ; `tools` + `services` sur `AsAIAgent` | `Agent_Step11_Middleware`, `Agent_Step12_Plugins` | ✅ appliqué (cf. [Lab03-Migration-Report.md](Lab03-Migration-Report.md)) |
| Lab04 | `McpClient.CreateAsync` + `HttpClientTransport` ; `[.. mcpTools.Cast<AITool>()]` ; `ChatClientAgentOptions { ChatOptions = … }` ; filtrage des outils MCP (équivalent C# de `allowed_tools`) | `Agent_MCP_Server`, `Agent_Step09_UsingMcpClientAsTools`, `04_memory`, page Learn « Using MCP tools » | ✅ appliqué (cf. [Lab04-Migration-Report.md](Lab04-Migration-Report.md)) |
| Lab05 | `CreateSessionAsync` / `SerializeSessionAsync` / `DeserializeSessionAsync` ; `InMemoryChatHistoryProvider.GetMessages(session)` ; réducteurs d'historique à étudier (`MEAI001`) | `Agent_Step03_PersistedConversations`, `Agent_Step04_3rdPartyChatHistoryStorage`, `AgentWithMemory_Step05_BoundedChatHistory` | ✅ appliqué (réducteurs : `MEAI001` confirmé → « Going further » seulement ; cf. [Lab05-Migration-Report.md](Lab05-Migration-Report.md)) |
| Lab07 | `TextSearchProvider` + `ChatClientAgentOptions.AIContextProviders` (scénario 4), `RecentMessageMemoryLimit`, filtre `StorageInputRequestMessageFilter` ; `InMemoryVectorStore` avec `EmbeddingGenerator` ; affichage des appels d'outil depuis `response.Messages` | `AgentWithRAG_Step01_BasicTextRAG`, `AgentWithRAG_Step02_CustomVectorStoreRAG` | ✅ appliqué (cf. [Lab07-Migration-Report.md](Lab07-Migration-Report.md)) |
| Lab08 | Mesure du résultat d'outil tel que le modèle le reçoit (`FunctionResultContent.Result`, sérialisé comme `OpenAIChatClient`) ; `RunAsync<T>` avec outils ; format compact demandé par instructions et relu défensivement (`FormatException`) | `Agent_Step02_StructuredOutput`, `01-get-started/02_add_tools`, sources MEAI `AIFunctionFactory` / `OpenAIChatClient` | ✅ appliqué (cf. [Lab08-Migration-Report.md](Lab08-Migration-Report.md)) |
| Lab09 | `ApprovalRequiredAIFunction` + boucle `ToolApprovalRequestContent` → `CreateResponse(bool)` | `Agent_Step01_UsingFunctionToolsWithApprovals` | ✅ appliqué (cf. [Lab09-Migration-Report.md](Lab09-Migration-Report.md)) ; `CreateResponse(approved, reason)` et résultats d'outils lus dans `response.Messages` (`FunctionResultContent`) en plus |
| MAS-Lab01 | `AsAIFunction(new AIFunctionFactoryOptions { Name = ... })` | `Agent_Step09_AsFunctionTool` | à faire (vague 5) ; `AsAIFunction()` déjà utilisé (agent A2A distant) par Lab06_A2AClient scénario 3 |
| Lab06 | Agent A2A distant comme outil (`AsAIFunction`) ; découverte par carte (`GetAgentCardAsync` + `AsAIAgent`) ; serveur avec les deux liaisons | `A2AAgent_AsFunctionTool`, `Agent_With_A2A`, `A2AClientServer` | ✅ appliqué (cf. [Lab06-Migration-Report.md](Lab06-Migration-Report.md)) |
| tous | Central Package Management | `dotnet/Directory.Packages.props` officiel | après la vague 5 |
| à décider | Observabilité `UseOpenTelemetry` (aucun lab ne la couvre) | `Agent_Step05_Observability` | à étudier |

## 6. Critères de validation

Un lab est migré lorsque :

1. `dotnet restore` + `dotnet build` Start et Solution : 0 erreur, 0 warning C# (les NU19xx hérités de CommonUtilities tolérés jusqu'à la phase 0, puis 0).
2. Solution exécutée avec succès contre Azure OpenAI (clé API ; Entra ID si RBAC disponible).
3. Start livré compile et s'exécute ; Start complété selon le README reproduit la Solution.
4. Aucune API supprimée ou renommée restante (grep §5 du manuel).
5. README, commentaires et Solution cohérents entre eux et avec les samples officiels 1.22.0.
6. Rapport de migration du lab rédigé (même format que Lab01).
