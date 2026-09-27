# Rapport de migration — Lab02-AIAgentWithSO (Structured Output)

> Date : 2026-09-27 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.8 ajouté), [Lab01-Migration-Report.md](Lab01-Migration-Report.md), sample officiel `dotnet/samples/02-agents/Agents/Agent_Step02_StructuredOutput` (tag `dotnet-1.22.0`), page Learn [Producing Structured Outputs with agents](https://learn.microsoft.com/agent-framework/agents/structured-outputs?pivots=programming-language-csharp).

**Statut : migré.** Tous les critères du manuel (§7) sont remplis. Réserve inchangée par rapport à Lab01 : les warnings de vulnérabilité NuGet hérités de `CommonUtilities` (phase 0).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/AIAgentWithSO.csproj` | Socle Lab01 : `Microsoft.Agents.AI.OpenAI` **1.22.0**, `Azure.Identity` **1.21.0**, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé ; suppression de `Azure.AI.OpenAI 2.7.0-beta.2` et `Microsoft.Extensions.Hosting 9.0.0` | Version stable, plus aucune préversion (règles 1, 2, 6) |
| `ConfigurationHelper.cs`, `AzureOpenAISettings.cs`, `appsettings.json` | Copies conformes de Lab01 (namespace `AIAgentWithSO`) ; `APIKey` retiré de `appsettings.json` | User-secrets chargés, validation au démarrage, aucun secret versionné |
| `Models/Restaurant.cs` (Start = Solution) | Ajout de `[Description]` sur la classe et sur chaque propriété (ex. « Average price per person, in euros », « from 0 to 3 ») | Les descriptions sont copiées dans le schéma JSON généré (`ForJsonSchema<T>`, `RunAsync<T>`), comme dans le sample officiel (`[Description("Information about a city")]`). Sans elles, les scénarios 2 à 4 n'indiquaient plus au modèle la devise ni la plage d'étoiles, qui n'étaient précisées que dans les instructions du scénario 1 |
| `RestaurantConsole.cs` (**nouveau**, Start = Solution) | `WriteRestaurant(Restaurant)` et `WriteTokenUsage(AgentResponse)`, importés par `using static` | Le même bloc d'affichage (7 + 5 lignes) était répété 3 fois ; le recentrer sur le sujet (structured output) réduit le bruit des TODO. Placé dans un fichier fourni plutôt qu'en fonctions locales de `Program.cs`, car celles-ci provoquaient `CS8321` dans le Start livré (règle 10 : le Start compile sans warning) |
| `Solution/Program.cs` | Client `OpenAIClient` sur l'endpoint v1 (clé API ou Entra ID) ; `AsAIAgent` ; variables `AIAgent` ; `AgentResponse` / `AgentResponse<T>` ; `ChatResponseFormat.ForJsonSchema<Restaurant>()` ; `JsonSerializer.Deserialize<Restaurant>(response.Text, …)` ; `jsonOptions` unique (web + `JsonStringEnumConverter`) partagé par le schéma et la désérialisation ; instructions en raw string literal ; **nouveau scénario 4** (`AgentRunOptions.ResponseFormat` + streaming) | APIs 1.22.0, patterns du sample et de la doc officiels |
| `Start/Program.cs` | TODO réécrits et renumérotés **1 → 19** (setup 1–3 identique à Lab01 ; S1 4–7 ; S2 8–11 ; S3 12–15 ; S4 16–19) ; `#pragma OPENAI001` et `jsonOptions` fournis | Cohérence exercice ↔ solution ; compile et s'exécute tel que livré |
| `README.md` (lab) | Réécrit sur le gabarit Lab01 : configuration user-secrets / Entra ID, fichiers fournis, tableau des 19 TODO, **« Which approach should I use? »**, concepts, namespaces, sortie attendue réelle, note sur le mode non strict, dépannage (dont `response_format` et enums), packages, « Going further » (strict, schéma brut, agents sans SO natif), encadré « Coming from an older version », liens mis à jour | Aucune API obsolète expliquée ; l'ancienne affirmation « `RunAsync<T>` only with `ChatClientAgent` » était devenue fausse |
| `README.md` (racine) | Lab02 ajouté aux labs migrés ; tableau des 4 scénarios | Documentation associée |
| `Migration/Migration-Manual.md` | §4.8 « Structured Output » + ligne dans la table §5 | Référence pour les labs suivants |
| `Migration/Migration-Plan.md` | Pratique Lab02 (§5 bis) → ✅ appliqué | Suivi |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `AzureOpenAIClient` (Azure.AI.OpenAI) | `OpenAIClient` + `OpenAIClientOptions.Endpoint = …/openai/v1/` (`ApiKeyCredential` \| `BearerTokenPolicy`) |
| `ChatClientAgent x = chatClient.CreateAIAgent(...)` | `AIAgent x = chatClient.AsAIAgent(...)` |
| `AgentRunResponse` / `AgentRunResponse<T>` | `AgentResponse` / `AgentResponse<T>` |
| `ChatClientAgent.RunAsync<T>` (seulement) | `AIAgent.RunAsync<T>` (tout agent) |
| `AIJsonUtilities.CreateJsonSchema(typeof(Restaurant))` + `ChatResponseFormat.ForJsonSchema(schema, name, description)` | `ChatResponseFormat.ForJsonSchema<Restaurant>(jsonOptions, schemaName: "RestaurantInfo")` (description issue de `[Description]`) |
| `response.Deserialize<Restaurant>(options)` | **supprimé** de l'API publique → `JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions)` |
| `JsonSerializer.Deserialize(response.ToString(), …)` | `response.Text` |
| — | `AgentRunOptions { ResponseFormat = … }` (format pour un seul run, prioritaire sur celui de l'agent) |
| — | `RunStreamingAsync(..., options: runOptions)` + `updates.ToAgentResponse()` |

Dépendances résolues (Solution) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. **Aucune préversion.**

## 3. Changements conceptuels

1. **`RunAsync<T>` n'est plus propre à `ChatClientAgent`** : il est défini sur `AIAgent` (`AIAgentStructuredOutput.cs`). Il génère le schéma, le passe via `AgentRunOptions.ResponseFormat` puis renvoie un `AgentResponse<T>` dont `Result` désérialise `Text`. Le lab enseigne donc `AIAgent` partout (règle 3), et le scénario 2 reste l'approche recommandée.
2. **Deux niveaux de configuration du format** : sur l'agent (`ChatClientAgentOptions.ChatOptions.ResponseFormat`, scénario 3) et pour un run (`AgentRunOptions.ResponseFormat`, scénario 4, nouveau). Le second prend le pas sur le premier (vérifié dans `ChatClientAgent.cs` : `requestChatOptions.ResponseFormat ??= agentOptions…`).
3. **Structured output et streaming** : il n'y a pas de `RunStreamingAsync<T>`. On assemble les updates, puis on désérialise (doc Learn + sample officiel). C'est le scénario 4, qui prolonge le streaming vu dans Lab01.
4. **Le schéma est la source de vérité** : les consignes de format (unité, plage) passent des instructions aux `[Description]` du type.
5. **Guide de choix** ajouté au README (quand utiliser chaque approche ; `ResponseFormat` n'accepte que des objets, alors que `RunAsync<T>` accepte aussi les primitives et les tableaux), repris de la page Learn.

## 4. Éléments supprimés ou remplacés

- `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `Azure.Identity` beta : supprimés ou remplacés (cf. Lab01).
- `AgentResponse.Deserialize<T>()` : n'existe plus en 1.22.0 → `JsonSerializer.Deserialize<T>(response.Text, …)`.
- `AIJsonUtilities.CreateJsonSchema` : toujours disponible dans MEAI, mais plus nécessaire ici → `ForJsonSchema<T>()`. On ne garde pas une API uniquement parce qu'elle fonctionne encore.
- Aucun scénario pédagogique supprimé : les 3 scénarios d'origine sont conservés dans leur intention ; 1 scénario ajouté.
- Corrigé au passage : les instructions tronquées du TODO 7 d'origine (« When asked about a restaurant. »), le titre du scénario 3 affiché dans le Start mais pas dans la Solution, et le commentaire « JsonSerializerOptions.Web » d'une option (`WebDefaults`) qui n'existe pas.

## 5. Tests effectués et résultats

Environnement : macOS, SDK .NET 10.0.100, ressource `*.cognitiveservices.azure.com`, déploiement `gpt-4o-mini`.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build Start/Solution) | ✅ 0 erreur, 8 warnings NU1902/NU1903 (CommonUtilities) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances | ✅ MAF 1.22.0, pas d'`Azure.AI.OpenAI`, aucune préversion |
| 3 | Vulnérabilités | ⚠️ `Snappier` 1.0.0 (high), `SharpCompress` 0.30.1 (moderate) — transitifs de CommonUtilities, **identiques à la baseline** |
| 4 | `dotnet build -warnaserror` (NU1902/NU1903 exclus) Start + Solution | ✅ 0 erreur, 0 warning C# (1ʳᵉ tentative du Start : `CS8321`/`CS0219` → helpers déplacés dans `RestaurantConsole.cs`, question remise en ligne) |
| 5 | grep des anciens noms (`CreateAIAgent`, `AgentRunResponse`, `AzureOpenAIClient`, `CreateJsonSchema`, `response.Deserialize`, `ToString()`, `.NET 8`, Hosting) | ✅ 0 occurrence, hors encadré « Coming from an older version » |
| 6 | Run Solution — **clé API** | ✅ scénarios 1–4 corrects (Le Bernardin, Éric Ripert, French, 3 étoiles, 1986) ; usage ~238 / 362 / 340 / 334 tokens ; JSON streamé visible progressivement |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`) | ✅ scénarios 2 et 4 corrects (l'authentification est commune aux 4) |
| 8 | Config placeholder | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set …', or with the environment variable 'AzureOpenAI__Endpoint'.` |
| 9 | Run Start livré | ✅ compile, affiche les 4 en-têtes de scénario sans erreur |
| 10 | Start complété **uniquement d'après les indices du README** (copie temporaire dans le lab, supprimée ensuite) | ✅ compile sans warning C# et reproduit les 4 scénarios |
| 11 | `diff Start Solution` hors `Program.cs` | ✅ 6 fichiers identiques (`.csproj`, config ×3, `Models/Restaurant.cs`, `RestaurantConsole.cs`) |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO 1–19 a un indice qui mène au code de la Solution |
| + | Essai du mode strict (`AdditionalProperties["strict"] = true`, modification temporaire du scénario 3, annulée) | ✅ 2 runs : JSON strictement conforme, plus de propriété `description` |

Pas de projet de test automatisé dans le dépôt : la validation suit la stratégie manuelle §7 du manuel, comme pour Lab01. `CommonUtilities` n'a pas été modifié, donc pas de risque de régression pour les autres labs.

## 6. Problèmes rencontrés

1. **Propriété hors schéma en mode non strict** : avec `ForJsonSchema<T>` (scénarios 3 et 4), `gpt-4o-mini` ajoute presque toujours une propriété `"description"`. Cause, vérifiée dans `Microsoft.Extensions.AI.OpenAI` 10.10.0 (`OpenAIChatClient.ToOpenAIChatResponseFormat`) : le schéma est transformé au format strict, mais `strict` n'est envoyé que si `ChatOptions.AdditionalProperties["strict"]` vaut `true`. Sans impact fonctionnel (propriété ignorée à la désérialisation). **Décision** : le code reste aligné sur le sample officiel (qui n'active pas le mode strict), et le README l'explique, avec l'option dans « Going further ».
2. **Warnings du Start livré** (`CS8321` fonctions locales non utilisées, `CS0219` constante non utilisée) : résolus en déplaçant les helpers dans un fichier fourni et en gardant la question en ligne dans chaque appel, comme dans le lab d'origine.
3. **Le sample officiel utilise Foundry** (`AIProjectClient`, `Microsoft.Agents.AI.Foundry`, préversion en 1.22.0) : seuls les patterns agent (`RunAsync<T>`, `ForJsonSchema<T>`, `ToAgentResponseAsync`) ont été repris, appliqués au `ChatClient` Azure OpenAI v1 (même décision que Lab01).
4. **Middleware `UseStructuredOutput`** du sample officiel : c'est un code **propre au sample** (`StructuredOutputAgent.cs`, `AIAgentBuilderExtensions.cs`), pas une API du framework. Il n'est pas enseigné (règle 12) et seulement mentionné dans « Going further ».

## 7. Décisions à valider

- **Ajout du scénario 4** (`AgentRunOptions` + streaming, TODO 16–19) : ajout de contenu justifié par la doc Learn et le sample officiel (3ᵉ approche de `Agent_Step02_StructuredOutput`).
- **Nouveau fichier fourni `RestaurantConsole.cs`** : c'est un écart au principe « seul `Program.cs` diffère du lab précédent », mais il est identique dans Start et Solution (la règle 9 est respectée) et ne masque aucune API du framework.
- **`[Description]` ajoutés au modèle** : ils modifient un fichier « fourni ». C'est volontaire, car ils font partie de la leçon.
- **Mode strict non activé dans le code** (voir §6.1).

## 8. Points à surveiller / pour les prochains labs

- **Alias `AIExtensions`** : `OpenAI.Chat` définit aussi `ChatResponseFormat` (en plus de `ChatMessage`), donc l'alias est indispensable dès qu'on manipule un format de réponse.
- **`JsonSerializerOptions`** : si un lab désérialise lui-même une réponse structurée, utiliser les **mêmes options** pour générer le schéma et pour désérialiser (camelCase + enums en chaîne), sinon les noms ou les enums divergent.
- **Warnings du Start** : les helpers en fonctions locales de top-level déclenchent `CS8321` tant que les TODO ne sont pas faits → les placer dans un fichier fourni.
- **Dashboard** : chaque lab migré s'y enregistre par une entrée `labs.json` + son id dans `LabCatalogTests` (cf. §9). Écrire les checks pour qu'ils échouent sur le Start livré.
- **Lab08 (Data formats)** utilise probablement aussi des réponses JSON : réutiliser §4.8 du manuel.
- **Vague 1 restante** : Lab03, Lab08, Lab11.

---

## 9. Intégration au dashboard (Lab Bench)

**Analyse** : le dashboard est piloté par `Dashboard/LabDashboard/labs.json`. Le lancement (`dotnet build` puis `dotnet run --no-build`), les logs SSE, la ligne d'état du spinner, le rendu du README, le diff de solution et l'historique sont génériques. Seuls trois points étaient liés au fait qu'il n'y avait qu'un lab :

| Point | Nécessaire ? | Changement |
|---|---|---|
| Lab02 absent du catalogue | oui | Entrée `azureopenai-lab02` dans `labs.json` : métadonnées EN + FR (titre, résumé, niveau, objectifs), projets Start/Solution, timeout 180 s, **9 checks** (config ; pour chacun des 4 scénarios, un résultat structuré réellement obtenu + la consommation de tokens) |
| `LabCatalogTests` supposait un seul lab (`Assert.Single`) | oui (sinon le test échoue) | Le test vérifie la liste des ids et les chemins de **chaque** lab ; nouveau test : chaque check a sa description FR |
| Tokens : Lab02 affiche 4 blocs « Token Usage », donc 4 lignes identiques dans le tableau | oui (affichage ambigu) | `OutputAnalyzer.ParseTokenUsage` préfixe chaque bloc par le dernier en-tête `=== Scenario N` (`Scenario 2 · Token Usage`). Changement générique : Lab01 affiche désormais `Scenario 4 · Token Usage` et `Scenario 5 · Token Usage (streaming)` (test mis à jour) |

Non modifiés (déjà génériques) : `Program.cs`, `LabRunner`, `ProcessRunner`, `ConsoleStreamDecoder`, UI (`wwwroot`), sécurité. Documentation : `Dashboard/README.md` (labs enregistrés, libellé des tokens, procédure « Add a lab » sans code, conseil sur les checks par scénario), README racine.

**Choix des checks** : ils vérifient le résultat, pas seulement la présence d'une ligne. Par exemple `Successfully parsed structured response:` en scénario 1, et `JSON response:` → `{…}` → `Deserialized response:` → `Name: …` en scénario 3. Les checks d'usage s'arrêtent à l'en-tête suivant (`(?!^=== Scenario)`), pour qu'un scénario sans usage n'emprunte pas celui du scénario suivant (test dédié).

**Tests** :

| Test | Résultat |
|---|---|
| `dotnet test` (Dashboard) | ✅ 21/21 (baseline 17/17 ; 4 tests ajoutés : libellés par scénario, checks Lab02 sur une sortie Solution et Start, non-emprunt de l'usage, descriptions FR) |
| API : `GET /api/labs`, détails FR, README rendu, commandes CLI, diff de solution | ✅ 2 labs ; commandes `cd …/Lab02-AIAgentWithSO/Start && dotnet run` ; seul `Program.cs` diffère |
| Run Lab02 **Solution** via l'API | ✅ `passed`, 9/9 checks, 4 rapports de tokens `Scenario N · Token Usage`, total 1 304 |
| Run Lab02 **Start** livré | ✅ `failed` attendu : seul `config` passe (même comportement que Lab01) |
| Non-régression Lab01 Solution / Start | ✅ `passed` 7/7 / `failed` attendu (1/7) |
| UI (navigateur) : liste, sélection de Lab02, run Solution depuis le bouton « Exécuter » | ✅ « Réussi · Solution », tokens rapportés, logs en direct |
| Lab02 en CLI sans dashboard (`dotnet run` dans `Solution/`) | ✅ 4 scénarios, code de sortie 0 |

Les runs de vérification ont ajouté des entrées dans l'historique local `Dashboard/.data/history.json` (fichier git-ignoré).

