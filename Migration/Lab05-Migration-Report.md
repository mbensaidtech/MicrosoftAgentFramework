# Rapport de migration — Lab05-AIAgentWithThreads (Sessions)

> Date : 2026-09-27 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.11 ajouté), [Migration-Plan.md](Migration-Plan.md), rapports [Lab01](Lab01-Migration-Report.md), [Lab02](Lab02-Migration-Report.md), [Lab03](Lab03-Migration-Report.md). Sources officielles (tag `dotnet-1.22.0`) : samples `02-agents/Agents/Agent_Step03_PersistedConversations`, `Agent_Step04_3rdPartyChatHistoryStorage`, `AgentWithMemory_Step05_BoundedChatHistory`, `PublicAPI.Shipped.txt` de `Microsoft.Agents.AI(.Abstractions/.OpenAI)`, code de `ChatHistoryProvider`, `InMemoryChatHistoryProvider`, `ChatClientAgent`, `CosmosChatHistoryProvider` ; pages Learn [Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp) et [Storage](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp).

**Statut : migré.** Tous les critères du manuel (§7) sont remplis. Deux réserves : les warnings NU1902/NU1903 hérités de `CommonUtilities`, qui ne concernent plus le graphe de Lab05 (§6.2), et un incident Docker hors dépôt pendant les tests, corrigé et à finaliser par vous (§6.1).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AIAgentWithThreads.csproj` | Socle Lab01–03 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé ; **ajout** de `CommunityToolkit.VectorData.InMemory` **1.0.1** et `MongoDB.Driver` **3.12.0** (commentés). Supprimés : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251002.1`, `Microsoft.Extensions.Hosting 9.0.0`, `Microsoft.SemanticKernel.Connectors.InMemory 1.67.1-preview`, `…Connectors.MongoDB 1.66.0-preview`, `MongoDB.Driver 2.30.0` et le `<NoWarn>MEAI001</NoWarn>` global | Aucune préversion ; connecteurs SK remplacés (§3) ; aucun `NoWarn` global (règle 11) |
| `AzureOpenAISettings.cs`, `ConfigurationHelper.cs` (racine, Start = Solution) | Copies du socle (namespace `AIAgentWithThreads`) ; `ConfigurationHelper` ajoute `ConnectToMongoDbAsync()` : lecture/validation de `MongoDb:*`, `ServerSelectionTimeout` 5 s, `ping`, message actionnable si MongoDB ne répond pas | User-secrets, validation au démarrage ; erreur claire en ~6 s au lieu d'un timeout de 30 s avec trace illisible |
| `MongoDbSettings.cs` (**nouveau**) | Remplace `Configuration/MongoDbConfiguration.cs` | Même rôle, rangé avec les autres réglages à la racine comme dans le socle |
| `appsettings.json` | `APIKey` retiré ; bloc `Agents` retiré ; `MongoDb` = `mongodb://localhost:27017` (sans mot de passe), base `AIAgentSessions` | Aucun secret versionné (l'ancienne chaîne contenait `admin:password123`) |
| `Stores/VectorChatHistoryProvider.cs` (**nouveau**, remplace `InMemoryVectorChatMessageStore.cs` + `ChatHistoryItem.cs`) | `ChatHistoryProvider` sur `VectorStore`, adapté du sample `Agent_Step04` : `ProvideChatHistoryAsync` / `StoreChatHistoryAsync`, état `SessionDbKey` via `ProviderSessionState<State>`, `GetSessionDbKey(session)`, 10 derniers messages ; **correctif d'ordre** (1 tick par message) | API 1.22.0 ; provider sans état (doc « Storage ») |
| `Stores/MongoChatHistoryProvider.cs` (**nouveau**, remplace `MongoVectorChatMessageStore.cs`) | Même pattern sur `IMongoCollection` (driver officiel 3.12.0) : documents `SessionId`, `Timestamp`, `Order`, `Role`, `MessageText`, `SerializedMessage` | Pas de connecteur `VectorData` MongoDB stable (§3) |
| `SessionConsole.cs` (**nouveau**) | `WriteSerializedSession(JsonElement)`, `WriteTokenUsage(AgentResponse)` (`using static`) | Helpers d'affichage dans un fichier fourni (pas de `CS8321` dans le Start), comme `AgentConsole.cs` de Lab03 |
| `MongoDB/docker-compose.yml` | `mongo:8.0` (au lieu de `latest`), **nom de projet `lab05-aiagent-sessions`**, `container_name: lab05-mongodb`, port lié à `127.0.0.1`, pas d'authentification, volume nommé ; **Mongo Express supprimé** | Version figée ; isolement des autres projets compose (§6.1) ; plus d'identifiants par défaut dans le dépôt |
| Supprimés | `Configuration/` (3 fichiers), `models/AgentThreadState.cs`, `models/AgentResponse.cs` (Solution seulement, homonyme de `Microsoft.Agents.AI.AgentResponse`), anciens stores | Concepts disparus (§4) |
| `Solution/Program.cs` | Réécrit : client `OpenAIClient` v1 (clé API ou Entra ID) ; 3 scénarios (§3) ; `AIAgent`, `AgentSession`, `AgentResponse`, `CreateSessionAsync`, `SerializeSessionAsync`, `DeserializeSessionAsync`, `GetService<T>()`, `ChatClientAgentOptions { Name, ChatOptions.Instructions, ChatHistoryProvider }` ; constantes partagées (`AgentName`, `AgentInstructions`, `FirstQuestion`, `FollowUpQuestion`) | APIs et patterns des samples / de la doc officiels |
| `Start/Program.cs` | TODO renumérotés **1 → 22** (setup 1–3 identique à Lab01–03 ; S1 4–9 ; S2 10–15 ; S3 16–22) ; plus de fonctions locales à compléter (6 `CS8321` dans l'ancien Start) | Compile sans warning et s'exécute tel que livré |
| `README.md` (lab) | Réécrit sur le gabarit Lab03 : titre « AI Agent with Sessions », configuration, démarrage de MongoDB, fichiers fournis, tableau des 22 TODO, schéma « How sessions and chat history work » + comparatif des 3 stockages, concepts, namespaces, sortie réelle, dépannage, packages, « Going further », encadré « Coming from an older version » | L'ancien README expliquait `AgentThread`, `ChatMessageStore`, `CreateAIAgent`, une clé et un mot de passe dans `appsettings.json` |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab05` (EN + FR, 11 checks) | §9 |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 3 tests Lab05 dans `OutputAnalyzerTests` | §9 |
| `Dashboard/README.md`, `README.md` (racine) | Lab05 enregistré / migré ; prérequis MongoDB (conteneur fourni) ; 3 scénarios décrits ; conseil de checks pour les labs à sessions | Documentation associée |
| `Migration/Migration-Manual.md` | §4.11 « Sessions et `ChatHistoryProvider` » ; §1 (packages `CommunityToolkit.VectorData.InMemory`, `MongoDB.Driver`, décision SK connectors) ; §4.5 validé ; §5 (lignes `AgentThread`, `ChatMessageStore`, SK connectors, `MongoDB.Driver`) ; §8 (CommonUtilities, **nom de projet Docker Compose**) | Référence pour Lab07, Lab09, Lab12 |
| `Migration/Migration-Plan.md` | Statut, phase 0, vague 3, ligne Lab05, pratique §5 bis → ✅ | Suivi |

`CommonUtilities` n'a **pas** été modifié.

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `chatClient.CreateAIAgent(agentOptions)` | `AIAgent agent = chatClient.AsAIAgent(agentOptions)` (ou `AsAIAgent(instructions, name)` au scénario 1) |
| `ChatClientAgentOptions.Instructions` | `ChatClientAgentOptions.ChatOptions.Instructions` |
| `AgentThread` / `agent.GetNewThread()` | `AgentSession` / `await agent.CreateSessionAsync()` |
| `thread.Serialize()` | `await agent.SerializeSessionAsync(session)` |
| `agent.DeserializeThread(JsonElement)` | `await agent.DeserializeSessionAsync(JsonElement)` |
| `AgentRunResponse` | `AgentResponse` ; `response.ToString()` → `response.Text` |
| `ChatMessageStore` (`AddMessagesAsync`, `GetMessagesAsync`, `Serialize`) | `ChatHistoryProvider` (`StoreChatHistoryAsync`, `ProvideChatHistoryAsync`, `StateKeys`) |
| `ChatMessageStoreFactory = ctx => new Store(…, ctx.SerializedState, ctx.JsonSerializerOptions)` | `ChatHistoryProvider = new Provider(…)` (instance unique) + `ProviderSessionState<T>` (`GetOrInitializeState`) |
| — | `agent.GetService<InMemoryChatHistoryProvider>()!.GetMessages(session)`, `agent.GetService<TProvider>()` |
| `Microsoft.SemanticKernel.Connectors.InMemory.InMemoryVectorStore` | `CommunityToolkit.VectorData.InMemory.InMemoryVectorStore` (même API `VectorStore`) |
| `Microsoft.SemanticKernel.Connectors.MongoDB.MongoVectorStore` | `MongoDB.Driver.IMongoCollection<T>` (`Find/SortByDescending/Limit`, `InsertManyAsync`) |

Toutes ces API MAF sont dans `PublicAPI.Shipped.txt` 1.22.0, sans `[Experimental]`. Seuls les constructeurs de `InvokingContext` / `InvokedContext` le sont (`MAAI001`), et le lab ne les appelle pas.

Dépendances résolues (Start = Solution) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `Microsoft.Extensions.VectorData.Abstractions` 10.10.0, `CommunityToolkit.VectorData.InMemory` 1.0.1, `MongoDB.Driver`/`MongoDB.Bson` 3.12.0 (→ `Snappier` 1.3.1, `SharpCompress` 0.48.1), `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. Le graphe ne contient **aucune préversion**, ni `Azure.AI.OpenAI`, ni Semantic Kernel, ni Hosting.

## 3. Changements conceptuels

1. **Thread → session, et session opaque.** L'ancien lab extrayait à la main l'id du store (`storeState`) du JSON du thread, puis reconstruisait un JSON `AgentThreadState` pour le restaurer (`ExtractThreadIdFromState`, `RestoreThreadFromId`). La documentation 1.22.0 dit au contraire de *persister la session entière* et de la traiter comme un objet opaque, restauré avec la même configuration d'agent. Le lab enseigne donc : `SerializeSessionAsync` → enregistrer le texte JSON → `DeserializeSessionAsync`. La clé reste visible, mais elle est lue via le provider (`GetSessionDbKey(session)`) et jamais réinjectée à la main.
2. **Store par thread → provider par agent.** Un `ChatMessageStore` était créé pour chaque thread par une fabrique, avec la clé dans un champ. Un `ChatHistoryProvider` est une instance unique par agent, sans état, et la clé vit dans `AgentSession.StateBag` (`ProviderSessionState<T>`). C'est la section « Look at the provided files » du README qui l'explique.
3. **Nouveau scénario 1 : l'historique par défaut.** Sans provider, `ChatClientAgent` utilise `InMemoryChatHistoryProvider`, et la session sérialisée contient tous les messages. Le contraste avec les scénarios 2–3, où seule la clé est sérialisée, est le cœur pédagogique du lab. Ce scénario reprend `Agent_Step03_PersistedConversations` et la page Learn « Storage » (`GetMessages(session)`), et correspond à la pratique Lab05 du plan (§5 bis).
4. **Scénario 3 : vrai redémarrage simulé.** La question de suivi est posée à un **nouvel** agent, avec un nouveau provider et une nouvelle connexion MongoDB. Seuls le JSON enregistré et les documents MongoDB sont partagés. Dans l'ancien lab, le même agent relisait simplement le même store.
5. **Vérifiabilité.** La première question donne un prénom (« my name is Ada »), et la question de suivi demande la population de « that city » et le prénom. Seul l'historique permet d'y répondre, et les checks du dashboard le vérifient. Les 3 scénarios envoient le même historique au modèle : 79 tokens d'entrée à chaque question de suivi, sur tous les runs.

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `Azure.Identity` beta : remplacés comme dans Lab01.
- **Connecteurs Semantic Kernel (préversion)** : InMemory → `CommunityToolkit.VectorData.InMemory` 1.0.1 (stable, propriétaires Microsoft/.NET Foundation, utilisé par 4 samples officiels) ; MongoDB → `ChatHistoryProvider` sur `MongoDB.Driver` 3.12.0.
- **`models/AgentThreadState.cs`, `ExtractThreadIdFromState`, `RestoreThreadFromId`** : supprimés (point 1 du §3), sans équivalent voulu.
- **Fonctions locales à compléter** (`RunThreadConversationTestAsync`, `AskQuestionAsync`, …) : remplacées par des TODO en ligne dans chaque scénario, avec un helper d'affichage fourni. Les anciennes provoquaient 6 `CS8321` dans le Start livré.
- **Configuration de l'agent dans `appsettings.json`** (`AzureOpenAI:Agents:GlobalAgent`) : remplacée par des constantes dans `Program.cs`. Ce n'était pas un objectif du lab, et cela ajoutait une section de configuration hors socle.
- **Mongo Express** et identifiants MongoDB (`admin/password123`, `admin/admin`) : supprimés, remplacés par une commande `mongosh` dans le README.
- `models/AgentResponse.cs` (Solution uniquement) : supprimé, car homonyme de `Microsoft.Agents.AI.AgentResponse`.
- Aucun scénario pédagogique supprimé ; un scénario ajouté (historique par défaut).

## 5. Tests effectués et résultats

Environnement : macOS, SDK .NET 10.0.100, ressource `*.cognitiveservices.azure.com`, déploiement `gpt-4o-mini`, Docker 29.1.3, MongoDB 8.0.32 (conteneur du lab).

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build Start/Solution) | ✅ 0 erreur ; Start : **6 warnings `CS8321`** ; NU1902/NU1903 sur CommonUtilities **et** sur le projet Lab05 (4 + 4) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances | ✅ MAF 1.22.0, aucune préversion, pas d'`Azure.AI.OpenAI` ni de Semantic Kernel (Start = Solution) |
| 3 | Vulnérabilités (`--vulnerable --include-transitive`) | ✅ **« has no vulnerable packages »** pour `AIAgentWithThreads` (mieux que la baseline : `MongoDB.Driver` 3.12.0 remplace 2.30.0 dans le graphe du lab). Les NU1902/NU1903 ne restent émis que par `CommonUtilities.csproj` |
| 4 | `dotnet build -warnaserror` Start + Solution | ✅ 0 erreur, 0 warning C# ; seuls NU1902/NU1903 de `CommonUtilities.csproj` échouent sans `-nowarn:NU1902,NU1903` (avec : `0 Warning(s) 0 Error(s)`) |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `GetNewThread`, `DeserializeThread`, `ChatMessageStore`, `SemanticKernel`, Hosting, `NoWarn`, `"APIKey"`, `.NET 8`, `ToString()`, `ExtractThreadId`, `StoreState`, `password`, Mongo Express) | ✅ 0 occurrence dans `Start/`, `Solution/`, `README.md`, hors encadré « Coming from an older version » |
| 6 | Run Solution — **clé API** | ✅ S1 : `Messages in the session: 2 (user, assistant)`, JSON avec les 2 messages, suivi « population of Paris … 2.1 million. Your name is Ada. » ; S2 : JSON limité à `sessionDbKey` (= clé affichée), même suivi ; S3 : clé identique après « redémarrage », même suivi ; usage 79/23/102, 79/23/102, 79/17/96 |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`, `az login`) | ✅ 3 scénarios corrects ; usage 79/17/96, 79/17/96, 79/23/102 |
| 7b | Contenu MongoDB (`mongosh`) | ✅ 4 documents par session, `Order` 0/1, ordre user → assistant → user → assistant |
| 8 | Config placeholder (variables `AzureOpenAI__*` retirées, HOME isolé) | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set …', or with the environment variable 'AzureOpenAI__Endpoint'.` |
| 8b | MongoDB arrêté (`docker stop lab05-mongodb`), scénario 3 | ✅ en 6 s : `MongoDB is not reachable with the connection string of 'MongoDb:ConnectionString'. Start it with 'docker compose up -d' in the MongoDB folder of the lab.` (exception non gérée avec message clair, comme la config) |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche les 3 en-têtes, exit 0 |
| 10 | Start complété **uniquement d'après les indices du README** (copie temporaire `.tmp-start-check` dans le lab, supprimée ensuite) | ✅ compile sans warning C# et reproduit les 3 scénarios (mêmes lignes, même usage 79 tokens d'entrée) |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (10 fichiers : `.csproj`, 4 `.cs` racine, `appsettings.json`, 2 providers, `docker-compose.yml`) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–22 a un indice qui mène au code de la Solution (vérifié par le test 10) |
| + | Chat reducers (`MessageCountingChatReducer`) | Compilation d'essai hors dépôt : `error MEAI001` → cités seulement dans « Going further » |
| 13 | Non-régression | Sans objet : `CommonUtilities` n'est pas modifié |

