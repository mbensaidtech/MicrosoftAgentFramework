# Rapport d'évolution — introduction de la Responses API (Lab04, Lab05, Lab07)

> Date : 2026-11-07 · Cible : **Microsoft Agent Framework 1.27.0**, OpenAI .NET **2.14.0**, Microsoft.Extensions.AI **10.10.1**, ModelContextProtocol **2.2.0**, .NET 10
> Sources : documentation Agent Framework (*Azure OpenAI provider*, *local / hosted MCP tools*, *Storage*), samples officiels au tag `dotnet-1.27.0` (`ResponseAgent_Hosted_MCP`, `Agent_With_AzureOpenAIResponses`, `Agent_Step04_3rdPartyChatHistoryStorage`, `Agent_OpenAI_Step05_Conversation`, `foundry/Agent_Step16_FileSearch`), sources `ChatClientAgent.cs` (MAF) et `OpenAIResponsesChatClient.cs` (M.E.AI 10.10.1), documentation Azure OpenAI *Responses API* et OpenAI (*migrate to Responses*, *conversation state*, *file search*), XML docs des paquets installés, et **sondes exécutées contre la ressource Azure OpenAI de test** (`gpt-4o-mini`, endpoint v1).

**Statut : fait.** Lab05 est renommé `Lab05-AIAgentWithSessions`. Six scénarios Responses ajoutés (Lab04 : 4, 5 ; Lab05 : 4, 5 ; Lab07 : 5, 6), en Start (TODO) et Solution, documentés, intégrés au Dashboard, exécutés de bout en bout contre Azure OpenAI.

---

## 1. Analyse de l'existant

### 1.1 Structure

