# Rapport de migration — Lab07-AgenticRAG-VectorStore

> Date : 2026-09-29 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.13 ajouté, §1, §5, §8), [Migration-Plan.md](Migration-Plan.md), rapports [Lab01](Lab01-Migration-Report.md) à [Lab06](Lab06-Migration-Report.md). Sources officielles (tag `dotnet-1.22.0`) : samples `02-agents/AgentWithRAG/AgentWithRAG_Step01_BasicTextRAG`, `AgentWithRAG_Step02_CustomVectorStoreRAG`, `AgentWithRAG_Step03_CustomRAGDataSource` ; sources `Microsoft.Agents.AI/TextSearchProvider.cs` et `TextSearchProviderOptions.cs` ; `PublicAPI/net10.0/PublicAPI.Shipped.txt` de `Microsoft.Agents.AI` et `Microsoft.Agents.AI.Abstractions` ; `dotnet/Directory.Packages.props` (versions `CommunityToolkit.VectorData.InMemory` 1.0.1, `Microsoft.Extensions.VectorData.Abstractions` 10.10.0) ; source MEVD `VectorPropertyModel.cs` (dotnet/extensions) ; documentation XML des paquets `CommunityToolkit.VectorData.InMemory` 1.0.1, `Microsoft.Extensions.AI.OpenAI` 10.10.0, `OpenAI` 2.13.0.

