# Rapport de migration — Lab09-AIAgentWithFunctionToolsHumanApproval

> Date : 2026-10-02 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.15 ajouté, §3, §5, §6, §7, §8), [Migration-Plan.md](Migration-Plan.md), rapports [Lab01](Lab01-Migration-Report.md) à [Lab08](Lab08-Migration-Report.md). Sources officielles : sample `dotnet/samples/02-agents/Agents/Agent_Step01_UsingFunctionToolsWithApprovals` au tag `dotnet-1.22.0`, page Learn [Using function tools with human in the loop approvals](https://learn.microsoft.com/agent-framework/agents/tools/tool-approval?pivots=programming-language-csharp) (2026-09-28), release notes `dotnet-1.14.0` (#7107, #7111) et `dotnet-1.22.0` (#8375, #8432), docs XML des paquets `Microsoft.Agents.AI` 1.22.0 (`ChatClientAgentOptions`, `ApprovalResponseBindingChatClient`, `ApprovalNotRequiredFunctionBypassingChatClient`, `ToolApprovalAgent`) et `Microsoft.Extensions.AI.Abstractions` 10.10.0 (`ApprovalRequiredAIFunction`, `ToolApprovalRequestContent`, `ToolApprovalResponseContent`), source MEAI `FunctionInvokingChatClient` (texte du rejet) ; comportements **vérifiés hors ligne** avec un `IChatClient` factice (projet jetable dans le scratchpad) puis **en exécution réelle** contre Azure OpenAI (clé API et Entra ID).

**Statut : migré.** Tous les critères du manuel (§7) sont remplis, y compris l'exécution réelle de la Solution par les deux voies d'authentification et les runs du dashboard (Start `failed` attendu, Solution `passed` avec saisie `Y`). Seule réserve : les warnings NU1902/NU1903 hérités de `CommonUtilities` (identiques à la baseline).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AIAgentWithFunctionToolsHumanApproval.csproj` | Socle Lab01–08 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé. **Supprimés** : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251204.1`, `Microsoft.Extensions.Hosting 9.0.0`, `<NoWarn>MEAI001</NoWarn>` | Aucune préversion, aucun `NoWarn` global (règle 11 : aucune API enseignée n'est expérimentale) |
| `AzureOpenAISettings.cs`, `ConfigurationHelper.cs`, `appsettings.json` | Copies conformes de Lab01 (namespace `AIAgentWithFunctionToolsHumanApproval`) | User-secrets, validation au démarrage, aucun secret versionné |
| `Tools/SensitiveTools.cs` → **`Tools/HrTools.cs`** | Deux outils statiques avec `[Description]` : `GetEmployeeInfo` (non sensible) et `DeleteEmployeeData` (sensible, simulation, message d'origine conservé) ; rien dans le fichier ne dit qu'un outil exige une approbation (c'est `Program.cs` qui enveloppe) | Scénario 2 mélange outil libre et outil sensible ; la décision « approbation requise » appartient à la composition de l'agent |
| `EmployeeDirectory.cs` (**nouveau**) | Annuaire RH simulé (`Employee` record, 3 employés : EMP001 parti, EMP002 en poste, EMP003 parti) | Données déterministes pour les outils, la politique et les checks du dashboard |
| `DeletionPolicy.cs` (**nouveau**) | `Decide(FunctionCallContent)` → `ApprovalDecision(Approved, Reason)` : approuve la suppression d'un ancien employé, rejette le reste avec motif | Scénario 2 : décision d'approbation prise par du code (« a policy decision », doc officielle) ; fichier fourni pour que `Program.cs` reste centré sur l'API |
| `ApprovalConsole.cs` (**nouveau**) | `WriteApprovalRequest(request)`, `FormatCall(call)`, `WriteToolResults(runs)` (associe `FunctionResultContent` et `FunctionCallContent` par `CallId`, `JsonElement` → texte), `WriteTokenUsage(runs)` (`UsageDetails.Add` sur tous les runs) ; `using static` | Helpers d'affichage fournis (pattern `AgentConsole.cs` de Lab03) ; montrer ce que l'outil a renvoyé ou le rejet reçu par le modèle |
| `Solution/Program.cs` | Réécrit : client `OpenAIClient` v1 (clé API ou Entra ID) ; `scenariosToRun` ; **scénario 1** approbation humaine à la console (`ApprovalRequiredAIFunction`, `AsAIAgent`, `CreateSessionAsync`, requêtes lues dans `response.Messages`, boucle `while`, `Console.ReadLine` → `CreateResponse(approved)`, `RunAsync(messages, session)`, résultats d'outils, usage cumulé) ; **scénario 2** deux outils, prompt sur deux employés, décisions par `DeletionPolicy` avec `CreateResponse(decision.Approved, decision.Reason)` | APIs 1.22.0 et pattern du sample officiel ; refonte conceptuelle (§3) |
| `Start/Program.cs` | TODO renumérotés **1 → 17** (setup 1–3 identique à Lab01 ; S1 4–10 ; S2 11–17) ; prompts, en-têtes et `AgentInstructions` fournis (la constante est affichée dans le Setup pour que le Start compile sans `CS0219`) | Compile sans warning et s'exécute tel que livré (exit 0, aucun appel réseau) ; seul `Program.cs` diffère |
| `README.md` (lab) | Réécrit sur le gabarit Lab03 : objectif, configuration user-secrets / Entra ID / dashboard, fichiers fournis, tableaux des 17 TODO (chaque indice contient le code de la Solution), « How the approval flow works » (session obligatoire, outils non sensibles exécutés seuls, coût d'un rejet), concepts, namespaces, sortie attendue (issue d'un run réel), dépannage, packages, « Going further » (streaming, session persistée, `UseToolApproval` / « don't ask again », `DisableApprovalNotRequiredFunctionBypassing`, `RequiresConfirmation` expérimental, `AsHarnessAgent`), encadré « Coming from an older version », liens officiels | L'ancien README expliquait `AzureOpenAIClient`, `CreateAIAgent`, `GetNewThread`, `UserInputRequests`, `FunctionApprovalRequestContent`, `AgentRunResponse` et une clé dans `appsettings.json` |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab09` (EN + FR, niveau *Intermediate*, **`"interactive": true`**, timeout 240 s, 9 checks) | Intégration au dashboard (§9) ; premier lab interactif du catalogue |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 3 tests Lab09 dans `OutputAnalyzerTests` (sortie réelle de la Solution, sortie du Start livré, approbations non honorées, étiquetage des deux blocs d'usage) | Idem |
| `Dashboard/README.md`, `README.md` (racine) | Lab09 enregistré / migré ; note « lab interactif » ; exemple « Add a lab » passé sur Lab10 ; conseil de checks pour les labs à approbation ; scénarios du lab ; liste des tests | Documentation associée |
| `Migration/Migration-Manual.md` | §3 : lignes 1.14.0 (#7111, #7107) et 1.22.0 (#8375, #8432) ; **§4.15** « Approbations humaines sur les function tools » ; §5 : lignes `UserInputRequests`/`UserInputRequestContent` et `FunctionApprovalRequestContent`, `AgentThread` → Lab09 ✅ ; §8 : vérification des identifiants (`/models` ≠ chat), `CS0219` des Start | Référence pour les labs suivants |
| `Migration/Migration-Plan.md` | Statut, vague 4, ligne Lab09 (§5) et pratique Lab09 (§5 bis) → ✅ | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) + `DefaultAzureCredential` seul | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `ChatClientAgent agent = chatClient.CreateAIAgent(instructions, tools)` | `AIAgent agent = chatClient.AsAIAgent(instructions, name, tools)` |
| `var thread = agent.GetNewThread();` (`AgentThread`) | `AgentSession session = await agent.CreateSessionAsync();` — **obligatoire** (liaison des approbations) |
| `AgentRunResponse` | `AgentResponse` |
| `response.UserInputRequests` (`UserInputRequestContent`) — supprimé (#3682) | `response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>()` |
| `FunctionApprovalRequestContent` (`.FunctionCall`) | `ToolApprovalRequestContent` (`.ToolCall`, casté en `FunctionCallContent`) — MEAI 10.10.0 |
| `CreateResponse(approved)` | inchangé + surcharge `CreateResponse(approved, reason)` (motif transmis au modèle sur rejet) |
| `if (userInputRequests.Any()) { … }` | `while (approvalRequests.Count > 0) { … }` (pattern du sample : l'agent peut redemander) |
| `response.ToString()` | `response.Text` ; `FunctionCallContent` / `FunctionResultContent` lus dans `response.Messages` |
| `ApprovalRequiredAIFunction`, `AIFunctionFactory.Create`, `[Description]` | **inchangés** (Microsoft.Extensions.AI) |

Dépendances résolues (Start = Solution, `diff` des graphes vide) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0, `System.ClientModel` 1.15.0. **Aucune préversion**, plus d'`Azure.AI.OpenAI` ni de `Microsoft.Extensions.Hosting`.

## 3. Changements conceptuels

1. **Les demandes d'approbation sont des contenus de la réponse, plus une propriété dédiée.** `UserInputRequests` n'existe plus : la réponse d'un run qui attend une décision ne contient pas de texte, mais un `ToolApprovalRequestContent` par appel à approuver, dont `ToolCall` est le `FunctionCallContent` choisi par le modèle. Le lab lit ces contenus dans `response.Messages`, comme le sample et la page Learn.
2. **La session est obligatoire (liaison des approbations, 1.14 → 1.22 `[BREAKING]`).** `ChatClientAgent` injecte par défaut `ApprovalResponseBindingChatClient` : il enregistre dans le `StateBag` de la session chaque demande qu'il surface et n'honore qu'une réponse liée à une demande enregistrée (appel rebasé sur le nom et les arguments d'origine, consommé une seule fois). **Vérifié hors ligne** : sans session, la réponse d'approbation est ignorée en silence, l'outil n'est jamais exécuté et le modèle répond sans résultat. L'ancien « thread pour garder le contexte » devient « session pour que l'approbation soit valide » ; le README l'explique et le dépannage couvre le symptôme (`Tool results: none`).
3. **Seuls les outils sensibles demandent une approbation.** `FunctionInvokingChatClient` est « tout ou rien », mais `ChatClientAgent` ajoute `ApprovalNotRequiredFunctionBypassingChatClient` par défaut : les outils non enveloppés sont exécutés par l'agent lui-même. **Vérifié** hors ligne (outil libre + outil sensible dans le même tour → une seule demande) et en réel (scénario 2 : `get_employee_info` n'apparaît jamais comme demande).
4. **Ce que le modèle reçoit est montré, pas supposé.** Après approbation, la réponse contient le `FunctionCallContent` réémis et le `FunctionResultContent` (JsonElement) ; après rejet, un `FunctionResultContent` texte `Tool call invocation rejected.` suivi du motif. `WriteToolResults` l'affiche pour tous les runs du flux ; l'usage de tokens est cumulé sur tous les runs (un rejet coûte aussi un aller-retour).
5. **Scénario 2 ajouté : plusieurs demandes, décision par une politique.** Justification : la boucle sur plusieurs demandes (`ConvertAll`) est celle du sample officiel ; la page Learn (exemple complet, pivot Python) mélange exactement un outil sans approbation et un outil avec approbation sur plusieurs entités ; la doc XML décrit la confirmation comme « a user prompt, a policy decision, or any other approver » ; `CreateResponse(approved, reason)` est l'API publique. Seule la source de la décision change (code au lieu de la console) : mêmes contenus, même `CreateResponse`. Le scénario est déterministe, ce qui donne au dashboard des checks indépendants de la saisie.

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `Azure.Identity` beta, `<NoWarn>MEAI001</NoWarn>` : supprimés ou remplacés (cf. Lab01).
- `response.UserInputRequests`, `UserInputRequestContent`, `FunctionApprovalRequestContent.FunctionCall`, `GetNewThread` : remplacés (§2) ; cités uniquement dans l'encadré « Coming from an older version ».
- `Tools/SensitiveTools.cs` : remplacé par `Tools/HrTools.cs` (le message de l'outil de suppression est conservé).
- **Non enseigné, cité en « Going further »** : `ToolApprovalRequestContent.RequiresConfirmation` (**`[Experimental]` `MEAI001`**, vérifié à la compilation), `UseToolApproval` / `CreateAlwaysApproveToolResponse` (« don't ask again »), `DisableApprovalNotRequiredFunctionBypassing`, streaming, `AsHarnessAgent`.
- Aucun scénario supprimé : l'approbation humaine à la console d'origine est le scénario 1, enrichie de la boucle, des résultats d'outils et de l'usage cumulé.

## 5. Tests effectués et résultats

Environnement : macOS (culture fr-FR), SDK .NET 10.0.100, variables `AzureOpenAI__*` de l'environnement (aucun user-secret configuré), `az login` valide, déploiement `gpt-4o-mini`.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration | ✅ Start et Solution compilent (préversions ; 8 warnings NU1902/NU1903) |
| 0b | Vérification préalable des identifiants | ⚠️ `curl …/openai/v1/models` : clé **401**, Entra **400** — pourtant **les deux voies fonctionnent** en Chat Completions (tests 6–7). Cette commande n'est pas un test fiable (manuel §8 mis à jour) |
| 0c | Comportement 1.22.0 hors ligne (`IChatClient` factice, projet jetable hors dépôt) | ✅ demandes dans `response.Messages` (rôle assistant) ; approbation → `FunctionCallContent` + `FunctionResultContent` (JsonElement) ; rejet → `Tool call invocation rejected. <reason>` ; deux demandes traitées en un run ; outils mixtes → une seule demande ; **sans session → approbation ignorée** ; `UsageDetails.Add` cumule |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances (`--include-transitive`) | ✅ MAF 1.22.0, MEAI 10.10.0, OpenAI 2.13.0 ; aucune préversion ; pas d'`Azure.AI.OpenAI` ni de Hosting ; graphes Start et Solution **identiques** |
| 3 | Vulnérabilités (`--vulnerable --include-transitive`) | ✅ aucune introduite : seuls `SharpCompress` 0.30.1 / `Snappier` 1.0.0 via `CommonUtilities` (identique à la baseline) |
| 4 | `dotnet build -warnaserror` Start + Solution (`--no-incremental`) | ✅ `0 Warning(s) 0 Error(s)` (avec `-nowarn:NU1902,NU1903` hérités) |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent(`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `GetNewThread`, `ChatMessageStore`, `Microsoft.Extensions.Hosting`, `NoWarn`, `MEAI001`, `"APIKey"`, `UserInputRequest`, `FunctionApprovalRequestContent`, `SensitiveTools`, `ChatClientAgent `, `.NET 8`) | ✅ 0 occurrence dans `Start/`, `Solution/` ; dans le README, uniquement dans l'encadré « Coming from an older version » |
| 5b | Code des scénarios de la Solution exécuté hors ligne contre un modèle scripté (fichiers du lab compilés tels quels) | ✅ sorties `Y`, `N` et entrée fermée conformes (rejet si `ReadLine()` renvoie `null`) ; format des lignes des checks validé avant le run réel |
| 6 | Run Solution — **clé API**, saisie `Y` | ✅ S1 : `Agent run paused: 1 approval request(s) pending`, `Function: delete_employee_data` / `Arguments: employeeId=EMP001`, approbation, `Tool result (delete_employee_data): Sensitive operation executed: … 'EMP001' …`, réponse cohérente, `Token Usage (2 runs)` 317/38/355 ; S2 : `2 approval request(s) pending`, EMP001 approuvé / EMP002 rejeté avec motif, deux `get_employee_info` exécutés sans demande, rejet `Tool call invocation rejected. EMP002 (Bob Lee) is still employed…`, EMP001 supprimé, réponse qui distingue les deux cas, 879/175/1054 ; exit 0 |
| 6b | Run Solution — clé API, saisie `N` | ✅ `Function call rejected by user.`, `Tool result (delete_employee_data): Tool call invocation rejected.`, l'agent explique que la suppression a été refusée (aucun outil exécuté) ; 298/51/349 |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`), saisie `Y` | ✅ même comportement que le test 6 (317/38/355 ; 879/167/1046) |
| 8 | Config placeholder (variables retirées) / déploiement absent | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set "AzureOpenAI:Endpoint" <value>', or with the environment variable 'AzureOpenAI__Endpoint'.` ; message équivalent pour `ChatDeploymentName` |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche la configuration, les instructions, les 2 en-têtes et les 2 prompts, **exit 0** (aucun appel réseau, aucune lecture console) |
| 10 | Start complété **uniquement d'après les indices du README** (copie `.tmp-start-check` dans le dossier du lab, TODO 1–17 remplacés par le code des tableaux, supprimée ensuite) | ✅ compile `-warnaserror` sans warning, 0 TODO restant, **même comportement que la Solution** avec clé API (ce run-là, le modèle a demandé EMP002 avant EMP001 : la boucle et les checks s'en accommodent). La copie complétée est conservée hors dépôt (`scratchpad/Lab09-StartCompleted-Program.cs`) |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (10 fichiers partagés) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–17 a un indice qui contient le code de la Solution (prouvé par le test 10) ; sortie attendue mise à jour d'après le run réel |
| 13 | Non-régression | Sans objet : `CommonUtilities` n'est pas modifié ; les tests du dashboard passent avec les 9 labs déjà enregistrés (§9) |

## 6. Problèmes rencontrés

1. **Fausse alerte sur les identifiants.** Le `curl` recommandé par les rapports précédents (`GET /openai/v1/models`) renvoie 401 avec la clé et 400 avec Entra, alors que Chat Completions fonctionne avec les deux. Les blocages notés pour Lab07 et Lab08 venaient peut-être d'un incident réel à l'époque (leurs runs chat échouaient aussi), mais cette commande ne doit plus servir de critère : testé maintenant par un vrai appel. **Les tests d'exécution de Lab07 et Lab08 peuvent être rejoués.**
2. **`RequiresConfirmation` est expérimental** (`MEAI001`, découvert à la compilation du projet jetable) : retiré du code, cité en « Going further ».
3. **`CS0219` dans le Start** : une constante partagée (`AgentInstructions`) référencée seulement dans les commentaires TODO fait échouer `-warnaserror`. Résolu en l'affichant dans le Setup (`Agent instructions: …`), Start et Solution. Au passage : **le Start de Lab05 présente aujourd'hui 4 `CS0219`** en `-warnaserror` (constantes `AgentName`, `AgentInstructions`, `FirstQuestion`, `FollowUpQuestion`) — hors périmètre, non corrigé, consigné dans le manuel §8.
4. **Copie temporaire du Start** : placée dans `<lab>/.tmp-start-check/`, elle est au même niveau que `Start/` ; j'ai d'abord modifié à tort le chemin du `ProjectReference` (erreur MSB9008), corrigé en reprenant le `.csproj` tel quel. MSBuild a recréé `obj/` après la première suppression : dossier supprimé à nouveau en fin de session.
5. **API du dashboard** : les `POST` exigent l'en-tête `X-Lab-Dashboard: 1` (garde CSRF), sinon `403` ; `macOS` n'a pas de commande `timeout`.
6. **Ordre des résultats d'outils** : dans un même run, `FunctionInvokingChatClient` liste les rejets avant les exécutions, et le modèle peut demander les suppressions dans un ordre variable ; les checks du dashboard utilisent des lookaheads (ordre indifférent) et le README le précise.

## 7. Décisions à valider

- [x] **Scénario 2 (politique de décision avec motif, outils mixtes)** ajouté — justifié par le sample (boucle sur plusieurs demandes), la page Learn (exemple complet mixant les deux types d'outils) et la doc XML (« a policy decision ») ; il rend aussi les checks du dashboard déterministes. À confirmer.
- [x] **Niveau *Intermediate*** dans le dashboard (le lab suppose Lab03 et Lab05).
- [x] **Lab interactif dans le dashboard** (`"interactive": true`) : scénario 1 attend `Y` dans la zone de saisie ; les checks du scénario 1 exigent l'approbation (une saisie `N` fait échouer le run, volontairement documenté).
- [x] **`Agent instructions: …` affiché** dans le Setup (Start et Solution) pour utiliser la constante partagée : alternative possible, placer les instructions dans un fichier fourni.
- [ ] **Rejouer les tests d'exécution de Lab07 et Lab08** (et re-tester Lab01/Lab02 streaming, cf. manuel §8) maintenant que les identifiants fonctionnent.
- [ ] **Corriger les `CS0219` du Start de Lab05** lors d'un prochain passage (hors périmètre de ce lab).

## 8. Points pour les prochains labs

- **Vérification des identifiants** : lancer un vrai appel chat (p. ex. `dotnet run --project …/Lab01-FirstBasicAIAgent/Solution` avec `scenariosToRun = [1]`), pas `curl /models`.
- **Lab12 (AIContextProvider)** : même socle ; `AgentSession.StateBag` est aussi le mécanisme utilisé par la liaison des approbations (`ApprovalResponseBindingChatClient.StateBagKey`) — utile pour expliquer ce qu'une session contient.
- **Lab10 (agent exposé en MCP) et MAS-Lab01 (agent comme outil)** : un agent avec `ApprovalRequiredAIFunction` exposé en outil/MCP fait remonter la demande d'approbation à l'appelant ; penser à la session côté serveur.
- **Labs interactifs** : `"interactive": true` + `inputIdleTimeoutSeconds` ; les prompts `Console.ReadLine` doivent accepter `null` (entrée fermée) ; dans les tests du dashboard, l'écho de la saisie n'est pas dans stdout.
- **Start** : toute constante partagée doit être utilisée hors des TODO (affichage ou fichier fourni) pour que `-warnaserror` passe.

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire. Le mode interactif existait déjà (`"interactive": true`, hook `LabInputHook`) mais aucun lab du catalogue ne l'utilisait : Lab09 est le premier.

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab09` : métadonnées EN + FR (niveau *Intermediate*), projets Start/Solution, `interactive: true`, timeout 240 s (l'attente de saisie n'est pas décomptée), **9 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab09 sur la sortie réelle de la Solution (tous passent, `Interactive` vérifié) et sur la sortie réelle du Start (seul `config` passe) ; sortie « approbations non honorées » (approbation sans exécution, outil libre surfacé en demande, décisions inversées, EMP002 supprimé malgré le rejet : aucun check hors `-usage` ne passe) ; 2 rapports de tokens étiquetés par scénario (`Scenario 1 · Token Usage (2 runs)`, total 1 409) |
| Documentation | `Dashboard/README.md` : labs enregistrés, note lab interactif, exemple « Add a lab » (Lab10), conseil de checks pour les labs à approbation, liste des tests |

**Choix des checks** (chacun échoue sur le Start livré et passe sur la Solution) :

| Check | Preuve |
|---|---|
| `scenario1-request` | `Agent run paused: N approval request(s) pending` puis `APPROVAL REQUIRED`, `Function: delete_employee_data`, `Arguments: employeeId=EMP001` (la demande elle-même) |
| `scenario1-approved` | `Function call approved by user.` puis `Tool result (delete_employee_data): Sensitive operation executed: All data for employee 'EMP001' has been permanently deleted` (seul un outil réellement exécuté après l'approbation produit cette ligne) |
| `scenario2-info` | les deux `Tool result (get_employee_info): Employee EMP00x: …` (lookaheads, ordre indifférent) **et** absence de `Approval requested for get_employee_info` |
| `scenario2-policy` | `Approval requested for delete_employee_data(employeeId=EMP001)` suivi de `Policy decision: approved - EMP001 (Alice Martin) left the company on 2025-12-31.` et `…EMP002…` suivi de `Policy decision: rejected - EMP002 (Bob Lee) is still employed` (ordre indifférent) |
| `scenario2-executed` | résultat d'outil de la suppression d'EMP001 |
| `scenario2-rejected` | `Tool result (delete_employee_data): Tool call invocation rejected. EMP002 (Bob Lee) is still employed` **et** aucune ligne `employee 'EMP002' has been permanently deleted` dans le scénario (lookahead négatif) |
| `scenarioN-usage` | usage du scénario, borné à l'en-tête suivant |

**Résultats** (instance de vérification sur le port 5063, historique dans un dossier temporaire hors dépôt, supprimé ; `Dashboard/.data/history.json` inchangé) :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ tous les tests passent (dont les 3 tests Lab09 et le catalogue à 10 labs) |
| API : `GET /api/labs` | ✅ 10 labs (lab01 → lab09) |
| API : détails FR, README rendu, packages, diff de solution | ✅ README rendu (36,1 Ko HTML) ; 6 packages listés (MAF 1.22.0, Azure.Identity 1.21.0, Configuration 10.0.12) ; seul `Program.cs` diffère |
| Run Lab09 **Start** livré via l'API | ✅ `failed` attendu à l'étape `checks` — « 8 of 9 check(s) failed », seul `config` passe, exit 0, aucun usage, aucune demande de saisie |
| Run Lab09 **Solution** via l'API (événement `input-request` #1 → `POST …/input {"text":"Y"}`) | ✅ **`passed`** — « All 9 checks passed », exit 0 ; tokens affichés : `Scenario 1 · Token Usage (2 runs)` 317/38/355, `Scenario 2 · Token Usage (2 runs)` 879/177/1056, total 1 411 |

Aucun artefact laissé dans le dépôt : `.tmp-start-check` supprimé, instance du dashboard arrêtée, dossier temporaire hors dépôt supprimé.