Chaque Lab = `README.md` + `Start/` (TODO) + `Solution/` + `exercise-fingerprints.json` ; fichiers fournis identiques entre Start et Solution (`LabPrompts.cs`, helpers d'affichage, stores) ; `Program.cs` découpé en `#region Scenario N: …` lus par `ScenarioSelection` (CLI) et par le Dashboard (`labs.json` : expectations par scénario, traductions FR, tests `OutputAnalyzerTests` sur des sorties réelles). Les Labs sont **autonomes** : pas de projet partagé hors `CommonUtilities` (console, spinner, endpoint v1, sélection des scénarios), sans dépendance de paquet.

### 1.2 Les trois Labs avant évolution

| Lab | Scénarios | Objectif pédagogique | Abstractions |
|---|---|---|---|
| Lab04 MCP Client | 1 découverte des outils · 2 agent + outils MCP + sortie structurée · 3 seulement l'outil utile | Un `McpClientTool` **est** une `AIFunction` ; coût des définitions d'outils | `McpClient`, `HttpClientTransport`, `ChatClientAgentOptions`, `RunAsync<T>`, `FunctionCallContent` |
| Lab05 Threads | 1 historique en mémoire dans la session · 2 réducteur · 3 `ChatHistoryProvider` sur MongoDB/JSON | Où vit l'historique, sérialisation/restauration d'`AgentSession` | `AgentSession`, `InMemoryChatHistoryProvider`, `ProviderSessionState<T>`, `MessageCountingChatReducer` |
| Lab07 Agentic RAG | 1 indexation · 2 recherche sémantique · 3 RAG agentique (outil) · 4 `TextSearchProvider` | Embeddings, vector store, RAG outil vs context provider | `InMemoryVectorStore`, `VectorStoreCollection`, `AIFunctionFactory`, `TextSearchProvider` |

**Intégration Chat Completions** : `OpenAIClient` sur `…/openai/v1/` (clé API ou Entra ID), `client.GetChatClient(deployment).AsAIAgent(...)`. La Responses API n'apparaissait que dans **Lab01 scénario 6** (`GetResponsesClient().AsAIAgent(model: …)`, chaînage par `ResponseId`) ; le README de Lab01 annonçait explicitement que les Labs suivants restaient sur Chat Completions, et Lab05 décrivait en prose le cas Responses.

### 1.3 Capacités réellement disponibles dans les versions installées (vérifiées par réflexion et à l'exécution)

| Capacité | API | Statut | Résultat sur Azure OpenAI v1 |
|---|---|---|---|
| Agent sur Responses | `ResponsesClient.AsAIAgent(options, model)` | `OPENAI001` | ✅ |
| État côté service | `store` (défaut `true`), `ChatClientAgentSession.ConversationId` = `resp_…` (change à chaque tour) | — | ✅ la session sérialisée ne contient que `conversationId` |
| Preuve du stockage | `GetResponseAsync`, `DeleteResponseAsync` | `OPENAI001` | ✅ trouvé / 404 après suppression ; chaîner une réponse supprimée → `400 previous_response_not_found` |
| Responses sans stockage | `AsIChatClientWithStoredOutputDisabled(model)` | `MAAI001` | ✅ id `resp_` renvoyé mais `GET` → 404 |
| Provider + stockage service | — | — | ❌ `Only ConversationId or ChatHistoryProvider may be used, but not both` (comportement documenté de MAF) |
| Conversations API | `ConversationClient`, `ChatClientAgent.CreateSessionAsync(conversationId)` | `OPENAI001` | ❌ **404** sur `POST /openai/v1/conversations` (même constat rapporté par d'autres utilisateurs) |
| Hosted MCP | `HostedMcpServerTool` (`AllowedTools`, `ApprovalMode`, `Headers`) | stable | ✅ le service liste (`mcp_list_tools`) et appelle (`McpServerToolCallContent`) ; flux d'approbation `ToolApprovalRequestContent` vérifié |
| File search hébergé | `OpenAIFileClient`, `VectorStoreClient`, `HostedFileSearchTool`, `HostedVectorStoreContent`, `CitationAnnotation` | `OPENAI001` (clients) | ✅ upload, indexation (~1–2 s), recherche, citations, `include file_search_call.results`, suppression |
| Outil hébergé sur Chat Completions | — | — | ⚠️ **ignoré silencieusement** : pas d'erreur, le modèle répond de mémoire (vérifié pour MCP et file search) |

### 1.4 Différences de comportement à enseigner (et non « Responses remplace Chat Completions »)

- Ce qui **ne change pas** : `AIAgent`, `RunAsync<T>`, outils de fonction, outils MCP issus de **notre** `McpClient`, `AgentSession` et `ChatHistoryProvider` tant que l'historique reste côté application.
- Ce qui **change** : items typés au lieu de messages ; état côté service optionnel (et par défaut !) ; outils hébergés exécutés par le service ; `previous_response_id` n'est pas moins cher (l'historique est lu et facturé) ; ids `resp_` éphémères (30 jours) ; dépendance au fournisseur.
- Ce qui **n'est pas exclusif** à Responses : MCP, conversation persistante, RAG — tous réalisables avec Chat Completions par l'application. Responses ajoute la **version hébergée** (orchestrée par le fournisseur).

---

## 2. Organisation des Labs : comparaison des trois options

| Critère | Option 1 — scénarios Responses dans les Labs existants | Option 2 — groupe de Labs Responses distinct | Option 3 — groupe distinct + composants mutualisés |
|---|---|---|---|
| Cohérence pédagogique | ✅ chaque thème (MCP, sessions, RAG) est traité une fois, avec ses deux architectures côte à côte | ⚠️ thème coupé en deux parcours ; la comparaison est laissée à l'apprenant | ⚠️ idem option 2 |
| Compréhension | ✅ même serveur MCP, même conversation, même FAQ, mêmes questions : seule l'architecture change, les tokens sont comparables dans une seule exécution | ⚠️ il faut refaire tout le Lab (connexion MCP, stores, vector store) pour arriver aux 20 % qui diffèrent | ⚠️ idem, plus une dépendance externe à comprendre |
| Maintenabilité | ✅ 3 Labs, 3 fiches catalogue | ❌ 6 Labs, 6 fiches, 12 projets à faire évoluer ensemble | ⚠️ 6 Labs + bibliothèque partagée à versionner |
| Duplication de code | ✅ nulle (stores, outils, helpers réutilisés tels quels) | ❌ ~80 % dupliqué (setup, MCP client, `PersistentChatHistoryProvider`, stores Mongo/JSON, `FaqRecord`, data, helpers) | ⚠️ réduite, mais la structure Start/Solution autonome des Labs impose de toute façon des copies |
| Mise à jour des dépendances | ✅ même `.csproj` : les deux APIs viennent des **mêmes paquets** (`Microsoft.Agents.AI.OpenAI` → `OpenAI`) | ❌ deux fois plus de projets à monter de version | ⚠️ idem + projet partagé |
| Comparaison des deux APIs | ✅ native (`--scenarios 3,4,5`) | ❌ deux exécutions de deux projets | ❌ idem |
| Évolution indépendante | ⚠️ les deux parcours vivent dans le même `Program.cs` ; atténué : les scénarios sont des régions indépendantes, sélectionnables, avec checks propres | ✅ | ✅ |
| Risque « copier-coller `GetChatClient` → `GetResponsesClient` » | ✅ évité : seuls les écarts réels sont scénarisés | ❌ structurel : un Lab Responses refait d'abord tout ce qui ne change pas | ❌ idem |

**Recommandation retenue : Option 1, structurée.** Chaque Lab garde son thème et se termine par une **partie B « Responses API »** construite toujours de la même façon :

1. *ce qui ne change pas* — notre composant (client MCP, store de conversation, vector store) sur Responses ;
2. *ce que Responses ajoute* — la version orchestrée par le fournisseur (MCP hébergé, état côté service, file search hébergé), avec preuve observable (qui a exécuté l'appel, `GET /responses/{id}`, citations) et comparaison chiffrée.

Les conditions de durabilité qui rendent cette option préférable aux deux autres : (a) les deux APIs partagent les mêmes paquets et abstractions MAF, donc aucune dépendance à séparer ; (b) le catalogue sait déjà sélectionner et vérifier les scénarios individuellement ; (c) un guide transverse ([ChatCompletions-vs-Responses.md](../LearningLabs/AzureOpenAI/ChatCompletions-vs-Responses.md)) donne la vue d'ensemble que fournirait un groupe séparé. **Critère de réexamen** : si un jour les Labs devaient basculer *par défaut* sur Responses (par exemple dépréciation de Chat Completions), la bonne évolution serait d'inverser la base des Labs (Responses en partie A), pas de créer un second groupe.

---

## 3. Changements réalisés

### 3.1 Renommage Lab05

| Avant | Après |
|---|---|
| `Lab05-AIAgentWithThreads/` | `Lab05-AIAgentWithSessions/` (`git mv`, historique conservé) |
| `AIAgentWithThreads.csproj` (Start, Solution) | `AIAgentWithSessions.csproj` |
| namespace `AIAgentWithThreads` (15 fichiers × 2) | `AIAgentWithSessions` |
| `labs.json` (`path`, `startProject`, `solutionProject`), `Dashboard/README.md` | mis à jour ; l'id `azureopenai-lab05` est **inchangé** (historique des exécutions et progression préservés) |

Inchangés volontairement : base `AIAgentSessions`, conteneur `lab05-mongodb`, `chat_history.json`, user secrets (aucune donnée d'apprenant perdue) ; mentions historiques dans `Migration/*` et dans les tableaux « Coming from an older version » (`AgentThread` → `AgentSession`).

### 3.2 Nouveaux scénarios

| Lab | Scénario | Contenu | Ce qu'il démontre |
|---|---|---|---|
| Lab04 | 4 — *Responses API - the same MCP client, the tools still run in your application* | Mêmes `McpClientTool`, agent sur `GetResponsesClient()`, sortie structurée | Le MCP orchestré par l'application fonctionne avec les deux APIs ; `FunctionCallContent` = exécuté par l'app |
| Lab04 | 5 — *Hosted MCP - the service connects to the MCP server and runs its tools* | `HostedMcpServerTool` (`AllowedTools`, `NeverRequire`, `Headers`), aucun `McpClient` | Le service découvre et appelle ; `McpServerToolCallContent` ; contraintes (serveur public, jeton transmis au fournisseur, approbations) |
| Lab05 | 4 — *Responses API - the service keeps the conversation* | Session = `resp_…`, sérialisation, restauration, chaînage, `GET` (stockée), usage, `DeleteResponseAsync` | État côté fournisseur, id qui change à chaque tour, coût non réduit, suppression = responsabilité côté fournisseur |
| Lab05 | 5 — *Responses API without storage - the conversation stays in your application* | `AsIChatClientWithStoredOutputDisabled` + **même** `PersistentChatHistoryProvider`, id métier `support-case-1042`, redémarrage simulé, `GET` → 404 | Maîtrise de la persistance dans notre infrastructure avec Responses ; distinction id métier / session MAF / clé d'historique / ids OpenAI |
| Lab07 | 5 — *Responses API with your own vector store - nothing stored by the service* | Même outil `search_faq` sur notre `InMemoryVectorStore`, `store = false`, `GET` → 404 | Responses ≠ file search hébergé ; les passages récupérés ne restent pas chez le fournisseur |
| Lab07 | 6 — *Hosted file search - the service stores, chunks, embeds and searches the FAQ* | Upload d'un document par entrée, vector store du service (expiration 1 jour), `HostedFileSearchTool`, `include file_search_call.results`, citations, nettoyage en `finally` | RAG du fournisseur : contrôle, flexibilité, persistance, dépendance, coût ; découpage = seul levier conservé |

Conversations API (`conv_…`) : **documentée** dans Lab05 (code natif MAF `CreateSessionAsync(conversationId)`) mais **non scénarisée**, car indisponible sur Azure OpenAI (404 vérifié) — l'exécuter seulement contre OpenAI aurait introduit un second fournisseur dans un parcours Azure.

### 3.3 Fichiers

| Fichier | Changement |
|---|---|
| `Lab04/*/Program.cs`, `AgentConsole.cs` (`WriteToolCalls` : qui a exécuté chaque appel), `LabPrompts.cs` (commentaires) | Scénarios 4–5, TODO 19–27 |
| `Lab05/*/Program.cs`, `SessionConsole.cs` (`WriteServiceStorageAsync`, `WriteIdentifiers`), `LabPrompts.cs` (`BusinessIds.SupportCase`) | Scénarios 4–5, TODO 23–34 |
| `Lab07/*/Program.cs`, `RagConsole.cs` (`WriteServiceVectorStore`, `WriteFileSearch`), `FaqData.cs` (`ToDocument`), `HostedFileSearch.cs` (nouveau : `WaitUntilIndexedAsync`), `LabPrompts.cs` (`Agents.HostedFaq`) | Scénarios 5–6, TODO 20–30 ; alias `ServiceVectorStores` (collision `VectorStore` MEVD / OpenAI) |
| `exercise-fingerprints.json` (Lab04, 05, 07) | Régénérés (`dotnet run -- exercise-fingerprints`) ; les 16 autres inchangés |
| READMEs Lab04, Lab05, Lab07 | Objectifs, TODO, comparatifs (*Local MCP or hosted MCP?*, *Where does the conversation live?*, *Four identifiers*, *The Conversations API*, *Your RAG or the RAG of the provider?*), sorties attendues réelles, dépannage (messages d'erreur vérifiés), liens |
| `LearningLabs/AzureOpenAI/ChatCompletions-vs-Responses.md` | Nouveau guide transverse |
| `README.md` racine, `Lab01/README.md` | Tableaux des Labs 04/05/07, parcours, renvoi vers le guide |
| `Dashboard/LabDashboard/labs.json` | Résumés, objectifs, 29 nouveaux checks + traductions FR, `timeoutSeconds` 180 → 300 (Labs 04, 05, 07) |
| `Dashboard/LabDashboard.Tests/OutputAnalyzerTests.cs` | Sorties réelles des nouveaux scénarios ajoutées aux fixtures ; tests de libellés/totaux de tokens mis à jour |
| `CommonUtilities/ConsoleSpinner.cs` | Correctif : `PadRight(Console.WindowWidth - 1)` levait `ArgumentOutOfRangeException` quand la sortie est redirigée (largeur 0) ; même garde que `ColoredConsole.WriteDividerLine` |

Les checks du Dashboard respectent la règle du dépôt (*les checks jugent ce que fait le scénario, pas les textes écrits par `Program.cs`*) : ils reposent sur les ids `resp_`/`vs_`, le JSON des sessions et les sorties des helpers fournis ; les messages de suppression (Lab05 sc. 4, Lab07 sc. 6), écrits par l'apprenant, ne sont donc pas vérifiés.

---

## 4. Validation

| # | Vérification | Résultat |
|---|---|---|
| 1 | `dotnet build` des **39 projets** des Labs (Start + Solution + `CommonUtilities`), après le correctif du spinner | ✅ 0 erreur, **0 warning** |
| 2 | Tests du Dashboard (`dotnet test LabDashboard.Tests`) | ✅ **672/672** (dont catalogue, traductions FR de chaque check, checks sur les sorties réelles des Solutions, checks « sans présentation », libellés et totaux de tokens) |
| 3 | `exercise-fingerprints` | ✅ régénérés : seuls Lab04, Lab05, Lab07 changent ; Start = aucun scénario implémenté, Solution = tous |
| 4 | Exécution complète des Solutions contre Azure OpenAI (`gpt-4o-mini`, v1) | ✅ Lab04 scénarios 1–5, Lab05 scénarios 1–5 (`Storage:Mode = JsonFile`), Lab07 scénarios 1–6 : code de sortie 0 ; **aucune régression** des scénarios Chat Completions (mêmes réponses et ordres de grandeur de tokens qu'avant) |
| 5 | Exécution des Start tels que livrés | ✅ code 0, stderr vide (aucun scénario exécuté, comme attendu) |
| 6 | Lab01 scénario 6 (Responses) | ✅ inchangé et fonctionnel (il échouait hors terminal à cause du spinner avant le correctif) |
| 7 | Références à l'ancien nom | ✅ plus aucune hors documents historiques ; liens relatifs et ancres des fichiers modifiés vérifiés |
| 8 | Affirmations de la documentation | ✅ vérifiées par exécution : outils hébergés ignorés par Chat Completions, 404 Conversations API, 404 sur réponse non stockée, `400 previous_response_not_found`, exception provider + `store`, flux d'approbation MCP hébergé |

Mesures (une exécution, même question ; à titre indicatif) :

| Lab | Chat Completions | Responses, composant de l'application | Responses, version hébergée |
|---|---|---|---|
| Lab04 (outil `hub_repo_search` seul) | sc. 3 : 1 900 tokens en entrée | sc. 4 : 1 847 | sc. 5 (MCP hébergé) : 1 344 |
| Lab05 (question de suivi) | sc. 1 : ~100 | sc. 5 (`store = false`) : 76 | sc. 4 (`previous_response_id`) : 76 — pas moins cher |
| Lab07 (`BrokenItem`) | sc. 3 : 572 | sc. 5 : 566 | sc. 6 (file search) : ~1 680 |

---

## 4 bis. Scénarios Responses en bonus (2026-11-07)

Les scénarios Responses (Lab04 : 4, 5 ; Lab05 : 4, 5 ; Lab07 : 5, 6) sont **facultatifs**.

| Élément | Règle / changement |
|---|---|
| Source de vérité | `labs.json` : `"bonusScenarios": [...]` (publié à l'admin) ; marqueur `// Bonus scenario` dans les deux `Program.cs` (CLI : `[bonus]` dans `--list-scenarios`) ; cohérence vérifiée par `BonusScenariosTests` ; le catalogue refuse un bonus inconnu ou un Lab 100 % bonus |
| Terminé | Tous les scénarios **non bonus** validés (`ExerciseProgress.CompletingRun(runs, bonus)`) |
| Terminé+ (Done+) | Tous les scénarios, bonus compris, validés (`BonusCompletingRun`) : `progress = "completedPlus"` (liste des Labs), `bonusCompleted` (parcours, +50 XP, pastille « + », toast) |
| Échec d'un bonus | `PassedExercise` : un essai dont les seuls échecs sont des scénarios bonus réussit l'exercice → jamais « À reprendre », ni perte de « premier essai » |
| Sélection par défaut | Un scénario bonus n'est coché d'office qu'une fois écrit |
| Admin (rétrocompatible) | `catalog.synced` et `PUT /api/catalog` : `bonusScenarios` ; `progress.snapshot` : `bonusCompletedAt` (l'état reste `completed`) ; `run.finished` : `bonus: true` par scénario bonus. L'admin actuel ignore ces champs sans erreur ; son évolution fait l'objet de [Admin-Dashboard-Bonus-Prompt.md](Admin-Dashboard-Bonus-Prompt.md) |
| Validation | 682/682 tests du Dashboard (dont 10 nouveaux), API et rendu vérifiés sur un Dashboard local sans reporting, `publish-lab-catalog.sh --dry-run` valide |

## 5. Limites et points ouverts

- **Conversations API** indisponible sur Azure OpenAI au 2026-11-07 : à réévaluer ; si elle arrive, un scénario 6 de Lab05 (`CreateSessionAsync(conv.Id)`) suffit.
- **API expérimentales** : `OPENAI001` (clients Responses, vector stores, items OpenAI) et `MAAI001` (`AsIChatClientWithStoredOutputDisabled`) — `#pragma` locaux, à retirer quand les attributs disparaîtront (à suivre à chaque montée de version, manuel §8).
- **Coût / ressources** : Lab07 sc. 6 crée 20 fichiers et un vector store, supprimés en `finally` (expiration d'un jour en filet de sécurité ; les fichiers, eux, n'expirent pas — procédure de nettoyage dans le dépannage).
- **Variabilité** : sorties du modèle et du Hub Hugging Face ; les checks visent des faits stables (faq-005, Paris/Ada, ids, outil appelé).
- **Non testé ici** : scénarios 3 et 5 de Lab05 en mode `MongoDb` (Docker non démarré pendant la session) et `EmbeddedMongoDb` ; testés en `JsonFile`. Le code de stockage est inchangé et partagé avec le scénario 3, testé dans les mêmes conditions.
