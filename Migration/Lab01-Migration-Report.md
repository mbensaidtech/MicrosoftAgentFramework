# Rapport de migration — Lab01-FirstBasicAIAgent (pilote)

> Date : 2026-09-26 · Cible : **Microsoft Agent Framework 1.22.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md), [Migration-Plan.md](Migration-Plan.md)

**Statut : migré.** Tous les critères de la définition de « terminé » sont remplis, avec une réserve : les warnings de vulnérabilité NuGet hérités de `CommonUtilities` (voir §6), qui ne viennent pas de Lab01 et sont planifiés en phase 0.

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` + `Solution/FirstBasicAIAgent.csproj` | `Microsoft.Agents.AI.OpenAI` 1.0.0-preview.251204.1 → **1.22.0** ; suppression de `Azure.AI.OpenAI 2.7.0-beta.2` et `Microsoft.Extensions.Hosting 9.0.0` ; `Azure.Identity` 1.18.0-beta.2 → **1.21.0** ; ajout de `Microsoft.Extensions.Configuration.{Json,EnvironmentVariables,UserSecrets,Binder}` 10.0.12 ; ajout de `UserSecretsId` commun à tous les labs ; chaque package commenté | Version stable ; `Azure.AI.OpenAI` n'est plus utilisé par MAF (1.21.0, PR #7986) ; Hosting inutile pour lire la config ; plus aucune préversion |
| `ConfigurationHelper.cs` (Start = Solution) | `Host.CreateApplicationBuilder()` → `ConfigurationBuilder` (`appsettings.json` → user-secrets → variables d'env.) + validation avec message actionnable | Les user-secrets n'étaient pas chargés hors environnement `Development` ; erreurs de config lisibles |
| `AzureOpenAISettings.cs` (Start = Solution) | Mêmes propriétés (compatibles avec vos variables `AzureOpenAI__*`) + documentation XML | `APIKey` documenté comme secret à ne pas mettre dans `appsettings.json` |
| `appsettings.json` (Start = Solution) | Champ `APIKey` retiré | Pas de secret dans un fichier versionné |
| `Solution/Program.cs` | `AzureOpenAIClient` → `OpenAIClient` sur l'endpoint v1 (clé API **ou** `BearerTokenPolicy` + `DefaultAzureCredential`) ; `CreateAIAgent` → `AsAIAgent` ; variables typées `AIAgent` ; `AgentRunResponse` → `AgentResponse` ; `ToString()` → `.Text` ; nom de l'agent affiché (`agent.Name`) ; **nouveau scénario 5 : streaming** (`RunStreamingAsync` / `AgentResponseUpdate`) ; commentaire de sécurité sur les messages système | APIs 1.22.0 et pattern des samples officiels ; le streaming est enseigné dans le premier sample officiel et n'était couvert par aucun lab |
| `Start/Program.cs` | TODO réécrits pour les nouvelles APIs, renumérotés **1 → 20** (setup : 3 TODO ; scénario 5 : TODO 19–20) ; `#pragma` `OPENAI001` fourni ; le projet compile et s'exécute tel que livré | Cohérence exercice ↔ solution |
| `README.md` (lab) | Réécrit : prérequis .NET 10, configuration user-secrets / Entra ID, tableau des 20 TODO, concepts clés actualisés, namespaces, table des packages, dépannage, « Going further » (Responses API, Foundry), encadré « Coming from an older version », liens corrigés (`microsoft/agent-framework`) | Le README ne doit expliquer aucune API obsolète |
| `CommonUtilities/AzureOpenAIEndpoint.cs` (**nouveau**) | `ToV1Uri(string)` : ajoute `/openai/v1/` à l'endpoint de la ressource | Réutilisable par les 16 labs ; équivalent du helper officiel `SampleHelpers.AzureOpenAIEndpoint` ; ajout pur, sans impact sur les labs non migrés |
| `CommonUtilities/README.md` | Documente `AzureOpenAIEndpoint` et `WithSpinner` | Documentation du composant partagé |
| `README.md` (racine) | Prérequis .NET 10 (toutes les TFM sont déjà `net10.0`), statut de migration, scénario 5 de Lab01, variable `AzureOpenAI__APIKey`, note user-secrets, lien du dépôt officiel corrigé | Documentation associée |

## 2. APIs et dépendances modifiées

| Ancien | Nouveau |
|---|---|
| `Azure.AI.OpenAI.AzureOpenAIClient(Uri, TokenCredential/ApiKeyCredential)` | `OpenAI.OpenAIClient(ApiKeyCredential \| BearerTokenPolicy, OpenAIClientOptions { Endpoint = …/openai/v1/ })` |
| `ChatClientAgent x = chatClient.CreateAIAgent(...)` | `AIAgent x = chatClient.AsAIAgent(...)` |
| `AgentRunResponse` | `AgentResponse` |
| — | `RunStreamingAsync` → `IAsyncEnumerable<AgentResponseUpdate>` |
| `Host.CreateApplicationBuilder().Configuration` | `ConfigurationBuilder` + `AddUserSecrets` |

