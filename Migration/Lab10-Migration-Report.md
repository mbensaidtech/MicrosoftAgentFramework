# Rapport de migration — Lab10_ExposeAIAgentAsMCPTool

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.22.0** · **ModelContextProtocol(.AspNetCore) 2.2.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.16 ajouté, §1, §3, §5, §8), [Migration-Plan.md](Migration-Plan.md), rapports [Lab01](Lab01-Migration-Report.md) à [Lab09](Lab09-Migration-Report.md) (surtout [Lab04](Lab04-Migration-Report.md) pour MCP et [Lab06](Lab06-Migration-Report.md) pour les serveurs). Sources officielles : samples `dotnet/samples/02-agents/Agents/Agent_Step07_AsMcpTool` et `02-agents/ModelContextProtocol/Agent_MCP_Server` au tag `dotnet-1.22.0`, source `Microsoft.Agents.AI/AgentExtensions.cs` (`AsAIFunction`) et `PublicAPI.Shipped.txt` 1.22.0, `Directory.Packages.props` 1.22.0, docs XML des paquets `ModelContextProtocol`, `.Core` et `.AspNetCore` 2.2.0 et `Microsoft.Extensions.AI` 10.10.0, pages Learn [Self-host agents as MCP tools](https://learn.microsoft.com/agent-framework/hosting/self-hosting/mcp?pivots=programming-language-csharp) (2026-10-22), [Build a minimal MCP server](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server) et [Build a minimal MCP client](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-client). Comportements **vérifiés en exécution réelle** contre Azure OpenAI (clé API et Entra ID).

**Statut : migré.** Tous les critères du manuel (§7) sont remplis, y compris l'exécution réelle de la Solution par les deux voies d'authentification et les runs du dashboard (Start `failed` attendu, Solution `passed`, tokens affichés). Réserve : les warnings NU1902/NU1903 hérités de `CommonUtilities` (identiques à la baseline).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/ExposeAIAgentAsMCPTool.csproj` | `Microsoft.NET.Sdk` ; `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `ModelContextProtocol.AspNetCore` **2.2.0**, `UserSecretsId` partagé. **Supprimés** : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251204.1` (Start) / `251219.1` (Solution), `ModelContextProtocol.AspNetCore 0.5.0-preview.1` (Solution seulement : **le Start ne compilait pas**, `CS0246`), `Microsoft.Extensions.Hosting 9.0.0` (Start), `Microsoft.NET.Sdk.Web` (Solution) | Une seule version, aucune préversion, Start = Solution. `ModelContextProtocol.AspNetCore` est le paquet de plus haut niveau utile : il apporte `ModelContextProtocol` et le framework partagé ASP.NET Core (`WebApplication`, `Host`, DI, fournisseurs de configuration) — les 4 `Microsoft.Extensions.Configuration.*` du socle deviennent inutiles (même logique que Lab06_A2AServer) |
| `AzureOpenAISettings.cs`, `ConfigurationHelper.cs`, `appsettings.json` | Socle Lab01 (namespace `ExposeAIAgentAsMCPTool`) + `GetMcpServerSettings()` (URL absolue http(s), message actionnable) ; `appsettings.json` sans `APIKey`, avec `McpServer:BaseUrl = http://localhost:5100` et `Logging:LogLevel:Default = Warning` | User-secrets, validation au démarrage ; port configurable (5000 évité : AirPlay sur macOS) ; journaux ASP.NET Core / MCP lisibles |
| `McpServerSettings.cs` (**nouveau**) | `BaseUrl` du serveur HTTP | Comme `A2AServerSettings` (Lab06) |
| `Tools/CompanyTools.cs` | Copie de la version migrée de Lab03 (namespace, commentaire) : `using Microsoft.Extensions.AI` inutile retiré, `✅` et espace parasite retirés | Même jeu de données et mêmes outils que Lab03 |
| `McpConsole.cs` (**nouveau**) | `WriteTools` (nom, description, schéma d'entrée vus par un client MCP), `WriteToolResult` (`CallToolResult`, `IsError`), `WriteToolCalls` (`FunctionCallContent` / `FunctionResultContent` par `CallId`), `WriteTokenUsage` ; décodage d'affichage des chaînes JSON | Helpers fournis : `Program.cs` reste centré sur MCP, pas de `CS8321` dans le Start |
| `Scenario.cs` | **Supprimé** | Remplacé par `scenariosToRun` (format des labs migrés) |
| `Solution/Program.cs` | Réécrit : client `OpenAIClient` v1 (clé API ou Entra ID) ; agent `CompanyAssistant` (`AsAIAgent` avec **description**, 3 outils explicites) → `AsAIFunction()` → `McpServerTool.Create()` ; mode `--stdio` (serveur stdio, `Console.SetOut(Console.Error)`) ; **S1** serveur Streamable HTTP (`WithHttpTransport`, `MapMcp`, `StartAsync`) testé par un `McpClient` HTTP ; **S2** le programme est l'hôte MCP et se relance avec `--stdio` (`StdioClientTransport`, stderr relayé `[server]`) ; **S3** un agent `OfficeAssistant` reçoit l'outil MCP et délègue une réservation ; `--http` garde le serveur HTTP | API 1.22.0 / MCP 2.2.0 ; lab exécutable et vérifiable avec la seule CLI `dotnet` (§3) |
| `Start/Program.cs` | TODO **1 → 18** (setup 1–3 identique aux labs migrés ; agent + outil 4–6 ; serveur stdio 7–8 ; S1 9–12 ; S2 13–15 ; S3 16–18) ; requêtes et prompt fournis et affichés (pas de `CS0219`) | Compile sans warning et s'exécute tel que livré (exit 0, aucun appel réseau) ; seul `Program.cs` diffère |
| `README.md` (lab) | Réécrit sur le gabarit Lab09 : objectif, configuration, fichiers fournis, commutateurs `--stdio` / `--http`, tableaux des 18 TODO (chaque indice contient le code de la Solution), Step 5 (MCP Inspector et VS Code, `--no-build`), « How it works » (schéma, session par appel, chaîne JSON, sans état, pas de SSE, stdout réservé, qui consomme les tokens), concepts, namespaces, sortie attendue (run réel), dépannage (erreurs réelles), sécurité, paquets, « Going further », encadré « Coming from an older version », liens (tous HTTP 200) | L'ancien README enseignait `AzureOpenAIClient`, `CreateAIAgent`, `ChatClientAgent`, l'URL SSE `…/sse`, `UseHttpsRedirection`, une clé dans `appsettings.json`, et son lien Learn redirige aujourd'hui vers une page sans rapport |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab10` (EN + FR, niveau *Intermediate*, timeout 180 s, **9 checks**, ni interactif ni compagnon) | Intégration au dashboard (§9) |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 3 tests Lab10 dans `OutputAnalyzerTests` | Idem |
| `Dashboard/README.md`, `README.md` (racine) | Lab10 enregistré / migré ; note « serveurs démarrés par le lab, port libre » ; conseil de checks pour les labs MCP ; exemple « Add a lab » passé sur Lab11 ; scénarios du lab ; liste des tests | Documentation associée |
| `Migration/Migration-Manual.md` | §1 (MCP 2.2.0 appliqué, 2.3.0 non retenue), §3 (SDK MCP serveur : sans état et sans SSE par défaut), **§4.16** « Agent exposé comme outil MCP », §5 (4 lignes ajoutées/corrigées), §8 (stdout des serveurs stdio) | Référence pour les labs suivants |
| `Migration/Migration-Plan.md` | Statut, vague 2 terminée, ligne Lab10 (§5), pratique Lab10 (§5 bis) | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) ; le Start n'avait que `DefaultAzureCredential` | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `ChatClientAgent agent = chatClient.CreateAIAgent(new ChatClientAgentOptions { Name, Description, ChatOptions = { Instructions, Tools } })` | `AIAgent agent = chatClient.AsAIAgent(instructions, name, description, tools)` |
| Outils par réflexion (`GetMethods(BindingFlags…)` + `AIFunctionFactory.Create(method, target)`) | Liste explicite `AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info")`… (Lab03, scénario 1) |
| `agent.AsAIFunction()`, `McpServerTool.Create(…)`, `AddMcpServer()`, `WithHttpTransport()`, `WithStdioServerTransport()`, `WithTools([…])`, `MapMcp(path)`, `Host.CreateEmptyApplicationBuilder(settings: null)` | **inchangés** (MAF 1.22.0 / MCP 2.2.0) — mais transport HTTP **sans état** et **sans SSE** par défaut |
| `app.UseHttpsRedirection(); app.Run();` (bloquant) | `app.Urls.Add(baseUrl); await app.StartAsync();` … `await app.StopAsync()` (ou `WaitForShutdownAsync()` avec `--http`) |
| `enum Scenario` (un scénario bloquant par exécution) | `scenariosToRun` + commutateurs `--stdio` / `--http` |
| Test par MCP Inspector (Node.js) ou un IDE tiers | `McpClient` + `HttpClientTransport` (S1), `StdioClientTransport` (S2, S3), `CallToolAsync` / `CallToolResult`, agent client avec `[.. mcpTools.Cast<AITool>()]` (S3) ; MCP Inspector et VS Code en option |

Dépendances résolues (Start = Solution, `diff` des graphes vide, 46 paquets) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `ModelContextProtocol(.Core/.AspNetCore)` 2.2.0, `Microsoft.Extensions.Hosting.Abstractions` 10.0.10 (transitif de `ModelContextProtocol`), `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0 ; ASP.NET Core 10 en framework partagé. **Aucune préversion**, plus d'`Azure.AI.OpenAI`. Aucune API utilisée n'est expérimentale (build sans `#pragma` hors `OPENAI001` du socle).

## 3. Changements conceptuels

1. **Le transport HTTP n'est plus « SSE et Streamable HTTP ».** Avec le SDK MCP C# 2.2.0, `WithHttpTransport()` est **sans état** par défaut (révision de protocole 2026-07-28 : pas de session MCP, chaque requête indépendante) et `MapMcp` **ne mappe plus** `/sse` ni `/message` (`EnableLegacySse = false`, incompatible avec le mode sans état). **Vérifié** : un client en mode SSE reçoit `405`, `GET …/sse` renvoie `404`. Le lab enseigne Streamable HTTP ; l'ancienne URL `…/sse` n'apparaît que dans l'encadré « Coming from an older version » et le dépannage.
2. **Un serveur stdio n'écrit rien d'autre que le protocole sur stdout.** L'ancien serveur écrivait **8 lignes non JSON** (titres, configuration) avant le premier message MCP (**vérifié** en reconstruisant l'ancienne Solution depuis git). Le client C# 2.2.0 les ignorait, mais c'est contraire au protocole. Désormais `--stdio` ⇒ `Console.SetOut(Console.Error)` : **vérifié**, stdout ne contient que les réponses JSON-RPC, le journal du lab passe sur stderr (affiché `[server]` par l'hôte). Découverte associée : un `dotnet run` **avec build** écrit les warnings de build sur stdout → `--no-build` ou la `.dll` (vérifié, documenté).
3. **L'outil MCP est un agent sans mémoire entre appels.** `AsAIFunction()` sans session crée une nouvelle session par appel (source au tag) : en S3 l'`OfficeAssistant` appelle souvent l'outil plusieurs fois, et chaque appel repart de zéro. Le README l'explique (et pourquoi ne pas passer de session sur un serveur partagé).
4. **Le lab se vérifie lui-même avec la CLI `dotnet`.** L'ancien lab bloquait sur un serveur (`app.Run()`, `RunAsync()`) et ne pouvait être testé qu'avec MCP Inspector (Node.js) ou un IDE. Chaque scénario démarre maintenant son serveur, l'appelle comme le ferait un client/hôte MCP, puis l'arrête ; `--http` / `--stdio` gardent un serveur pour MCP Inspector ou VS Code (Step 5). Le pattern hôte stdio (`StdioClientTransport` qui lance un serveur .NET) est celui du quickstart Learn « Build a minimal MCP client » et du sample `Agent_MCP_Server`.
5. **Scénario 3 ajouté : un autre agent utilise l'agent exposé.** Justification : c'est l'objectif même de l'ancien README (*« enabling other applications and agents to use your AI agent's capabilities »*), auparavant illustré par un IDE externe ; le sample `Agent_MCP_Server` montre exactement un agent `AsAIAgent(…, tools: [.. mcpTools.Cast<AITool>()])` sur un serveur stdio ; le sample `Agent_Step07_AsMcpTool` renvoie à « any MCP client ». Il rend aussi visible l'usage de tokens (seul celui de l'agent client est mesurable : l'agent exposé consomme dans le processus serveur).
6. **Le résultat de l'outil est une chaîne JSON.** `AIFunctionFactory` sérialise le `string` renvoyé par l'agent : le bloc texte MCP contient `"…\n…"` (déjà le cas avant). Le helper le décode pour l'affichage ; `AIFunctionFactoryOptions.MarshalResult` (API stable de MEAI, **vérifiée** : texte brut) est cité en « Going further », pas imposé (le sample officiel ne le fait pas).

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Azure.Identity` beta, MAF en préversions différentes Start/Solution, `ModelContextProtocol.AspNetCore` 0.5.0-preview.1, `Microsoft.Extensions.Hosting` (Start), `Microsoft.NET.Sdk.Web` (Solution) : remplacés (§2).
- `Scenario.cs` / `enum Scenario` : supprimé (`scenariosToRun` + `--stdio` / `--http`).
- `app.UseHttpsRedirection()` : supprimé (aucun endpoint HTTPS : sans effet).
- Point de terminaison SSE (`/mcp/companyassistant/sse`) : n'existe plus par défaut ; non réactivé (le SDK le réserve aux clients de confiance dans des processus isolés).
- Découverte des outils par réflexion : remplacée par la liste explicite (la réflexion reste enseignée par Lab03, scénario 2).
- Exemple de configuration d'un IDE tiers : remplacé par `.vscode/mcp.json` (format documenté par Microsoft Learn) ; MCP Inspector conservé en option (outil du projet MCP, cité par le sample officiel).
- Lien Learn `tutorials/agents/agent-as-mcp-tool` : remplacé (il sert aujourd'hui la page « Hosted MCP Tools », sans rapport ; la page .NET « Self-host agents as MCP tools » indique *« coming soon »*) par les samples officiels et les quickstarts Learn .NET MCP.
- Aucun objectif pédagogique supprimé : HTTP (S1) et stdio (S2) conservés ; ajout du S3.

## 5. Tests effectués et résultats

Environnement : macOS (culture fr-FR), SDK .NET 10.0.402 (runtimes 10.0.13), variables `AzureOpenAI__*` de l'environnement (présence vérifiée, valeurs jamais affichées), `az login` valide, déploiement `gpt-4o-mini`.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build) | ⚠️ **Start : ne compile pas** (`CS0246` `ModelContextProtocol`, paquet absent du Start) ; Solution : 0 erreur, 8 warnings NU1902/NU1903 |
| 0b | Baseline fonctionnelle (ancienne Solution en stdio, client MCP 2.2.0 jetable) | ✅ outil `CompanyAssistant` listé et appelé (réponse en chaîne JSON) ; test brut : **8 lignes non JSON sur stdout** avant le premier message MCP |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances (`--include-transitive`) | ✅ MAF 1.22.0, MCP 2.2.0, OpenAI 2.13.0, MEAI 10.10.0 ; aucune préversion ; pas d'`Azure.AI.OpenAI` ; graphes Start et Solution **identiques** (46 paquets) |
| 3 | Vulnérabilités (`--vulnerable --include-transitive`) | ✅ aucune introduite : seuls `SharpCompress` 0.30.1 / `Snappier` 1.0.0 via `CommonUtilities` (identique à la baseline) |
| 4 | `dotnet build -warnaserror -nowarn:NU1902,NU1903 --no-incremental` Start + Solution | ✅ `0 Warning(s) 0 Error(s)` (aucun diagnostic expérimental ; sans les 4 `Microsoft.Extensions.Configuration.*` : pas de `NU1510`) |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent(`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `GetNewThread`, `ChatMessageStore`, paquet `Microsoft.Extensions.Hosting`, `NoWarn`, `MEAI001`, `"APIKey"`, `ChatClientAgent`, `UseHttpsRedirection`, `enum Scenario`, `Scenario.Remote/Local`, `McpClientFactory`, `GetMethods`, `BindingFlags`, `app.Run()`, `0.5.0`, `preview.25`, `Sdk.Web`, `.NET 8`, `/sse`, nom de l'IDE tiers de l'ancien README) | ✅ 0 occurrence dans `Start/` et `Solution/` ; README : dans l'encadré « Coming from an older version » (8 lignes), plus `/sse` dans « How it works » et le dépannage pour expliquer que SSE n'est **plus** servi. Le *namespace* `Microsoft.Extensions.Hosting` (`Host`) reste utilisé, comme dans le sample officiel |
| 6 | Run Solution — **clé API** | ✅ S1 : `MCP server listening on http://localhost:5100/mcp/companyassistant`, outil `CompanyAssistant` (schéma `query`), résultat EMP003 = Charlie Brown, Tech Lead ; S2 : `[server] … ready on stdio.`, Innovation Lab (10) et Executive Suite (20) ; S3 : 3 appels `CompanyAssistant(query: …)`, réservation ROOM-B (Brainstorm Corner) pour Bob Johnson ; usage 568 / 169 / 737 ; exit 0, ~24 s |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`, hérité par le serveur stdio) | ✅ même comportement ; usage 532 / 167 / 699 ; exit 0 |
| 7b | Modes serveur | ✅ `--http` : après S1 le serveur reste actif et un client externe (Streamable HTTP, `AutoDetect`) l'appelle (EMP005 → Edward Smith, Financial Analyst) ; client **SSE → 405**, `GET …/sse` → **404** ; arrêt propre, port libéré. `--stdio` brut : stdout = 2 messages JSON-RPC seulement, journal sur stderr, exit 0 à la fermeture de stdin. `dotnet run --no-build -- --stdio` : stdout propre ; **avec build : 8 lignes de warnings de build sur stdout** |
| 7c | Chemins d'erreur | ✅ déploiement inexistant : S1/S2 `The tool reported an error: An error occurred invoking 'CompanyAssistant'.` (le serveur HTTP journalise `DeploymentNotFound`), S3 `ClientResultException 404` ; port 5100 occupé : `IOException: Failed to bind … address already in use` ; serveur stdio sans TODO 7–8 : `ClientTransportClosedException: MCP server process exited unexpectedly (exit code: 0)` + fin du stderr. Messages repris dans le dépannage |
| 8 | Configuration absente / placeholder | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set "AzureOpenAI:Endpoint" <value>', or with the environment variable 'AzureOpenAI__Endpoint'.` ; idem `ChatDeploymentName` ; `'McpServer:BaseUrl' must be an absolute http:// or https:// URL, e.g. http://localhost:5100.` ; même message clair dans le processus `--stdio` |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche la configuration, les 3 en-têtes, les requêtes et le prompt, **exit 0**, aucun appel réseau ; `--stdio` : exit 0 |
| 10 | Start complété **uniquement d'après les indices du README** (copie `.tmp-start-check` dans le dossier du lab, TODO 1–18 remplacés par script à partir des tableaux, supprimée ensuite) | ✅ compile `-warnaserror` sans warning, 0 TODO restant, **même comportement que la Solution** (Charlie Brown ; Innovation Lab / Executive Suite ; Brainstorm Corner / Bob Johnson ; usage 536 / 151 / 687) ; code identique à la Solution hors commentaires (seule différence : une chaîne coupée par `+`) |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (7 fichiers partagés) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–18 a un indice qui contient le code de la Solution (prouvé par le test 10) ; sortie attendue issue d'un run réel ; liens vérifiés (HTTP 200) |
| 13 | Non-régression | Sans objet pour `CommonUtilities` (non modifié) ; dashboard : 148/148 tests (§9) |
| + | `MarshalResult` (texte brut) | ✅ `"text": "The position of EMP005 (Edward Smith) is Financial Analyst."` sans guillemets ; cité en « Going further » |

