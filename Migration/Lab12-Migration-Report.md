# Rapport de migration — Lab12-AIAgentWithAIContextProvider

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.18 ajouté, §1, §3, §5, §8), [Migration-Plan.md](Migration-Plan.md), rapports [Lab01](Lab01-Migration-Report.md) à [Lab11](Lab11-Migration-Report.md) (surtout [Lab05](Lab05-Migration-Report.md) pour les sessions, `ProviderSessionState<T>` et MongoDB, [Lab02](Lab02-Migration-Report.md) pour la sortie structurée, [Lab09](Lab09-Migration-Report.md) pour le lab interactif et [Lab11](Lab11-Migration-Report.md) pour le code de l'apprenant placé en bas de `Program.cs`). Sources officielles (tag `dotnet-1.22.0`) : samples `01-get-started/04_memory`, `02-agents/Agents/Agent_Step17_AdditionalAIContext`, `02-agents/AgentWithMemory/AgentWithMemory_Step02_MemoryUsingMem0` et `_Step08_MemoryUsingCosmosNoSql` ; sources `AIContextProvider.cs`, `MessageAIContextProvider.cs`, `ProviderSessionState{TState}.cs`, `ChatClientAgent.cs`, `Mem0Provider.cs`, `ChatHistoryMemoryProvider.cs` ; `PublicAPI.Shipped.txt` de `Microsoft.Agents.AI(.Abstractions)` ; pages Learn [Context Providers](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/context-providers?pivots=programming-language-csharp) (2026-09-09) et [Context provider integrations](https://learn.microsoft.com/agent-framework/integrations/by-component/context-providers/?pivots=programming-language-csharp) (2026-08-25). Comportements **vérifiés hors ligne** (`IChatClient` factice) puis **en exécution réelle** contre Azure OpenAI (clé API et Entra ID) et MongoDB 8.0.

**Statut : migré.** Tous les critères du manuel (§7) sont remplis, y compris l'exécution réelle de la Solution par les deux voies d'authentification et les runs du dashboard (Start `failed` attendu, Solution `passed`, tokens affichés). La **phase 0 `CommonUtilities`** est faite avec ce lab : les warnings NU1902/NU1903 hérités ont disparu de tous les labs AzureOpenAI. Un point de procédure est signalé au §6.6 (affichage de l'hôte de l'endpoint dans ma session, jamais de la clé).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AIAgentWithAIContextProvider.csproj` | Socle Lab01–11 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé, paquets commentés ; **`MongoDB.Driver` 3.12.0**. **Supprimés** : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251204.1`, `Microsoft.Extensions.Hosting 9.0.0`, `MongoDB.Driver 2.30.0` | Version stable unique, aucune préversion ; driver 3.x comme Lab05 (graphe sans paquet vulnérable) |
| `AzureOpenAISettings.cs`, `MongoDbSettings.cs`, `ConfigurationHelper.cs` (racine, Start = Solution) | Copies du socle Lab05 (namespace `AIAgentWithAIContextProvider`) : user-secrets, validation au démarrage, `ConnectToMongoDbAsync()` (ping, `ServerSelectionTimeout` 5 s, message actionnable) | Remplacent `Configuration/AzureOpenAISettings.cs`, `Configuration/MongoDbConfiguration.cs`, `Host.CreateApplicationBuilder()` et `MongoDbHealthCheck` |
| `appsettings.json` | `APIKey` retiré ; `MongoDb` = `mongodb://localhost:27018` (sans identifiants), base `AIAgentContext` ; `UserMemoriesCollectionName` retiré (constante du dépôt) | Aucun secret versionné (l'ancienne chaîne contenait `admin:password123`) |
| `MongoDB/docker-compose.yml` | `mongo:8.0`, **nom de projet `lab12-aiagent-memory`**, `container_name: lab12-mongodb`, port `127.0.0.1:27018`, sans authentification, volume nommé ; **Mongo Express supprimé** | Règle du manuel §8 (l'ancien fichier prenait le nom de projet `mongodb`, celui de l'incident Lab05) ; 27018 pour tourner à côté de Lab05 ; plus d'identifiants dans le dépôt |
| `UserMemoriesRepository.cs` (réécrit, Start = Solution) | Collection `user_memories`, un document par souvenir (`UserId`, `Memory`, `CreatedAt`) ; `GetMemoriesAsync(userId)` (ordre d'insertion), `AddMemoriesAsync(userId, memories)`, `DeleteMemoriesAsync(userId)` (« forget me ») ; `CancellationToken` partout | L'id utilisateur est un paramètre de l'application, plus un champ rempli par le modèle |
| `ExtractedMemories.cs` (**nouveau**) | Type de sortie structurée de l'extracteur : `List<string> Memories` avec `[Description]` | Remplace `List<UserMemory>` (dont le `UserId` était écrit par le modèle, §3.3) |
| `MemoryConsole.cs` (**nouveau**) | `WriteMemoriesProvided`, `WriteMemoriesExtracted` (coût de l'extraction), `WriteNothingToExtract`, `WriteStoredMemories`, `WriteTokenUsage` ; `using static` | Helpers d'affichage fournis (pattern `SessionConsole.cs` de Lab05) : les lignes `[Memory]` rendent visibles les deux hooks du provider |
| Supprimés | `IUserMemoriesRepository.cs` (une seule implémentation, règle 12), `Models/UserMemory.cs`, `Models/UserMemoryDocument.cs` (devenu type privé du dépôt), `Configuration/` (2 fichiers), `UserMemoryAIContextProvider.cs` (**déplacé** en bas de `Program.cs`) | Règle 9 : l'ancien Start faisait écrire `UserMemoryAIContextProvider.cs` (Start ≠ Solution hors `Program.cs`) |
| `Solution/Program.cs` | Réécrit : client `OpenAIClient` v1 (clé API ou Entra ID) ; section partagée (MongoDB, extracteur `AsAIAgent`, agent avec `ChatClientAgentOptions { Name, ChatOptions.Instructions, AIContextProviders }`) ; **S1** scénarisé (effacement, 1ʳᵉ session, souvenirs stockés, **nouvelle** session, usage) ; **S2** conversation d'origine (`RunAsync(session)` sans message puis boucle `ReadLine`) ; classe `UserMemoryAIContextProvider : AIContextProvider` (`ProvideAIContextAsync`, `StoreAIContextAsync`, `ProviderSessionState<State>`, `StateKeys`, `State(UserId)`) | API 1.22.0 et patterns des samples officiels (§3) |
| `Start/Program.cs` | TODO **1 → 16** (setup 1–3 ; agent 4–6 ; S1 7–10 ; S2 11–12 ; provider 13–16) ; constantes, en-têtes et prompts fournis et **utilisés** hors TODO (pas de `CS0219`) ; squelette du provider fourni (constructeur, état, `StateKeys`) dont les deux méthodes renvoient `await base.…` en attendant les TODO | Compile sans warning et s'exécute tel que livré (exit 0, ni réseau ni MongoDB) |
| `README.md` (lab) | Réécrit sur le gabarit Lab05/Lab09 : objectif, configuration, démarrage de MongoDB, fichiers fournis (pourquoi aucun champ de session dans le provider), tableaux des 16 TODO (chaque indice contient le code de la Solution) + boucle et deux méthodes complètes, « How an AIContextProvider works » (schéma, comparatif `ChatHistoryProvider` / `AIContextProvider`, ce que reçoit le provider, coût mesuré), concepts, namespaces, sortie réelle, dépannage, **Security**, paquets, « Going further », encadré « Coming from an older version », liens (tous HTTP 200) | L'ancien README enseignait `CreateAIAgent`, `ChatClientAgent`, `AIContextProviderFactory`, les surcharges `InvokingAsync`/`InvokedAsync`, `GetNewThread`, `AgentRunResponse`, une clé dans `appsettings.json` et `docker-compose` + Mongo Express |
| `CommonUtilities/MongoDbHealthCheck.cs`, `CommonUtilities.csproj`, `README.md` | **Phase 0** : classe supprimée, `MongoDB.Driver 2.30.0` retiré (le projet n'a plus aucune dépendance de paquet), README mis à jour | Plus aucun lab ne l'utilisait après Lab12 (§7) ; source des NU1902/NU1903 de tous les labs |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab12` (EN + FR, *Intermediate*, **`"interactive": true`**, timeout 240 s, **7 checks**) ; 83 lignes ajoutées, rien d'autre modifié | Intégration au dashboard (§9) |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 4 tests Lab12 (dont une théorie à 4 cas) dans `OutputAnalyzerTests` | Idem |
| `Dashboard/README.md`, `README.md` (racine) | Lab12 enregistré / migré ; note « conteneur MongoDB + lab interactif » ; conseil de checks pour les labs à context provider ; exemple « Add a lab » passé sur MAS-Lab01 ; scénarios du lab ; ports MongoDB 27017/27018 ; liste des tests | Documentation associée |
| `Migration/Migration-Manual.md` | §1 (`MongoDB.Driver`), §3 (lignes Lab12 ✅, `AIContextProviderFactory`, méthodes non virtuelles), §4.6, **§4.18** « `AIContextProvider` : mémoire à long terme », §5 (3 lignes + `MongoDB.Driver 2.30.0` résolu), §8 (CommonUtilities résolu, Docker Compose, checks des labs à provider) | Référence pour les labs suivants |
| `Migration/Migration-Plan.md` | Statut (vague 4 terminée, phase 0 faite), §2, §4, ligne Lab12 (§5), pratique Lab12 (§5 bis) | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `ChatClientAgent x = chatClient.CreateAIAgent(...)` | `AIAgent x = chatClient.AsAIAgent(...)` |
| `ChatClientAgentOptions.AIContextProviderFactory = _ => new Provider(...)` | `ChatClientAgentOptions.AIContextProviders = [new Provider(...)]` — **`CS0117`** sur l'ancien code (vérifié) |
| `public override ValueTask<AIContext> InvokingAsync(InvokingContext, …)` | `protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext, …)` — **`CS0506`** sur l'ancien code (vérifié) |
| `public override ValueTask InvokedAsync(InvokedContext, …)` | `protected override ValueTask StoreAIContextAsync(InvokedContext, …)` (non appelée si le run a échoué) |
| Champs `_userId`, `_userMemories` dans le provider | `ProviderSessionState<State>` (`GetOrInitializeState(session)`), `stateInitializer`, `override StateKeys` |
| `AgentThread thread = agent.GetNewThread();` / `RunAsync(thread)` | `AgentSession session = await agent.CreateSessionAsync();` / `RunAsync(session)` |
| `AgentRunResponse` / `AgentRunResponse<List<UserMemory>>` | `AgentResponse` / `AgentResponse<ExtractedMemories>` (`.Result`, `.Usage`) |
| `RunAsync<T>` sur `ChatClientAgent` | `RunAsync<T>` sur `AIAgent` (Lab02) |
| `IUserMemoriesRepository` + `UserMemoriesRepository(db, collectionName)` | `UserMemoriesRepository(db)` (collection constante) |
| `MongoDbHealthCheck.EnsureMongoDbIsRunningAsync` (CommonUtilities, `Environment.Exit(1)`) | `ConfigurationHelper.ConnectToMongoDbAsync()` (exception avec message actionnable) |
| `Host.CreateApplicationBuilder().Configuration` | `ConfigurationBuilder` (`AppContext.BaseDirectory`) + user-secrets + variables d'environnement |

Toutes ces API MAF sont dans `PublicAPI.Shipped.txt` 1.22.0, sans `[Experimental]` (seuls les constructeurs de `InvokingContext`/`InvokedContext` le sont, `MAAI001`, non appelés). Dépendances résolues (Start = Solution, graphes identiques, 46 paquets) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `System.ClientModel` 1.15.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `MongoDB.Driver`/`MongoDB.Bson` 3.12.0 (→ `Snappier` 1.3.1, `SharpCompress` 0.48.1), `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. **Aucune préversion**, plus d'`Azure.AI.OpenAI` ni de `Microsoft.Extensions.Hosting` (seul `Hosting.Abstractions` reste, transitif via MAF).

## 3. Changements conceptuels

1. **Un provider par agent, sans état ; l'état dans la session.** L'ancien lab créait un provider par thread (`AIContextProviderFactory`) et gardait l'id utilisateur et les souvenirs chargés dans des champs (remplis par `InvokingAsync`, relus par `InvokedAsync`). En 1.22.0, `AIContextProviders` reçoit des instances partagées par toutes les sessions de l'agent ; la page Learn dit explicitement de ne garder **aucun** identifiant de mémoire dans un champ. Le lab enseigne le pattern des providers intégrés (`Mem0Provider`, `ChatHistoryMemoryProvider`) : la portée (l'id utilisateur) est dans `AgentSession.StateBag` via `ProviderSessionState<State>`, initialisée par un `stateInitializer` fourni par l'application ; les souvenirs sont relus depuis MongoDB dans `StoreAIContextAsync` au lieu d'être partagés par un champ (l'ancien champ `_userMemories` aurait mélangé deux sessions concurrentes).
2. **Deux méthodes à surcharger, la plomberie dans la classe de base.** `InvokingAsync`/`InvokedAsync` ne sont plus virtuelles : la base filtre les messages (seulement ceux passés à `RunAsync`, pas l'historique ni les messages d'autres providers — **vérifié hors ligne**), appelle `ProvideAIContextAsync`/`StoreAIContextAsync`, marque la provenance des messages fournis et fusionne l'`AIContext` (instructions ajoutées à celles de l'agent) ; `StoreAIContextAsync` est ignorée si le run a échoué. `InvokingCoreAsync`/`InvokedCoreAsync` (contrôle complet) sont cités en « Going further ».
3. **La portée vient de l'application, jamais du modèle.** L'ancien extracteur recevait *« The userId is: … »* et renvoyait `List<UserMemory>(UserId, Memory)` : le modèle décidait sous quel utilisateur stocker. Le nouvel extracteur ne renvoie que du texte (`ExtractedMemories`), l'id vient de la session. Section *Security* du README : injection indirecte depuis le stockage (avertissement de la doc `AIContextProvider`), données personnelles, « forget me ».
4. **Scénario 1 ajouté (scénarisé, déterministe).** Justification : c'est le déroulé du sample officiel `04_memory` (une session qui crée la mémoire, puis une **nouvelle** session qui la réutilise) et du sample `AgentWithMemory_Step02` (*« Start a new session that shares the same Mem0 scope »*) ; aucune API nouvelle. La nouvelle session a un historique vide : le prénom **Ada** et ses préférences ne peuvent venir que du provider, ce que vérifient les checks du dashboard (impossible avec la seule conversation libre de l'ancien lab).
5. **Scénario 2 = l'exercice d'origine.** L'agent parle en premier (`RunAsync(session)` sans message : le provider est appelé, `StoreAIContextAsync` ne voit aucun message utilisateur — vérifié), puis boucle de conversation ; un second lancement avec `scenariosToRun = [2]` montre la persistance entre deux exécutions du programme.
6. **Ce que fait le provider est montré, pas supposé.** Lignes `[Memory]` écrites pendant le run (souvenirs ajoutés avant l'appel du modèle ; souvenirs extraits après, avec le coût de l'extraction) ; mesure réelle : 119 tokens d'entrée avec 5 souvenirs, 71 sans ; ~250 tokens par extraction (un second appel modèle par message, inclus dans la latence de `RunAsync`).

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Azure.Identity` beta, MAF préversion, `Microsoft.Extensions.Hosting` : remplacés (§2).
- `AIContextProviderFactory`, surcharges `InvokingAsync`/`InvokedAsync`, `ChatClientAgent`, `AgentThread`/`GetNewThread`, `AgentRunResponse` : remplacés ; cités seulement dans l'encadré « Coming from an older version ».
- **`CommonUtilities.MongoDbHealthCheck`** et `MongoDB.Driver 2.30.0` de `CommonUtilities` : supprimés (phase 0, §7) ; remplacés par `ConnectToMongoDbAsync()` du lab.
- `IUserMemoriesRepository`, `Models/`, `Configuration/` : supprimés ou fusionnés (§1).
- **Mongo Express** et identifiants MongoDB (`admin/password123`, `admin/admin`) : supprimés, remplacés par une commande `mongosh` dans le README.
- `WithSpinner()` autour des runs : retiré (le provider écrit sur la console pendant le run, cf. Lab11).
- Exemple d'instruction de l'extracteur : *« The user's name is Ada. »* remplacé par *« The user works as a nurse. »* (§6.1).
- Aucun objectif pédagogique supprimé : provider personnalisé, extraction en sortie structurée, persistance MongoDB et conversation conservés ; S1 ajouté.

## 5. Tests effectués et résultats

Environnement : macOS (culture fr-FR), SDK .NET 10.0.402, variables `AzureOpenAI__*` de l'environnement (présence vérifiée, valeurs jamais affichées : sorties filtrées — hôte remplacé, occurrences de la clé et de `eyJ` comptées), `az login` valide, déploiement `gpt-4o-mini` (France Central), Docker 29.1.3, MongoDB 8.0 (conteneur du lab `lab12-mongodb`).

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build) | ✅ Start et Solution compilent (préversions) ; Start : **`CS0219`** (`userId`) ; NU1902/NU1903 sur le projet du lab **et** sur `CommonUtilities` (7 et 6 warnings) |
| 0b | Baseline fonctionnelle (ancienne Solution reconstituée depuis git dans `.tmp-baseline`, ancien `CommonUtilities`, base MongoDB `Lab12Baseline` supprimée ensuite) | ✅ fonctionne **depuis son dossier seulement** : lancée d'ailleurs → `ArgumentException: Value cannot be empty (Parameter 'name')` (`Host.CreateApplicationBuilder` lit `appsettings.json` dans le dossier courant) ; mémoire retrouvée au 2ᵉ lancement ; ⚠️ **doublons** stockés (un souvenir « tout-en-un » + les 4 faits séparés) et `userId` recopié par le modèle |
| 0c | Sondes hors ligne (`IChatClient` factice, `.tmp-probe`) | ✅ messages vus par `ProvideAIContextAsync` / `StoreAIContextAsync` = ceux du run seulement ; état `{"userId":"ada"}` dans `stateBag.<clé>` après `SerializeSessionAsync`, relu après `DeserializeSessionAsync` ; même clé d'état sur deux providers → `InvalidOperationException: Multiple providers use the same state key …` ; ancien code compilé contre 1.22.0 → `CS0506` (×2), `CS0117` |
| 0d | Sonde d'extraction (3 runs par variante) | Message système « faits connus » suivi d'une liste vide → **0 fait** (4 runs sur 4, dans la Solution) ; avec `(none)` → 5 faits (3/3) ; faits connus + question → 0 (3/3) ; faits connus + fait nouveau → 1 (3/3) ; faits connus + présentation répétée → 0 (3/3) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances (`--include-transitive`) | ✅ MAF 1.22.0, OpenAI 2.13.0, MEAI 10.10.0, MongoDB 3.12.0 ; aucune préversion ; pas d'`Azure.AI.OpenAI` ; graphes Start et Solution **identiques** (46 paquets) |
| 3 | Vulnérabilités (`--vulnerable --include-transitive`) | ✅ **« has no vulnerable packages »** (Start et Solution) — mieux que la baseline |
| 4 | `dotnet build -warnaserror --no-incremental` Start + Solution | ✅ `0 Warning(s) 0 Error(s)` **sans** `-nowarn` (plus aucun NU19xx) |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent(`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `GetNewThread`, `DeserializeThread`, `ChatMessageStore`, `AIContextProviderFactory`, `InvokingAsync`, `InvokedAsync`, `ChatClientAgent\b`, `Microsoft.Extensions.Hosting`, `Host.CreateApplicationBuilder`, `NoWarn`, `"APIKey"`, `MongoDbHealthCheck`, `IUserMemoriesRepository`, `password123`, Mongo Express, `docker-compose `, `.NET 8`, `preview.25`, `2.7.0-beta`, `1.18.0-beta`, `Running Scenario`, `MEAI001`, `.ToString()`, `UserMemory(`, `_userMemories`, `_userId`) | ✅ 0 occurrence dans `Start/` et `Solution/` ; README : 0 hors de l'encadré « Coming from an older version » (11 lignes dedans) |
| 6 | Run Solution — **clé API** (saisie S2 : *« I also play the piano every evening. »* puis `exit`) | ✅ S1 : `cleared`, `No memory … yet`, **5 souvenirs** extraits (nom, métier, ville, randonnée, végétarienne), MongoDB 5, nouvelle session : `5 memories … added`, réponse « Hi Ada! … vegetarian stir-fry … » ; usage 119 / 89 / 208 ; S2 : accueil « Hello, Ada! », `No user message in this run`, 1 souvenir extrait (piano) ; **0 occurrence de la clé** ; exit 0 |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`) | ✅ même comportement ; usage 119 / 100 / 219 ; S2 : 1 souvenir (*works remotely on Fridays*) ; aucune clé ni jeton (`eyJ`) ; exit 0 |
| 7b | Contenu MongoDB (`mongosh`) | ✅ un document par souvenir, `UserId: 'ada'`, ordre d'insertion |
| 8 | Configuration absente / placeholder (variables retirées, `HOME` isolé dans un dossier `.tmp-` du lab, supprimé) | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set "AzureOpenAI:Endpoint" <value>', or with the environment variable 'AzureOpenAI__Endpoint'.` ; idem `ChatDeploymentName` |
| 8b | MongoDB arrêté (`docker stop lab12-mongodb`) | ✅ en 5 s : `MongoDB is not reachable with the connection string of 'MongoDb:ConnectionString'. Start it with 'docker compose up -d' in the MongoDB folder of the lab.` (conteneur redémarré ensuite) |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche configuration, instructions et en-têtes, **exit 0**, ni réseau ni MongoDB |
| 10 | Start complété **uniquement d'après les indices du README** (copie `.tmp-start-check` dans le dossier du lab ; TODO 1–11 remplacés **par script** à partir des cellules des tableaux, TODO 12 et les deux méthodes à partir des blocs de code ; supprimée ensuite) | ✅ 0 TODO restant, `-warnaserror` sans warning, **code identique à la Solution** (une fois commentaires et espaces retirés), même comportement réel (5 souvenirs, Ada, 119 tokens d'entrée), 7/7 checks |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (9 fichiers partagés) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–16 a un indice qui contient le code de la Solution (prouvé par le test 10) ; sortie attendue issue d'un run réel ; liens vérifiés (HTTP 200) |
| 13 | Non-régression **`CommonUtilities`** (34 autres projets du dépôt recompilés `--no-incremental`, avant / après) | ✅ aucune nouvelle erreur ni nouveau warning ; NU1902/NU1903 venant de `CommonUtilities` **disparus** de tous les labs AzureOpenAI (de 2 à 6 warnings par projet → 0, `CommonUtilities` lui-même 4 → 0) ; restent : `CS0219` ×4 de Lab05 Start (préexistant, manuel §4.15), 4 `CS1061` de MAS-Lab01 Start (préexistant, non migré), NU1902 `OpenTelemetry.Api` 1.13.1 de MAS-Lab02/03 (leur propre dépendance) ; dashboard : 11 warnings inchangés |

