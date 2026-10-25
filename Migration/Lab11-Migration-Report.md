# Rapport de migration — Lab11-AIAgentWithCustomHttpTransport

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.17 ajouté, §3, §5, §8), [Migration-Plan.md](Migration-Plan.md), rapports [Lab01](Lab01-Migration-Report.md) à [Lab10](Lab10-Migration-Report.md) (surtout [Lab03](Lab03-Migration-Report.md) pour les outils et [Lab08](Lab08-Migration-Report.md) pour ce que le modèle reçoit). Sources officielles : samples au tag `dotnet-1.22.0` — `01-get-started/02_add_tools`, `02-agents/AgentProviders/foundry/Agent_Step25_FoundryToolboxMcp` et `Agent_Step26_FoundryToolboxMcpSkills`, `02-agents/ModelContextProtocol/Agent_MCP_PerRun_AuthHeaders` (`DelegatingHandler` + `InnerHandler`), `05-end-to-end/AgentWebChat` et `04-hosting/.../Using-E2E-Resilience` (`HttpClientPipelineTransport`), `04-hosting/.../SessionFilesClient` (`PipelinePolicy` + `AddPolicy`), `02-agents/Agents/Agent_Step05_Observability` ; docs XML `System.ClientModel` 1.15.0 (`ClientPipelineOptions`, `HttpClientPipelineTransport`, `PipelinePosition`, `ClientLoggingOptions`) ; pages Learn [System.ClientModel library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/system.clientmodel-readme) (2026-09-17), [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle), [Using function tools with an agent](https://learn.microsoft.com/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp). Comportements **vérifiés en exécution réelle** contre Azure OpenAI (clé API et Entra ID) et, pour les cas d'erreur, **hors ligne** avec un handler factice.

**Statut : migré.** Tous les critères du manuel (§7) sont remplis, y compris l'exécution réelle de la Solution par les deux voies d'authentification et les runs du dashboard (Start `failed` attendu, Solution `passed`, tokens affichés). Réserve : les warnings NU1902/NU1903 hérités de `CommonUtilities` (identiques à la baseline). **Point de sécurité** : l'ancien lab affichait la clé API en clair (§3.2, §6.1).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AIAgentWithCustomHttpTransport.csproj` | Socle Lab01 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé, paquets commentés. **Supprimés** : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251204.1`, `Microsoft.Extensions.Hosting 9.0.0` | Version stable unique, aucune préversion ; `HttpClientPipelineTransport` vient de `System.ClientModel` (transitif via `OpenAI`) et `DelegatingHandler` de .NET : aucun paquet ajouté |
| `AzureOpenAISettings.cs`, `ConfigurationHelper.cs`, `appsettings.json` | Copies conformes de Lab01 (namespace `AIAgentWithCustomHttpTransport`) ; `APIKey` retiré d'`appsettings.json` | User-secrets, validation au démarrage, aucun secret versionné |
| `CustomHttpClientHandler.cs` (Start **et** Solution) | **Supprimé** : le handler devient la classe `HttpLoggingHandler : DelegatingHandler` en bas de `Program.cs` | Règle 9 (seul `Program.cs` diffère) : l'ancien Start faisait écrire ce fichier à l'apprenant, donc Start ≠ Solution hors `Program.cs` ; pattern des samples officiels (§3.1) |
| `HttpConsole.cs` (**nouveau**, Start = Solution) | `WriteHeaders(headers, content)` (en-têtes + en-têtes de contenu), `MaskIfSensitive(name, value)` (`Authorization` / `api-key` → schéma seul, `Bearer [MASKED]`), `WriteBodyAsync(content, ct)` (JSON indenté, `text/event-stream` non lu), `StatusLabel(status)`, `WriteTokenUsage(response)` ; `using static` | Helpers d'affichage fournis (comme `AgentConsole.cs` de Lab03) ; le masquage n'est plus laissé au code de l'apprenant (§3.2) ; pas de `CS8321` dans le Start |
| `Tools/WeatherTools.cs` (**nouveau**, Start = Solution) | `GetWeather(location)` avec `[Description]`, réponse fixe *cloudy with a high of 15 degrees Celsius* | L'outil du sample officiel `02_add_tools` (degrés en toutes lettres : sortie ASCII, portable) ; donnée que le modèle ne peut pas inventer |
| `Solution/Program.cs` | Réécrit : handler + `HttpClient` ; `OpenAIClientOptions { Endpoint = v1, Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout = 30 s }` ; client clé API ou Entra ID ; **S1** agent de base, requêtes comptées (`RequestCount`), réponse, usage ; **S2** agent avec l'outil `get_weather` : 2 requêtes HTTP, usage cumulé ; `HttpLoggingHandler` : numéro de requête atomique (`Interlocked.Increment`), en-tête `X-agent-name`, requête (méthode, URL, en-têtes, corps), `base.SendAsync` chronométré (`Stopwatch.GetTimestamp`/`GetElapsedTime`), réponse (statut, durée, en-têtes, corps) | API 1.22.0 ; scénario 2 ajouté (§3.4) ; pas de spinner (il écrirait par-dessus le journal du handler) |
| `Start/Program.cs` | TODO **1 → 14** (setup 1–4, S1 5–7, S2 8–10, handler 11–14) ; en-têtes, prompts et squelette du handler fournis (le squelette compte et transmet les requêtes) | Compile sans warning et s'exécute tel que livré (exit 0, aucun appel réseau) ; après TODO 1–10 l'agent répond déjà via le transport, le journal apparaît avec TODO 11–14 |
| `README.md` (lab) | Réécrit sur le gabarit Lab09/Lab10 : objectif, configuration, fichiers fournis, tableaux des 14 TODO (chaque indice contient le code de la Solution) + `SendAsync` complet, « How it works » (schéma des couches, requête finale, 1 run = N requêtes, usage = somme, retries, erreurs, corps mis en tampon, streaming, jeton Entra ID), en-têtes de réponse utiles, concepts, namespaces, sortie attendue (run réel abrégé), dépannage (messages réels), **Security**, paquets, « Going further », encadré « Coming from an older version », liens (tous HTTP 200) | L'ancien README enseignait `AzureOpenAIClient(Options)`, `CreateAIAgent`, `ChatClientAgent`, `AgentRunResponse`, une clé dans `appsettings.json` et un masquage partiel d'`Authorization` |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab11` (EN + FR, *Intermediate*, timeout 180 s, **12 checks**, ni interactif ni compagnon) ; 112 lignes ajoutées, rien d'autre modifié | Intégration au dashboard (§9) |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 4 tests Lab11 dans `OutputAnalyzerTests` | Idem |
| `Dashboard/README.md`, `README.md` (racine) | Lab11 enregistré / migré ; note « trafic HTTP complet, `Authorization` masqué par le lab » ; conseil de checks pour les labs qui journalisent du HTTP ; exemple « Add a lab » passé sur Lab12 ; scénarios du lab ; liste des tests | Documentation associée |
| `Migration/Migration-Manual.md` | §3 (ligne `RunCoreAsync` : Lab11 non concerné), **§4.17** « Transport HTTP personnalisé », §5 (3 lignes), §8 (journaux HTTP et secrets ; observation streaming) | Référence pour les labs suivants |
| `Migration/Migration-Plan.md` | Statut (vague 1 terminée), ligne Lab11 (§5), pratique Lab11 (§5 bis) | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient(Uri, ApiKeyCredential \| DefaultAzureCredential, AzureOpenAIClientOptions)` | `OpenAIClient(ApiKeyCredential \| BearerTokenPolicy, OpenAIClientOptions { Endpoint = …/openai/v1/ })` |
| `AzureOpenAIClientOptions { Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout }` | `OpenAIClientOptions { Endpoint, Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout }` — mêmes propriétés de `ClientPipelineOptions`, **inchangées** |
| `new HttpClient(new CustomHttpClientHandler())` (`: HttpClientHandler`) | `new HttpClient(new HttpLoggingHandler { InnerHandler = new HttpClientHandler() })` (`: DelegatingHandler`) |
| `ChatClientAgent agent = chatClient.CreateAIAgent();` | `AIAgent agent = chatClient.AsAIAgent();` |
| `AgentRunResponse`, `response.ToString()` | `AgentResponse`, `response.Text` |
| — | `AsAIAgent(instructions, name, tools: [AIFunctionFactory.Create(WeatherTools.GetWeather, "get_weather")])` (S2) ; `Stopwatch.GetTimestamp()` / `GetElapsedTime()` ; `Interlocked.Increment` |
| `HttpRequestMessage` / `HttpResponseMessage`, `request.Headers.Add`, `Content.ReadAsStringAsync`, `base.SendAsync` | **inchangés** (.NET) |

Dépendances résolues (Start = Solution, `diff` des graphes vide, 50 paquets) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `System.ClientModel` 1.15.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. **Aucune préversion**, plus d'`Azure.AI.OpenAI` ni de `Microsoft.Extensions.Hosting`. Aucune API utilisée n'est expérimentale (seul le `#pragma OPENAI001` du socle).

## 3. Changements conceptuels

1. **`DelegatingHandler` au lieu d'un `HttpClientHandler` dérivé.** Tous les samples officiels 1.22.0 qui donnent un `HttpClient` à un SDK utilisent un `DelegatingHandler` avec `InnerHandler = new HttpClientHandler()` (5 samples ; un seul dérive de `HttpClientHandler`). Le handler n'intercepte que : le handler réseau reste séparé, les handlers se chaînent, et c'est le modèle d'`IHttpClientFactory` (« outgoing request middleware », cité en « Going further »). Concept du lab inchangé : `Transport` / `NetworkTimeout` sont les mêmes propriétés de `ClientPipelineOptions`.
2. **La clé API voyage désormais dans `Authorization: Bearer <clé>`** (SDK `OpenAI`), et non plus dans `api-key` (SDK Azure). Le handler voit la requête **après** l'authentification : tout journal d'en-têtes doit masquer la valeur entière. **Faille de l'ancien lab vérifiée** (baseline, §5 test 0b) : avec une clé API, l'ancienne Solution affichait l'en-tête `api-key` **en clair** — 1 ligne de sortie contenait la clé complète, 0 était masquée (compté par script, valeur jamais affichée) ; son masquage d'`Authorization` (20 premiers caractères) aurait en plus exposé 13 caractères de la clé avec le SDK `OpenAI`. Le masquage est maintenant fait par `HttpConsole.MaskIfSensitive` (schéma seul), enseigné dans le README (section *Security*) et vérifié par un check du dashboard.
3. **Ce que l'agent envoie est montré, pas supposé.** URL v1 (`/openai/v1/chat/completions`, sans `api-version`), nom du déploiement dans le champ `model`, `User-Agent` qui révèle les couches (`OpenAI/2.13.0 …,MEAI/10.10.0 agent-framework-dotnet/1.22.0 …`), instructions en message `system`, outils dans `tools` avec leur schéma JSON. Le handler numérote les requêtes, mesure la durée de chaque appel et les compte par exécution (cas d'usage « metrics » de l'ancien README).
4. **Scénario 2 ajouté : une exécution, plusieurs requêtes HTTP.** Avec un outil, la 1ʳᵉ réponse a `"finish_reason": "tool_calls"`, l'agent exécute l'outil, la 2ᵉ requête rejoue la conversation + `tool_calls` + un message `"role": "tool"` (résultat en chaîne JSON) ; l'usage de l'`AgentResponse` est la **somme** des `usage` des réponses (vérifié : 74 + 111 = 185, 14 + 17 = 31). Justification : l'objectif du lab est d'intercepter *toutes* les requêtes ; l'agent et l'outil sont ceux du sample officiel `01-get-started/02_add_tools` et de la page Learn « Using function tools with an agent », qui décrit cette boucle ; aucune nouvelle API n'est introduite. Il rend aussi les checks du dashboard déterministes (le résultat d'outil journalisé ne dépend pas du modèle).
5. **Retries, erreurs, streaming.** Chaque tentative du SDK repasse par le handler (vérifié hors ligne : `503` puis `200` → 2 passages) ; une erreur est journalisée avant l'exception (`404 DeploymentNotFound`, `401`, vérifiés) ; un corps `text/event-stream` n'est pas lu (le lire attendrait la fin du flux). Documentés dans « How it works » et le dépannage.

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Azure.Identity` beta, MAF préversion, `Microsoft.Extensions.Hosting` : remplacés (§2).
- `CustomHttpClientHandler.cs` (TODO dans un fichier séparé, Start ≠ Solution) : remplacé par `HttpLoggingHandler` dans `Program.cs` (TODO 11–14) + `HttpConsole.cs` fourni. Le fichier de l'apprenant contenait aussi l'affichage (`GetStatusLabel`, `WriteFormattedJson`), désormais dans le helper.
- **Masquage écrit par l'apprenant** (exemple de code du README, 20 premiers caractères) : remplacé par le masquage complet du helper — la leçon « ne jamais journaliser un identifiant » reste au README.
- Titre « Running Scenario 1 » → format des labs migrés (`=== Scenario 1: … ===`).
- **Non enseigné, cité en « Going further »** : journalisation intégrée `ClientLoggingOptions` (vérifiée : `REDACTED`), `PipelinePolicy` + `AddPolicy` (vérifiée), `IHttpClientFactory`, `UseOpenTelemetry`, middleware d'agent (Lab03), handler pour un client MCP.
- **Non retenu** : en-tête de corrélation `x-ms-client-request-id` — Azure OpenAI renvoie la valeur envoyée (observé), mais ce n'est pas documenté pour Azure OpenAI.
- Aucun objectif pédagogique supprimé : interception, ajout d'en-tête, journalisation formatée et transport personnalisé conservés ; ajout du S2.

## 5. Tests effectués et résultats

Environnement : macOS (culture fr-FR), SDK .NET 10.0.402 (runtimes 10.0.13), variables `AzureOpenAI__*` de l'environnement (présence vérifiée, valeurs jamais affichées : chaque sortie a été filtrée — hôte remplacé, occurrences de la clé comptées), `az login` valide, déploiement `gpt-4o-mini` (France Central).

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build) | ✅ Start et Solution compilent (préversions ; 6 warnings NU1902/NU1903) |
| 0b | Baseline fonctionnelle (ancienne Solution, clé API, sortie analysée par script sans afficher les valeurs) | ✅ réponse « Paris » ; URL `/openai/deployments/<déploiement>/chat/completions?api-version=2025-04-01-preview`, `User-Agent: azsdk-net-AI.OpenAI/2.7.0-beta.2 …` ; ⚠️ **en-tête `api-key` affiché en clair** : 1 ligne contenant la clé complète, 0 masquée |
| 0c | Sonde 1.22.0 (`DelegatingHandler` + `HttpClientPipelineTransport`, projet jetable dans `.tmp-probe`) | ✅ clé API → `Authorization: Bearer` (longueur seule affichée) ; Entra ID OK ; outil → 2 requêtes ; streaming → `text/event-stream`, OK ; écho de `x-ms-client-request-id` observé |
| 0d | Comportements hors ligne (handler factice) | ✅ `503` puis `200` → 2 passages dans le handler ; délai dépassé → `AggregateException: Retry failed after 4 tries. (… exceeded the configured timeout …)` (4 tentatives) ; `InnerHandler` absent → `InvalidOperationException: The inner handler has not been assigned.` ; `PipelinePolicy` `BeforeTransport` voit `Authorization` ; `ClientLoggingOptions` écrit `[Authorization, REDACTED]` |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances (`--include-transitive`) | ✅ MAF 1.22.0, OpenAI 2.13.0, System.ClientModel 1.15.0, MEAI 10.10.0 ; aucune préversion ; pas d'`Azure.AI.OpenAI` ni de Hosting ; graphes Start et Solution **identiques** (50 paquets) |
| 3 | Vulnérabilités (`--vulnerable --include-transitive`) | ✅ aucune introduite : seuls `SharpCompress` 0.30.1 / `Snappier` 1.0.0 via `CommonUtilities` (identique à la baseline) |
| 4 | `dotnet build -warnaserror -nowarn:NU1902,NU1903 --no-incremental` Start + Solution | ✅ `0 Warning(s) 0 Error(s)` ; sans `-nowarn` : seulement NU1902/NU1903 (4 + 4) |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent(`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `GetNewThread`, `ChatMessageStore`, `Microsoft.Extensions.Hosting`, `Host.CreateApplicationBuilder`, `NoWarn`, `MEAI001`, `"APIKey"`, `ChatClientAgent`, `CustomHttpClientHandler`, `: HttpClientHandler`, `api-version`, `deployments/`, `.ToString()`, `preview.25`, `2.7.0-beta`, `1.18.0-beta`, `.NET 8`, `GetStatusLabel`, `Running Scenario`) | ✅ 0 occurrence dans `Start/` et `Solution/` ; README : dans l'encadré « Coming from an older version » (8 lignes), plus « no `api-version` » (description de l'API v1) et le slug du lien Learn `api-version-lifecycle` |
| 6 | Run Solution — **clé API** | ✅ S1 : `POST …/openai/v1/chat/completions`, `X-agent-name` présent, `Authorization: Bearer [MASKED]`, `200 OK (OK) in 1485 ms`, « The capital of France is Paris. », `HTTP requests sent by this run: 1`, usage 16 / 7 / 23 ; S2 : requête #2 avec `system` + `tools` (`get_weather`), réponse #2 `tool_calls`, requête #3 avec `"role": "tool"` et le résultat de l'outil, réponse finale « …cloudy with a high of 15 degrees Celsius », `2` requêtes, usage 185 / 31 / 216 ; **0 occurrence de la clé** ; exit 0 |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`) | ✅ même comportement ; usage 16/7/23 et 185/32/217 ; aucune clé ni jeton (`eyJ`) dans la sortie ; exit 0 |
| 7b | Chemins d'erreur | ✅ déploiement inexistant : `[Status] 404 NotFound (CLIENT ERROR)`, corps `"code": "DeploymentNotFound"`, puis `ClientResultException: HTTP 404 (invalid_request_error: DeploymentNotFound)`, exit 134 ; clé invalide : `Authorization: Bearer [MASKED]`, `401 Unauthorized`, *Access denied due to invalid subscription key…*, `ClientResultException: HTTP 401`. Messages repris dans le dépannage |
| 8 | Configuration absente / placeholder (variables retirées, `HOME` isolé dans un dossier `.tmp-` du lab, supprimé) | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set "AzureOpenAI:Endpoint" <value>', or with the environment variable 'AzureOpenAI__Endpoint'.` ; idem `ChatDeploymentName` |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche la configuration, les 2 en-têtes et les 2 prompts, **exit 0**, aucun appel réseau |
| 10 | Start complété **uniquement d'après les indices du README** (copie `.tmp-start-check` dans le dossier du lab ; TODO 1–14 remplacés **par script** à partir des cellules des tableaux ; supprimée ensuite) | ✅ 0 TODO restant, `-warnaserror` sans warning, **code identique à la Solution** (hors commentaires et espaces), même comportement (1 puis 2 requêtes, Paris, 15 degrés, 16/7/23 et 185/32/217), 12/12 checks du dashboard |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (6 fichiers partagés) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–14 a un indice qui contient le code de la Solution (prouvé par le test 10) ; sortie attendue issue du run réel ; liens vérifiés (HTTP 200) |
| 13 | Non-régression | Sans objet pour `CommonUtilities` (non modifié) ; dashboard : 152/152 tests (§9) |

