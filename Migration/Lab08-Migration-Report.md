# Rapport de migration — Lab08-DataFormatComparison

> Date : 2026-09-30 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.14 ajouté, §1, §5, §6, §7, §8), [Migration-Plan.md](Migration-Plan.md), rapports [Lab01](Lab01-Migration-Report.md) à [Lab07](Lab07-Migration-Report.md). Sources officielles (tag `dotnet-1.22.0`) : `dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgentStructuredOutput.cs` (`RunAsync<T>`, `WrapNonObjectSchema`), samples `02-agents/Agents/Agent_Step02_StructuredOutput` et `01-get-started/02_add_tools` ; sources `dotnet/extensions` (MEAI 10.10.0) `OpenAIChatClient.ToOpenAIChatMessages` (envoi des `FunctionResultContent`) ; comportement de `AIFunctionFactory` vérifié empiriquement avec les paquets 1.22.0 / 10.10.0 (`AIFunction.InvokeAsync` + `AsOpenAIChatMessages()`) ; pages Learn « Using function tools with an agent » et « Producing Structured Outputs with agents ».

**Statut : code, README et dashboard migrés ; validation d'exécution BLOQUÉE (même cause que Lab07).** Tout ce qui ne dépend pas d'un appel à Azure OpenAI est vérifié (restauration, graphe, build sans warning, grep, robustesse de la configuration, Start livré, Start complété compilé et exécuté jusqu'au même point que la Solution, diff Start/Solution, helpers testés hors ligne, tests du dashboard, runs du dashboard). **Les tests 6, 7 et 10-exécution (résultat réel des deux scénarios et de la comparaison) n'ont pas pu être menés** : la clé API de l'environnement renvoie `401` et Entra ID `400 SubscriptionNotRegistered`, comme le 2026-09-29 pour Lab07 et Lab01 (§6.1). Le lab ne peut être déclaré « migré » au sens du manuel (§7) qu'après ces tests, à relancer dès que les identifiants sont rétablis (§7).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/DataFormatComparison.csproj` | Socle Lab01–07 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé, `Data\hotels.json` copié à la sortie. **Supprimés** : `Azure.AI.OpenAI 2.7.0-beta.2`, `Azure.Identity 1.18.0-beta.2`, `Microsoft.Agents.AI.OpenAI 1.0.0-preview.251204.1`, `Microsoft.Extensions.Hosting 9.0.0`, `<NoWarn>MEAI001</NoWarn>`, et les **packages tiers** `ToonNet 1.0.4` (Solution) / `ToonNetSerializer 1.0.0` (Start — introuvable sur NuGet : `NU1101`, le Start ne restaurait plus) | Aucune préversion, aucune bibliothèque tierce non officielle (règle 12), aucun `NoWarn` global (règle 11) ; Start et Solution avaient des `.csproj` différents |
| `AzureOpenAISettings.cs`, `ConfigurationHelper.cs`, `appsettings.json` | Copies conformes de Lab01 (namespace `DataFormatComparison`) ; `APIKey` retiré de `appsettings.json` | User-secrets, validation au démarrage, aucun secret versionné |
| `Models/Hotel.cs` | Classe `sealed`, `[Description]` sur la classe et chaque propriété, `required` sur `Name`/`City` seulement (l'ancien `required bool HasWifi` retiré), résumé XML expliquant ses quatre usages | Le schéma JSON de `RunAsync<List<Hotel>>` est généré depuis ce type : les descriptions guident le modèle (pattern Lab02) |
| `HotelData.cs` (**nouveau**) | `LoadAsync()` lit `Data/hotels.json` (snake_case) | Même pattern que `FaqData.cs` (Lab07) ; le chargement est fourni dans le setup |
| `HotelCsv.cs` (**nouveau**) | `Header`, `Serialize(hotels)` (RFC 4180, culture invariante, `\n`), `Deserialize(text)` (`FormatException` sur en-tête ou colonnes inattendus, clôtures Markdown tolérées) | Remplace les packages tiers ; le format compact comparé à JSON devient un standard connu de tous (CSV) |
| `Tools/HotelTools.cs` | Classe instance `HotelTools(IReadOnlyList<Hotel>)` : `GetAllHotelsAsJson()` renvoie **des objets** (le framework les sérialise en JSON), `GetAllHotelsAsCsv()` renvoie **du texte** ; `[Description]` sur les deux ; plus de TODO ni de lecture de fichier | Fichier fourni identique Start/Solution (règle 9) ; l'ancien outil « JSON » renvoyait le texte brut du fichier et l'outil « Toon » était à compléter dans le Start |
| `FormatConsole.cs` (**nouveau**) | `WriteHotels`, `WriteToolCalls` (appels + **taille du résultat d'outil tel que le modèle le reçoit**), `ToolResultLength`, `WriteTokenUsage(response, heading)`, `WriteComparison(json, csv)` (tableau sans deux-points) ; `CultureInfo.InvariantCulture` pour les nombres | Helpers d'affichage fournis (comme `RagConsole.cs`) ; la mesure reproduit exactement la sérialisation de `OpenAIChatClient` |
| `Solution/Program.cs` | Réécrit : client `OpenAIClient` v1 (clé API ou Entra ID) ; chargement des hôtels et `question` communs ; **scénario 1** JSON (outil objets + `RunAsync<List<Hotel>>`, `WriteToolCalls`, `WriteHotels`, usage) ; **scénario 2** CSV (outil texte + instructions CSV avec `{HotelCsv.Header}`, `RunAsync` texte, affichage brut, `HotelCsv.Deserialize` dans un `try/catch FormatException`, usage) ; **comparaison** finale (`WriteComparison`) quand les deux scénarios ont tourné ; `scenariosToRun = [1, 2]` (l'ancienne Solution ne lançait que `[2]`) | APIs 1.22.0 ; même question et mêmes 9 colonnes dans les deux scénarios pour une comparaison honnête (l'ancien scénario 2 ne renvoyait que 3 colonnes) |
| `Start/Program.cs` | TODO renumérotés **1 → 13** (setup 1–3 identique à Lab01 ; S1 4–7 ; S2 8–12 ; comparaison 13) ; chargement des hôtels, `question`, variables `jsonResponse`/`csvResponse` et bloc `if` de comparaison fournis ; `#pragma OPENAI001` fourni | Compile sans warning et s'exécute tel que livré (exit 0, aucun appel réseau) ; seul `Program.cs` diffère |
| `README.md` (lab) | Réécrit sur le gabarit Lab07 : objectif JSON vs CSV, configuration user-secrets / Entra ID / dashboard, fichiers fournis, tableau des 13 TODO (chaque indice contient le code de la Solution), schéma « What the model receives and produces », tableau JSON vs CSV, concepts, namespaces, sortie attendue, dépannage, packages (aucune bibliothèque de sérialisation), « Going further » (`AIFunctionFactoryOptions.SerializerOptions`, `MaxOutputTokens`, wrapper pour `ForJsonSchema<T>`), encadré « Coming from an older version », liens officiels | Aucune API obsolète expliquée ; l'ancien README présentait `AzureOpenAIClient`, `CreateAIAgent`, `AgentRunResponse`, `ChatClientAgent`, un lien vers le dépôt GitHub d'un package tiers et une clé dans `appsettings.json` |
| `Dashboard/LabDashboard/labs.json` | Entrée `azureopenai-lab08` (EN + FR, niveau *Intermediate*, timeout 240 s, 11 checks) | Intégration au dashboard (§9) |
| `Dashboard/LabDashboard.Tests/*` | Id ajouté à `LabCatalogTests` ; 3 tests Lab08 dans `OutputAnalyzerTests` | Idem |
| `Dashboard/README.md`, `README.md` (racine) | Lab08 enregistré / migré ; scénarios du lab ; exemple « Add a lab » passé sur Lab09 ; conseil pour les checks des labs de comparaison ; liste des tests | Documentation associée |
| `Migration/Migration-Manual.md` | §4.14 « Formats de données et résultats d'outils », ligne `ToonNet`/`ToonNetSerializer` dans la table §5, point « Culture du poste » en §8 | Référence pour les labs suivants |
| `Migration/Migration-Plan.md` | Statut, vague 1, ligne Lab08 (§5) et pratique Lab08 (§5 bis) → ✅ | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `ChatClientAgent x = chatClient.CreateAIAgent(instructions, name, tools: [...])` | `AIAgent x = chatClient.AsAIAgent(instructions, name, tools: [...])` |
| `AgentRunResponse<List<Hotel>>` (`RunAsync<T>` réservé à `ChatClientAgent`) | `AgentResponse<List<Hotel>>` (`RunAsync<T>` sur tout `AIAgent` ; schéma non-objet enveloppé par le framework) |
| `AgentRunResponse`, `response.ToString()` | `AgentResponse`, `response.Text` |
| `ToonNetSerializer.ToonNet.Encode(hotels)` (package tiers) | `HotelCsv.Serialize(hotels)` (helper fourni, quelques lignes de .NET) |
| Outil renvoyant `File.ReadAllText("hotels.json")` | Outil renvoyant `IReadOnlyList<Hotel>` : sérialisé en JSON par `AIFunctionFactory` |
| — | `FunctionCallContent` / `FunctionResultContent` lus dans `response.Messages` ; `AIJsonUtilities.DefaultOptions` pour mesurer le résultat d'outil envoyé |
| `AIFunctionFactory.Create(delegate, name)`, `[Description]`, `Usage` | **inchangés** (Microsoft.Extensions.AI 10.10.0) |

Dépendances résolues (Start = Solution, `diff` des graphes vide) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0, `System.ClientModel` 1.15.0. **Aucune préversion**, plus d'`Azure.AI.OpenAI`, de `Microsoft.Extensions.Hosting` ni de package tiers.

## 3. Changements conceptuels

1. **Le format comparé est mesuré, pas supposé.** L'ancien lab affichait seulement l'usage des tokens. Le lab montre désormais **ce que le modèle reçoit** : `AIFunctionFactory` transforme toujours la valeur de retour d'un outil en `JsonElement`, puis `OpenAIChatClient` envoie une chaîne telle quelle (comme chaîne JSON, `\n` échappés) et tout autre résultat en JSON **indenté** avec `AIJsonUtilities.DefaultOptions` (vérifié dans la source MEAI et empiriquement). Pour les 50 hôtels : **10 348** caractères en JSON contre **2 765** en CSV, chiffres exacts et reproductibles (le test du dashboard s'appuie sur le signe de la différence, pas sur ces valeurs).
2. **Comparaison à périmètre égal.** Même question, même outil (`get_all_hotels`), mêmes 9 colonnes en entrée et en sortie dans les deux scénarios ; un tableau final compare taille du résultat d'outil, tokens d'entrée, de sortie et total. L'ancien scénario 2 ne demandait que 3 colonnes (`Name,PricePerNight,Currency`), ce qui faussait la comparaison.
3. **Ce que l'on perd avec un format compact est enseigné** : la sortie structurée de `RunAsync<T>` garantit la forme (schéma) et désérialise pour vous ; le CSV demandé dans les instructions n'est qu'une requête, d'où `HotelCsv.Deserialize` dans un `try/catch (FormatException)` — le pendant du scénario 1 de Lab02.
4. **« Toon » remplacé par CSV.** Le format « Toon » de l'ancien lab reposait sur deux packages tiers différents entre Start et Solution, dont l'un n'existe plus sur NuGet ; ses instructions décrivaient en réalité du CSV. Le CSV (RFC 4180) porte la même leçon (les noms de propriétés ne sont écrits qu'une fois) sans dépendance ni référence externe. Signalé dans l'encadré « Coming from an older version ».
5. **Aucun scénario ajouté** au sens d'une nouvelle API ; le bloc de comparaison est un affichage (pas d'appel modèle) et le scénario 1 combine deux concepts déjà enseignés (Lab02 + Lab03), démontrés ensemble par le sample `Agent_Step02_StructuredOutput` (agent + `RunAsync<T>`) et par `01-get-started/02_add_tools`.

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `Azure.Identity` beta, `<NoWarn>MEAI001</NoWarn>` : supprimés ou remplacés (cf. Lab01).
- **`ToonNet` / `ToonNetSerializer`** (packages tiers) et le lien README vers leur dépôt GitHub : supprimés, remplacés par `HotelCsv.cs`.
- **TODO dans `Tools/HotelTools.cs`** (encoder en Toon) : supprimé — le Start et la Solution partagent le fichier (règle 9) ; l'objectif pédagogique (choisir le format renvoyé par l'outil) est porté par les TODO 4 et 8 de `Program.cs` (choix de l'outil et des instructions) et par la mesure du résultat d'outil.
- `scenariosToRun = [2]` dans la Solution (le scénario 1 n'était pas exécuté) : `[1, 2]`.
- Aucun scénario supprimé ; les deux scénarios d'origine sont conservés dans leur intention, avec le même périmètre de données, plus un tableau de comparaison.

## 5. Tests effectués et résultats

Environnement : macOS (culture fr-FR), SDK .NET 10.0.100, variables `AzureOpenAI__*` de l'environnement (aucun user-secret configuré : `dotnet run` sans ces variables tombe sur les placeholders), `az login` valide.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration | ⚠️ **Start : restauration impossible** (`NU1101: Unable to find package ToonNetSerializer`) ; Solution : build OK (NU1902/NU1903 hérités) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances (`--include-transitive`) | ✅ MAF 1.22.0, MEAI 10.10.0, OpenAI 2.13.0 ; **aucune préversion**, pas d'`Azure.AI.OpenAI`, de Hosting ni de package tiers ; graphes Start et Solution **identiques** (`diff`) |
| 3 | Vulnérabilités (`--vulnerable --include-transitive`) | ✅ aucune introduite : seuls `SharpCompress` 0.30.1 / `Snappier` 1.0.0 via `CommonUtilities` (identique à la baseline) |
| 4 | `dotnet build -warnaserror` Start + Solution | ✅ `0 Warning(s) 0 Error(s)` (avec `-nowarn:NU1902,NU1903` hérités) |
| 5 | grep des anciens noms (`CreateAIAgent`, `GetAIAgent(`, `AgentRunResponse`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `AgentThread`, `ChatMessageStore`, `Microsoft.Extensions.Hosting`, `NoWarn`, `MEAI001`, `"APIKey"`, `ToonNet`, `Toon`, `ChatClientAgent `, `.NET 8`) | ✅ 0 occurrence dans `Start/`, `Solution/` ; dans le README, uniquement dans l'encadré « Coming from an older version » |
| 5b | Helpers hors ligne (projet jetable dans le scratchpad référençant la Solution, supprimé) | ✅ `HotelCsv` : aller-retour des 50 hôtels identique (`Serialize` → `Deserialize`, 2 713 caractères, 51 lignes) ; clôtures ` ``` `, espaces après les virgules, valeur entre guillemets et booléens `Yes`/`No` tolérés ; `FormatException` claire sur un en-tête, un nombre de colonnes ou une valeur invalides ; `ToolResultLength` = **10 348** (objets → JSON indenté) et **2 765** (chaîne → chaîne JSON) ; `WriteComparison` produit le tableau attendu par les checks |
| 6 | Run Solution — **clé API** | ⛔ **BLOQUÉ** : `HTTP 401 Access denied due to invalid subscription key or wrong API endpoint` au premier appel du scénario 1 (`ChatClient.CompleteChatAsync`), exit 134 — même clé, même erreur sur Lab01 et Lab07 (§6.1) |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`) | ⛔ **BLOQUÉ** : `HTTP 400 SubscriptionNotRegistered — This subscription is not registered with the Microsoft.CognitiveServices resource provider` (identique à Lab07) |
| 8 | Config placeholder (variables `AzureOpenAI__*` retirées) | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set "AzureOpenAI:Endpoint" <value>', or with the environment variable 'AzureOpenAI__Endpoint'.` ; déploiement seul absent → message équivalent pour `ChatDeploymentName` |
| 9 | Run Start livré | ✅ compile sans warning C#, affiche `Loaded 50 hotels…`, les 2 en-têtes de scénario et la question, **exit 0** (aucun appel réseau) |
| 10 | Start complété **uniquement d'après les indices du README** (copie temporaire `.tmp-start-check` dans le dossier du lab, TODO 1–13 remplacés par le code des tableaux, supprimée ensuite) | ✅ compile `-warnaserror` sans warning, 0 TODO restant, **même sortie que la Solution jusqu'au même `401`** ; ⛔ comparaison des résultats des scénarios impossible (même blocage que 6/7). La copie complétée est conservée hors dépôt (`scratchpad/Lab08-StartCompleted-Program.cs`) pour rejouer le test |
| 11 | `diff -r Start Solution` hors `Program.cs` | ✅ aucune différence (10 fichiers partagés) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–13 a un indice qui contient le code de la Solution (prouvé par le test 10) |
| 13 | Non-régression | Sans objet : `CommonUtilities` n'est pas modifié ; les tests du dashboard passent avec les 8 labs déjà enregistrés (§9) |

## 6. Problèmes rencontrés

1. **Identifiants Azure OpenAI de l'environnement invalides** (clé `401`, Entra `400 SubscriptionNotRegistered`), constatés avant toute modification (`curl` sur `/openai/v1/models`, puis Lab01 Solution : même erreur) et inchangés en fin de session. Aucun user-secret n'est configuré (le lancement sans les variables d'environnement tombe sur les placeholders). Cause hors dépôt, déjà consignée dans le manuel §8 et le rapport Lab07.
2. **Le Start d'origine ne se restaurait plus** (`ToonNetSerializer` absent de NuGet) : la baseline du Start est donc « ne compile pas ». La Solution référençait un autre package (`ToonNet`).
3. **Culture du poste (fr-FR)** : le projet jetable a affiché `55,5` pour un `decimal` interpolé sans culture. Les helpers du lab utilisent `CultureInfo.InvariantCulture` (ajouté au manuel §8) ; les autres labs migrés affichent surtout des entiers et des textes, mais Lab07 affiche des scores (`{result.Score:F4}`) — à vérifier sur un poste non anglophone.
4. **Sérialisation du résultat d'outil** : l'outil CSV renvoie une chaîne, mais le modèle la reçoit **comme chaîne JSON** (guillemets, `\n`), car `AIFunctionFactory` produit toujours un `JsonElement`. Ce n'est pas un défaut du lab (le CSV reste 3,7× plus petit que le JSON indenté), mais c'est documenté dans le README pour ne pas surprendre.
5. **Dashboard** : le bloc de comparaison affiche des lignes `Input tokens …` qui pourraient être lues comme un usage ; elles sont écrites **sans deux-points** et un test (`Labels_the_two_lab08_token_usage_blocks…`) vérifie que l'analyseur n'en fait que deux rapports. `dotnet test` doit être lancé **depuis `Dashboard/`** (son `global.json` sélectionne `Microsoft.Testing.Platform`) ; lancé depuis la racine avec le chemin du dossier, le SDK 10 refuse (« Testing with VSTest target is no longer supported »).
6. **Artefacts de sessions précédentes** : deux dossiers `.tmp-start-check` vides ou ignorés (Lab05, Lab07), restes des tests 10 des rapports précédents, ont été trouvés et supprimés. Un script de vérification du dashboard lancé en parallèle d'une autre commande a partagé son répertoire courant : la copie temporaire de ce lab a été refaite avec des chemins absolus, et vérifiée absente à la fin.

## 7. Décisions à valider

- [x] **CSV à la place de « Toon »** et suppression des packages tiers (`ToonNet`, `ToonNetSerializer`) — **décidé** : le README ne nomme plus ces packages (l'encadré « Coming from an older version » parle de « packages tiers TOON » sans les nommer ni les lier) ; leurs noms ne subsistent que dans les documents de migration, à titre historique.
- [x] **Titre et objectif conservés** (« Data Format Comparison ») avec deux scénarios + un bloc de comparaison non numéroté (pas de « Scenario 3 » : aucun appel modèle).
- [ ] **Exécution réelle à refaire** dès que les identifiants sont rétablis : (1) `dotnet run --project Solution` (clé API puis Entra ID) et vérifier que les 43 hôtels sont renvoyés, `Backpacker Hostel` en premier dans les deux scénarios, le CSV relu (`Parsed back 43 hotels`) et un tableau de comparaison à différences négatives ; (2) rejouer le test 10 avec `scratchpad/Lab08-StartCompleted-Program.cs` → même comportement ; (3) remplacer les `xxx` de la sortie attendue du README et, si utile, les chiffres de `Lab08SolutionOutput` des tests du dashboard par la sortie réelle ; (4) un run Solution `passed` dans le dashboard (tokens affichés pour les scénarios 1 et 2, deux rapports).
- [x] Niveau *Intermediate* dans le dashboard (le lab suppose Lab02 et Lab03) — **décidé**.

## 8. Points pour les prochains labs

- **Labs à outils (Lab09, Lab10, MAS)** : `FormatConsole.ToolResultLength` / `WriteToolCalls` donnent la taille exacte de ce que le modèle reçoit ; utile pour expliquer un usage d'entrée élevé. Un outil qui renvoie une **chaîne** est envoyé comme chaîne JSON ; un outil qui renvoie des **objets** est envoyé en JSON indenté (`AIFunctionFactoryOptions.SerializerOptions` pour changer cela).
- **`RunAsync<T>` + outils** fonctionne sans configuration particulière et accepte `List<T>` (enveloppe automatique) ; `ChatResponseFormat.ForJsonSchema<T>()` ne l'accepte pas (wrapper nécessaire, Lab02).
- **Affichage de nombres** : passer par `CultureInfo.InvariantCulture` dès qu'une sortie est attendue par un README ou un check du dashboard.
- **Dashboard** : ne jamais écrire de ligne `Input/Output/Total tokens: N` hors d'un bloc d'usage (l'analyseur la lirait) ; un tableau sans deux-points est ignoré.
- **Lab11** (dernier de la vague 1) : `OpenAIClientOptions.Transport` (`HttpClientPipelineTransport`) — le client v1 de ce lab est le point de départ.
- **Avant tout test d'exécution** : `curl -s -o /dev/null -w '%{http_code}' -H "api-key: $AzureOpenAI__APIKey" "$AzureOpenAI__Endpoint/openai/v1/models"` doit renvoyer `200` (manuel §8).

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : aucun changement de code nécessaire. Le catalogue est piloté par `labs.json` ; les deux blocs d'usage (`Token Usage (JSON):`, `Token Usage (CSV):`) sont étiquetés par scénario par l'analyseur existant ; le tableau de comparaison n'est pas pris pour un usage (test dédié).

| Point | Changement |
|---|---|
| Catalogue | Entrée `azureopenai-lab08` : métadonnées EN + FR (niveau *Intermediate*), projets Start/Solution, timeout 240 s (deux runs avec ~43 lignes de sortie chacun), **11 checks** |
| Tests | Id ajouté à `LabCatalogTests` ; `OutputAnalyzerTests` : checks Lab08 sur une sortie Solution (tous passent) et sur la sortie réelle du Start (seul `config` passe) ; sortie « mauvais formats / pas d'économie » (aucun check hors `-usage` ne passe) ; 2 rapports de tokens étiquetés par scénario (total 7 201), lignes de comparaison ignorées |
| Documentation | `Dashboard/README.md` : labs enregistrés, exemple « Add a lab » (Lab09), conseil de checks pour les labs de comparaison, liste des tests |

**Choix des checks** (chacun échoue sur le Start livré et passe sur la sortie attendue de la Solution) :

| Check | Preuve |
|---|---|
| `scenario1-tool` | `Tool called: get_all_hotels()` suivi de `Tool result sent to the model: NNNNN characters` (≥ 4 chiffres : le résultat est bien la liste d'objets en JSON) |
| `scenario1-hotels` | `Hotels returned by the agent: N` puis, en première ligne, `Backpacker Hostel (Bangkok) - 25 USD/night` (l'hôtel le moins cher du jeu de données, en premier comme demandé) |
| `scenario2-tool` | appel de l'outil et taille du résultat, dans le scénario 2 |
| `scenario2-csv` | `Agent answer (CSV):` suivi de l'en-tête exact `Name,City,Stars,PricePerNight,Currency,Rooms,HasPool,HasWifi,Rating` puis `Backpacker Hostel,Bangkok,N,25,USD,` |
| `scenario2-parsed` | `Parsed back N hotels from the CSV answer` (la relecture a réussi) |
| `comparison-tool-result`, `comparison-input`, `comparison-output` | lignes `Tool result (chars)`, `Input tokens`, `Output tokens` du tableau avec une différence **négative** (`-NN%`) : CSV plus petit et moins coûteux que JSON |
| `scenarioN-usage` | usage du scénario, borné à l'en-tête suivant |

**Résultats** (instance de vérification sur les ports 5061/5062, historique dans un dossier temporaire hors dépôt, supprimé ; `Dashboard/.data/history.json` inchangé) :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ **103/103** (dont les 3 tests Lab08) |
| API : `GET /api/labs` | ✅ 9 labs (lab01 → lab08) |
| API : détails, README rendu, packages, traduction, diff de solution | ✅ README rendu (31,6 Ko HTML) ; 6 packages listés ; titre FR « Comparaison de formats de données » ; seul `Program.cs` diffère |
| Run Lab08 **Start** livré via l'API | ✅ `failed` attendu à l'étape `checks` — « 10 of 11 check(s) failed », seul `config` passe, exit 0, aucun usage |
| Run Lab08 **Solution** via l'API | ⛔ `failed` à l'étape `run` (exit 134) : `HTTP 401 … Access denied due to invalid subscription key` — même blocage que le test 6 ; le verdict `passed` et l'affichage des tokens restent **à vérifier** après rétablissement des identifiants |

Aucun artefact laissé dans le dépôt : `.tmp-start-check` supprimé, instances du dashboard arrêtées, dossiers temporaires hors dépôt supprimés.