## 6. Problèmes rencontrés

1. **Extraction vide au premier message** : le prompt *« Facts already known about the user (do not extract them again): »* suivi d'une liste **vide** faisait renvoyer `{"memories":[]}` à `gpt-4o-mini` pour la présentation (4/4), alors que le même message sans ce préfixe donnait les 5 faits. Corrigé par `knownMemories.DefaultIfEmpty("(none)")` (3/3 et toutes les exécutions suivantes) ; l'exemple d'instruction qui reprenait la donnée de test (*« The user's name is Ada. »*) a aussi été remplacé. Documenté dans le dépannage et le manuel (§4.18).
2. **Check trop strict** : au 5ᵉ run, la réponse de la nouvelle session proposait un *« vegetable stir-fry with tofu »* sans le mot *vegetarian* → `scenario1-recall` en échec dans le dashboard. Le check exige désormais **Ada** et **l'un** des détails que seule la mémoire donne (`vegetarian|tofu|Alps|Lyon`), présents dans les 5 réponses réelles et absents de la réponse sans mémoire ; une théorie de tests couvre les deux cas.
3. **L'ancienne Solution ne tournait que depuis son dossier** (§5 test 0b) : `Host.CreateApplicationBuilder` prend le dossier courant comme racine de contenu. Le socle (`AppContext.BaseDirectory`) corrige ce point.
4. **Doublons dans l'ancien stockage** : sans liste des faits connus correcte ni filtrage, l'ancien lab stockait un souvenir global plus les faits séparés. Le nouveau provider passe les faits connus à l'extracteur et ignore ceux déjà stockés à l'identique.
5. **Coquille Learn** : la page « Context Providers » écrit `public override string StateKey => …` ; l'API est `public override IReadOnlyList<string> StateKeys` (samples, `PublicAPI.Shipped.txt`). Le lab suit l'API.
6. **Affichage de l'hôte de l'endpoint (procédure)** : en inspectant le premier événement `result` du dashboard (journal complet du run), j'ai affiché une fois dans ma session la ligne `Endpoint: https://<ressource>…` (valeur issue de `AzureOpenAI__Endpoint`). Ce n'est pas la clé API (0 occurrence dans tous les flux, vérifié par comptage), mais la consigne demandait de ne pas afficher les variables `AzureOpenAI__*` : toutes les sorties suivantes ont été filtrées. Rien n'a été écrit dans le dépôt.
7. **Dashboard de l'utilisateur en cours d'exécution** (PID 15339, 127.0.0.1:5057, depuis `bin/Debug`) : comme pour Lab11, `dotnet test` a recompilé `LabDashboard.dll` sans changement de code ; le processus est resté sain (`GET /api/labs` → 200) et ne verra Lab12 qu'après redémarrage. Vérifications faites sur une **instance séparée** (port 5065, historique dans `Dashboard/.data/lab12-verification`, reporting désactivé), arrêtée et supprimée ensuite.
8. **Docker** : d'autres projets locaux ont des conteneurs MongoDB (dont `aiagents-mongo-express`, celui de l'incident Lab05) : l'ancien `docker-compose.yml` (projet implicite `mongodb`) n'a **pas** été lancé ; seul le nouveau projet `lab12-aiagent-memory` a été créé (conteneur `lab12-mongodb` et volume `lab12-aiagent-memory_mongodb_data`, laissés en place pour vos essais : `docker compose down -v` dans `MongoDB/` les supprime).
9. **Sortie de la conversation** : l'invite `You: ` (sans retour à la ligne) est suivie, dans un flux redirigé (dashboard, `dotnet run < fichier`), de la ligne suivante du programme ; les checks n'ancrent donc pas les lignes `[Memory]` de la conversation, seulement celles du greeting.
10. **Fichiers hors dépôt** : aucun lu ni écrit, hors caches `dotnet`/NuGet. Le dépôt officiel (tag `dotnet-1.22.0`, `dotnet/samples` + `dotnet/src`) a été cloné dans `<lab>/.tmp-probe` (ignoré), avec les sondes, supprimé en fin de session ; pages Learn lues par requête HTTP.