Dépendances résolues (Solution) : `Microsoft.Agents.AI(.Abstractions/.OpenAI)` 1.22.0, `OpenAI` 2.13.0, `Microsoft.Extensions.AI(.Abstractions/.OpenAI)` 10.10.0, `System.ClientModel` 1.15.0, `Azure.Identity` 1.21.0, `Azure.Core` 1.53.0. **Aucun package en préversion** (`dotnet list package --include-transitive`).

## 3. Patterns remplacés

- Client Azure spécifique → **SDK OpenAI officiel + API v1 d'Azure OpenAI** (pas d'`api-version`).
- Variables typées sur l'implémentation (`ChatClientAgent`) → **abstraction `AIAgent`**.
- `response.ToString()` → **`response.Text`** (intention explicite ; `ToString()` renvoie toujours `Text`).
- Secret dans `appsettings.json` → **user-secrets / variables d'environnement**.
- Spinner via `using var` (qui restait actif jusqu'à la fin du scope) → bloc `using (...) { }` dans l'exemple du README.

## 4. Éléments supprimés ou dépréciés

- `Azure.AI.OpenAI` (dépendance retirée de MAF en 1.21.0), `Microsoft.Extensions.Hosting` (inutile ici).
- `CreateAIAgent`, `AgentRunResponse` (renommés en preview.260121.1).
- Champ `APIKey` de `appsettings.json`.
- Aucune fonctionnalité pédagogique supprimée : les 4 scénarios d'origine sont conservés à l'identique dans leur intention ; 1 scénario ajouté.

## 5. Tests effectués et résultats

Environnement : macOS, SDK .NET 10.0.100, ressource Azure OpenAI `*.cognitiveservices.azure.com`, déploiement `gpt-4o-mini`.

| # | Test | Résultat |
|---|---|---|
| 0 | Baseline avant migration (build Start/Solution) | 0 erreur, 8 warnings NU1902/NU1903 (CommonUtilities) |
| 1 | `dotnet restore` Start + Solution | ✅ |
| 2 | Graphe de dépendances | ✅ MAF 1.22.0, aucun `Azure.AI.OpenAI`, aucune préversion |
| 3 | Vulnérabilités | ⚠️ `Snappier` 1.0.0 (high), `SharpCompress` 0.30.1 (moderate) — transitifs de `MongoDB.Driver 2.30.0` via CommonUtilities, **identiques à la baseline** |
| 4 | Build Solution | ✅ 0 erreur, 0 warning C# (1ʳᵉ tentative : erreur `OPENAI001` → corrigée par une suppression ciblée et commentée, pattern de la doc Microsoft) |
| 4b | Build Start | ✅ 0 erreur, 0 warning C# |
| 5 | grep des anciens noms | ✅ 0 occurrence, sauf l'encadré « Coming from an older version » du README (voulu) |
| 6 | Run Solution — **clé API** | ✅ scénarios 1–5 corrects ; usage 56 / 31 / 87 tokens ; streaming affiché progressivement |
| 7 | Run Solution — **Entra ID** (`AzureOpenAI__APIKey=""`, `az login`) | ✅ scénarios 1–5 corrects ; scope `https://ai.azure.com/.default` accepté |
| 8 | Config absente (placeholders) | ✅ `'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set …', or with the environment variable 'AzureOpenAI__Endpoint'.` |
| 8b | Source **user-secrets** seule (HOME isolé, variables d'env. retirées) | ✅ scénario 1 correct |
| 9 | Run Start livré | ✅ compile, affiche les 5 en-têtes de scénario sans erreur |
| 10 | Start complété **uniquement d'après les indices du README** (copie temporaire, supprimée ensuite) | ✅ compile et reproduit les 5 scénarios |
| 11 | `diff Start Solution` | ✅ seul `Program.cs` diffère |
| 12 | Non-régression des 15 autres labs après l'ajout dans CommonUtilities | ✅ 15/15 Solutions compilent (0 erreur) |

## 6. Problèmes rencontrés

1. **`OPENAI001`** : le constructeur `OpenAIClient(AuthenticationPolicy, OpenAIClientOptions)` est encore marqué `[Experimental]` dans OpenAI 2.13 (et 2.14). Le dépôt MAF le masque globalement pour tous ses samples via `dotnet/samples/.editorconfig` ; la doc Azure utilise `#pragma warning disable OPENAI001`. Choix : pragma **ciblé** autour de la seule ligne concernée, commenté, fourni dans le Start pour ne pas bloquer l'apprenant.
2. **Documentation Learn** : le « Get started » C# utilise désormais Foundry (`Microsoft.Agents.AI.Foundry`, installé avec `--prerelease` ; 1.22.0 n'existe qu'en préversion). Écarté : règle « stable uniquement » et le lab cible Azure OpenAI. Le pattern retenu est celui du sample officiel `02-agents/AgentProviders/azure/Agent_With_AzureOpenAIChatCompletion`.
3. **Pas de guide d'upgrade .NET sur Learn** (seulement Python) : les breaking changes ont été reconstitués à partir des release notes GitHub `dotnet-*` et du code source au tag `dotnet-1.22.0`.
4. **Vulnérabilités NuGet** héritées de CommonUtilities (`MongoDB.Driver 2.30.0`) : non corrigées dans le pilote, car monter le driver casserait (`NU1605`) Lab05/07/12, qui le référencent directement en 2.30.0. Planifié en phase 0 avec Lab05.
5. **graphify** : l'AST n'extrait aucun symbole des `Program.cs` à top-level statements de 5 Solutions ; sans effet sur le code.

## 7. Décisions à valider

- **Chat Completions plutôt que Responses API** : Microsoft recommande la Responses API pour les nouvelles apps Azure OpenAI, mais elle stocke l'historique côté service par défaut, ce qui contredit la pédagogie des labs sessions/persistance (Lab05, Lab12). Lab01 reste sur `GetChatClient()` et présente la Responses API dans « Going further ».
- **Ajout du scénario 5 (streaming)** : c'est un ajout de contenu (TODO 19–20), justifié par le sample officiel `01_hello_agent`.
- **`UserSecretsId` partagé par tous les labs** (`microsoft-agent-framework-learninglabs`) : l'apprenant configure sa clé une seule fois.

## 8. Points à surveiller

- Cadence de release MAF (~hebdomadaire) : rester sur 1.22.0 jusqu'à la fin de la migration.
- `OPENAI001` : retirer le pragma dès que le SDK OpenAI stabilise le constructeur.
- Les réponses de `gpt-4o-mini` ignorent parfois les consignes de format (ex. liste des pays avec la Guyane) : c'est un comportement du modèle, pas du code.

## 9. Recommandations pour la suite

1. Réutiliser tel quel le socle Lab01 : `.csproj`, `ConfigurationHelper.cs`, `AzureOpenAISettings.cs`, `appsettings.json`, bloc « Setup » de `Program.cs`, sections README (configuration, packages, dépannage, « Coming from an older version »).
2. Vague 1 (Lab02, Lab03, Lab08, Lab11) : surtout mécanique ; corriger au passage les écarts Start/Solution existants (Lab03 : packages MCP/DI présents seulement dans la Solution ; Lab08 : `ToonNet` vs `ToonNetSerializer`).
3. Avant la vague 3 : décider du sort de `MongoDbHealthCheck` (projet séparé ?) et du connecteur vectoriel MongoDB (aucune version stable identifiée).
4. Lab06 : obtenir votre accord sur l'exception « préversion » (A2A n'a aucune version stable).
5. Automatiser les tests 1–5 et 11 du manuel dans un script (`Migration/validate-lab.sh`) pour les labs suivants.

---

## 10. Addendum (2026-09-26) — pratiques complémentaires retenues

Seules les pratiques démontrées dans les samples officiels 1.22.0 ont été ajoutées à Lab01 :

| Changement | Fichiers | API officielle |
|---|---|---|
| Scénario 4 : affichage de `Usage.ReasoningTokenCount` (part des tokens de sortie consacrée au raisonnement caché) | `Solution/Program.cs`, TODO 18, README | `Microsoft.Extensions.AI.UsageDetails.ReasoningTokenCount` |
| Scénario 5 : collecte des `AgentResponseUpdate` puis `updates.ToAgentResponse().Usage` pour afficher la consommation d'un run en streaming ; **nouveau TODO 21** | `Solution/Program.cs`, `Start/Program.cs`, README | `AgentResponseExtensions.ToAgentResponse` |

**Tests** : build Start + Solution sans erreur ni warning C# ; exécution réelle (clé API) : scénario 4 `56 / 29 / reasoning 0 / 85`, scénario 5 `Token Usage (streaming): 38 / 120 / 158` — l'usage est bien renseigné en Chat Completions streaming.

**Décision `OPENAI001`** : on conserve le `#pragma disable/restore` ciblé dans `Program.cs` (règle 13 du manuel). L'option « fabrique dans CommonUtilities » est abandonnée, car ce serait une abstraction maison qui éloignerait le code des samples officiels.

**Non retenu** : bibliothèques tierces non officielles, variables typées `ChatClientAgent`, clé API seule, Responses API par défaut (expérimentale en OpenAI 2.13).