## 6. Problèmes rencontrés

1. **Incident Docker (hors dépôt) — à finaliser par vous.** Mon premier `docker compose up -d` a utilisé le nom de projet par défaut `mongodb` (dérivé du dossier `MongoDB/`). Ce nom était déjà celui d'un autre projet local (`…/EndToEndAppDemo/AIAgentsBackend/MongoDB`, conteneurs `aiagents-mongodb` et `aiagents-mongo-express`). Compose a donc **recréé** le service `mongodb` : il a supprimé le conteneur `aiagents-mongodb` et monté son volume `mongodb_mongodb_data`. mongod 8.0 a refusé ces données, dont la FCV est 8.2 (`UPGRADE PROBLEM … version: "8.2"`), avec un arrêt fatal au démarrage : **les données n'ont pas été modifiées**. Actions menées : arrêt puis suppression de mon conteneur, suppression du réseau `mongodb_default` que j'avais créé ; les volumes `mongodb_mongodb_data`, `mongodb_mongodb_atlas_data` et les volumes anonymes sont conservés, et `aiagents-mongo-express` n'a pas été touché. **Reste à faire de votre côté** : recréer `aiagents-mongodb` avec `docker compose up -d` dans le dossier de cet autre projet (je n'ai pas lu ce fichier, qui est hors dépôt). Correctif dans le lab : `name: lab05-aiagent-sessions` et `container_name: lab05-mongodb` ; la règle est ajoutée au manuel (§8) pour Lab07/Lab12, qui ont aussi un dossier `MongoDB/`.
2. **Pas de connecteur MongoDB stable** pour `Microsoft.Extensions.VectorData` : SK `1.74.0-preview` seulement, et `CommunityToolkit.VectorData.CosmosMongoDB` cible Azure Cosmos DB. Un historique n'a pas besoin de recherche vectorielle : le provider utilise directement le driver officiel, comme les providers intégrés à MAF le font avec le SDK natif de leur base (`CosmosChatHistoryProvider` → `CosmosClient`).
3. **Phase 0 CommonUtilities non réalisable avec Lab05 seul.** Monter `MongoDB.Driver` en 3.x dans `CommonUtilities` casserait Lab07/Lab12 (`NU1605`, 2.30.0 en direct), ce qui aurait voulu dire modifier d'autres labs. Lab05 référence donc 3.12.0 en direct (la version la plus haute gagne) et n'utilise pas `MongoDbHealthCheck`, compilé contre 2.x (la 3.0 a fusionné `MongoDB.Driver.Core` : compatibilité binaire non garantie). Il vérifie la connexion dans son `ConfigurationHelper`.
4. **Ordre des messages du sample officiel** : `Agent_Step04` donne le même `Timestamp` à tous les messages d'un run ; avec un tri décroissant puis `Reverse()`, la réponse peut précéder la question. Correctif : un tick par message (vector store) et un champ `Order` (MongoDB, dates à la milliseconde).
5. **Coquille Learn** : les pages Session/Storage écrivent `agent.SerializeSession(session)`, alors que l'API est `SerializeSessionAsync` (sample + `PublicAPI.Shipped.txt`). Le lab suit l'API.
6. **Samples officiels sur Foundry** (`AIProjectClient`) : seuls les patterns session/provider ont été repris sur le `ChatClient` Azure OpenAI v1, comme pour Lab01–03.
7. **Session parallèle sur le dépôt** : une autre session migrait Lab04 en même temps. Les fichiers partagés (`labs.json`, `LabCatalogTests`, READMEs, manuel, plan) ont été modifiés par remplacements ciblés, et les entrées lab04/lab05 coexistent. Pour la même raison, les runs du dashboard ont été faits sur une **instance séparée** (port 5058, historique dans un dossier temporaire hors dépôt) : mon premier run, lancé sur l'instance de l'autre session, a été interrompu quand elle l'a arrêtée, et il n'a laissé aucune trace dans son historique.
8. **`dotnet test -nologo`** : avec le runner Microsoft.Testing.Platform (`global.json`), `-nologo` est transmis à l'application de test, qui ne lance alors aucun test (exit 5, « Zero tests ran »). `dotnet test` sans cette option fonctionne. Aucun changement n'a été fait dans le dashboard ; une modification d'essai du `.csproj` de test a été annulée.

## 7. Décisions à valider

- **Nom du lab** : titre « AI Agent with Sessions » dans le README, le dashboard et le README racine. En revanche, **dossier et projet inchangés** (`Lab05-AIAgentWithThreads`, `AIAgentWithThreads.csproj`) pour garder les chemins et liens existants. Renommer le dossier reste possible (chemins à mettre à jour dans `labs.json`, le README racine et le plan).
- **MongoDB via `MongoDB.Driver` 3.12.0** plutôt qu'un connecteur `VectorData` en préversion (règle « stable uniquement »).
- **`CommunityToolkit.VectorData.InMemory`** : package de la .NET Foundation / Microsoft, utilisé par les samples officiels. Je le considère comme « officiel » au sens de la règle 12.
- **Ajout du scénario 1** (historique par défaut dans la session), justifié par `Agent_Step03_PersistedConversations` et la page Learn « Storage ».
- **Suppressions** : configuration de l'agent dans `appsettings.json`, Mongo Express et ses identifiants, MongoDB sans authentification (local, lié à `127.0.0.1`).
- **Questions de la conversation** modifiées pour être vérifiables (prénom « Ada »).
- **Phase 0 CommonUtilities reportée** à Lab07/Lab12 (§6.3).

## 8. Points pour les prochains labs

- **Lab09, Lab12** : réutiliser §4.11 du manuel (`CreateSessionAsync`, `SerializeSessionAsync`/`DeserializeSessionAsync`, `StateBag`, `ProviderSessionState<T>`). Pour Lab12, les `AIContextProvider` suivent le même principe « provider sans état, état dans la session » (`StateKeys` uniques par provider, sinon exception de `ChatClientAgent`).
- **Lab07** (RAG) : `CommunityToolkit.VectorData.InMemory` est la voie stable (sample `AgentWithRAG_Step01_BasicTextRAG`). MongoDB y demande une vraie recherche vectorielle : il n'existe pas de connecteur stable, il faut décider (InMemory ou exception préversion).
- **Tout `docker-compose.yml`** d'un lab : `name:` de projet et `container_name` propres au lab, image à version figée, port lié à `127.0.0.1` (§8 du manuel).
- **Phase 0** : avec Lab07 ou Lab12, monter `CommonUtilities` en `MongoDB.Driver` 3.12.0 et retirer les 2.30.0 directs, ou sortir `MongoDbHealthCheck` dans un projet dédié.
- **Checks du dashboard** : pour les labs à mémoire, vérifier une information que seul l'historique donne, et une clé réaffichée identique (référence arrière `\1`).

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire. Le catalogue est piloté par `labs.json`, et l'étiquetage des usages par scénario couvre les 3 blocs `Token Usage (follow-up):`. Seule contrainte : le scénario 3 a besoin du conteneur MongoDB. Sans lui, le run Solution échoue avec le message clair du §5-8b, ce qui est documenté dans `Dashboard/README.md` et dans le résumé du lab.

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab05` : métadonnées EN + FR (niveau *Intermediate*), projets Start/Solution, timeout 180 s, **11 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab05 sur une **sortie réelle** de la Solution (tous passent) et sur le Start (seul `config` passe) ; conversation « sans historique » (aucun check ne passe) ; 3 rapports de tokens étiquetés par scénario (total 300) |
| Documentation | `Dashboard/README.md` : labs enregistrés, prérequis MongoDB, conseil de checks pour les labs à sessions, liste des tests |

**Choix des checks** :

| Check | Preuve |
|---|---|
| `scenario1-history` | `Messages in the session: 2 (user, assistant)` — valeur déterministe lue par `GetMessages(session)` |
| `scenario1-serialized` | JSON `stateBag` → `InMemoryChatHistoryProvider` → `messages` |
| `scenarioN-followup` | après la restauration, une ligne `Agent:` contenant **Paris** et **Ada** (lookaheads) |
| `scenario2-key` | la clé affichée est exactement celle du JSON, et le JSON ne contient **que** `sessionDbKey` (structure complète, référence arrière `\1`) |
| `scenario3-restart` | la clé du JSON sérialisé est celle qu'affiche le nouvel agent après restauration (`\1`) |
| `scenarioN-usage` | usage du scénario, borné à l'en-tête suivant |

**Résultats** :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ 83/83 (dont les 3 tests Lab05 et ceux ajoutés en parallèle pour Lab04) |
| API : `GET /api/labs` | ✅ 5 labs (lab01 → lab05) |
| API : détails, README rendu, packages, diff de solution | ✅ README rendu (38 Ko HTML) ; 8 packages listés ; seul `Program.cs` diffère |
| Run Lab05 **Solution** via l'API | ✅ `passed` — « All 11 checks passed », exit 0, 7,7 s ; 3 rapports `Scenario N · Token Usage (follow-up)` (102 / 96 / 102, total 300) |
| Run Lab05 **Start** livré | ✅ `failed` attendu — « 10 of 11 check(s) failed », seul `config` passe ; exit 0 ; aucun usage |

Aucun artefact laissé dans le dépôt : l'instance de test (port 5058) écrivait son historique dans un dossier temporaire hors dépôt, et elle est arrêtée ; `Dashboard/.data/history.json` (git-ignoré) ne contient aucun run Lab05.