## 6. Problèmes rencontrés

1. **Documentation Learn déplacée.** Le lien de l'ancien README sert aujourd'hui « Hosted MCP Tools » (outils MCP hébergés par Foundry) ; la page « Self-host agents as MCP tools » ne couvre que Python (*« Self-hosting MCP tool support in .NET is coming soon »*). Le sample au tag et les quickstarts Learn .NET MCP ont fait foi.
2. **Pollution de stdout** (ancien serveur stdio, et `dotnet run` avec build) : corrigée dans le lab, documentée (README Step 5 et dépannage, manuel §4.16 et §8).
3. **Spinner et journal relayé** : les lignes `[server]` (stderr du processus enfant, reçues sur un autre thread) s'inséraient dans la ligne du spinner de connexion. Pas de spinner sur `McpClient.CreateAsync` (démarrage < 1 s) ; les lignes `[server]` peuvent arriver un peu avant ou après (signalé dans le README, checks indépendants de l'ordre).
4. **Journaux ASP.NET Core et MCP** très verbeux en `Information` (~8 lignes par requête) : `Logging:LogLevel:Default = Warning` dans `appsettings.json` ; ce fichier est lu depuis le dossier courant (content root), donc effectif depuis le dossier du projet et dans le dashboard — noté dans le README.
5. **Environnement** : un dashboard de l'utilisateur tournait sur 127.0.0.1:5057 (depuis `bin/Debug`). Aucun code du dashboard n'a été modifié : `dotnet test` n'a pas recompilé sa DLL (horodatage inchangé vérifié) ; la vérification a utilisé une **instance séparée** (port 5064, historique dans `Dashboard/.data/lab10-verification`, reporting désactivé, donc aucun envoi au serveur d'administration), supprimée ensuite. Ce dashboard ne verra `azureopenai-lab10` qu'après redémarrage. Un dossier non suivi `.playwright-mcp/` existait à la racine **avant** cette session : non créé ici, laissé en place.
6. **Fichiers hors dépôt** : au début de la session, les sources officielles (arbre du tag, samples, `PublicAPI.Shipped.txt`, pages Learn) ont été téléchargées dans le dossier temporaire de session (hors dépôt) ; ensuite, tous les fichiers de travail sont restés dans le dossier du lab (`.tmp-probe`, `.tmp-start-check`), supprimés en fin de session. Les docs XML des paquets ont été lues dans le cache NuGet. Aucun secret lu ni affiché (présence des variables seulement ; endpoint masqué dans les sorties).
7. **Variabilité du modèle** : l'`OfficeAssistant` appelle l'outil 1 à 3 fois et reformule ; la question de S3 demande explicitement les noms de la salle et de l'employé pour que les checks reposent sur des données que seuls les outils connaissent.

