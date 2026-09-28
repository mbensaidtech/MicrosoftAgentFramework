# Rapport de migration — Lab06 (A2A) : `Lab06_A2AClient` **et** `Lab06_A2AServer`

> Date : 2026-09-28 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`) · A2A : `Microsoft.Agents.AI.A2A` / `.Hosting.A2A.AspNetCore` **1.22.0-preview.260918.1**, SDK `A2A` **1.0.0-preview2** (exception documentée, manuel §1)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.12 ajouté, §3, §5, §8), [Migration-Plan.md](Migration-Plan.md), rapports Lab01 à Lab05. Sources officielles (tag `dotnet-1.22.0`) : samples `02-agents/AgentProviders/a2a/Agent_With_A2A`, `02-agents/A2A/A2AAgent_AsFunctionTool`, `A2AAgent_ProtocolSelection`, `A2AAgent_Skills`, `05-end-to-end/A2AClientServer` (client + serveur), sources `Microsoft.Agents.AI.A2A` (`A2AAgent`, `A2AClientExtensions`, `A2ACardResolverExtensions`, `A2AAgentCardExtensions`), `Microsoft.Agents.AI.Hosting.A2A(.AspNetCore)` (`A2AAgentHandler`, `A2AServerServiceCollectionExtensions`, `A2AEndpointRouteBuilderExtensions`), documentation XML des paquets `A2A` / `A2A.AspNetCore` 1.0.0-preview2 ; page Learn [A2A SDK v1 Migration Guide](https://learn.microsoft.com/agent-framework/migration-guide/agent-to-agent-sdk-v1?pivots=programming-language-csharp).

**Statut : migrés (client et serveur).** Tous les critères du manuel (§7) sont remplis. Trois réserves : (1) le **périmètre a été étendu au serveur**, avec votre accord, parce que le client A2A v1 ne peut pas parler au serveur v0.3 ; (2) le serveur contient un **contournement temporaire** d'une régression streaming d'Azure OpenAI / `Microsoft.Extensions.AI.OpenAI`, qui casse aussi, aujourd'hui, le streaming de Lab01 et Lab02 (§6.2) ; (3) les warnings NU1902/NU1903 restent hérités de `CommonUtilities`.

---

## 1. Changements réalisés

### Lab06_A2AServer

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/A2AServer.csproj` | `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` **1.22.0-preview.260918.1** (commenté : préversion, pas de stable), `Azure.Identity` **1.21.0**, `UserSecretsId` partagé. Supprimés : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.A2A`/`.Hosting.A2A.AspNetCore 1.0.0-preview.251219.1`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251204.1`, `Microsoft.Agents.Hosting.AspNetCore 1.4.9-beta`, `Microsoft.Extensions.Hosting 10.0.0`, `<NoWarn>MEAI001</NoWarn>` | Socle ; paquet de plus haut niveau (il apporte `Microsoft.Extensions.Configuration.*` 10.0.12 : pas de référence explicite) ; aucun `NoWarn` global |
| `AzureOpenAISettings.cs`, `ConfigurationHelper.cs` | Socle Lab01 (namespace `A2AServer`) + `GetA2AServerSettings()` (URL absolue http(s)) et `GetAPIKeySettings()` (placeholder refusé, secret facultatif) | User-secrets, validation au démarrage |
| `A2AServerSettings.cs` (**nouveau**) | `BaseUrl` : adresse d'écoute **et** URL publiée dans les cartes | L'URL `http://localhost:5000` était codée en dur dans les cartes |
| `APIKeySettings.cs` | `SecretKey` facultatif et documenté comme secret | Le secret HMAC était versionné dans `appsettings.json` |
| `appsettings.json` | `APIKey` et `APIKeySettings:SecretKey` retirés ; `A2AServer:BaseUrl` ; `Logging` (`Microsoft.AspNetCore` : `Warning`) | Aucun secret versionné ; console lisible (une ligne par requête sinon) |
| `Tools/APIKeyTools.cs` | Secret aléatoire généré au démarrage si non configuré (`IsSecretConfigured`), `ArgumentNullException.ThrowIfNull`, `using` inutile retiré | Lab exécutable sans configuration de secret |
| `AgentCards.cs` | Cartes A2A v1 : `SupportedInterfaces` (JSON-RPC + HTTP+JSON, version `1.0`) construites à partir de l'URL de l'agent ; `DefaultInput/OutputModes` `text/plain` ; **skill `DetectTone` ajouté** au CustomerToneAgent (il n'en avait aucun) | `AgentCard.Url` n'existe plus ; format du sample `A2AClientServer` (`PolicyAgentCard`) |
| `StreamingWorkaround.cs` (**nouveau, temporaire**) | `IChatClient.WithNonStreamingResponses()` : middleware `ChatClientBuilder.Use(getStreamingResponseFunc: …)` qui sert les requêtes streaming par un appel non streaming (`ToChatResponseUpdates()`) | Régression streaming (§6.2) : sans lui, toute requête A2A échoue |
| `Solution/Program.cs` | Réécrit : client `OpenAIClient` v1 (clé API ou Entra ID) ; `agentChatClient` (contournement) ; `AsAIAgent` (agent nommé **`AuthAgent`**, comme sa carte, au lieu de `APIKeyAgent`) ; `builder.AddA2AServer(agent)` ×2 ; `MapA2AJsonRpc` + `MapA2AHttpJson` par agent ; `MapWellKnownAgentCard(card, path)` par agent ; affichage des URL ; `app.RunAsync(BaseUrl)` | API A2A v1 du guide Learn et du sample `A2AClientServer` |
| `Start/Program.cs` | TODO **1 → 12** (setup 1–3 identique aux labs migrés, 4 = contournement, 5–12 = scénario) ; compile sans warning, affiche en-têtes et URL puis se termine | Format Lab04/Lab05 |
| `README.md` | Réécrit (gabarit Lab04) : encadré préversion, configuration (secret facultatif), fichiers fournis (dont le contournement), tableau des 12 TODO, tests `curl` des deux liaisons, schéma, concepts, namespaces, sortie réelle, dépannage (dont macOS/AirPlay), sécurité, « Going further », « Coming from an older version » | L'ancien README enseignait `MapA2A`, `ChatClientAgent`, `CreateAIAgent`, un secret dans `appsettings.json` et une URL de carte fausse |

### Lab06_A2AClient

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/A2AClient.csproj` | Socle + `Microsoft.Agents.AI.A2A` **1.22.0-preview.260918.1** ; mêmes suppressions que le serveur (sauf `Microsoft.Agents.Hosting.AspNetCore`, absent) | Idem |
| `AzureOpenAISettings.cs`, `ConfigurationHelper.cs` | Socle + `GetRemoteAgentSettings(name)` (section `RemoteAgents:<name>`, URL validée) | Ancienne lecture via `Host.CreateApplicationBuilder` : `appsettings.json` n'était **pas lu** (content root = dossier courant), le nom et la description du Solution s'affichaient vides |
| `RemoteAgentSettings.cs` (**remplace** `RemoteAuthAgentSettings.cs`) | `Url`, `Name`, `Description` d'un agent distant | Deux agents distants au lieu d'un |
| `AgentConsole.cs` (**nouveau**) | `WriteAgentCard`, `WriteToolCalls`, `WriteTokenUsage`, `FindApiKey` (regex du format `Meknes<aléatoire>.<signature>`) | Helpers fournis (pas de `CS8321` dans le Start ; l'ancien Start en avait **4**) |
| `appsettings.json` | `APIKey` retiré ; `RemoteAgents:AuthAgent:Url`, `RemoteAgents:CustomerToneAgent:{Name, Description, Url}` | Aucun secret ; configuration directe du scénario 2 |
| `Solution/Program.cs` | Réécrit : 3 scénarios (§3) ; `A2ACardResolver.GetAgentCardAsync`, `AgentCard.AsAIAgent`, `A2AClient.AsAIAgent`, `A2ACardResolver.GetAIAgentAsync`, `AsAIFunction` | API A2A v1 et samples officiels |
| `Start/Program.cs` | TODO **1 → 12** (setup 1–3, S1 4–7, S2 8–9, S3 10–12) ; compile sans warning et s'exécute | Idem |
| `README.md` | Réécrit sur le même gabarit ; « Step 0 » (démarrer le serveur) | L'ancien README enseignait `GetAIAgent()` et une URL de carte fausse (`/.well-known/a2a/authAgent/agent.json`) |

### Dashboard et documentation

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Dashboard/LabDashboard/Catalog/LabCatalog.cs` | `LabCompanion` (`Role` `server`/`client`, `Project`, `ReadyPattern`, `ReadyTimeoutSeconds`, `Environment`) + validation (chemin dans le dépôt, `.csproj` existant, regex, noms de variables, pas de lab interactif) | §9 |
| `…/Execution/LabRunner.cs`, `ProcessRunner.cs` | Build du compagnon ; exécution avec compagnon serveur (démarré, attendu, arrêté) ou client (lancé quand le lab est prêt) ; étapes d'échec `companion` / `ready` ; flux `companion` avec ses propres masqueurs de secrets ; variables d'environnement par run | §9 |
| `…/Program.cs`, `wwwroot/js/dashboard.js`, `i18n.js` (EN + FR), `css/dashboard.css` | `companion` exposé dans le résumé du lab ; note « À propos » ; résumés d'échec ; couleur des lignes `companion` | §9 |
| `…/labs.json` | Entrées `azureopenai-lab06-server` (10 checks) et `azureopenai-lab06-client` (9 checks), EN + FR, port dédié 5071 | §9 |
| `Dashboard/LabDashboard.Tests/*` | Ids au `LabCatalogTests` ; test du couplage des deux labs ; 5 cas de compagnon invalide ; `CompanionRunTests` (4 exécutions de bout en bout) ; 4 tests `OutputAnalyzerTests` Lab06 (sorties réelles) | §9 |
| `Dashboard/README.md`, `README.md` (racine) | Labs enregistrés/migrés, section « Labs run in pairs (companion) », scénarios du Lab06, conseil de checks | Documentation |
| `Migration/Migration-Manual.md` | §1 (exception A2A appliquée), §3 (A2A v0.3 → v1), **§4.12 A2A v1**, §5 (3 lignes), §8 (**régression streaming**, port 5000 macOS) | Référence pour la suite |
| `Migration/Migration-Plan.md` | Statut, vague 5, ligne Lab06, §5 bis | Suivi |

`CommonUtilities` n'a **pas** été modifié.

## 2. APIs et dépendances modifiées

| Ancien (A2A 0.3, MAF preview) | Nouveau (A2A v1, MAF 1.22.0) |
|---|---|
| `new A2A.A2AClient(uri).GetAIAgent()` | `new A2A.A2AClient(uri).AsAIAgent(name:, description:)` (JSON-RPC, configuration directe) |
| `A2ACardResolver.GetAIAgentAsync()` (helper inutilisé) | `GetAgentCardAsync()` + `AgentCard.AsAIAgent()` (scénario 1) ; `GetAIAgentAsync()` (scénario 3) |
| — | `AIAgent.AsAIFunction()` (agent distant comme outil) |
| `app.MapA2A(agent, path, agentCard, taskManager => app.MapWellKnownAgentCard(taskManager, path))` | `builder.AddA2AServer(agent)` + `app.MapA2AJsonRpc(agent, path)` + `app.MapA2AHttpJson(agent, path)` + `app.MapWellKnownAgentCard(card, path)` |
| `AgentCard.Url` | `AgentCard.SupportedInterfaces` (`AgentInterface { Url, ProtocolBinding, ProtocolVersion }`) |
| `ChatClientAgent x = chatClient.CreateAIAgent(...)` | `AIAgent x = agentChatClient.AsAIAgent(...)` (`IChatClient`, contournement) |
| `AzureOpenAIClient` | `OpenAIClient` + endpoint v1 (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `AgentRunResponse` (implicite `var`) | `AgentResponse` |

Dépendances résolues (Start = Solution) — client : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `Microsoft.Agents.AI.A2A` 1.22.0-preview.260918.1, `A2A` 1.0.0-preview2, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.OpenAI)` 10.10.0, `Azure.Identity` 1.21.0. Serveur : les mêmes + `Microsoft.Agents.AI.Hosting(.A2A/.A2A.AspNetCore/.AspNetCore)` 1.22.0-preview.260918.1, `A2A.AspNetCore` 1.0.0-preview2, `Microsoft.Agents.AI.Workflows` 1.22.0 et `Microsoft.Extensions.Hosting` 10.0.12 (transitifs de `Microsoft.Agents.AI.Hosting`). **Aucun** `Azure.AI.OpenAI`. Seules préversions : celles de l'exception A2A. `AddA2AServer` (sans options), `MapA2A*`, `AsAIAgent`, `AsAIFunction` : aucun diagnostic expérimental à la compilation (seul `A2AServerRegistrationOptions` est `[Experimental]`, non utilisé).

## 3. Changements conceptuels

1. **Protocole A2A v1, incompatible avec v0.3.** Le client v1 reçoit *« Invalid JSON-RPC request: 'method' field is not a valid A2A method »* du serveur v0.3 (testé avec un prototype, serveur d'origine inchangé). D'où la migration conjointe (décision validée en cours de session).
2. **Serveur : enregistrer, mapper, publier.** `MapA2A` faisait tout ; en v1, l'agent est enregistré (`AddA2AServer`, clé = **nom de l'agent**), exposé avec **deux liaisons** (JSON-RPC et HTTP+JSON) et décrit par une carte publiée séparément. Le README explique le rôle de chaque étape et fournit un test `curl` de chaque liaison.
3. **Une carte par agent** à `<path>/.well-known/agent-card.json` (paramètre `path` de `MapWellKnownAgentCard`). Les anciennes cartes étaient à `/a2a/authAgent/v1/card`, et la route `.well-known` d'origine renvoyait **500** (deux cartes à la racine).
4. **Client : découverte vs configuration directe.** L'ancien lab créait deux fois l'AuthAgent, dont une méthode jamais appelée. Désormais : scénario 1 = découverte par la carte (well-known URI) avec affichage des skills et des interfaces ; scénario 2 = configuration directe (`A2AClient` + nom/description fournis par le client) sur le **CustomerToneAgent**, que l'ancien client n'utilisait jamais.
5. **Nouveau scénario 3 : agent distant comme outil** (`AsAIFunction`), justifié par le sample officiel `A2AAgent_AsFunctionTool`. Il donne enfin un rôle au client Azure OpenAI, qui était créé sans être utilisé dans l'ancien client, et produit un usage de tokens.
6. **Vérifiabilité.** L'ancien client prenait toute la réponse comme clé et affichait « valid ». Désormais `FindApiKey` extrait la clé, et une **copie modifiée** est aussi validée : seule la vérification de signature du serveur peut la rejeter. Les checks du dashboard s'appuient sur ce contraste.

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting` (lecture de configuration), `Azure.Identity` beta, `NoWarn` global : comme Lab01.
- `Microsoft.Agents.Hosting.AspNetCore` 1.4.9-beta (SDK M365 Agents, inutilisé) : supprimé.
- `MapA2A`, `ITaskManager`, `AgentCard.Url`, `GetAIAgent()` : remplacés (§2).
- Secret HMAC versionné (`MeknesUfoN…`) : **supprimé** du contenu versionné, secret facultatif en user-secrets, aléatoire par défaut. ⚠️ Il reste dans l'historique git : ne le réutilisez pas.
- `RemoteAuthAgentSettings` : remplacé par `RemoteAgentSettings` (deux agents).
- Fonctions locales du Start client (4 `CS8321`) : remplacées par des TODO en ligne et des helpers fournis.
- Aucun objectif pédagogique supprimé. Ajouts : scénario 3 du client, skill `DetectTone` dans la carte du CustomerToneAgent, secret aléatoire.

## 5. Tests effectués et résultats

Environnement : macOS, SDK .NET 10.0.100, ressource `*.cognitiveservices.azure.com`, déploiement `gpt-4o-mini`.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline (build, puis run de l'ancien client contre l'ancien serveur, port 5050) | ✅ build 0 erreur ; Start client : **4 `CS8321`**, Solution : 1 `CS8321` ; NU1902/NU1903. Le run fonctionnait, mais nom/description vides et clé = phrase entière. Carte `.well-known` : **500** |
| 0b | Client v1 (prototype) contre serveur v0.3 inchangé | ❌ attendu : `'method' field is not a valid A2A method` → décision de migrer le serveur |
| 1 | `dotnet restore` des 4 projets | ✅ |
| 2 | Graphe de dépendances | ✅ MAF 1.22.0 ; préversions limitées à l'exception A2A (alignées 1.22.0-preview.260918.1 / A2A 1.0.0-preview2) ; pas d'`Azure.AI.OpenAI` ; Start = Solution |
| 3 | Vulnérabilités | ✅ aucune introduite : seuls `SharpCompress` 0.30.1 / `Snappier` 1.0.0 via `CommonUtilities` (identique à la baseline) |
| 4 | `dotnet build -warnaserror` (4 projets, `--no-incremental`) | ✅ 0 warning C#, 0 erreur ; seuls NU1902/NU1903 hérités échouent sans `-nowarn:NU1902,NU1903` |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent(`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `ChatMessageStore`, `Microsoft.Extensions.Hosting`, `NoWarn`, `MEAI001`, `MapA2A(`, `ITaskManager`, `taskManager`, `Microsoft.Agents.Hosting.AspNetCore`, `RemoteAuthAgentSettings`, `APIKeyAgent`, `v1/card`, `ChatClientAgent `, `.ToString()`, `"APIKey"`, `SecretKey` dans `appsettings.json`) | ✅ 0 dans `Start/`, `Solution/` ; dans les README, uniquement dans l'encadré « Coming from an older version » |
| 6 | Run Solution — **clé API** (serveur `:5000` + client) | ✅ S1 : carte (2 skills, interfaces JSONRPC + HTTP+JSON A2A 1.0), clé `Meknes….…`, « valid », copie modifiée « not valid » ; S2 : « Frustrated… » ; S3 : 2 appels de l'outil `AuthAgent`, clé + « valid » ; usage 689 / 184 / 873 |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""` pour serveur **et** client, `az login`) | ✅ mêmes résultats ; usage 683 / 177 / 860 |
| 7b | Secret configuré (`APIKeySettings__SecretKey`) + appel `curl` HTTP+JSON | ✅ pas de message « random secret », clé générée |
| 7c | Liaison utilisée | JSON-RPC par défaut (journaux du serveur : `POST /a2a/…`) ; `PreferredBindings = [HttpJson]` → `POST …/message:send` ✅ |
| 8 | Robustesse de la configuration | ✅ Placeholders (user-secrets masqués par un HOME temporaire dans le dépôt) : `'AzureOpenAI:Endpoint' is not configured…` (client et serveur) ; `'RemoteAgents:AuthAgent:Url' must be an absolute http:// or https:// URL…` ; `'A2AServer:BaseUrl' is not configured…` ; `'APIKeySettings:SecretKey' still contains a placeholder…` ; serveur absent : `A2AException: HTTP request failed ---> Connection refused (localhost:5099)` (documenté) |
| 9 | Start livrés | ✅ compilent sans warning ; client : 3 en-têtes, exit 0 ; serveur : en-tête + URL, exit 0 (il n'écoute pas) |
| 10 | Copies temporaires des Start (`.tmp-start-check`, supprimées) complétées **uniquement** d'après les tableaux du README, exécutées l'une contre l'autre (port 5072) | ✅ 0 warning ; même comportement que la Solution (carte, clé valide, copie rejetée, ton « Frustrated », 2 appels d'outil, usage 618 / 186 / 804) |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (client 6 fichiers, serveur 9 fichiers) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ (validée par le test 10) ; une correction du README serveur (sortie du Start) |
| + | Vérifications README | ✅ `/` final requis par `A2ACardResolver` (sans : 404) ; liens Learn et samples (HTTP 200) |
| 13 | Non-régression | `CommonUtilities` inchangé ; Lab03 relancé dans le dashboard modifié : `passed` (12/12). **Lab01 (scénario 5) et Lab02 (scénario 4) échouent aujourd'hui** (exit 134, `InvalidOperationException … 'Null'`) : régression externe, sans lien avec cette migration (§6.2) |

## 6. Problèmes rencontrés

1. **Incompatibilité A2A v0.3 ↔ v1** (§3.1) : bloquant pour « migrer seulement le client ». J'ai posé la question, et vous avez choisi de migrer aussi le serveur.
2. **Régression streaming Azure OpenAI** (constatée aujourd'hui) : Azure envoie des annotations de filtre de contenu **sans `delta`**, et `Microsoft.Extensions.AI.OpenAI` 10.10.0 (et 10.10.1, dernière version publiée) lève une exception dans `OpenAIChatClient.TryGetReasoningDelta`. Ticket officiel ouvert : [dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790). Flux SSE brut capturé : dernier chunk de filtre = `choices[0]` sans `delta`. L'hébergement A2A appelle **toujours** `RunStreamingAsync` → *« Agent handler did not produce any response events »* sur les deux liaisons. Un agent « écho » sans Azure fonctionnait, ce qui a isolé la cause. **Contournement** : `StreamingWorkaround.cs` (API officielles MEAI). **Effets de bord hors périmètre, non corrigés** : Lab01 scénario 5 et Lab02 scénario 4 échouent maintenant (et donc leurs runs Solution dans le dashboard).
3. **Port 5000 sur macOS** : le récepteur AirPlay (ControlCenter) écoute sur `*:5000`. Kestrel se lie tout de même à `localhost:5000`, mais sans le serveur du lab, un client reçoit `403 Forbidden` d'AirPlay. C'est documenté dans les deux README, et le dashboard utilise le port 5071.
4. **Écart entre la doc Learn et le comportement** : le guide annonce HTTP+JSON par défaut, mais on observe JSON-RPC avec les cartes du lab (§5-7c). Le guide dit aussi « une carte par hôte », alors que `MapWellKnownAgentCard(card, path)` en sert une par agent. Le lab suit le comportement vérifié.
5. **Ambiguïtés de noms** : `A2A.AgentSkill` / `Microsoft.Agents.AI.AgentSkill` ; le namespace `A2AClient` masque le type `A2A.A2AClient`. Le lab écrit les noms complets, avec un commentaire.
6. **Environnement** : `timeout`/`gh` absents (contournés) ; Playwright a écrit des instantanés dans `.playwright-mcp/` à la racine du dépôt, **supprimés** ; `git rm --cached` lancé par erreur sur `RemoteAuthAgentSettings.cs`, **annulé** tout de suite (`git reset`) : l'index n'est pas modifié.

## 7. Décisions à valider

- **Extension du périmètre au serveur** (validée pendant la session) et **rapport unique** pour les deux labs.
- **Contournement streaming** dans le serveur (fichier fourni + TODO 4), à retirer dès qu'une version corrigée de `Microsoft.Extensions.AI.OpenAI` existe. Alternative : attendre le correctif (lab inutilisable jusque-là).
- **Secret de signature facultatif** (aléatoire au démarrage) plutôt qu'obligatoire en user-secrets.
- **Ajout du scénario 3 client** (`AsAIFunction`, sample `A2AAgent_AsFunctionTool`) et **utilisation du CustomerToneAgent** en scénario 2 ; skill `DetectTone` ajouté à sa carte.
- **Agent renommé `AuthAgent`** (ancien `APIKeyAgent`), comme sa carte.
- **Validation d'une clé modifiée** (scénario 1), pour que la vérification soit démontrable.
- **Dashboard** : mécanisme « companion » générique (§9), qui modifie le code du dashboard ; port dédié 5071.
- **Lab01/Lab02** : à re-tester après le correctif MEAI (ou à doter du même contournement : décision hors périmètre).

## 8. Points pour les prochains labs

- **MAS-Lab01** : `AsAIFunction()` est validé ici sur un agent distant ; le nom de l'outil est le nom de l'agent.
- **Tout lab qui fait du streaming** (Lab01, Lab02, Workflows MAS-Lab02/03, hébergement) : vérifier d'abord le §8 du manuel (régression #7790).
- **Hébergement** (Lab10, MAS) : `AddA2AServer`/`AddAIAgent` exigent `using Microsoft.Extensions.DependencyInjection;` ; `Microsoft.Agents.AI.Hosting` apporte `Microsoft.Extensions.Hosting` et `Microsoft.Agents.AI.Workflows` en transitif.
- **Labs serveur** dans le dashboard : utiliser `companion` (rôle `client`) et un port dédié via `environment`.
- **Phase 0 CommonUtilities** : toujours à faire (NU1902/NU1903).

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : une modification du code était **nécessaire**. Un run = un processus qui doit se terminer. Or le client a besoin d'un serveur en marche, et un serveur ne se termine jamais : le run du serveur finirait en *Timed out*, et celui du client en *Connection refused*. Solution retenue : un `companion` générique et optionnel dans `labs.json`. Les labs existants n'en déclarent pas et suivent le même code qu'avant (`RunLabAsync`).

| Rôle | Déroulé |
|---|---|
| `server` (client) | build lab + compagnon → démarrage du serveur de référence → attente de `Now listening on: http://localhost:5071` (60 s max) → run du lab → arrêt du serveur (arbre de processus tué) ; checks et tokens sur la sortie du lab |
| `client` (serveur) | build des deux → démarrage du lab → attente de la même ligne → run du client de référence → arrêt du lab ; checks et tokens sur la sortie du lab **puis** du client |

`environment` (`A2AServer__BaseUrl`, `RemoteAgents__*__Url` = `http://localhost:5071`) est appliqué aux deux processus et affiché dans le journal : pas de conflit avec un serveur lancé à la main sur 5000, ni avec AirPlay.

**Checks** (client : 9, serveur : 10 = `server-listening` + les mêmes) :

| Check | Preuve |
|---|---|
| `config` | `Endpoint:` puis `Deployment:` |
| `server-listening` (serveur) | `Now listening on: http://localhost:<port>` — le Start ne l'atteint jamais |
| `scenario1-card` | carte `AuthAgent`, skill `GenerateAPIKey`, **deux** interfaces `JSONRPC` et `HTTP+JSON` en `A2A 1.0` |
| `scenario1-key` | `Generated API key: Meknes<…>.<…>` |
| `scenario1-valid` | la clé générée est « valid », sans « not valid » ni « invalid » |
| `scenario1-tampered` | la copie modifiée est rejetée (seul l'outil du serveur le sait) |
| `scenario2-tone` | `Remote agent: CustomerToneAgent at …` puis un ton négatif (frustrated, angry, upset…) |
| `scenario3-tool` / `scenario3-key` | `Tool called: AuthAgent` ; une clé signée dans la réponse de l'agent local |
| `scenario3-usage` | usage du scénario 3, borné à l'en-tête suivant |

**Résultats** :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ **97/97** (dont `CompanionRunTests` ×4, validations de compagnon ×5, couplage Lab06, checks Lab06 sur sorties réelles, réponses sans outils distants) ; aucun nouveau warning d'analyse |
| API : `GET /api/labs` | ✅ 7 labs (lab01 → lab05, lab06-server, lab06-client) |
| API : détails, README rendu, paquets, diff de solution | ✅ README rendu (30 et 32 Ko) ; paquets listés ; seul `Program.cs` diffère ; `companion` exposé |
| UI (Playwright) | ✅ deux cartes « 06 » ; lignes `companion`, « Companion server ready. / stopped. » ; note « This lab calls a server… » dans « À propos » |
| Run **client Solution** | ✅ `passed` — « All 9 checks passed », `Scenario 3 · Token Usage` 612 / 181 / 793 |
| Run **client Start** | ✅ `failed` attendu — « 8 of 9 check(s) failed » (seul `config` passe), exit 0, aucun usage |
| Run **serveur Solution** | ✅ `passed` — « All 10 checks passed », 675 / 172 / 847 |
| Run **serveur Start** | ✅ `failed` attendu — étape `ready` : « The lab exited with code 0 before it was ready », seul `config` passe |
| Non-régression | ✅ Lab03 Solution `passed` (12/12, 4 rapports de tokens) ; aucun processus restant sur 5071 |

Aucun artefact laissé : l'instance de test (port 5059) écrivait dans `Dashboard/.data/lab06-verification` (git-ignoré), supprimé ; `Dashboard/.data/history.json` est inchangé.
