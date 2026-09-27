# Rapport de migration — Lab03-AIAgentWithFunctionTools (Function Tools)

> Date : 2026-09-27 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.9 ajouté), [Lab01-Migration-Report.md](Lab01-Migration-Report.md), [Lab02-Migration-Report.md](Lab02-Migration-Report.md), samples officiels (tag `dotnet-1.22.0`) `01-get-started/02_add_tools`, `02-agents/Agents/Agent_Step12_Plugins`, `02-agents/Agents/Agent_Step11_Middleware`, pages Learn [Using function tools with an agent](https://learn.microsoft.com/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp) et [Agent Middleware](https://learn.microsoft.com/agent-framework/agents/middleware?pivots=programming-language-csharp).

**Statut : migré.** Tous les critères du manuel (§7) sont remplis. Réserve inchangée par rapport à Lab01/Lab02 : les warnings de vulnérabilité NuGet hérités de `CommonUtilities` (phase 0).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AIAgentWithFunctionTools.csproj` | Socle Lab01/Lab02 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé ; **ajout** de `Microsoft.Extensions.DependencyInjection` 10.0.12 (commenté) ; suppression de `Azure.AI.OpenAI 2.7.0-beta.2`, `Microsoft.Extensions.Hosting 9.0.0`, `ModelContextProtocol 0.5.0-preview.1` (inutilisé) et du `<NoWarn>MEAI001</NoWarn>` global (Solution) | Version stable, plus aucune préversion ; `ServiceCollection` n'est plus apporté par Hosting (cf. §6.1) ; aucun `NoWarn` global (règle 11). Start et Solution avaient des `.csproj` différents : ils sont désormais identiques |
| `ConfigurationHelper.cs`, `AzureOpenAISettings.cs`, `appsettings.json` | Copies conformes de Lab02 (namespace `AIAgentWithFunctionTools`) ; `APIKey` retiré de `appsettings.json` | User-secrets chargés, validation au démarrage, aucun secret versionné |
| `Tools/CompanyTools.cs` (Start = Solution) | Version Solution retenue (avec les `[Description]` de `GetEmployeeInfo`) ; `using Microsoft.Extensions.AI` inutile retiré ; résumé XML expliquant le rôle des `[Description]` ; « ✅ » et espace initial retirés des messages de réservation | Start et Solution différaient (TODO « Step 0 » dans le Start, emoji dans la Solution) : règle 9 « seul `Program.cs` diffère ». L'emoji s'affichait mal selon l'encodage de la console |
| `Tools/NotificationTools.cs`, `Repositories/*` | Inchangés (déjà identiques) | Le pattern `IServiceProvider` en paramètre d'outil est toujours celui de MEAI / du sample `Agent_Step12_Plugins` |
| `AgentConsole.cs` (**nouveau**, Start = Solution) | `WriteTokenUsage(AgentResponse)`, importé par `using static` | Bloc de 5 lignes répété 4 fois (même décision que `RestaurantConsole.cs` de Lab02) ; placé dans un fichier fourni pour éviter `CS8321` dans le Start |
| `Solution/Program.cs` | Client `OpenAIClient` sur l'endpoint v1 (clé API ou Entra ID) ; `AsAIAgent(..., name, tools, services)` ; variables `AIAgent` ; `AgentResponse` ; `.Text` au lieu de `ToString()` ; scénario 1 : affichage de `AITool.Name` / `Description` ; scénario 2 : `[.. methods.Select(...)]` ; scénario 3 : `ServiceCollection` / `IServiceProvider` typés ; **nouveau scénario 4** : middleware d'appel de fonction (`AsBuilder().Use(FunctionCallMiddleware).Build()`) ; format de sortie aligné sur Lab02 (`Endpoint:` / `Deployment:`, `Token Usage:`) | APIs 1.22.0, patterns des samples et de la doc officiels ; pratique Lab03 du plan (§5 bis) |
| `Start/Program.cs` | TODO réécrits et renumérotés **1 → 23** (setup 1–3 identique à Lab02 ; S1 4–8 ; S2 9–14 ; S3 15–19 ; S4 20–23, avec squelette commenté du middleware en fin de fichier) ; `#pragma OPENAI001` fourni | Cohérence exercice ↔ solution ; compile sans warning et s'exécute tel que livré |
| `README.md` (lab) | Réécrit sur le gabarit Lab02 : configuration user-secrets / Entra ID / dashboard, fichiers fournis, tableau des 23 TODO, **« How function calling works »** (boucle, usage cumulé), concepts, namespaces, sortie attendue réelle, dépannage (dont l'exception d'outil renvoyée au modèle), packages, « Going further », encadré « Coming from an older version », liens mis à jour | Aucune API obsolète expliquée ; l'ancien README présentait `AzureOpenAIClient`, `CreateAIAgent`, `AgentRunResponse`, `ChatClientAgent` et une configuration avec clé dans `appsettings.json` |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab03` (EN + FR, 12 checks) | Intégration au dashboard (§9) |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 3 tests Lab03 dans `OutputAnalyzerTests` | Idem |
| `Dashboard/README.md`, `README.md` (racine) | Lab03 enregistré / migré ; 4 scénarios décrits ; exemple « Add a lab » passé sur Lab04 ; conseil pour les checks des labs à outils | Documentation associée |
| `Migration/Migration-Manual.md` | §4.9 « Function tools, DI et middleware » + ligne `Microsoft.Extensions.DependencyInjection` dans la table §5 | Référence pour les labs suivants |
| `Migration/Migration-Plan.md` | Statut, vague 1, ligne Lab03 et pratique §5 bis → ✅ | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `ChatClientAgent x = chatClient.CreateAIAgent(instructions, tools: [...])` | `AIAgent x = chatClient.AsAIAgent(instructions, name, tools: [...])` |
| `CreateAIAgent(..., services: serviceProvider)` | `AsAIAgent(..., services: serviceProvider)` (même sémantique, vérifiée dans `PublicAPI.Shipped.txt` et `Agent_Step12_Plugins`) |
| `AgentRunResponse` | `AgentResponse` |
| `response.ToString()` | `response.Text` |
| `Microsoft.Extensions.Hosting` (apportait `ServiceCollection`) | `Microsoft.Extensions.DependencyInjection` 10.0.12 explicite |
| — | `AITool.Name` / `AITool.Description` (scénario 1) |
| — | `AIAgent.AsBuilder().Use(Func<AIAgent, FunctionInvocationContext, Func<…>, CancellationToken, ValueTask<object?>>).Build()` (scénario 4) |
| `AIFunctionFactory.Create(delegate, name)`, `AIFunctionFactory.Create(MethodInfo, target)`, `[Description]`, paramètre `IServiceProvider` | **inchangés** (Microsoft.Extensions.AI 10.10.0) |

Dépendances résolues (Start = Solution) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `Microsoft.Extensions.DependencyInjection` 10.0.12, `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. **Aucune préversion**, plus d'`Azure.AI.OpenAI` ni de `ModelContextProtocol`.

## 3. Changements conceptuels

1. **Le concept de function tool n'a pas changé** : il appartient à `Microsoft.Extensions.AI` (`AIFunctionFactory`, `AITool`, `FunctionInvokingChatClient`), pas à MAF. Les trois scénarios d'origine sont conservés dans leur intention ; seuls la création de l'agent et les types de réponse changent.
2. **Middleware d'appel de fonction** (nouveau scénario 4) : MAF 1.x permet d'envelopper un agent existant (`AsBuilder().Use(...)`) pour intercepter chaque appel d'outil — nom, arguments choisis par le modèle, résultat. C'est la pratique Lab03 retenue dans le plan (§5 bis), démontrée par `Agent_Step11_Middleware` et la page Learn « Agent Middleware ». Elle rend visible la boucle de function calling, jusque-là implicite.
3. **Ce que le modèle voit** : le scénario 1 affiche le nom et la description de chaque outil ; le README explique la boucle (schéma envoyé → appels demandés → exécution locale → résultats renvoyés) et pourquoi `Usage` cumule plusieurs appels modèle.
4. **Erreurs d'outil** : une exception dans un outil n'interrompt pas le run, elle est renvoyée au modèle (vérifié, §5 test +). Documenté dans le dépannage.

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `Azure.Identity` beta : supprimés ou remplacés (cf. Lab01).
- `ModelContextProtocol 0.5.0-preview.1` et `<NoWarn>MEAI001</NoWarn>` (Solution uniquement) : supprimés, aucun usage.
- **Étape « Step 0 » (ajout manuel des `[Description]` dans `CompanyTools.cs`) : remplacée.** Elle imposait un `CompanyTools.cs` différent entre Start et Solution (contraire à la règle 9 et au test §7-11). Les attributs sont désormais fournis ; l'objectif pédagogique est conservé autrement : README « Look at the provided files » + TODO 5, qui affiche la description que le modèle reçoit pour chaque outil. Signalé dans l'encadré « Coming from an older version ».
- Aucun scénario supprimé ; 1 scénario ajouté (middleware).

## 5. Tests effectués et résultats

Environnement : macOS, SDK .NET 10.0.100, ressource `*.cognitiveservices.azure.com`, déploiement `gpt-4o-mini`.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build Start/Solution) | ✅ 0 erreur, warnings NU1902/NU1903 uniquement (CommonUtilities) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances | ✅ MAF 1.22.0, pas d'`Azure.AI.OpenAI`, aucune préversion (Start = Solution) |
| 3 | Vulnérabilités | ⚠️ `Snappier` 1.0.0 (high), `SharpCompress` 0.30.1 (moderate) — transitifs de CommonUtilities, **identiques à la baseline** ; aucune introduite par le lab |
| 4 | `dotnet build -warnaserror` Start + Solution | ✅ 0 erreur, 0 warning C# ; seuls NU1902/NU1903 hérités (4 + 4 par projet) échouent sans `-nowarn:NU1902,NU1903` |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `ChatClientAgent`, Hosting, `ToString()`, `.NET 8`, `ModelContextProtocol`, `NoWarn`, `"APIKey"`) | ✅ 0 occurrence hors encadré « Coming from an older version » (seul `result?.ToString()` sur le résultat d'outil dans le middleware, légitime) |
| 6 | Run Solution — **clé API** | ✅ S1 : `get_employee_info` → Mohammed BEN SAID ; S2 : 3 outils découverts, salles listées, ROOM-A réservée ; S3 : notification envoyée puis relue (id `97576bb9`) ; S4 : traces `[Middleware] Calling get_employee_info(employeeId: EMP003)` … `book_meeting_room returned: Booking confirmed!` ; usage 582 / 1 345 / 505 / 1 495 tokens |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`, `az login`) | ✅ 4 scénarios corrects, même usage |
| 8 | Config placeholder (variables d'env. retirées, HOME isolé) | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set …', or with the environment variable 'AzureOpenAI__Endpoint'.` (exception non gérée avec message clair, comme Lab01/Lab02) |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche les 4 en-têtes de scénario sans erreur |
| 10 | Start complété **uniquement d'après les indices du README** (copie temporaire `.tmp-start-check` dans le lab, supprimée ensuite) | ✅ compile sans warning C# et reproduit les 4 scénarios (mêmes outils appelés, même usage) |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ 9 fichiers identiques (`.csproj`, config ×3, `AgentConsole.cs`, `Tools/` ×2, `Repositories/` ×2) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–23 a un indice qui mène au code de la Solution |
| + | Copie temporaire sans `services: serviceProvider` | Pas d'exception : l'erreur de l'outil est renvoyée au modèle, qui répond « there was an error… » → ligne de dépannage réécrite en conséquence |

`CommonUtilities` n'a pas été modifié : pas de risque de régression pour les autres labs.

## 6. Problèmes rencontrés

1. **`ServiceCollection` introuvable une fois Hosting retiré** : MAF 1.22.0 n'apporte que `Microsoft.Extensions.DependencyInjection.Abstractions`. Référence explicite à `Microsoft.Extensions.DependencyInjection` 10.0.12 (version du `Directory.Packages.props` officiel) dans Start et Solution — l'ancien Start ne la référençait pas et compilait grâce à Hosting.
2. **Start et Solution divergents hors `Program.cs`** (`.csproj`, `CompanyTools.cs`) : alignés (§1, §4).
3. **Dépannage trompeur** : la première version du README annonçait l'exception `No service for type …` ; le test réel montre que l'exception est absorbée par `FunctionInvokingChatClient` et transmise au modèle. Corrigé.
4. **Les samples officiels utilisent Foundry** (`AIProjectClient`) : seuls les patterns agent (`tools`, `services`, middleware) ont été repris sur le `ChatClient` Azure OpenAI v1 (même décision que Lab01/Lab02).
5. **Variabilité du modèle** : au scénario 4, le modèle appelle parfois `get_meeting_rooms` avant de réserver (2 ou 3 appels) ; les checks du dashboard ne dépendent pas de cet appel.

## 7. Décisions à valider

- **Remplacement de l'étape « Step 0 »** (écrire les `[Description]`) par des attributs fournis + TODO 5 (afficher la description reçue par le modèle) : nécessaire pour la règle « seul `Program.cs` diffère », mais c'est une activité pratique en moins.
- **Ajout du scénario 4** (middleware d'appel de fonction, TODO 20–23) : prévu par le plan (§5 bis), justifié par `Agent_Step11_Middleware` et la page Learn.
- **Nouveau fichier fourni `AgentConsole.cs`** (comme `RestaurantConsole.cs` de Lab02).
- **Noms d'agents ajoutés** (`CompanyAssistant`, `NotificationAssistant`) : absents à l'origine, alignés sur la pratique des samples.
- **Messages de `CompanyTools` sans emoji** (`Booking confirmed!`).

## 8. Points à surveiller / pour les prochains labs

- **Labs qui construisent un `ServiceCollection`** (Lab10, MAS-Lab*) : ajouter `Microsoft.Extensions.DependencyInjection` 10.0.12 dès le retrait de Hosting.
- **Labs à outils** (Lab07, Lab08, Lab09, Lab10) : réutiliser §4.9 du manuel ; documenter qu'une exception d'outil est renvoyée au modèle ; le middleware d'appel de fonction est un bon outil de diagnostic pour l'apprenant.
- **Lab09** (approbations) : le sample `Agent_Step11_Middleware` montre aussi un middleware d'approbation console (`ToolApprovalRequestContent`) ; Lab09 peut s'appuyer sur le scénario 4 de Lab03.
- **Checks du dashboard pour les labs à outils** : vérifier une donnée que seul l'outil peut produire (donnée du jeu d'essai, id généré, trace du middleware), pas seulement la présence d'une réponse.
- **Vague 1 restante** : Lab08, Lab11.

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire — le catalogue est piloté par `labs.json`, et l'étiquetage des usages par scénario (ajouté pour Lab02) couvre les 4 blocs `Token Usage:` de Lab03.

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab03` : métadonnées EN + FR, projets Start/Solution, timeout 180 s, **12 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab03 sur une sortie Solution (tous passent) et Start (seul `config` passe) ; réponse « sans outil » (aucun check de résultat ne passe) ; 4 rapports de tokens étiquetés par scénario |
| Documentation | `Dashboard/README.md` : labs enregistrés, exemple « Add a lab » (Lab04), conseil de checks pour les labs à outils, liste des tests |

**Choix des checks** — ils vérifient ce que seul un outil exécuté peut produire :

| Check | Preuve |
|---|---|
| `scenario1-tools` | les 3 lignes `- nom: description`, la 1ʳᵉ avec la description exacte de `[Description]` |
| `scenario1` | « Mohammed BEN SAID » (donnée du jeu d'essai) dans la section du scénario 1 |
| `scenario2-tools` | exactement `GetEmployeeInfo, GetMeetingRooms, BookMeetingRoom` (pas les méthodes privées ni héritées) |
| `scenario2` | « Innovation Lab » (liste des salles) puis « booked/confirmed » |
| `scenario3` | une ligne contenant un id hexadécimal de 8 caractères (généré par le repository) **et** le message |
| `scenario4-middleware` / `scenario4-booking` | traces du middleware : appel `get_employee_info(employeeId: EMP003)`, retour `Booking confirmed!` |
| `scenarioN-usage` | usage du scénario, borné à l'en-tête suivant |

**Tests** :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ 49/49 (46 avant ; 3 tests ajoutés) |
| API : `GET /api/labs`, détails FR, README rendu, packages, diff de solution | ✅ 3 labs ; traductions FR ; 7 packages ; seul `Program.cs` diffère |
| Run Lab03 **Solution** via l'API (×3) | ✅ `passed` 12/12 à chaque run ; 4 rapports `Scenario N · Token Usage` ; totaux 3 993 / 3 932 / 3 927 |
| Run Lab03 **Start** livré | ✅ `failed` attendu : 11/12 checks échouent, seul `config` passe ; aucun usage |
| Non-régression Lab01 / Lab02 Solution | ✅ `passed` 7/7 et 9/9 |

L'historique local `Dashboard/.data/history.json` (git-ignoré) a été sauvegardé avant les runs puis restauré ; le log temporaire du dashboard a été supprimé.