## 7. Décisions à valider

- [ ] **Scénario 3 ajouté** (un autre agent utilise l'agent exposé via MCP) — justifié par l'objectif du lab, le sample `Agent_MCP_Server` et le quickstart Learn MCP client ; il fournit aussi l'usage de tokens au dashboard.
- [ ] **Lab auto-vérifiant** : chaque scénario démarre son serveur, l'appelle et l'arrête ; le programme se relance lui-même avec `--stdio` ; `--http` / `--stdio` pour MCP Inspector / VS Code. Alternative : garder un serveur bloquant par exécution (non vérifiable sans Node.js ni dashboard « compagnon »).
- [ ] **SSE non réactivé** (défaut du SDK 2.2) plutôt que `EnableLegacySse = true` + mode avec état.
- [ ] **Socle** : pas de `Microsoft.Extensions.Configuration.*` explicites (apportés par le framework partagé via `ModelContextProtocol.AspNetCore`), `Microsoft.NET.Sdk` (comme Lab06_A2AServer).
- [ ] **Port 5100** (`McpServer:BaseUrl`) et `Logging:LogLevel:Default = Warning` dans `appsettings.json`.
- [ ] **Décodage des chaînes JSON dans le helper d'affichage** ; `MarshalResult` seulement en « Going further ».
- [ ] **Outils en liste explicite** (au lieu de la réflexion de l'ancien lab) ; ajout d'une **description** à l'agent (= description de l'outil MCP).
- [ ] **Configuration IDE** : VS Code (`mcp.json`, documenté par Learn) à la place de l'IDE tiers de l'ancien README ; MCP Inspector en option.
- [x] **Niveau *Intermediate*** dans le dashboard (le lab suppose Lab03 et Lab04), timeout 180 s.
- [ ] **Checks dépendants du texte du modèle** (Charlie Brown, Innovation Lab / Executive Suite, Brainstorm Corner / Bob Johnson) : stables sur les 4 runs réels faits avec la question finale (clé API, Entra ID, Start complété, dashboard) ; sans la demande explicite des noms, un run précédent n'avait pas cité `Bob Johnson` — un modèle qui reformule sans les noms ferait échouer le run.

## 8. Points pour les prochains labs

- **MAS-Lab01 (agent comme outil)** : `AsAIFunction()` nomme l'outil d'après l'agent (caractères non alphanumériques → `_`), reprend sa **description** (à renseigner), expose `query` et crée **une session par appel** ; `AIFunctionFactoryOptions { Name, Description, MarshalResult }` pour personnaliser.
- **Tout lab qui lance un serveur MCP stdio** : stdout réservé au protocole ; lancer la `.dll` ou `dotnet run --no-build` (la sortie de build va sur stdout tant que les warnings NU1902/NU1903 de `CommonUtilities` existent — raison de plus pour la phase 0).
- **Dashboard** : un lab qui démarre et arrête lui-même ses serveurs n'a pas besoin de `companion` ; seuls ses ports doivent être libres. Pour vérifier un lab en présence d'un dashboard utilisateur déjà lancé : instance séparée (`Dashboard__Port`, `Dashboard__DataDirectory` sous `Dashboard/.data/`, `Dashboard__Reporting__ServerUrl=` vide).
- **SDK MCP** : 2.3.0 est publiée ; rester sur 2.2.0 (version du `Directory.Packages.props` 1.22.0) jusqu'à la fin de la migration.
- **Lab11 (transport HTTP personnalisé)** : même socle ; les journaux ASP.NET Core ne sont pas concernés (console).

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire. Le lab n'est pas interactif et se termine seul : ses serveurs (HTTP dans le processus, stdio en processus enfant) sont démarrés et arrêtés par le lab lui-même, donc pas de `companion`. Le stderr du serveur stdio est relayé par le lab sur **son** stdout (`[server] …`), donc lisible par les checks. Seul prérequis : le port de `McpServer:BaseUrl` (5100) libre pendant le run (documenté).

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab10` : métadonnées EN + FR, projets Start/Solution, timeout 180 s, **9 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab10 sur la sortie réelle de la Solution (tous passent, ni interactif ni compagnon) et sur la sortie réelle du Start (seul `config` passe) ; sortie « réponses qui ne viennent pas de l'agent exposé » (agent en erreur dans le serveur HTTP, serveur stdio jamais démarré, agent client qui invente : seuls `scenario1-server` et `scenario1-tool` passent) ; rapport de tokens `Scenario 3 · Token Usage` (737) |
| Documentation | `Dashboard/README.md` : labs enregistrés, note Lab10, conseil de checks pour les labs MCP, exemple « Add a lab » (Lab11), liste des tests |

**Choix des checks** (chacun échoue sur le Start livré et passe sur la Solution) :

| Check | Preuve |
|---|---|
| `config` | `Endpoint:` |
| `scenario1-server` | `MCP server listening on http(s)://…/mcp/companyassistant` dans le bloc S1 |
| `scenario1-tool` | `MCP tools available (1):` puis `- CompanyAssistant: …` puis un schéma d'entrée contenant `"query"` (ce que voit un client MCP) |
| `scenario1-result` | `Tool result:` puis `Charlie Brown` (la requête ne donne que `EMP003`) |
| `scenario2-server` | `[server] CompanyAssistant MCP server ready on stdio.` (stderr du processus enfant) **et** l'outil listé (lookaheads, ordre indifférent) |
| `scenario2-result` | `Tool result:` puis `Innovation Lab` **et** `Executive Suite` |
| `scenario3-tool-call` | `Tool called: CompanyAssistant(query: …)` |
| `scenario3-result` | après un appel de l'outil : `Bob Johnson` **et** `Brainstorm Corner` (le prompt ne donne que `EMP002` et `ROOM-B`) |
| `scenario3-usage` | usage du scénario 3, borné à l'en-tête suivant |

**Résultats** (instance de vérification sur le port 5064, historique dans `Dashboard/.data/lab10-verification` — git-ignoré — supprimé ensuite ; reporting désactivé ; `Dashboard/.data/history.json` de l'utilisateur non utilisé) :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ **148/148** (dont les 3 tests Lab10 et le catalogue à 11 labs) |
| API : `GET /api/labs` | ✅ 11 labs (lab01 → lab10) |
| API : détails, README rendu, paquets, diff de solution | ✅ README rendu (46 Ko HTML) ; traductions FR exposées ; 3 paquets listés (MAF 1.22.0, Azure.Identity 1.21.0, ModelContextProtocol.AspNetCore 2.2.0) ; seul `Program.cs` diffère |
| Run Lab10 **Start** livré via l'API | ✅ `failed` attendu à l'étape `checks` — « 8 of 9 check(s) failed », seul `config` passe, exit 0, aucun usage |
| Run Lab10 **Solution** via l'API | ✅ **`passed`** — « All 9 checks passed », exit 0, 23,8 s, aucune erreur ni ligne stderr ; tokens affichés : `Scenario 3 · Token Usage` 543 / 134 / 677 ; avertissements de build : NU1902/NU1903 hérités |

Aucun artefact laissé : copies `.tmp-probe` / `.tmp-start-check` supprimées, instance de vérification arrêtée et son dossier supprimé, ports 5064 et 5100 libres.
