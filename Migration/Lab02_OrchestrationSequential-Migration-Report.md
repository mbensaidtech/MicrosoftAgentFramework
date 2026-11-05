# Rapport de migration — MultiAgentSystem/Lab02_OrchestrationSequential

> Date : 2026-11-05 · Cible : **Microsoft Agent Framework 1.26.0** · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§4.19 ajouté, §3, §5), [Migration-Plan.md](Migration-Plan.md), [Communication-Step4-Report.md](Communication-Step4-Report.md) (MAS-Lab01, socle de référence), [Lab09-Migration-Report.md](Lab09-Migration-Report.md) (approbations). Sources officielles (tag `dotnet-1.26.0`) : samples `03-workflows/_StartHere/02_AgentsInWorkflows`, `03_AgentWorkflowPatterns`, `06_MixedWorkflowAgentsAndExecutors`, `Agents/GroupChatToolApproval` ; sources `AgentWorkflowBuilder.cs`, `SequentialWorkflowBuilder.cs`, `OrchestrationBuilderBase.cs`, `AIAgentHostOptions.cs`, `Specialized/AIAgentHostExecutor.cs`, `Specialized/OutputMessagesExecutor.cs`, `ChatProtocolExecutor.cs`, `InProcessExecution.cs`, `WorkflowOutputEvent.cs`, `TurnToken.cs` ; `PublicAPI.Shipped.txt` / `.Unshipped.txt` de `Microsoft.Agents.AI.Workflows` ; tests `SequentialWorkflowBuilderTests.cs`, `OrchestrationTestHelpers.cs` ; page Learn [Sequential orchestration](https://learn.microsoft.com/agent-framework/workflows/orchestrations/sequential?pivots=programming-language-csharp) (mise à jour du 2026-10-28) ; notes de version `dotnet-1.27.0`.

**Statut : migré (refonte).** Le lab enseignait un seul appel de `BuildSequential` sur des API supprimées ; il est réécrit pour couvrir progressivement l'orchestration séquentielle : fonctionnement, contexte transmis entre agents, observation par les événements, gestion des erreurs, intervention humaine, cas d'usage et limites. Tous les critères du manuel (§7) sont remplis, dont l'exécution réelle de la Solution par clé API **et** Entra ID et le run du dashboard (Solution `passed` 13/13, tokens affichés).

**Version** : la dernière version publiée au 2026-11-05 est **1.27.0** (2026-10-30). Le lab cible **1.26.0**, version figée du dépôt (manuel §8 : une montée de version se fait par vague, sur tous les labs). Les notes de 1.27.0 ne contiennent aucune rupture dans Workflows (hors déclaratif et Magentic) et un prototype avec les mêmes appels donne les mêmes résultats en 1.27.0 (§5) : le lab passera en 1.27.0 sans changement de code lors de la prochaine vague. **À valider** (§7).

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| `Start/` et `Solution/OrchestrationSequential.csproj` | `Microsoft.Agents.AI.OpenAI` 1.26.0 + `Microsoft.Agents.AI.Workflows` 1.26.0 + `Microsoft.Extensions.AI.OpenAI` 10.11.0 explicite + `Azure.Identity` 1.21.0 + `Microsoft.Extensions.Configuration.*` 10.0.12 ; `UserSecretsId` partagé ; supprimés : `Azure.AI.OpenAI` 2.7.0-beta.2, `Azure.Identity` 1.18.0-beta.2, `Microsoft.Extensions.Hosting` 9.0.0, `NoWarn MEAI001` global | Socle des labs migrés (identique à MAS-Lab01) ; 10.11.0 : les agents d'un workflow sont streamés quand le `TurnToken` demande des événements (manuel §8) |
| `appsettings.json`, `ConfigurationHelper.cs`, `AzureOpenAISettings.cs` | Repris de MAS-Lab01 (espace de noms `OrchestrationSequential`) : plus de champ `APIKey` dans `appsettings.json`, validation avec message actionnable | Manuel §4.7 |
| `LabPrompts.cs` (nouveau, fourni) | Noms et instructions des 5 agents : `MeetingSummarizer`, `ActionItemsExtractor`, `FollowUpWriter`, `Translator`, `MinutesSender` | Convention des labs (textes fournis) |
| `Tools/EmailTools.cs` (nouveau, fourni) | `SendEmail(to, subject, body)` simulé | Scénario 3 : action à effet de bord, soumise à approbation |
| `WorkflowConsole.cs` (nouveau, fourni) | `WritePipeline` (chemin du graphe via `StartExecutorId` + `ReflectEdges()`), `WriteWorkflowOutput`, `WriteWorkflowOutputAuthors`, `WriteExecutorFailed`, `WriteWorkflowError`, `WriteRunStatus`, `WriteApprovalRequest` / `Response`, `AgentNameOf` ; classe `PipelineTrace` (messages reçus par executor via `ExecutorInvokedEvent`, texte streamé, usage par agent via `updates.ToAgentResponse().Usage`) | `Program.cs` se concentre sur l'API Workflows ; les attentes du dashboard lisent la sortie des helpers (robuste au retrait de la présentation) |
| `Data/meeting-transcript.txt` | Inchangé | Bonne matière : propriétaires, échéances, participants |
| `Solution/Program.cs` | Réécrit : setup `OpenAIClient` v1 (comme MAS-Lab01) ; **scénario 1** pipeline résumé → actions, `TurnToken(emitEvents: false)`, sortie + erreurs ; **scénario 2** pipeline actions → e-mail → traduction construit deux fois (défaut / `chainOnlyAgentResponses: true`), streaming, contexte reçu et tokens par agent ; **scénario 3** actions → e-mail → expéditeur avec `ApprovalRequiredAIFunction`, pause `RequestInfoEvent` / `PendingRequests`, réponse `SendResponseAsync`, statut final `Idle` | Couvrir la notion de façon progressive (demande de la refonte) avec les seules API stables et documentées |
| `Start/Program.cs` | TODO 1–20 en commentaires (setup 1–5, scénario 1 : 6–10, scénario 2 : 11–15, scénario 3 : 16–20) ; compile sans warning et s'exécute | Convention des labs |
| `README.md` | Réécrit : objectif, « Sequential orchestration in a nutshell » (graphe, executors, `TurnToken`, contexte, sortie, événements, sessions), cas d'usage et contre-indications, tableaux de TODO, « The events of a run », « The context between agents » (mesures réelles), « Sequential workflow or code? » (renvoi à MAS-Lab01), limites, « Try it », concepts, sortie attendue réelle, dépannage, « Going further », encadré « Coming from an older version » | Contenu pédagogique demandé |
| `exercise-fingerprints.json` (nouveau) | Généré par `dotnet run -- exercise-fingerprints` | Le Start livré est vu « non implémenté » par la sélection des scénarios |
| `Dashboard/LabDashboard/labs.json` | Nouveau lab `multiagent-lab02` (après `multiagent-lab01`, piste « Multi-Agent System », Intermédiaire, timeout 300 s, 13 attentes, EN + FR) ; insertion textuelle (aucune ligne existante modifiée) | Intégration au dashboard |
| `Dashboard/LabDashboard.Tests` | `LabCatalogTests` (18 labs) ; `OutputAnalyzerTests` : sortie réelle Solution / Start livré, test négatif (ordre inversé, `chainOnlyAgentResponses` oublié, approbation sans réponse), libellés des rapports de tokens, entrée dans `SolutionOutputs` (test « sans la présentation ») | Non-régression |
| `README.md` racine, `Dashboard/README.md`, `Migration-Manual.md`, `Migration-Plan.md` | MAS-Lab02 migré, scénarios, parcours (étape 4) ; convention `LabPrompts.cs` ; §4.19 et lignes §3/§5 ; statut et pratique 5 bis | Cohérence |

## 2. APIs et dépendances modifiées

| Avant (preview.251204/251219) | Après (1.26.0) |
|---|---|
| `AzureOpenAIClient` + `ChatClient` (Azure.AI.OpenAI) | `OpenAIClient` (endpoint v1, clé API ou `BearerTokenPolicy`) + `ChatClient` |
| `chatClient.CreateAIAgent(name, instructions)` | `chatClient.AsAIAgent(instructions:, name:, tools:)` |
| `AgentWorkflowBuilder.BuildSequential(workflowName:, agents:)` | Inchangé, plus la surcharge `(name, chainOnlyAgentResponses, agents)` |
| `InProcessExecution.StreamAsync(workflow, messages)` | `InProcessExecution.RunStreamingAsync(workflow, messages)`, `await using` |
| `(List<ChatMessage>)outputEvt.Data!` | `WorkflowOutputEvent.As<List<ChatMessage>>()` |
| — | `ExecutorFailedEvent`, `WorkflowErrorEvent`, `ExecutorInvokedEvent`, `AgentResponseUpdateEvent`, `RequestInfoEvent`, `StreamingRun.SendResponseAsync`, `GetStatusAsync` / `RunStatus` |
| — | `ApprovalRequiredAIFunction`, `ToolApprovalRequestContent.CreateResponse`, `ExternalRequest.TryGetDataAs` / `CreateResponse` |

Graphe vérifié (Start et Solution) : MAF 1.26.0 partout, `OpenAI` 2.14.0, `Microsoft.Extensions.AI.Abstractions` 10.11.0 ; transitifs nouveaux apportés par Workflows : `Microsoft.Extensions.AI.Evaluation` 10.10.0, `Microsoft.Extensions.Hosting.Abstractions` 10.0.9 ; pas d'`Azure.AI.OpenAI` ; **aucune vulnérabilité**. Aucune API expérimentale utilisée : aucun `#pragma` hors `OPENAI001` du setup.

## 3. Changements conceptuels

- **Un seul scénario → trois scénarios progressifs** : construire / exécuter / lire la sortie ; observer et choisir le contexte ; garder un humain dans la boucle.
- **Le `TurnToken` expliqué** : les executors d'agent accumulent les messages et n'agissent qu'à sa réception, puis le retransmettent ; `emitEvents: false` (scénarios 1 et 3 : seule la sortie compte) ou `true` (scénario 2 : streaming des agents). L'ancien lab demandait le streaming sans l'utiliser.
- **Les erreurs sont des événements** : un agent en échec ne lève pas d'exception ; l'ancien lab les ignorait (un échec finissait sans rien afficher).
- **Le contexte entre agents** (nouveau) : conversation entière par défaut, messages des autres agents en rôle `user`, ou `chainOnlyAgentResponses` ; mesuré en tokens et en messages reçus.
- **Intervention humaine** (nouveau) : démontrée par la page Learn et le sample `GroupChatToolApproval` ; réutilise `ApprovalRequiredAIFunction` de Lab09.
- **Positionnement** : section « Sequential workflow or code? » qui relie MAS-Lab01 (délégation par le code, agents comme outils) au workflow.

## 4. Éléments supprimés ou remplacés

| Élément | Devenir |
|---|---|
| `Azure.AI.OpenAI`, `AzureOpenAIClient`, `Microsoft.Extensions.Hosting`, `APIKey` dans `appsettings.json` | Remplacés par le socle des labs migrés |
| `InProcessExecution.StreamAsync`, cast de `Data` | `RunStreamingAsync`, `As<T>()` |
| `break` sur la première sortie | Supprimé : la boucle se termine quand l'exécution n'a plus rien à faire (nécessaire au scénario 3) |
| `ConfigureAwait(false)` dans le `await foreach` | Supprimé (console, absent des samples) |
| Instructions écrites dans `Program.cs` | Déplacées dans `LabPrompts.cs` |
| Ancienne section « Expected Output » (inventée) | Remplacée par une sortie réelle |

## 5. Tests effectués et résultats

| # | Vérification (manuel §7) | Résultat |
|---|---|---|
| 1–3 | Restauration, graphe, vulnérabilités (Start + Solution) | ✅ MAF 1.26.0, pas d'`Azure.AI.OpenAI`, 0 vulnérabilité |
| 4 | `dotnet build -warnaserror` Start + Solution | ✅ 0 erreur, 0 warning |
| 5 | Anciens noms (`StreamAsync(`, `CreateAIAgent`, `AzureOpenAIClient`, `Azure.AI.OpenAI`, `Microsoft.Extensions.Hosting`, `outputEvt.Data`) | ✅ 0 dans `Start/`, `Solution/` ; README : encadré « Coming from an older version » seulement |
| 6 | Solution, clé API (CLI) | ✅ 2 exécutions, ~31 s ; attentes du dashboard **13/13** |
| 7 | Solution, Entra ID (`AzureOpenAI__APIKey` vide, `az login`) | ✅ **13/13** |
| 8 | Configuration absente / déploiement inexistant | ✅ *"'AzureOpenAI:Endpoint' is not configured…"* ; déploiement inexistant : `[Error] Executor MeetingSummarizer failed: HTTP 404 (… DeploymentNotFound)` puis `[Error] The workflow failed…`, exit 0 |
| 9 | Start livré | ✅ compile, s'exécute (en-têtes seuls), `--list-scenarios` : 3 scénarios « not implemented » après génération des empreintes |
| 10 | Copie temporaire du Start complétée **d'après les seuls indices du README** | ✅ build sans warning, **13/13** ; copie supprimée |
| 11 | `diff` Start/Solution hors `Program.cs` | ✅ aucune différence |
| 12 | Relecture README ↔ TODO ↔ Solution | ✅ chaque TODO a un indice qui mène au code de la Solution |
| — | `dotnet test` du dashboard | ✅ **666/666** |
| — | Dashboard (API, port 5091, historique dans un dossier temporaire, rapport vers le serveur d'administration désactivé) | ✅ run Solution `passed`, « All 13 checks passed », scénarios 1–3 `passed`, 6 rapports de tokens (scénario 2) ; run Start livré : refusé par le dashboard (`noneImplemented`), comportement voulu de la sélection des scénarios — le Start « échoue » est couvert par le test unitaire (seul `config` passe) |
| — | Liens Learn du README | ✅ 200 (`workflows/overview` remplacé par l'URL canonique `workflows/`) |
| — | Rejet de l'approbation (« Try it ») | ✅ l'outil ne s'exécute pas, l'expéditeur répond que l'e-mail doit d'abord être relu |
| — | Compatibilité 1.27.0 (prototype, mêmes appels) | ✅ séquentiel, `chainOnlyAgentResponses`, approbation : mêmes résultats |

Mesures de contexte (scénario 2, gpt-4o-mini, 3 exécutions) : jetons d'entrée extracteur / rédacteur / traducteur ≈ 576 / 750–766 / 980–1002 (conversation entière) contre 576 / 195–282 / 248–319 (agent précédent seulement).

## 6. Problèmes rencontrés

1. **Entrée ignorée en silence** — Premier prototype : le premier agent répondait « please provide the transcript » (26 tokens d'entrée). D'abord interprété comme une concurrence entre l'entrée et le `TurnToken` (le `Lockstep` et un délai « corrigeaient »), l'analyse des sources a montré la vraie cause : mon helper typait l'entrée en `object`, `RunStreamingAsync<object>` déclarait ce type, refusé par l'executor, et `EnqueueMessageAsync` renvoyait `false` sans erreur. Le modèle documenté fonctionne. Piège documenté dans le dépannage du README et le manuel §4.19.
2. **Approbations parallèles** — Un agent qui demande plusieurs appels approuvables à la fois, avec des réponses envoyées une à une, fait échouer l'executor (*"The agent did not return a function call and result for approval request…"*) : la première réponse relance l'agent et les demandes restantes sont rejetées automatiquement (1.25.0, #9152). Le scénario 3 n'a qu'un appel (`send_email` « exactly once »), comme le sample officiel. Consigné au manuel ; à approfondir si un lab enseigne plusieurs approbations dans un workflow.
3. **Tags de sortie intermédiaire** — `OutputTag.Intermediate` / `WithIntermediateOutputFrom` sont publiés mais les événements de 1.26.0 n'en portent pas (activés derrière un « Futures scope » dans les tests officiels) : non enseignés.
4. **Spinner** — `WithSpinner` n'existe que pour `Task` ; pas de spinner sur `WatchStreamAsync` (il se mêlerait au texte streamé).
5. **Sérialisation de `labs.json`** — Une réécriture par `json.dumps` changeait la mise en forme d'entrées existantes : annulée, insertion textuelle à la place.

## 7. Décisions à valider

- **Version 1.26.0 plutôt que 1.27.0** (dernière publiée) : cohérence du dépôt ; passage en 1.27.0 sans changement de code à la prochaine vague.
- **Refonte du contenu** : trois scénarios et nouveau domaine d'agents (e-mail de suivi, traduction, envoi) au lieu d'un seul pipeline ; l'ancien domaine (transcription de réunion) est conservé.
- **Orchestration dans le périmètre** : le plan d'action V2 classait MAS-Lab02/03 hors périmètre ; la refonte répond à la demande du 2026-11-05. Lab 03 (concurrent) reste non migré.
- **Approbation automatique** par le code dans le scénario 3 (pas de saisie console : le dashboard n'exécute en interactif que les labs déclarés) ; le rejet est proposé en « Try it ».
- **Place dans le parcours** : étape 4, après MAS-Lab01 et avant Lab06 (dashboard et README racine).

## 8. Points pour les prochains labs

- **MAS-Lab03 (concurrent)** : réutiliser `WorkflowConsole` / `PipelineTrace` (adapter `WritePipeline` aux arêtes multiples : fan-out / fan-in), `BuildConcurrent(agents, aggregator)`, les mêmes événements et la même gestion d'erreurs ; vérifier le type d'entrée passé à `RunStreamingAsync`.
- Toujours tester un `switch` d'événements avec `AgentResponseUpdateEvent` **avant** `WorkflowOutputEvent` (le compilateur refuse l'inverse : CS8120).
- Les attentes du dashboard d'un lab de workflow peuvent s'appuyer sur des faits déterministes (chemin du graphe, nombre et auteurs des messages reçus, statut de l'exécution) plutôt que sur le texte des modèles.

## 9. Intégration au dashboard (Lab Bench)

- `multiagent-lab02`, 13 attentes : `config` ; `scenario1-pipeline`, `scenario1-output` ; `scenario2-full-context`, `scenario2-full-output`, `scenario2-chained-context`, `scenario2-chained-output`, `scenario2-translation`, `scenario2-usage` ; `scenario3-pipeline`, `scenario3-paused`, `scenario3-sent`, `scenario3-done`. Elles lisent les lignes des helpers fournis (passent sans la présentation du scénario, test théorique du catalogue) ; seule `scenario2-translation` dépend du texte du modèle (un mot français courant de l'e-mail traduit).
- Aucune modification du code du dashboard.
- Vérification par l'API (`POST /api/labs/multiagent-lab02/runs`, en-tête `X-Lab-Dashboard: 1`) : Solution `passed` 13/13 en 43,6 s, tokens affichés par agent ; aucun artefact dans le dépôt (historique dans le scratchpad, `Dashboard__Reporting__ServerUrl` vide).
