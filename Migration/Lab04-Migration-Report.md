# Rapport de migration — Lab04-AIAgentWithMCPClient (MCP Client)

> Date : 2026-09-27 · Cible : **Microsoft Agent Framework 1.22.0** · **ModelContextProtocol 2.2.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.10 ajouté), [Lab03-Migration-Report.md](Lab03-Migration-Report.md), samples officiels (tag `dotnet-1.22.0`) `02-agents/ModelContextProtocol/Agent_MCP_Server`, `Agent_MCP_PerRun_AuthHeaders`, `AgentProviders/foundry/Agent_Step09_UsingMcpClientAsTools`, `01-get-started/04_memory`, page Learn [Using MCP tools](https://learn.microsoft.com/agent-framework/agents/tools/local-mcp-tools?pivots=programming-language-csharp), documentation XML de `ModelContextProtocol.Core` 2.2.0.

**Statut : migré.** Tous les critères du manuel (§7) sont remplis. Réserve inchangée par rapport à Lab01–Lab03 : les warnings de vulnérabilité NuGet hérités de `CommonUtilities` (phase 0). Le test avec un **vrai** token Hugging Face n'a pas pu être fait (aucun token disponible) : seuls l'accès anonyme et un token invalide ont été testés (§5, §7).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AIAgentWithMCPClient.csproj` | Socle Lab03 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé ; `ModelContextProtocol` **0.5.0-preview.1 → 2.2.0** ; suppression de `Azure.AI.OpenAI 2.7.0-beta.2`, `Microsoft.Extensions.Hosting 9.0.0` et du `<NoWarn>MEAI001</NoWarn>` global (Solution) | Versions stables, aucune préversion ; `ModelContextProtocol` est le package du sample officiel et du `Directory.Packages.props` 1.22.0 ; aucun `NoWarn` global (règle 11) |
| `ConfigurationHelper.cs` | Version Lab03 (ConfigurationBuilder + user-secrets + env + validation) ; `GetMCPServerSettings` conservé et durci : endpoint obligatoire et `https://` absolu, token placeholder (`YOUR-…`) refusé avec message actionnable | Messages d'erreur clairs (§7-8) ; le token est un secret |
| `AzureOpenAISettings.cs` | Copie Lab03 (namespace `AIAgentWithMCPClient`) | Socle commun |
| `MCPServerSettings.cs` | `BearerToken` devient **facultatif** (`string?`), commentaires : secret, jamais dans `appsettings.json`, accès anonyme si vide | Le serveur MCP Hugging Face accepte l'accès anonyme (vérifié) |
| `appsettings.json` | `APIKey` et `BearerToken` retirés ; seuls endpoint, déploiement et URL MCP restent | Aucun secret versionné ; Start et Solution différaient (`"YOUR-HUGGINGFACE-TOKEN"` vs `""`) |
| `Models/HuggingFaceModel.cs` (Start = Solution) | `[Description]` sur les classes et chaque propriété | Copiées dans le schéma JSON de `RunAsync<T>` (même décision que Lab02) |
| `AgentConsole.cs` (**nouveau**, Start = Solution) | `WriteTokenUsage(AgentResponse)` (copie de Lab03) | Usage affiché dans 2 scénarios ; fichier fourni pour éviter `CS8321` dans le Start |
| `Solution/Program.cs` | Client `OpenAIClient` v1 (clé API ou Entra ID) ; connexion MCP déplacée dans le setup (`Name`, `StreamableHttp`, en-tête `Authorization` seulement si un token existe, `WithSpinner`) ; **S1 nouveau** : `ServerInfo` + `ListToolsAsync` ; **S2** (scénario d'origine) : `AsAIAgent(ChatClientAgentOptions { ChatOptions = { Instructions, Tools, MaxOutputTokens, Temperature } })`, `AgentResponse<T>`, liste des `FunctionCallContent` ; **S3 nouveau** : seul `hub_repo_search` donné à l'agent, comparaison de l'usage | APIs 1.22.0 / MCP 2.2.0, patterns des samples officiels |
| `Start/Program.cs` | TODO réécrits et numérotés **1 → 18** (setup 1–3 identique à Lab03, 4 = connexion MCP ; S1 5–7 ; S2 8–13 ; S3 14–18) ; chargement des settings MCP fourni | Compile sans warning et s'exécute tel que livré |
| `README.md` (lab) | Réécrit sur le gabarit Lab03 : configuration (Azure OpenAI + token HF facultatif en user-secrets), fichiers fournis, tableau des 18 TODO, **« How an agent uses MCP tools »**, concepts, namespaces, sortie attendue réelle, dépannage (401/404/429 MCP, `JsonException` si réponse tronquée), **sécurité des serveurs MCP tiers** (reprise de la page Learn), packages, « Going further », encadré « Coming from an older version », liens | L'ancien README présentait `AzureOpenAIClient`, `CreateAIAgent`, `AgentRunResponse<T>`, `ConfigureOptionsChatClient` et un token dans `appsettings.json` |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab04` (EN + FR, 9 checks), placée avant `azureopenai-lab05` | Intégration au dashboard (§9) |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 3 tests Lab04 dans `OutputAnalyzerTests` | Idem |
| `Dashboard/README.md`, `README.md` (racine) | Lab04 enregistré / migré ; 3 scénarios décrits ; prérequis « accès à huggingface.co/mcp » ; note sur le token HF (hors formulaire, jamais affiché) ; exemple « Add a lab » passé sur Lab08 | Documentation associée |
| `Migration/Migration-Manual.md` | §4.10 « Client MCP » ; 3 lignes dans la table §5 ; §3 : impact réel de #7774 et #8425 corrigé (aucun) | Référence pour Lab10 |
| `Migration/Migration-Plan.md` | Statut, vague 2, ligne Lab04, pratique §5 bis → ✅ | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `ChatClientAgent x = chatClient.CreateAIAgent(instructions, tools: …, clientFactory: c => new ConfigureOptionsChatClient(c, o => { o.MaxOutputTokens = 600; o.Temperature = 1; }))` | `AIAgent x = chatClient.AsAIAgent(new ChatClientAgentOptions { Name, ChatOptions = new ChatOptions { Instructions, Tools, MaxOutputTokens = 1000, Temperature = 0.2f } })` |
| `AgentRunResponse<HuggingFaceSearchResult>` | `AgentResponse<HuggingFaceSearchResult>` (`Result` inchangé) |
| `toolsInHuggingFaceMcp.Cast<AITool>().ToList()` | `[.. mcpTools.Cast<AITool>()]` (forme du sample officiel) |
| `AdditionalHeaders = { "Authorization": "Bearer {token}" }` (toujours envoyé, même vide) | `AdditionalHeaders = hasToken ? { ["Authorization"] = … } : null` |
| — | `HttpClientTransportOptions.Name`, `McpClient.ServerInfo` (S1), `FunctionCallContent` dans `response.Messages` (S2), filtrage de la liste d'outils (S3) |
| `McpClient.CreateAsync`, `HttpClientTransport`, `HttpClientTransportOptions`, `HttpTransportMode.StreamableHttp`, `ListToolsAsync`, `McpClientTool` | **inchangés** en `ModelContextProtocol` 2.2.0 |

Dépendances résolues (Start = Solution) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `ModelContextProtocol` + `.Core` 2.2.0, `Microsoft.Extensions.Hosting.Abstractions` 10.0.10 (transitif de `ModelContextProtocol`, pas `Hosting`), `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. **Aucune préversion**, plus d'`Azure.AI.OpenAI` ni de `Microsoft.Extensions.Hosting`.

## 3. Changements conceptuels

1. **Le client MCP n'a pas changé de modèle** : l'API client du SDK C# officiel est la même en 0.5.0-preview.1 et en 2.2.0 pour ce cas ; `McpClientTool` reste un `AIFunction`. L'essentiel de la migration porte sur le socle (client v1, `AsAIAgent`, `AgentResponse<T>`) et sur la configuration de l'agent.
2. **Options d'agent** : fixer `MaxOutputTokens` / `Temperature` avec `clientFactory` + `ConfigureOptionsChatClient` détournait un middleware `IChatClient`. MAF 1.x les porte dans `ChatClientAgentOptions.ChatOptions`, avec les instructions et les outils (samples `04_memory`, `Agent_Step13_Plugins`). L'objectif pédagogique « configurer les options du chat » est conservé. `Temperature` passe de 1 (valeur par défaut, sans effet démontrable) à 0.2 ; `MaxOutputTokens` de 600 à 1000 (marge pour 4 modèles, cf. §6.3).
3. **Token Hugging Face facultatif et secret** : le serveur `https://huggingface.co/mcp` répond anonymement (4 outils, limites réduites — message de ses propres `instructions`). Le lab n'exige plus de compte ; le token, s'il est fourni, va en user-secrets / variable d'environnement.
4. **Rendre visible ce qui se passe** : S1 montre que la découverte est une requête MCP pure (sans modèle) ; S2 affiche les appels d'outils de la réponse (`FunctionCallContent`) ; le README explique la séquence `initialize` → `tools/list` → function calling → `tools/call` exécuté **par le programme** (client MCP local, par opposition au *hosted MCP tool* de la Responses API).
5. **Coût et surface des outils** (S3) : les définitions de tous les outils sont envoyées à chaque appel modèle. Ne garder que `hub_repo_search` fait passer l'usage d'entrée de 4 435 à 1 927 tokens pour la même réponse. Justification : la page Learn « Using MCP tools » documente `allowed_tools` (Python) et recommande de restreindre et suivre ce qu'on expose ; en C#, l'équivalent est de filtrer la liste `McpClientTool` avant `AsAIAgent` (API standard, sans abstraction ajoutée).

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `Azure.Identity` beta, `NoWarn MEAI001` : supprimés ou remplacés (cf. Lab01/Lab03).
- `clientFactory` + `ConfigureOptionsChatClient` : **remplacés** par `ChatClientAgentOptions.ChatOptions` (§3.2). Signalé dans l'encadré « Coming from an older version ».
- Token Hugging Face obligatoire dans `appsettings.json` : **remplacé** par un token facultatif en user-secrets.
- Aucun scénario supprimé : le scénario d'origine devient S2 ; **2 scénarios ajoutés** (S1 découverte, S3 sélection des outils).
- Corrigé au passage : l'ancien Start ne gérait que `DefaultAzureCredential` alors que la Solution gérait aussi la clé API ; numérotation « Step 3 » / « Step 5 » dupliquée dans la Solution ; `AzureOpenAISettings.cs` / `MCPServerSettings.cs` différents entre Start et Solution.

## 5. Tests effectués et résultats

Environnement : macOS, SDK .NET 10.0.100, ressource `*.cognitiveservices.azure.com`, déploiement `gpt-4o-mini`, serveur `https://huggingface.co/mcp` (version 0.4.23), **accès anonyme**.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build Start/Solution) | ✅ 0 erreur, 8 warnings NU1902/NU1903 par projet (CommonUtilities) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances | ✅ MAF 1.22.0, MCP 2.2.0, pas d'`Azure.AI.OpenAI` ni de `Hosting`, aucune préversion (Start = Solution) |
| 3 | Vulnérabilités | ⚠️ `Snappier` 1.0.0 (high), `SharpCompress` 0.30.1 (moderate) — transitifs de CommonUtilities, **identiques à la baseline** ; aucune introduite par le lab (MCP 2.2.0 n'en apporte aucune) |
| 4 | `dotnet build -warnaserror -nowarn:NU1902,NU1903` Start + Solution | ✅ 0 erreur, 0 warning C# |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `ChatClientAgent`, `ConfigureOptionsChatClient`, `clientFactory`, `McpClientFactory`, Hosting, `.NET 8`, `NoWarn`, `"APIKey"`, `YOUR-HUGGINGFACE`, `0.5.0`, `ToString()`) dans `Start/`, `Solution/`, `README.md` | ✅ 0 occurrence hors encadré « Coming from an older version » (4 lignes, toutes dans l'encadré) |
| 6 | Run Solution — **clé API** | ✅ S1 : `huggingface.co/mcp 0.4.23`, 4 outils ; S2 : 4 modèles (`jinaai/jina-embeddings-v5-text-nano`…, liens `https://hf.co/…`), 1 appel `hub_repo_search(query: text embedding, repo_types: ["model"], limit: 4)`, usage 4 435 / 240 / 4 675 ; S3 : `hub_repo_search` seul, 4 modèles identiques, usage 1 927 / 240 / 2 167 ; ~6 s |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`, `az login`) | ✅ 3 scénarios corrects, même usage |
| 8 | Configuration absente / invalide | ✅ messages clairs (exception non gérée avec message actionnable, comme Lab01–Lab03) : endpoint Azure absent → `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set …', or with the environment variable 'AzureOpenAI__Endpoint'.` ; endpoint MCP vide → `'MCPServers:HuggingFace:Endpoint' is not configured…` ; `http://` → `… must be an absolute https:// URL.` ; token `YOUR-…` → `… still contains a placeholder. Remove it to connect anonymously, or set your token with …` ; token invalide → `HttpRequestException: Response status code does not indicate success: 401 (Unauthorized)` ; URL MCP erronée → `… 404 (Not Found)` |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche la configuration et les 3 en-têtes, code de sortie 0 |
| 10 | Start complété **uniquement d'après les indices du README** (copie temporaire `.tmp-start-check` dans le lab, supprimée ensuite) | ✅ compile sans warning C#, reproduit les 3 scénarios (mêmes modèles, même appel d'outil, mêmes usages 4 675 / 2 167) |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ 7 fichiers identiques (`.csproj`, config ×4, `AgentConsole.cs`, `Models/HuggingFaceModel.cs`) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–18 a un indice qui mène au code de la Solution (validé par le test 10) |
| + | `MaxOutputTokens = 60` (copie temporaire) | `JsonException: Expected end of string, but instead reached end of data` à la lecture de `Result` → ligne de dépannage |
| — | Run avec un **vrai** token Hugging Face | ❌ **non testé** : aucun token disponible dans l'environnement (seule la présence des variables a été vérifiée). Le chemin est couvert indirectement : l'en-tête est bien envoyé (token invalide → 401) |

`CommonUtilities` n'a pas été modifié : pas de risque de régression pour les autres labs.

## 6. Problèmes rencontrés

1. **Page Learn C# obsolète** : « Using MCP tools » (mise à jour 2026-09-23) montre encore `McpClientFactory.CreateAsync` (API 0.x, inexistante en 2.2.0). Le sample au tag `dotnet-1.22.0` (`McpClient.CreateAsync`) a fait foi ; ajouté à la table §5 du manuel.
2. **Breaking changes MCP de MAF sans impact** : #7774 (1.19, extension Tasks) et #8425 (1.22, sessions scoppées par invocation) ne concernent que l'extension Tasks et `Microsoft.Agents.AI.Workflows.Declarative.Mcp` (vérifié sur la PR #8425). Le manuel §3 les attribuait à Lab04 : corrigé.
3. **Troncature de la sortie structurée** : avec l'ancien `MaxOutputTokens = 600` la réponse tient (240 tokens), mais une valeur trop basse casse la désérialisation (`JsonException`) ; porté à 1000 et documenté.
4. **Les samples MCP HTTP officiels utilisent Foundry** (`AIProjectClient`) : seuls les patterns MCP et agent ont été repris sur le `ChatClient` Azure OpenAI v1, comme pour Lab01–Lab03 (le sample `Agent_MCP_Server`, lui, utilise déjà `OpenAIClient` + v1).
5. **Dépendance à un service tiers vivant** : la liste d'outils, la version du serveur et les modèles trouvés changent dans le temps (les noms d'outils Hugging Face ont déjà changé par le passé). Le README l'indique ; le dépannage couvre un S3 vide ; les checks du dashboard ne figent que `hub_repo_search` (voir §7).
6. **Migration de Lab05 en parallèle** : une autre session modifiait en même temps `labs.json`, `LabCatalogTests` et `OutputAnalyzerTests` (entrée et tests Lab05 apparus pendant ce travail). Toutes mes modifications de fichiers partagés ont été ciblées (remplacements exacts) et préservent les siennes ; le test du catalogue attend `lab01…lab05` dans l'ordre. Deux exécutions de `dotnet test` ont rapporté « Zero tests ran » (runner lancé mais aucun test, 90 ms) pendant cette période, puis 83/83 aux exécutions suivantes sans changement de code : probablement une interférence de build/exécution concurrente. Enfin, l'arrêt du dashboard a été fait avec `pkill -f "dotnet run"` : s'il y avait à ce moment un `dotnet run` de l'autre session, il a pu être interrompu.

