# Rapport de migration — MultiAgentSystem/Lab03_OrchestrationConcurrent

> Date : 2026-11-05 · Cible : **Microsoft Agent Framework 1.26.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.20 ajouté, §3, §5), [Migration-Plan.md](Migration-Plan.md), [Lab02_OrchestrationSequential-Migration-Report.md](Lab02_OrchestrationSequential-Migration-Report.md) (socle et méthode, migré le même jour), [Communication-Step4-Report.md](Communication-Step4-Report.md) (MAS-Lab01 : règles après-vente, délégation par le code). Sources officielles (tag `dotnet-1.26.0`) : samples `03-workflows/_StartHere/03_AgentWorkflowPatterns`, `04_MultiModelService`, `05_SubWorkflows`, `Concurrent/Concurrent`, `Concurrent/MapReduce`, `Agents/WorkflowAsAnAgent` ; sources `AgentWorkflowBuilder.cs`, `ConcurrentWorkflowBuilder.cs`, `Specialized/ConcurrentEndExecutor.cs`, `Specialized/AggregateTurnMessagesExecutor.cs`, `ChatForwardingExecutor.cs`, `InProc/InProcessRunner.cs` (`RunSuperstepAsync`), `WorkflowSession.cs` ; `PublicAPI.Shipped.txt` de `Microsoft.Agents.AI.Workflows` ; page Learn [Concurrent orchestration](https://learn.microsoft.com/agent-framework/workflows/orchestrations/concurrent?pivots=programming-language-csharp) (mise à jour du 2026-10-28).

**Statut : migré (refonte).** Même démarche que Lab02 : l'ancien lab (un scénario, API supprimées, un agent « follow-up » qui rédigeait la réponse en parallèle des spécialistes sans connaître leurs réponses) est réécrit pour enseigner progressivement l'orchestration concurrente. Tous les critères du manuel (§7) sont remplis, dont l'exécution réelle par clé API **et** Entra ID et le run du dashboard (Solution `passed` 9/9).

**Version** : 1.26.0 (version figée du dépôt), comme Lab02 ; 1.27.0 n'apporte aucune rupture dans Workflows hors déclaratif et Magentic (rapport Lab02 §5).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` et `Solution/OrchestrationConcurrent.csproj` | Socle de Lab02 : MAF 1.26.0 (`.OpenAI` + `.Workflows`), `Microsoft.Extensions.AI.OpenAI` 10.11.0, `Azure.Identity` 1.21.0, `Microsoft.Extensions.Configuration.*` 10.0.12, `UserSecretsId` partagé, copie de `MockedData/sav-rules.yml` ; supprimés : `Azure.AI.OpenAI` beta, `Azure.Identity` beta, `Microsoft.Extensions.Hosting`, `NoWarn MEAI001` | Manuel §1, §4.7, §8 (streaming) |
| `appsettings.json`, `ConfigurationHelper.cs`, `AzureOpenAISettings.cs` | Repris de Lab02 (espace de noms `OrchestrationConcurrent`) | Socle commun |
| `LabPrompts.cs` (nouveau) | 3 spécialistes (`ReturnsSpecialist`, `RefundSpecialist`, `ShippingSpecialist` : chaînes de format avec `{0}` = règles, réponse préfixée `Return/exchange:` / `Refund:` / `Delivery:`), `ReplyWriter`, `CustomerMessages.MissingChargerAndWrongSleeve` | Convention des labs ; réponses ancrées dans les règles |
| `MockedData/sav-rules.yml` (nouveau) | Copie des règles de MAS-Lab01 (même boutique) | Les spécialistes répondent avec des règles, pas d'invention |
| `WorkflowConsole.cs` (nouveau) | `WritePipeline` (graphe par niveaux, niveaux parallèles entre crochets), `WriteWorkflowOutput`, `WriteWorkflowOutputAuthors`, `WriteAgentAnswer`, erreurs, `AgentNameOf` ; `ParallelTrace` : début de tour (`ExecutorInvokedEvent` + `TurnToken`), fin (`ExecutorCompletedEvent`), messages reçus, usage par agent, durée du run vs somme des tours, chevauchement des tours | Attentes du dashboard fondées sur des faits déterministes |
| `Solution/Program.cs` | **Scénario 1** `BuildConcurrent`, sortie agrégée, erreurs ; **scénario 2** même workflow en streaming suivi par `ParallelTrace` ; **scénario 3** agrégateur personnalisé `MergeAnswers` (ordre des spécialistes, fusion en un message), puis `ReplyWriter` exécuté par le code après le *fan-in* | Progression : bases → preuve du parallélisme et de l'indépendance → agrégation et synthèse |
| `Start/Program.cs` | TODO 1–15 (setup 1–5, scénarios 6–8, 9–11, 12–15) | Convention |
| `README.md` | Réécrit : « in a nutshell » (fan-out, superstep, batcher, barrière, agrégateurs), cas d'usage et contre-indications, TODO, « Concurrent or sequential? » (mesures), limites, « Try it », événements, concepts, sortie attendue réelle, dépannage (dont `429`), « Going further », encadré « Coming from an older version » | Contenu pédagogique |
| `Workflow Schema.md` | **Supprimé** | Anciens noms (`AgentRunUpdateEvent`), schéma inexact (pas de fan-in) ; remplacé par le schéma du README |
| `exercise-fingerprints.json` (nouveau) | Généré | Sélection des scénarios |
| `Dashboard/LabDashboard/labs.json` | `multiagent-lab03` (après `multiagent-lab02`, 9 attentes, EN + FR), insertion textuelle | Intégration |
| `Dashboard/LabDashboard.Tests` | Catalogue (19 labs) ; sortie réelle Solution / Start ; test négatif (spécialistes enchaînés en séquentiel, agrégateur par défaut au scénario 3) ; libellés de tokens ; `SolutionOutputs` | Non-régression |
| `README.md` racine, `Dashboard/README.md`, manuel, plan | MAS-Lab03 migré, parcours, plus aucun lab « non migré » (phrase sur les variables d'environnement corrigée), §4.20 | Cohérence |

## 2. APIs et dépendances modifiées

Identiques à Lab02 (§2 de son rapport) ; en plus : `AgentWorkflowBuilder.BuildConcurrent(name, agents, aggregator)`, `ExecutorCompletedEvent`. Graphe : MAF 1.26.0, `OpenAI` 2.14.0, pas d'`Azure.AI.OpenAI`, **aucune vulnérabilité**, aucune API expérimentale.

## 3. Changements conceptuels

- **La rédaction de la réponse ne peut pas être parallèle** : l'ancien agent « follow-up » tournait en même temps que les spécialistes. Elle se fait désormais **après** le *fan-in*, avec les réponses fusionnées.
- **Sortie par défaut expliquée** : dernier message de chaque agent, ordre d'arrivée, sans l'entrée (l'ancien code filtrait `ChatRole.User`, inutilement).
- **Parallélisme démontré** par les événements (chevauchement des tours, temps du run vs somme) et **indépendance** (1 message reçu par agent, ≈ 800 tokens chacun).
- **Agrégateur personnalisé** : code déterministe et synchrone ; ordre stable de la sortie.
- **Échec** : un agent en échec fait perdre toute la sortie (« Try it » et limites).

## 4. Éléments supprimés ou remplacés

| Élément | Devenir |
|---|---|
| Agents `Return/Exchange Agent`, `Refund Agent`, `Follow-up Agent` (noms avec espaces et barre oblique) | Spécialistes nommés par identifiants, ancrés dans les règles ; `ReplyWriter` après le *fan-in* |
| `InProcessExecution.StreamAsync`, cast de `Data`, `ConfigureAwait(false)`, `break` | Comme Lab02 |
| `Workflow Schema.md` | Schéma du README |

## 5. Tests effectués et résultats

| # | Vérification (manuel §7) | Résultat |
|---|---|---|
| 1–3 | Restauration, graphe, vulnérabilités | ✅ MAF 1.26.0, pas d'`Azure.AI.OpenAI`, 0 vulnérabilité |
| 4 | `dotnet build -warnaserror` Start + Solution | ✅ 0 erreur, 0 warning |
| 5 | Anciens noms dans `Start/`, `Solution/`, README | ✅ 0 hors encadré |
| 6 | Solution, clé API (CLI) | ✅ 9/9 (2 exécutions complètes) |
| 7 | Solution, Entra ID | ✅ 9/9 |
| 8 | Configuration absente | ✅ *"'AzureOpenAI:Endpoint' is not configured…"* ; un agent en échec (prototype, déploiement inexistant) : `ExecutorFailedEvent`, `WorkflowErrorEvent`, statut `Ended`, aucune sortie |
| 9 | Start livré | ✅ compile, s'exécute ; `--list-scenarios` : 3 scénarios non implémentés |
| 10 | Copie temporaire du Start complétée d'après le README | ✅ build sans warning, 9/9 ; copie supprimée |
| 11 | `diff` Start/Solution hors `Program.cs` | ✅ aucune différence |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ |
| — | `dotnet test` du dashboard | ✅ **670/670** |
| — | Dashboard (API isolée, sans rapport vers le serveur d'administration) | ✅ Solution `passed` « All 9 checks passed » en 7,4 s, scénarios 1–3 `passed`, 4 rapports de tokens ; Start livré refusé (`noneImplemented`), voulu |

Mesures (gpt-4o-mini) : run du scénario 2 1,0–1,4 s contre 2,4–3,4 s de tours cumulés ; ≈ 803–809 tokens d'entrée par spécialiste ; le rédacteur ≈ 290 tokens d'entrée. L'ordre de la sortie par défaut change d'une exécution à l'autre (par exemple Returns, Shipping, Refund puis Shipping, Refund, Returns).

## 6. Problèmes rencontrés

1. **Exemple C# de la page Learn** : il montre `User: Hello, world!` dans la sortie agrégée ; le code 1.26.0 (agrégateur par défaut) ne renvoie que la dernière réponse de chaque agent. Le lab suit le code ; signalé au manuel §4.20.
2. **Workflow concurrent utilisé comme agent** dans un séquentiel : l'ordre de l'agrégateur personnalisé n'était pas conservé (l'agent renvoie les messages des agents internes ; la sortie terminale dépend d'options et de drapeaux « Futures »). Abandonné au profit d'un rédacteur exécuté par le code ; cité en « Going further » avec la réserve.
3. **Spécialistes hors de leur domaine** : malgré des instructions resserrées, chacun aborde parfois les autres sujets, en partie légitimement (une housse de la mauvaise couleur relève aussi de « wrong item received »). Conservé et enseigné : des agents parallèles ne se coordonnent pas, d'où la synthèse du scénario 3.
4. **Test du Start complété** : mon premier script d'insertion a produit un `Program.cs` invalide (erreur de mon outil, pas du README) ; copie réécrite directement d'après les cellules du README, 9/9.

## 7. Décisions à valider

- **Refonte** : trois scénarios, règles de MAS-Lab01, réponse rédigée **après** le *fan-in* au lieu d'un agent parallèle.
- **Synthèse par le code** (et non par un workflow composé) : agrégateur C# synchrone ; composition notée en « Going further ».
- **Seuil du check `scenario2-parallel`** : « tous les tours se chevauchent » (le dernier début avant la première fin) ; robuste sur toutes les exécutions observées, mais un déploiement très lent ou limité en débit pourrait le faire échouer (documenté dans le dépannage).

## 8. Points pour la suite

- Tous les labs du dépôt sont désormais migrés : la **Central Package Management** (plan, « après la vague 5 ») peut être envisagée, ainsi que la montée en 1.27.0 en vague unique.
- Orchestrations non couvertes : handoff, group chat, Magentic (expérimental) — candidates pour de futurs labs.

## 9. Intégration au dashboard (Lab Bench)

`multiagent-lab03`, 9 attentes : `config` ; `scenario1-graph`, `scenario1-output` (une réponse par spécialiste, ordre quelconque, sans l'entrée) ; `scenario2-context`, `scenario2-parallel`, `scenario2-usage`, `scenario2-order` ; `scenario3-merged` (ordre des spécialistes), `scenario3-reply` (seule attente qui dépend du texte du modèle : le mot « charger »). Aucune modification du code du dashboard.
