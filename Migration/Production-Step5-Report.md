# Rapport — étape 5 du plan d'action : observer, héberger, ordonner le parcours (A11, A16, NL1, A12)

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.26.0** · **.NET 10** (`net10.0`)
> Références : plan d'action [`AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md`](../AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md) (§3 A11, A12, A16 ; §5 NL1 ; §7 parcours essentiel), [Migration-Manual.md](Migration-Manual.md) (règles §6, tests §7), [Communication-Step4-Report.md](Communication-Step4-Report.md)

**Statut : fait.** Lab11 enseigne l'observation d'un agent avec OpenTelemetry, Lab10 explique comment choisir entre MCP et A2A, un nouveau lab (**Lab13**) héberge un agent dans une API ASP.NET Core, et le parcours essentiel est publié (README racine et ordre du dashboard). Aucune API expérimentale ni aucun paquet en préversion dans les labs modifiés ou créés.

---

## 1. Changements réalisés

| Action | Lab | Changement | Pourquoi |
|---|---|---|---|
| **A11** | `Lab11-AIAgentWithCustomHttpTransport` (dossier inchangé) | Lab renommé **« Observe an Agent: HTTP Transport and OpenTelemetry »**. **Scénario 3** (TODO 15–19) : un `TracerProvider` (`Sdk.CreateTracerProviderBuilder().AddSource(...).AddInMemoryExporter(spans)`) ; un agent météo et un assistant de voyage qui l'appelle comme outil (`AsAIFunction()`), instrumentés au niveau du chat client (`clientFactory` → `UseOpenTelemetry`, spans `chat` et `execute_tool`) et de l'agent (`AsBuilder().UseOpenTelemetry`, span `invoke_agent`) ; `EnableSensitiveData` laissé à `false`. Le journal HTTP est coupé pendant le run (`HttpLoggingHandler.WriteTraffic`, fourni) ; `TraceConsole.WriteSpanTree` (fourni) affiche l'arbre des spans avec durées et tokens (`gen_ai.usage.*`). Paquets `OpenTelemetry` et `OpenTelemetry.Exporter.InMemory` **1.20.1** (dernière stable). README : section **« Reading a trace »**, concepts, sécurité (`EnableSensitiveData`), dépannage, « Going further » (Aspire Dashboard/OTLP, `AddHttpClientInstrumentation`, métriques) | Diagnostiquer avec l'outil standard ; voir l'intérieur d'un agent délégué (*« If you need observability into inner agent behavior, use tracing »*) |
| **A16** | `Lab10_ExposeAIAgentAsMCPTool` | README : section **« Expose an agent: MCP or A2A? »** (tableau : ce que devient l'agent, qui peut l'utiliser, conversation, découverte, tâches longues, statut stable / préversion ; règle de choix ; renvoi vers `AsAIFunction` dans un même processus). Dashboard : objectif ajouté (EN/FR) | Savoir choisir le canal d'exposition |
| **NL1** | **Nouveau** `Lab13-AIAgentInAspNetCoreApi` (projet `AIAgentWebApi`) | API ASP.NET Core (minimal API) démarrée par le programme, puis 4 scénarios joués comme un front end : (1) requêtes refusées avant l'agent — sans jeton `401` (`RequireAuthorization`), message vide `400` (problem details) ; (2) Alice : réponse streamée (`RunStreamingAsync` → `TypedResults.ServerSentEvents`), outil `get_my_profile` qui lit l'utilisateur dans le jeton (`IHttpContextAccessor` via `services:`), jamais dans un argument du modèle ; (3) la conversation continue dans une autre requête (`SerializeSessionAsync` / `DeserializeSessionAsync`, magasin indexé par utilisateur + conversation) ; (4) Bob envoie l'identifiant de conversation d'Alice et obtient sa propre conversation. Agent en **singleton** dans l'injection de dépendances (comme `Agent_Step06_DependencyInjection`), JWT bearer (`AddJwtBearer`, `MapInboundClaims = false`), `AddProblemDetails` / `UseExceptionHandler`. Fichiers fournis : `Tools/EmployeeDirectory.cs`, `Storage/ConversationStore.cs`, `Auth/DevTokenIssuer.cs` (jetons de développement à la place d'un fournisseur d'identité), `ApiConsole.cs` (client HTTP + `SseParser`), `ChatRequest.cs`. Mode `--serve` pour appeler l'API avec curl. TODO 1–10 ; README complet (flux, sécurité, dépannage vérifié, « Going further » : bibliothèque d'hébergement MAF **préversion/alpha**, Entra ID, limitation de débit) | Étape 7 du Get Started : sans elle, le parcours s'arrête à la console ; *« verify the authenticated user or tenant before resuming a conversation »* ([Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp)) |
| **A12** | `README.md` racine, `Dashboard/LabDashboard/labs.json` | **Parcours essentiel publié** sans renuméroter (README, section *Learning Path*) : Lab01 → 02 → 03 → 04 → 05 → 09 → 07 → 12 → MultiAgentSystem Lab01 → Lab06 serveur → Lab06 client → Lab11 → Lab13 ; **facultatifs** : Lab10, Lab08 (mention dans le README et en tête de leur résumé dans le dashboard, EN/FR). **Ordre du catalogue du dashboard = ce parcours** (facultatifs en fin). Lab01-UseAgentAsTool était déjà enregistré (étape 4) | Rendre visible le chemin essentiel |
| — | `Dashboard/LabDashboard/labs.json` | Lab11 : titre, résumé, objectifs, **+6 attentes** (`scenario3-*`) ; Lab13 : **nouveau lab** (8 attentes, EN/FR) | Le dashboard vérifie les comportements |
| — | `Dashboard/LabDashboard.Tests` | Catalogue (15 labs, ordre du parcours) ; sorties réelles de Lab11 (scénario 3) et de Lab13 (Solution, Start) ; tests négatifs : Lab11 (agents instrumentés sans le chat client, journal HTTP resté actif) et Lab13 (endpoint ouvert, clé de stockage sans l'utilisateur : Bob voit ROOM-B) ; libellés de consommation de Lab11 | Non-régression |
| — | `README.md` racine, `Migration-Plan.md` | Lab11 (nouveau titre, scénario 3), Lab13, Lab08/Lab10 facultatifs, limitations du dashboard (port 5200), nombre de labs | Cohérence |

## 2. Écarts au plan et décisions

- **A11 — exportateur en mémoire** : le plan évoquait un export console. L'exportateur console d'OpenTelemetry imprime chaque span sur plusieurs dizaines de lignes, mêlées au journal HTTP du lab ; le lab garde les spans en mémoire (`OpenTelemetry.Exporter.InMemory`) et les affiche en arbre, ce qui montre l'imbrication de l'agent appelé comme outil et permet des attentes stables. Le README indique les exportateurs d'une application (OTLP + Aspire Dashboard, Azure Monitor, console).
- **A11 — version d'OpenTelemetry** : **1.20.1**, dernière stable (les exemples MAF 1.26.0 compilent avec 1.18.0) ; build sans warning, aucune vulnérabilité.
- **A11 — interrupteur `WriteTraffic`** : propriété fournie dans `HttpLoggingHandler` (Start et Solution), pour couper le journal HTTP pendant le scénario 3 ; les TODO 11–14 du handler sont inchangés.
- **NL1 — numéro et emplacement** : `LearningLabs/AzureOpenAI/Lab13-AIAgentInAspNetCoreApi`, piste Azure OpenAI (même client et même configuration que les autres labs).
- **NL1 — authentification** : un émetteur de jetons de développement (`DevTokenIssuer`, clé aléatoire) remplace un fournisseur d'identité, pour que le lab s'exécute sans configuration (dashboard compris) ; le README le signale comme substitut et renvoie vers Entra ID (`Microsoft.Identity.Web`).
- **NL1 — outil** : le plan citait « les outils de Lab03 » ; le lab utilise un seul outil, `get_my_profile`, sur le modèle de Lab03 scénario 3 (`IServiceProvider`), parce qu'il enseigne le point de sécurité propre à une API : l'utilisateur vient du jeton, pas du modèle.
- **NL1 — sessions** : sérialisation manuelle (`SerializeSessionAsync` / `DeserializeSessionAsync`, API stables) comme prévu ; `AgentSessionStore` de la bibliothèque d'hébergement (expérimental `MAAI001`, étape 4) n'est cité qu'en « Going further ».
- **NL1 — défaut corrigé pendant la relecture** : sans `MapInboundClaims = false`, `FindFirstValue("sub")!` renvoyait `null` sans erreur — tous les utilisateurs auraient partagé la clé `/{conversationId}`. Le code lève désormais une exception (`500` problem details) ; vérifié par une exécution de contrôle, comme l'erreur de démarrage sans `AddProblemDetails()`.
- **A12 — « facultatif » dans le dashboard** : le dashboard n'a pas de notion de lab facultatif (le niveau sert aux XP et aux paliers). Sans modifier son code, les labs facultatifs sont placés en fin de catalogue et signalés en tête de leur résumé. Un indicateur dédié dans l'interface reste possible (modification du dashboard).

## 3. Tests effectués et résultats

| # | Vérification (manuel §7) | Résultat |
|---|---|---|
| 1–3 | Paquets ajoutés (`OpenTelemetry` 1.20.1, `OpenTelemetry.Exporter.InMemory` 1.20.1, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14) | ✅ stables ; aucune vulnérabilité (`dotnet list package --vulnerable --include-transitive`) |
| 4 | Build Start + Solution (Lab11, Lab13) | ✅ 0 erreur, **0 warning** |
| 6–7 | Lab11 Solution contre les attentes du dashboard | ✅ **18/18 sur 4 exécutions** (clé API ×3, Entra ID ×1) |
| 6–7 | Lab13 Solution | ✅ **8/8 sur 4 exécutions** (clé API ×3, Entra ID ×1) |
| 9 | Start livrés | ✅ Lab11 : exit 0, 3 en-têtes, seul `config` passe ; Lab13 : exit 0, l'API démarre sans endpoint (`404` à chaque scénario), seuls `config` et `api-started` passent |
| 11 | Start = Solution hors `Program.cs` | ✅ Lab11 (`TraceConsole.cs`, `HttpConsole.cs`, `Tools/`, configuration, `.csproj`), Lab13 (tous les fichiers fournis) |
| — | Dépannage de Lab13 | ✅ deux lignes vérifiées par des exécutions de contrôle (sans `MapInboundClaims = false` → `500` et *« The token has no 'sub' claim »* ; sans `AddProblemDetails()` → `InvalidOperationException` au démarrage) |
| — | `dotnet test` du dashboard | ✅ **214/214** |
| 10 | Start complété en suivant les indices | ⚠️ Non rejoué : les indices (et, pour Lab13, le code complet des TODO 9 et 10 dans le README) reprennent la Solution |

Observation sur la trace de Lab11 (exécution de référence) : 8 spans ; `invoke_agent TravelAssistant` = 204 tokens d'entrée (83 + 121, ses deux `chat`), alors que l'agent météo appelé comme outil en a consommé 171 de plus, visibles seulement dans ses spans ; 4 spans `chat` = 4 requêtes HTTP comptées par le handler.

## 4. Points d'attention

- **Lab11, scénario 3** : le nom des spans `chat` contient le nom du déploiement (convention GenAI), comme le champ `model` des requêtes du scénario 1.
- **Lab13** : port 5200 (`WebApi:BaseUrl`) ; en mode `--serve`, les jetons de développement sont affichés (clé aléatoire, valable le temps du programme). Bearer sur `http://localhost` pour la simplicité : le README exige HTTPS en production.
- **Ordre du dashboard** : la progression (XP, paliers) ne change pas — tous les labs de Lab04 à Lab08 sont de niveau Intermédiaire ; seul l'ordre d'affichage et la « prochaine mission » suivent désormais le parcours.

## 5. Suite

Toutes les étapes du plan d'action v2 sont faites. Restent hors périmètre de cette phase : les labs MultiAgentSystem Lab02 et Lab03 (orchestrations et workflows), et l'éventuel indicateur « facultatif » dans l'interface du dashboard.