## 7. Décisions à valider

- **Token Hugging Face facultatif** (connexion anonyme par défaut) et retiré de `appsettings.json`.
- **Remplacement de `clientFactory` + `ConfigureOptionsChatClient`** par `ChatClientAgentOptions.ChatOptions`, et nouvelles valeurs `MaxOutputTokens = 1000`, `Temperature = 0.2f`.
- **Ajout de S1** (découverte des outils sans modèle, `ServerInfo`) et de **S3** (sélection des outils), justifiés par le sample `Agent_Step09_UsingMcpClientAsTools` (affichage des outils) et par la page Learn (`allowed_tools`, recommandations de sécurité).
- **Connexion MCP dans le setup** (partagée par les 3 scénarios, `await using` jusqu'à la fin du programme) plutôt que dans le scénario.
- **Checks du dashboard dépendants du serveur réel** : `scenario1-tools` et `scenario3-tools` exigent un outil nommé `hub_repo_search`. Si Hugging Face renomme l'outil, le lab (S3) et ces checks devront être mis à jour ensemble.
- **Nouveau fichier fourni `AgentConsole.cs`** (identique à Lab03).
- **Niveau « Intermediate »** dans le dashboard (Lab01–Lab03 : « Beginner »).

## 8. Points à surveiller / pour les prochains labs

- **Lab10** (agent exposé en serveur MCP) : `ModelContextProtocol.AspNetCore` 2.2.0 ; la page Learn montre `McpServerTool.Create(agent.AsAIFunction())` + `AddMcpServer().WithStdioServerTransport()` et `Microsoft.Extensions.Hosting` (légitime côté serveur, contrairement aux labs console) ; sample `Agent_Step07_AsMcpTool`. Un client de test peut reprendre S1 de Lab04.
- **Secrets propres à un lab** (token MCP, chaîne de connexion) : les mettre dans les user-secrets partagés sous leur propre section ; le formulaire du dashboard ne les gère pas et `SecretRedactor` ne masque que la clé Azure OpenAI → ne jamais les afficher.
- **Labs à outils nombreux** (Lab07, Lab09, Lab10) : montrer l'usage de tokens lié aux définitions d'outils ; `FunctionCallContent` dans `response.Messages` est une alternative légère au middleware pour afficher les appels.
- **Services externes vivants** : écrire les checks sur la forme du résultat (id `owner/name`, lien du service), pas sur des valeurs précises.

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire — le catalogue est piloté par `labs.json` ; le lab n'est pas interactif ; l'étiquetage des usages par scénario couvre les 2 blocs `Token Usage:`. Le token Hugging Face n'est pas affiché par le lab, donc l'absence de masquage par `SecretRedactor` est sans conséquence (documenté dans `Dashboard/README.md`).

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab04` : métadonnées EN + FR, projets Start/Solution, timeout 180 s, **9 checks**, insérée avant `azureopenai-lab05` |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab04 sur la sortie Solution réelle (tous passent) et Start (seul `config` passe) ; réponse « sans outil MCP » (aucun check de résultat ne passe) ; 2 rapports de tokens étiquetés `Scenario 2/3 · Token Usage` (total 6 842) |
| Documentation | `Dashboard/README.md` : labs enregistrés, exemples complets, exemple « Add a lab » (Lab08), tests, note sur les secrets propres à un lab |

**Choix des checks** — ils vérifient ce que seuls le serveur MCP ou un outil exécuté peuvent produire :

| Check | Preuve |
|---|---|
| `config` | `Endpoint:` puis `MCP Server: https://… (access token|anonymous)` |
| `scenario1-server` | `Connected to: <nom>` dans S1 (`ServerInfo` reçu à l'initialisation MCP) |
| `scenario1-tools` | `MCP tools available (N≥1): …` contenant `hub_repo_search` |
| `scenario2` | `Found N≥1 models:` puis un bloc `Name: owner/name` / `Task` / `Library` / `Link: https://hf.co/…` (ou `huggingface.co`) |
| `scenario2-tool-calls` | au moins une ligne `- <outil>(` sous `MCP tools called by the agent:` |
| `scenario3-tools` | exactement `Tools given to the agent: hub_repo_search` |
| `scenario3` | `Found N≥1 models: owner/name…` dans S3 |
| `scenarioN-usage` | usage du scénario, borné à l'en-tête suivant |

**Tests** :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ 83/83 (inclut les tests Lab05 ajoutés en parallèle par l'autre session ; 3 tests Lab04 ajoutés). Voir §6.6 pour deux exécutions transitoires « Zero tests ran » |
| API : `GET /api/labs`, détails `?lang=fr` (traductions fournies, appliquées côté UI comme pour Lab03), packages, commandes, diff de solution | ✅ 5 labs ; 7 packages ; seul `Program.cs` diffère |
| Run Lab04 **Solution** via l'API | ✅ `passed` 9/9 ; `Scenario 2 · Token Usage` 4 675 et `Scenario 3 · Token Usage` 2 167, total 6 842 ; 10,6 s |
| Run Lab04 **Start** livré | ✅ `failed` attendu : 8/9 checks échouent, seul `config` passe ; aucun usage |
| Non-régression Lab01 Solution | ✅ `passed` 7/7 |

L'historique local `Dashboard/.data/history.json` (git-ignoré) a été sauvegardé avant les runs puis restauré à l'identique (`cmp`) ; le log temporaire du dashboard était dans le scratchpad de session, hors dépôt.