## 7. Décisions à valider

- [x] **Phase 0 — option « suppression »** : `MongoDbHealthCheck` (classe publique de `CommonUtilities`) et `MongoDB.Driver 2.30.0` supprimés plutôt que montés en 3.x ou déplacés dans un projet dédié. Raisons : plus aucun lab ne l'utilisait (Lab05 et Lab12 vérifient la connexion dans leur `ConfigurationHelper`), `Environment.Exit(1)` dans une bibliothèque partagée, et tous les labs héritaient d'une dépendance vulnérable. Non-régression : §5 test 13. *(validé le 2026-10-25)*
- [x] **Scénario 1 ajouté** (scénarisé, deux sessions) ; la conversation d'origine devient le scénario 2 ; lab **interactif** dans le dashboard (comme Lab09) — ses checks lisent S1 et l'accueil de S2, pas les messages saisis. *(validé le 2026-10-25)*
- [x] **Provider en bas de `Program.cs`** (règle 9, comme le handler de Lab11), squelette fourni (constructeur, `ProviderSessionState`, `StateKeys`, `State`) ; l'apprenant écrit les deux méthodes et le `stateInitializer`. *(validé le 2026-10-25)*
- [x] **Extraction par un `AIAgent` + `RunAsync<T>`** (règle 3, continuité Lab02 et ancien lab) plutôt que par un `IChatClient` + `GetResponseAsync<T>` comme le sample `04_memory` ; même résultat, l'agent est appelé sans session. *(validé le 2026-10-25)*
- [x] **Souvenirs injectés en `Instructions`** (comme `04_memory` et l'ancien lab) ; `Messages`, outils et `MessageAIContextProvider` en « Going further ». *(validé le 2026-10-25)*
- [x] **Exceptions du provider propagées** à `RunAsync` (erreur visible pendant un lab), alors que les providers intégrés journalisent et continuent — expliqué en « Going further ». *(validé le 2026-10-25)*
- [x] **Suppression d'`IUserMemoriesRepository`** (une implémentation, règle 12) ; `UserMemoryDocument` devient privé au dépôt. *(validé le 2026-10-25)*
- [x] **MongoDB** : conteneur dédié sur `127.0.0.1:27018` sans authentification, Mongo Express et identifiants supprimés, base `AIAgentContext`, collection `user_memories` ; id utilisateur lisible `ada` ; S1 efface les souvenirs de `ada` à chaque exécution (résultat reproductible). *(validé le 2026-10-25)*
- [x] **Titre** « AI Agent with an AI Context Provider (Long-Term Memory) » ; dossier et projet inchangés ; niveau *Intermediate*, timeout 240 s. *(validé le 2026-10-25)*
- [x] **Check de rappel tolérant** (un détail parmi `vegetarian|tofu|Alps|Lyon`, en plus du prénom) ; `scenario1-stored` exige *vegetarian* dans les souvenirs (stable sur toutes les extractions observées). *(validé le 2026-10-25)*

## 8. Points pour les prochains labs

- **MAS-Lab01/02/03** (vague 5) : `CommonUtilities` n'apporte plus aucun paquet ; leurs warnings NU1902 viennent de leur propre `OpenTelemetry.Api` 1.13.1 (MAF préversion), et MAS-Lab01 Start a 4 erreurs `CS1061` préexistantes. L'exemple « Add a lab » du `Dashboard/README.md` vise désormais MAS-Lab01.
- **Lab05 Start** : les 4 `CS0219` (constantes inutilisées hors TODO) restent à corriger lors d'un passage ultérieur (manuel §4.15).
- **Checks du dashboard sur une réponse du modèle** : prévoir plusieurs formulations (alternatives dans un lookahead) et vérifier en priorité ce qui est déterministe (lignes écrites par le code du lab).
- **Prompts d'extraction / de contexte** : ne jamais laisser une liste vide après un titre (« (none) ») ; éviter les exemples qui reprennent les données de test.
- **Streaming** : toujours à re-tester (Lab01 scénario 5, Lab02, Lab06), cf. manuel §8 — non concerné ici.

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire. Le lab lit `Console.ReadLine` (scénario 2) → `"interactive": true` (mécanisme existant, Lab09) ; il a besoin de son conteneur MongoDB (documenté dans `Dashboard/README.md` et le résumé du lab). Les lignes `[Memory] … (extraction run: 265 tokens)` ne sont pas lues comme un usage (test dédié) : seul le bloc `Token Usage (new session, with memories):` du scénario 1 l'est.

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab12` : métadonnées EN + FR, projets Start/Solution, timeout 240 s, `interactive`, **7 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab12 sur la **sortie réelle** de la Solution (Entra ID, saisie dans le chat ; tous passent) et du Start livré (seul `config`) ; provider non écrit (seuls `config` et `scenario1-usage`) ; souvenirs stockés mais jamais fournis au modèle (`config`, `scenario1-extracted`, `scenario1-stored`, `scenario1-usage`) ; théorie sur le check de rappel (4 cas) ; étiquetage de l'usage `Scenario 1 · Token Usage (new session, with memories)` (119 / 209) |
| Documentation | `Dashboard/README.md` : labs enregistrés, note Lab12 (MongoDB 27018, chat interactif), conseil de checks pour les labs à `AIContextProvider`, exemple « Add a lab », liste des tests |

**Choix des checks** (chacun échoue sur le Start livré et passe sur la Solution) :

| Check | Preuve |
|---|---|
| `config` | `Endpoint:` |
| `scenario1-provide-empty` | après `Memories of user 'ada' cleared (N deleted)` et `First session`, la ligne écrite par `ProvideAIContextAsync` : `[Memory] No memory of user 'ada' yet …` |
| `scenario1-extracted` | ligne écrite par `StoreAIContextAsync` : `[Memory] N memor(y\|ies) extracted and stored for user 'ada' (extraction run: N tokens):` puis un `  + …Ada…` avant la réponse |
| `scenario1-stored` | `Memories stored in MongoDB for user 'ada': N` (N ≥ 1) et, dans la liste, un souvenir contenant **Ada** et un contenant **vegetarian** (lookaheads) |
| `scenario1-recall` | après `New session (empty chat history)`, `[Memory] N memories of user 'ada' added to the instructions`, puis une réponse (multi-ligne, bornée par `Token Usage`) contenant **Ada** et l'un de `vegetarian\|tofu\|Alps\|Lyon` |
| `scenario1-usage` | usage du scénario 1, borné à l'en-tête suivant |
| `scenario2-greeting` | dans S2 : `[Memory] N memories … added`, puis `[Memory] No user message in this run: nothing to extract` (l'agent a parlé en premier), puis un accueil contenant **Ada** |

**Résultats** (instance de vérification sur le port 5065, historique dans `Dashboard/.data/lab12-verification` — git-ignoré — supprimé ensuite ; reporting désactivé ; `Dashboard/.data/history.json` de l'utilisateur non utilisé : 0 run Lab12) :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ **160/160** (152 + 8 cas Lab12 ; catalogue à 13 labs) |
| Validation des regex avec le moteur .NET sur les sorties complètes | ✅ 7/7 sur la Solution clé API, 7/7 sur la Solution Entra ID, 7/7 sur le Start complété, seul `config` sur le Start livré ; ≤ 2 ms par check |
| API : `GET /api/labs` | ✅ 13 labs (lab01 → lab12) |
| API : détails, README rendu, paquets, diff de solution | ✅ README rendu (47,5 Ko HTML) ; traduction FR (`Agent IA avec un AI Context Provider`) ; 7 paquets listés (MAF 1.22.0, Azure.Identity 1.21.0, Configuration 10.0.12, MongoDB.Driver 3.12.0) ; seul `Program.cs` diffère |
| Run Lab12 **Start** livré via l'API | ✅ `failed` attendu à l'étape `checks` — « 6 of 7 check(s) failed », seul `config` passe, exit 0, aucun usage, aucun avertissement de build |
| Run Lab12 **Solution** via l'API (saisie *« I play the piano every evening. »* puis **End input**) | ✅ **`passed`** — « All 7 checks passed », exit 0, 11,1 s, aucune ligne stderr, aucun avertissement de build ; tokens affichés : `Scenario 1 · Token Usage (new session, with memories)` 119 / 87 / 206 ; 0 occurrence de la clé dans le flux d'événements. Un premier run (avant l'assouplissement de `scenario1-recall`, §6.2) avait donné « 1 of 7 check(s) failed » |

Aucun artefact laissé : `.tmp-probe` (clone officiel, sondes), `.tmp-start-check`, `.tmp-home`, `.tmp-baseline` supprimés ; base `Lab12Baseline` supprimée ; instance de vérification arrêtée et son dossier supprimé, port 5065 libre.