## 6. Problèmes rencontrés

1. **Fuite de la clé API dans l'ancien lab** (§3.2) : découverte en baseline ; le test a été conçu pour ne jamais afficher la valeur (comptage par script). Corrigée par le masquage complet et un check dédié.
2. **Règle « seul `Program.cs` diffère »** incompatible avec l'ancien exercice (l'apprenant écrivait `CustomHttpClientHandler.cs`) : le handler passe en bas de `Program.cs` (types déclarés après les top-level statements), l'affichage dans un fichier fourni.
3. **Page Learn conceptuelle de System.ClientModel introuvable** (`/dotnet/azure/sdk/system-clientmodel` → 404) : la page de référence du paquet (README Learn) et les docs XML 1.15.0 ont fait foi.
4. **Dashboard de l'utilisateur en cours d'exécution** (PID 15339, 127.0.0.1:5057, depuis `bin/Debug`). Contrairement à Lab10, `dotnet test` **a recompilé** `LabDashboard.dll` (horodatage 12:04 → 13:48 dans `bin/Debug` et `obj/`), sans modification de code du dashboard (seul `labs.json` a changé). Le processus est resté sain (`GET /api/labs` → 200 après coup) ; il ne verra Lab11 (ni Lab10, absent de sa liste) qu'après redémarrage. La vérification a utilisé une **instance séparée** (port 5064, historique dans `Dashboard/.data/lab11-verification`, reporting désactivé, donc aucun envoi au serveur d'administration), arrêtée et supprimée ensuite.
5. **Index git** : un `git rm` a mis en index la suppression de `CustomHttpClientHandler.cs` ; annulé aussitôt (`git reset` sur ces chemins) — l'index est laissé tel que trouvé, rien n'est commité.
6. **Fichiers hors dépôt** : aucun lu ni écrit, hors docs XML des paquets dans le cache NuGet. Le dépôt officiel (tag `dotnet-1.22.0`, `dotnet/samples` + `dotnet/src`) a été cloné dans `<lab>/.tmp-probe` (ignoré), avec les sondes et les sorties filtrées des runs, supprimé en fin de session. Pages Learn lues par requête HTTP.
7. **Longueur de la sortie** (~620 lignes par run) : voulue (chaque en-tête et chaque corps JSON) ; le dashboard l'affiche sans problème, les checks s'exécutent en ≤ 2 ms.