**Statut : code, README et dashboard migrés ; validation d'exécution BLOQUÉE.** Tout ce qui ne dépend pas d'un appel à Azure OpenAI est vérifié (restauration, graphe, build sans warning, grep, robustesse de la configuration, Start livré, Start complété compilé, diff Start/Solution, tests du dashboard, runs du dashboard). **Les tests 6, 7 et 10 (exécution réelle de la Solution et du Start complété) n'ont pas pu être menés** : les identifiants Azure OpenAI de l'environnement de migration sont invalides depuis le 2026-09-29 — clé API refusée (`401`) et Entra ID refusé (`400 SubscriptionNotRegistered`), pour Lab07 **comme pour Lab01** (§6.1). Le lab ne peut être déclaré « migré » au sens du manuel (§7) qu'après ces trois tests, à relancer dès que les identifiants sont rétablis (§7).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AgenticRAG.csproj` | Socle Lab01–06 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé ; **ajout** de `CommunityToolkit.VectorData.InMemory` **1.0.1** (commenté). Supprimés : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251204.1`, `Microsoft.Extensions.Hosting 9.0.0`, `Microsoft.SemanticKernel.Connectors.InMemory 1.67.1-preview`, `…Connectors.MongoDB 1.66.0-preview`, `Microsoft.Extensions.VectorData.Abstractions 9.7.0` (explicite ; 10.10.0 arrive en transitif), `MongoDB.Driver 2.30.0` et le `<NoWarn>MEAI001</NoWarn>` global ; `Data/sav-faq.json` toujours copié à la sortie | Aucune préversion ; connecteur SK remplacé (§3) ; aucun `NoWarn` global (règle 11) |
| `AzureOpenAISettings.cs` (racine, remplace `Configuration/AzureOpenAISettings.cs`) | Socle + `EmbeddingDeploymentName` | Un lab RAG a besoin d'un déploiement d'embedding |
| `ConfigurationHelper.cs` | Socle Lab01 (`ConfigurationBuilder` + user-secrets + validation) ; valide aussi `AzureOpenAI:EmbeddingDeploymentName` ; **plus de MongoDB** | User-secrets, message actionnable au démarrage |
| `appsettings.json` | `APIKey` retiré ; section `MongoDb` retirée (elle contenait une chaîne MongoDB Atlas avec un nom d'utilisateur réel et un mot de passe à remplacer) ; `EmbeddingDeploymentName` ajouté avec placeholder | Aucun secret ni identifiant versionné |
| `Models/FaqRecord.cs` | Même schéma `VectorData` (`[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]`, propriété `string Embedding` vectorisée par le store), classe `sealed`, constante `EmbeddingDimensions = 1536` documentée (métadonnée : la taille réelle est celle du modèle, cf. manuel §4.13), `IsIndexed` retiré (inutile pour le store en mémoire), `ToString` retirés | API 1.22.0 / MEVD 10.10 inchangée ; affichage dans `RagConsole` |
| `FaqData.cs` (**nouveau**) | `LoadAsync()` : lecture de `Data/sav-faq.json` (remplace `FaqVectorStoreService.LoadFaqDataAsync`) | Helper fourni ; les appels `VectorData` sont dans `Program.cs` (§3.3) |
| `Tools/FaqSearchTool.cs` (**nouveau**, remplace `Tools/SearchTools.cs`) | Function tool du scénario 3 : `SearchFaqAsync(question, top = 3, CancellationToken)` sur `VectorStoreCollection<string, FaqRecord>`, `[Description]` sur la méthode **et** les paramètres, résultat texte (id, score, question, réponse) | Fichier fourni, identique Start/Solution (règle 9) ; le modèle reçoit un texte plutôt qu'une `List<string>` |
| `RagConsole.cs` (**nouveau**) | `WriteSearchResult(rank, result)`, `WriteToolCalls(response)` (appels + arguments depuis `response.Messages`, ou `(none)`), `WriteTokenUsage(response, heading)` (`using static`) | Helpers d'affichage fournis, comme `AgentConsole.cs` (Lab03/06) |
| Supprimés | `Services/FaqVectorStoreService.cs`, `Tools/SearchTools.cs`, `Configuration/` (2 fichiers) | Concepts remplacés (§4) |
| `Solution/Program.cs` | Réécrit : client `OpenAIClient` v1 (clé API ou Entra ID) ; `GetEmbeddingClient(...).AsIEmbeddingGenerator()` ; `InMemoryVectorStore` avec `EmbeddingGenerator` ; **4 scénarios** (§3) ; `AIAgent`, `AgentResponse`, `AsAIAgent(instructions, name, tools)`, `ChatClientAgentOptions { Name, ChatOptions.Instructions, AIContextProviders, ChatHistoryProvider }`, `TextSearchProvider`, `TextSearchProviderOptions`, `AgentSession` | APIs et patterns des samples RAG officiels |
| `Start/Program.cs` | TODO **1 → 19** (setup 1–3 identique aux labs migrés, 4–6 embedding generator / store / collection ; S1 7–9 ; S2 10 ; S3 11–14 ; S4 15–19) ; plus de `throw new NotImplementedException` (l'ancien Start plantait dès le scénario 1) | Compile sans warning et s'exécute tel que livré (règle 10) |
| `README.md` (lab) | Réécrit sur le gabarit Lab05 : configuration (déploiement d'embedding, user-secrets), fichiers fournis, tableau des 19 TODO (chaque indice = le code de la Solution), schéma « How the two RAG approaches work » + comparatif outil / provider, concepts, namespaces, sortie attendue, dépannage, packages, « Going further », encadré « Coming from an older version », liens officiels | L'ancien README enseignait `MongoVectorStore` (SK, preview), MongoDB Atlas, `CreateAIAgent`, `AgentRunResponse`, une clé dans `appsettings.json`, un lien Semantic Kernel |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab07` (EN + FR, 12 checks) | §9 |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 3 tests Lab07 dans `OutputAnalyzerTests` | §9 |
| `Dashboard/README.md`, `README.md` (racine) | Lab07 enregistré / migré ; prérequis déploiement d'embedding (hors formulaire) ; 4 scénarios décrits ; conseil de checks pour les labs RAG | Documentation associée |
| `Migration/Migration-Manual.md` | §1 (décision Lab07 sur les connecteurs SK) ; **§4.13 « RAG : vector store, embeddings et `TextSearchProvider` »** ; §5 (4 lignes) ; §8 (CommonUtilities → Lab12 ; identifiants Azure de l'environnement) | Référence pour Lab12 et les labs RAG futurs |
| `Migration/Migration-Plan.md` | Statut, §2 (phase 0 → vague 4 avec Lab12), vague 3, ligne Lab07, §5 bis | Suivi |

`CommonUtilities` n'a **pas** été modifié (§6.3).

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `azureClient.GetEmbeddingClient(dep).AsIEmbeddingGenerator()` | `client.GetEmbeddingClient(settings.EmbeddingDeploymentName).AsIEmbeddingGenerator()` (même méthode MEAI, sur le client v1) |
| `new MongoVectorStore(database, new MongoVectorStoreOptions { EmbeddingGenerator })` (SK preview) | `new InMemoryVectorStore(new InMemoryVectorStoreOptions { EmbeddingGenerator })` (`CommunityToolkit.VectorData.InMemory`, stable) |
| `_vectorStore.GetCollection<string, FaqRecord>(name)` | `VectorStoreCollection<string, FaqRecord> collection = vectorStore.GetCollection<string, FaqRecord>("sav-faq")` (inchangé, typé explicitement) |
| `EnsureCollectionExistsAsync()`, `UpsertAsync(record)` un par un avec spinner | `EnsureCollectionExistsAsync()`, `UpsertAsync(IEnumerable<FaqRecord>)` en un appel (un seul appel au modèle d'embedding) |
| `collection.SearchAsync(query, topK, new VectorSearchOptions<FaqRecord> { IncludeVectors = false }).ToListAsync()` | `await foreach (VectorSearchResult<FaqRecord> result in collection.SearchAsync(question, top: 3))` |
| `chatClient.CreateAIAgent(instructions, name, tools)` | `AIAgent agent = chatClient.AsAIAgent(instructions, name, tools)` |
| `AgentRunResponse` ; `Console.WriteLine(response)` | `AgentResponse` ; `response.Text` ; appels d'outil lus dans `response.Messages` (`FunctionCallContent`) |
| — | `TextSearchProvider(searchAsync, options)`, `TextSearchProviderOptions { SearchTime, RecentMessageMemoryLimit }`, `TextSearchProvider.TextSearchResult`, `ChatClientAgentOptions.AIContextProviders`, `InMemoryChatHistoryProviderOptions.StorageInputRequestMessageFilter`, `GetAgentRequestMessageSourceType()`, `AgentRequestMessageSourceType`, `AgentSession` / `CreateSessionAsync` |

Toutes ces API MAF sont dans `PublicAPI.Shipped.txt` 1.22.0 et **aucune n'est `[Experimental]`** (la source de `TextSearchProvider` ne désactive `MAAI001` que pour son compteur d'usage interne) : build `-warnaserror` sans suppression autre que le `OPENAI001` du socle.

Dépendances résolues (Start = Solution, graphes identiques) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `Microsoft.Extensions.VectorData.Abstractions` 10.10.0, `CommunityToolkit.VectorData.InMemory` 1.0.1, `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. Le graphe ne contient **aucune préversion**, ni `Azure.AI.OpenAI`, ni Semantic Kernel, ni Hosting. `MongoDB.Driver` 2.30.0 n'y figure plus que par `CommonUtilities` (warnings NU1902/NU1903 hérités, identiques à la baseline).

## 3. Changements conceptuels

1. **MongoDB Atlas → vector store en mémoire.** Le connecteur `Microsoft.SemanticKernel.Connectors.MongoDB` est toujours en préversion (1.74.0-preview) et il n'existe aucun connecteur `Microsoft.Extensions.VectorData` MongoDB stable (`CommunityToolkit.VectorData.CosmosMongoDB` cible Azure Cosmos DB). Les samples RAG officiels utilisent `InMemoryVectorStore` (Step01) ou Qdrant (Step02). Lab07 passe sur `InMemoryVectorStore` : même API `VectorData`, aucun service externe, lab exécutable avec la seule CLI `dotnet` (règle du prompt) — et l'ancienne configuration pointait vers un cluster Atlas personnel. Conséquence : le store étant en mémoire, **le scénario 1 (remplissage) est obligatoire** avant les autres (documenté dans le sélecteur de scénarios et le README) ; l'ancienne Solution tournait `[2, 3]` parce que MongoDB persistait les vecteurs. Les connecteurs réels (Qdrant, Azure AI Search, Cosmos DB, pgvector…) sont cités en « Going further ».
2. **Nouveau scénario 4 : `TextSearchProvider`.** MAF 1.22.0 fournit un composant RAG intégré, absent de la preview du lab : un `AIContextProvider` qui appelle une fonction de recherche avant chaque appel du modèle et injecte les résultats (`BeforeAIInvoke`), ou expose la recherche comme outil (`OnDemandFunctionCalling`). Le lab enseigne désormais les **deux façons** de faire du RAG : le function tool (scénario 3, « agentique » : le modèle décide) et le provider (scénario 4 : recherche systématique, un seul appel modèle par question), avec un tableau comparatif dans le README. Le scénario 4 reprend le sample Step02 : session, `RecentMessageMemoryLimit = 2` (la question de suivi « And how long until I get my money back? » est cherchée avec la première), filtre `StorageInputRequestMessageFilter` pour ne pas stocker les messages du provider dans l'historique. L'adaptateur affiche l'entrée de recherche et les ids trouvés, ce qui rend le mécanisme visible (et vérifiable par le dashboard).
3. **Les appels `VectorData` dans `Program.cs`.** L'ancien Start demandait d'implémenter cinq méthodes d'un service (`FaqVectorStoreService`) et un outil, avec `throw new NotImplementedException` — le Start plantait dès le scénario 1 et Start/Solution différaient sur trois fichiers. Désormais, comme dans le sample Step02, le remplissage (`EnsureCollectionExistsAsync`, `UpsertAsync`) et la recherche (`SearchAsync`) sont écrits par l'apprenant dans `Program.cs` ; l'outil (`FaqSearchTool`) et le chargement du JSON (`FaqData`) sont fournis. Seul `Program.cs` diffère (règle 9).
4. **Vérifiabilité.** Les questions ont été choisies pour que les réponses contiennent des faits que seule la FAQ donne : question du scénario 2 sans aucun mot commun avec l'entrée attendue (`faq-010` « contact customer support » pour « Is there a phone number I can call for help? ») ; scénario 3 → `faq-005` (« within 48 hours », photos) ; scénario 4 → `faq-001` (30 jours) puis `faq-002` (5-7 jours ouvrés). Les appels d'outil sont affichés depuis `response.Messages` (pattern validé Lab04/Lab06).
5. **Déploiement d'embedding en configuration** (`AzureOpenAI:EmbeddingDeploymentName`), validé au démarrage, hors du formulaire du dashboard (documenté).

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `Azure.Identity` beta, `NoWarn` global : comme Lab01.
- **`Microsoft.SemanticKernel.Connectors.MongoDB` / `.InMemory` (préversion), `MongoVectorStore`, `MongoDB.Driver`, section `MongoDb` et `Configuration/MongoDbConfiguration.cs`** : supprimés (§3.1). La chaîne de connexion Atlas versionnée (nom d'utilisateur réel, mot de passe à remplacer) disparaît du contenu versionné ; ⚠️ elle reste dans l'historique git.
- **`FaqVectorStoreService`, `SearchTools`** : remplacés par des appels `VectorData` dans `Program.cs`, `FaqData` et `FaqSearchTool` (§3.3).
- `VectorSearchOptions { IncludeVectors = false }` + `ToListAsync()` : inutiles (vecteurs exclus par défaut, `IAsyncEnumerable` consommé par `await foreach`).
- `CreateAIAgent`, `AgentRunResponse`, `Console.WriteLine(response)` : remplacés (§2).
- Lien README vers la doc Semantic Kernel du connecteur MongoDB : remplacé par les samples `AgentWithRAG_*`, la doc `Microsoft.Extensions.VectorData` et la doc embeddings Azure OpenAI.
- Aucun objectif pédagogique supprimé (créer/remplir un vector store, recherche sémantique, outil de recherche pour un agent) ; un scénario ajouté (`TextSearchProvider`, justifié par les samples Step01–03).

## 5. Tests effectués et résultats

Environnement : macOS, SDK .NET 10.0.100, ressource `*.cognitiveservices.azure.com` (kind `AIServices`, `Succeeded`, 2 déploiements dont un d'embedding), variables `AzureOpenAI__*` de l'environnement (aucun user-secret configuré), Docker non démarré (sans incidence : le lab n'en a plus besoin).

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build Start/Solution) | ✅ 0 erreur, 0 warning C# ; NU1902/NU1903 (CommonUtilities + le projet lui-même via `MongoDB.Driver` 2.30.0) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances (`--include-transitive`) | ✅ MAF 1.22.0, MEVD 10.10.0, `CommunityToolkit.VectorData.InMemory` 1.0.1 ; **aucune préversion**, pas d'`Azure.AI.OpenAI`, de Semantic Kernel ni de Hosting ; graphes Start et Solution **identiques** (`diff`) |
| 3 | Vulnérabilités (`--vulnerable --include-transitive`) | ✅ aucune introduite : seuls `SharpCompress` 0.30.1 / `Snappier` 1.0.0 via `CommonUtilities` (identique à la baseline ; le lab ne référence plus `MongoDB.Driver`) |
| 4 | `dotnet build -warnaserror` Start + Solution | ✅ `0 Warning(s) 0 Error(s)` (avec `-nowarn:NU1902,NU1903` hérités) |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent(`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `ChatMessageStore`, `SemanticKernel`, `MongoVectorStore`, `MongoDB`, `Atlas`, `Microsoft.Extensions.Hosting`, `NoWarn`, `MEAI001`, `"APIKey"`, `topK`, `IncludeVectors`, `ToListAsync`, `FaqVectorStoreService`, `SearchTools`, `.NET 8`, `ChatClientAgent `) | ✅ 0 occurrence dans `Start/`, `Solution/` ; dans le README, uniquement dans l'encadré « Coming from an older version » |
| 6 | Run Solution — **clé API** | ⛔ **BLOQUÉ** : `HTTP 401 Access denied due to invalid subscription key or wrong API endpoint` dès l'appel d'embedding du scénario 1 (`EmbeddingClient.GenerateEmbeddingsAsync`). **Lab01 échoue de la même façon** avec la même clé (§6.1) : cause hors lab |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`, `az login` valide, même tenant) | ⛔ **BLOQUÉ** : `400 SubscriptionNotRegistered — CheckAccess request is invalid because: This subscription is not registered with the Microsoft.CognitiveServices resource provider` (Lab01 : idem) |
| 8 | Config placeholder (variables `AzureOpenAI__*` retirées, HOME isolé) | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set "AzureOpenAI:Endpoint" <value>', or with the environment variable 'AzureOpenAI__Endpoint'.` |
| 8b | Déploiement d'embedding absent seulement | ✅ `'AzureOpenAI:EmbeddingDeploymentName' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" <value>', or with the environment variable 'AzureOpenAI__EmbeddingDeploymentName'.` |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche les 4 en-têtes, **exit 0** (aucun appel réseau) |
| 10 | Start complété **uniquement d'après les indices du README** (copie temporaire `.tmp-start-check`, script de remplacement des 19 TODO par le code des tableaux, supprimée ensuite) | ✅ compile `-warnaserror` sans warning, 0 TODO restant ; ⛔ **exécution non comparée à la Solution** (même blocage que 6/7). La copie complétée est conservée hors dépôt (`scratchpad/Lab07-StartCompleted-Program.cs`) pour rejouer le test |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (10 fichiers) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–19 a un indice qui contient le code de la Solution (prouvé par le test 10 : la copie complétée avec le seul texte des indices compile) |
| + | Diagnostic des identifiants (lecture seule, aucune valeur affichée) | clé : `401` sur `/openai/v1/models` (en-tête `api-key` et `Bearer`), `401` sur `/openai/models?api-version=2024-10-21`, `401` sur l'hôte `<nom>.openai.azure.com` ; Entra : `400 SubscriptionNotRegistered` (v1 et legacy, scopes `ai.azure.com` et `cognitiveservices.azure.com`) ; ressource présente dans l'unique abonnement `az` (`Succeeded`, `disableLocalAuth = false`, `publicNetworkAccess = Enabled`), RP `Microsoft.CognitiveServices` **`Registered`**, tenant du jeton = tenant `az` ; variables identiques dans les shells interactif, login et non interactif ; aucun user-secret. Les deux voies fonctionnaient le 2026-09-28 (rapport Lab06 §5) |
| 13 | Non-régression | Sans objet : `CommonUtilities` n'est pas modifié ; les tests du dashboard passent avec les 7 labs déjà enregistrés (§9) |

## 6. Problèmes rencontrés

1. **Identifiants Azure OpenAI de l'environnement invalides (bloquant).** Voir tests 6, 7 et le diagnostic. La ressource est saine et le RP enregistré : la clé de l'environnement a probablement été régénérée, et l'erreur Entra `SubscriptionNotRegistered` (renvoyée par le plan de données alors que `az provider show` dit `Registered`) ressemble à un incident côté Azure ou à un changement de la ressource / de l'abonnement. Rien n'a été modifié hors dépôt (ni `az account set`, ni `az provider register`, ni user-secrets). **À faire de votre côté** : rétablir une clé valide (`AzureOpenAI__APIKey` ou user-secret `AzureOpenAI:APIKey`) ou l'accès Entra, puis rejouer §7.
2. **Pas de connecteur MongoDB stable** pour `Microsoft.Extensions.VectorData` : décision `InMemoryVectorStore` (§3.1, à valider §7).
3. **`Dimensions` de `[VectorStoreVector]`** n'est pas transmis au générateur d'embeddings (vérifié dans la source MEVD `VectorPropertyModel.GenerateEmbeddingsCoreAsync`) : la valeur 1536 de l'ancien lab est conservée en constante documentée ; le store en mémoire n'en a pas besoin, la taille réelle est celle du modèle. À vérifier sur un vrai run avec un modèle 3072 (`text-embedding-3-large`) si vous en utilisez un.
4. **Variable d'environnement d'embedding** : l'environnement définit `AzureOpenAI__DefaultEmbeddingDeploymentName` (ancien nom), pas `AzureOpenAI__EmbeddingDeploymentName` (nom lu par le lab et par le README racine). Les runs ont été lancés avec `AzureOpenAI__EmbeddingDeploymentName="$AzureOpenAI__DefaultEmbeddingDeploymentName"` (valeur jamais affichée). Le README racine documente le bon nom.
5. **`TextSearchProvider` avale les exceptions** de la fonction de recherche (journalisées seulement) : l'adaptateur du lab affiche lui-même l'entrée et les résultats de chaque recherche, sinon une collection vide passerait inaperçue.
6. **Sortie attendue du README et fixtures des tests du dashboard écrites à la main** (faute de run réel) : les lignes déterministes (`Loaded 20 FAQ entries…`, `Vector store ready…`, en-têtes, `[TextSearchProvider] Search input: …`) viennent du code ; les réponses, scores et tokens sont plausibles mais **à remplacer par une sortie réelle** dès que possible (§7).
7. `Dashboard/Prototype-Report.md` (suivi) cite `disco-architecture.html`, fichier de travail local git-ignoré : antérieur à cette migration, hors périmètre, signalé seulement.

## 7. Décisions à valider

- **`InMemoryVectorStore` à la place de MongoDB Atlas** (règle « stable uniquement », samples officiels, lab exécutable sans service externe). Alternative si vous tenez à MongoDB : exception documentée pour `Microsoft.SemanticKernel.Connectors.MongoDB` 1.74.0-preview (compte Atlas requis).
- **Ajout du scénario 4 `TextSearchProvider`** (samples `AgentWithRAG_Step01/02/03`), avec le filtre d'historique des samples et `RecentMessageMemoryLimit`.
- **Titre** « Agentic RAG with a Vector Store » ; dossier et projet inchangés (`Lab07-AgenticRAG-VectorStore`, `AgenticRAG.csproj`).
- **Questions et faits vérifiables** (`faq-010`, `faq-005`, `faq-001`, `faq-002`) choisis pour les checks.
- **`EmbeddingDeploymentName`** hors du formulaire du dashboard (le formulaire ne gère que endpoint, déploiement de chat et clé) : à ajouter au formulaire si vous le souhaitez (modification du dashboard, non faite : non exigée par le lab).
- **Phase 0 CommonUtilities reportée à Lab12** (dernier lab à référencer `MongoDB.Driver` 2.30.0 en direct ; Lab07 ne le référence plus).
- **Validation d'exécution à rejouer** dès que les identifiants sont rétablis, dans cet ordre : (1) `dotnet run --project Solution` avec la clé, puis avec `AzureOpenAI__APIKey=""` ; (2) copie du Start + `scratchpad/Lab07-StartCompleted-Program.cs` → même comportement ; (3) remplacer la sortie attendue du README et `Lab07SolutionOutput` des tests du dashboard par la sortie réelle, vérifier que les 12 checks passent (`dotnet test`), puis un run Solution `passed` dans le dashboard (tokens affichés pour les scénarios 3 et 4).

## 8. Points pour les prochains labs

- **Lab12** (`AIContextProvider`) : `TextSearchProvider` est un `MessageAIContextProvider` prêt à l'emploi ; son état par session (`RecentMessagesText`) vit dans `AgentSession.StateBag` via `ProviderSessionState<T>` (même principe que Lab05) ; `ChatClientAgentOptions.AIContextProviders` accepte plusieurs providers. Filtrer l'historique avec `StorageInputRequestMessageFilter` + `GetAgentRequestMessageSourceType()` quand un provider injecte des messages.
- **Lab12 / phase 0** : monter `CommonUtilities` en `MongoDB.Driver` 3.12.0 et retirer la référence 2.30.0 de Lab12 (ou sortir `MongoDbHealthCheck`).
- **Labs à outils** (Lab09, Lab10) : `WriteToolCalls` (appels + arguments depuis `response.Messages`) est le pattern d'affichage validé ici, Lab04 et Lab06.
- **Vector stores** : `CommunityToolkit.VectorData.*` 1.0.x (Qdrant, PgVector, AzureAISearch, Redis, SqlServer, CosmosNoSql, CosmosMongoDB…) sont stables et alignés sur MEVD 10.10 ; aucun n'est nécessaire dans les labs actuels.
- **Avant tout test d'exécution** : vérifier les identifiants avec `curl -s -o /dev/null -w '%{http_code}' -H "api-key: $AzureOpenAI__APIKey" "$AzureOpenAI__Endpoint/openai/v1/models"` (attendu `200`) — cf. manuel §8.

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire. Le catalogue est piloté par `labs.json` ; les trois blocs de tokens (`Token Usage:`, `Token Usage (first question):`, `Token Usage (follow-up):`) sont étiquetés par scénario par l'analyseur existant. Le lab a besoin du déploiement d'embedding, qui n'est pas dans le formulaire *Azure OpenAI settings* : documenté dans `Dashboard/README.md` (note en tête et section « Azure OpenAI settings »).

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab07` : métadonnées EN + FR (niveau *Intermediate*), projets Start/Solution, timeout 180 s, **12 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab07 sur une sortie Solution (tous passent) et sur le Start (seul `config` passe) ; réponses « hors FAQ » (aucun check hors `-usage` ne passe) ; 3 rapports de tokens étiquetés par scénario (total 1506) |
| Documentation | `Dashboard/README.md` : labs enregistrés, prérequis embedding, conseil de checks pour les labs RAG, liste des tests |

**Choix des checks** (chacun échoue sur le Start livré et passe sur la sortie attendue de la Solution) :

| Check | Preuve |
|---|---|
| `scenario1-indexed` | `Loaded 20 FAQ entries from Data/sav-faq.json` puis `Vector store ready: 20 FAQ entries indexed in the collection 'sav-faq'` |
| `scenario2-top-result` | `1. faq-010 (score …): How do I contact customer support?` — l'entrée attendue est classée première pour une question sans mot commun |
| `scenario2-results` | lignes `2.` et `3.` avec `faq-NNN (score …)` |
| `scenario3-tool` | `Tool called: search_faq(question: …)` |
| `scenario3-answer` | la réponse (`Agent:`) du scénario 3 contient `48 hours` (seule `faq-005` le donne) |
| `scenario4-search` | `[TextSearchProvider] Search input: I want to send back a jacket…` puis `Results:` contenant `faq-001` |
| `scenario4-answer` | la réponse contient `30 days` (`faq-001`) |
| `scenario4-followup-search` | l'entrée de recherche de la question de suivi contient **les deux questions** (`… \| And how long until I get my money back?`) et les résultats `faq-002` |
| `scenario4-followup-answer` | après la question de suivi, la réponse contient `5-7 business days` (`faq-002`) |
| `scenarioN-usage` | usage du scénario, borné à l'en-tête suivant |

**Résultats** (instance de vérification sur le port 5060, historique dans un dossier temporaire hors dépôt, supprimé ; `Dashboard/.data/history.json` inchangé) :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ **100/100** (dont les 3 tests Lab07) ; aucun nouveau warning d'analyse |
| API : `GET /api/labs` | ✅ 8 labs (lab01 → lab07) |
| API : détails, README rendu, packages, diff de solution | ✅ README rendu (39 Ko HTML) ; 7 packages listés ; 12 checks ; seul `Program.cs` diffère |
| Run Lab07 **Start** livré via l'API | ✅ `failed` attendu — « 11 of 12 check(s) failed », seul `config` passe, exit 0, aucun usage |
| Run Lab07 **Solution** via l'API | ⛔ `failed` à l'étape `run` (exit 134) : `HTTP 401 … Access denied due to invalid subscription key` — même blocage que le test 6 ; le verdict `passed` et l'affichage des tokens restent **à vérifier** après rétablissement des identifiants |

Aucun artefact laissé dans le dépôt : `.tmp-start-check` et `.tmp-home` supprimés, instance du dashboard arrêtée, dossier temporaire hors dépôt supprimé.