## 7. Décisions à valider

- [x] **Scénario 2 ajouté** (function calling vu sur le fil) — justifié par l'objectif du lab, le sample `02_add_tools` et la page Learn « function tools » ; aucune nouvelle API. *(validé le 2026-10-25)*
- [x] **Handler dans `Program.cs`** (TODO 11–14) et affichage dans `HttpConsole.cs` fourni, pour respecter la règle 9. *(validé le 2026-10-25)*
- [x] **Masquage fourni** par le helper (au lieu d'être écrit par l'apprenant) : évite qu'un exercice inachevé affiche une clé ; la leçon est portée par le README (*Security*) et un check. *(validé le 2026-10-25)*
- [x] **`DelegatingHandler`** (samples officiels) ; classe renommée `HttpLoggingHandler`. *(validé le 2026-10-25)*
- [x] **Compteur de requêtes et durée** ajoutés (cas d'usage « metrics » de l'ancien README) ; squelette du Start qui compte déjà les requêtes (l'agent fonctionne avant le journal). *(validé le 2026-10-25)*
- [x] **`NetworkTimeout = 30 s`** conservé (valeur de l'ancien lab) ; peut être court pour un modèle de raisonnement (documenté au dépannage). *(validé le 2026-10-25)*
- [x] **Journalisation intégrée (`ClientLoggingOptions`) et `PipelinePolicy`** seulement en « Going further » (le lab enseigne le transport, demandé par son titre ; les deux alternatives sont vérifiées). *(validé le 2026-10-25)*
- [x] **Checks** : la plupart reposent sur le journal (déterministe) ; seuls « Paris » et « 15 » dépendent du texte du modèle. *(validé le 2026-10-25)*
- [x] **Niveau *Intermediate*** dans le dashboard (le lab suppose Lab01 et Lab03), timeout 180 s.
- [x] **Nom du rapport** `Lab11-Migration-Report.md`, comme les 10 rapports existants.

## 8. Points pour les prochains labs

- **Streaming** : un `RunStreamingAsync` a réussi le 2026-10-25 via la sonde (manuel §8) alors que la régression Azure le faisait échouer depuis le 2026-09-28 → re-tester Lab01 scénario 5, Lab02 et Lab06 ; si c'est confirmé, retirer le contournement temporaire de Lab06_A2AServer.
- **Lab12 (AIContextProvider)** : même socle ; un handler comme celui de Lab11 est le moyen le plus direct de **voir** ce qu'un provider injecte dans la requête (messages ajoutés, instructions) — utile pour le README ou le dépannage.
- **Tout lab qui journalise des en-têtes ou des requêtes** : avec le SDK `OpenAI`, la clé API est dans `Authorization: Bearer` → masquage complet ; ne jamais afficher « les premiers caractères ».
- **Dashboard** : un `dotnet test` peut recompiler `LabDashboard.dll` même sans changement de code (ici après une modification de `labs.json`) ; une instance utilisateur en cours continue de fonctionner mais ne voit le nouveau lab qu'après redémarrage. Pour les checks d'un journal complet : bornes `(?:(?!^=== HTTP RE).)*?` pour rester dans un échange, et lookahead négatif ancré à `\A` pour interdire un motif dans toute la sortie (une seule tentative).
- **Outils** : le résultat d'un outil `string` arrive au modèle en chaîne JSON (`"content": "\"…\""`), visible dans le journal (cf. §4.14).

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire. Lab11 n'est pas interactif, n'a pas de compagnon et ne démarre aucun serveur ; sa sortie est longue mais n'est que du texte. L'analyseur d'usage n'est pas trompé par le journal HTTP : les objets `usage` des corps (`"prompt_tokens": 74`) et les en-têtes `x-ratelimit-remaining-tokens` ne sont pas des lignes `Input tokens: N` (test dédié).

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab11` : métadonnées EN + FR, projets Start/Solution, timeout 180 s, **12 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab11 sur la sortie réelle de la Solution (abrégée : filtres de contenu, latences, ids et la plupart des en-têtes de réponse retirés ; tous passent, ni interactif ni compagnon) et sur la sortie réelle du Start (seul `config` passe) ; handler qui ne fait que transmettre (seuls `config`, `scenario1-answer`, `scenario1-usage`, `scenario2-answer`, `scenario2-usage` passent) ; identifiant journalisé (masquage après 20 caractères, `api-key` en clair) **et** modèle qui répond sans appeler l'outil (`credentials-masked`, `scenario2-tool-call`, `scenario2-tool-result`, `scenario2-answer` échouent) ; rapports de tokens `Scenario 1 · Token Usage` / `Scenario 2 · Token Usage` (201 / 239) |
| Documentation | `Dashboard/README.md` : labs enregistrés, note Lab11, conseil de checks pour les labs qui journalisent du HTTP, exemple « Add a lab » (Lab12), liste des tests |

**Choix des checks** (chacun échoue sur le Start livré et passe sur la Solution) :

| Check | Preuve |
|---|---|
| `config` | `Endpoint:` |
| `scenario1-request` | `=== HTTP REQUEST #N ===` suivi de `[URL] POST https://…/openai/v1/chat/completions` (requête journalisée, URL v1) |
| `scenario1-header` | `X-agent-name: AIAgentWithCustomHttpTransport` dans les en-têtes d'une requête du S1 (ajouté par le handler) |
| `credentials-masked` | au moins une ligne `Authorization: Bearer [MASKED]` **et** aucune ligne `Authorization:` / `api-key:` non masquée dans toute la sortie (lookahead négatif ancré à `\A`) |
| `scenario1-response` | `=== HTTP RESPONSE #N ===` suivi de `[Status] 200 OK (OK) in N ms` (statut et durée) |
| `scenario1-answer` | après `Agent answer:`, « Paris », puis `HTTP requests sent by this run: N` (N ≥ 1) |
| `scenario1-usage` / `scenario2-usage` | usage du scénario, borné à l'en-tête suivant |
| `scenario2-tools` | dans une requête du S2 : `"tools": [` … `"name": "get_weather"` |
| `scenario2-tool-call` | dans une réponse du S2 : `"finish_reason": "tool_calls"` … `"name": "get_weather"` |
| `scenario2-tool-result` | dans une requête du S2 : `"role": "tool",` … `"content": "\"The weather in …Amsterdam… is cloudy with a high of 15 degrees Celsius.\""` (le résultat de l'outil renvoyé au modèle) |
| `scenario2-answer` | après `Agent answer:`, « 15 », puis `HTTP requests sent by this run: N` (N ≥ 2) |

**Résultats** (instance de vérification sur le port 5064, historique dans `Dashboard/.data/lab11-verification` — git-ignoré — supprimé ensuite ; reporting désactivé ; `Dashboard/.data/history.json` de l'utilisateur non utilisé) :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ **152/152** (148 + 4 tests Lab11 ; catalogue à 12 labs) |
| Validation des regex avec le moteur .NET sur les sorties complètes (~620 lignes) | ✅ 12/12 sur la Solution clé API, 12/12 sur la Solution Entra ID, 12/12 sur le Start complété, seul `config` sur le Start livré ; chaque check ≤ 2 ms (délai de l'analyseur : 1 s) |
| API : `GET /api/labs` | ✅ 12 labs (lab01 → lab11) |
| API : détails, README rendu, paquets, diff de solution | ✅ README rendu (40,4 Ko HTML) ; traduction FR exposée (`lab.translations.fr`, titre « Agent IA avec un transport HTTP personnalisé ») ; 6 paquets listés (MAF 1.22.0, Azure.Identity 1.21.0, Configuration 10.0.12) ; seul `Program.cs` diffère |
| Run Lab11 **Start** livré via l'API | ✅ `failed` attendu à l'étape `checks` — « 11 of 12 check(s) failed », seul `config` passe, exit 0, aucun usage |
| Run Lab11 **Solution** via l'API | ✅ **`passed`** — « All 12 checks passed », exit 0, 4,3 s, aucune ligne stderr, 644 lignes de sortie dont `Authorization: Bearer [MASKED]` uniquement ; tokens affichés : `Scenario 1 · Token Usage` 16 / 7 / 23, `Scenario 2 · Token Usage` 185 / 31 / 216, total 239 ; avertissements de build : NU1902/NU1903 hérités ; 0 occurrence de la clé dans le flux d'événements |

Aucun artefact laissé : `.tmp-probe` (clone officiel, sondes, sorties filtrées) et `.tmp-start-check` supprimés, instance de vérification arrêtée et son dossier supprimé, port 5064 libre.
