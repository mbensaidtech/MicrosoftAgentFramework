# Rapport — nouveaux labs Évaluation (Lab14) et Agent Skills (Lab15)

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.26.0** · **.NET 10** (`net10.0`)
> Références : spec [`specs/labs-evaluation-and-skills/spec.md`](../specs/labs-evaluation-and-skills/spec.md) (T1 à T11, sans Jira), audit [`AGENT_FRAMEWORK_LABS_AUDIT.md`](../AGENT_FRAMEWORK_LABS_AUDIT.md) (manques G7 et G12), plan d'action v2 §6, [Migration-Manual.md](Migration-Manual.md) (règles §6, tests §7)

**Statut : fait.** Deux nouveaux labs Azure OpenAI, en API **stables** uniquement : **Lab14 « Evaluate an Agent »** et **Lab15 « Give an Agent Skills »**. L'**Agent Harness** est reporté : ses fonctions les plus utiles sont expérimentales en 1.26.0.

---

## 1. Statut des API, vérifié par compilation (1.26.0)

| Sujet | Stable | Expérimental (`MAAI001`) |
|---|---|---|
| Évaluation | `AIAgent.EvaluateAsync` (questions ou réponses existantes), `LocalEvaluator`, `EvalChecks`, `FunctionEvaluator`, `ExpectedToolCall`, `AgentEvaluationResults` (cœur `Microsoft.Agents.AI`) ; évaluateurs LLM `Microsoft.Extensions.AI.Evaluation.Quality` 10.11.0 | — (`FoundryEvals` : paquet `Microsoft.Agents.AI.Foundry` en préversion, non utilisé) |
| Agent Skills | `AgentSkillsProvider`, skills en fichiers, `AgentInlineSkill` (`AddResource`, `AddScript`), `AgentFileSkillsSource`, `UseToolApproval`, `ReadOnlyToolsAutoApprovalRule` / `AllToolsAutoApprovalRule` | Skills via MCP (`UseMcpSkills`, d'après la documentation) |
| Agent Harness (reporté) | Paquet `Microsoft.Agents.AI.Harness` 1.26.0, `AsHarnessAgent()` | `MaxContextWindowTokens`, `MaxOutputTokens` (compaction), `FileSystemAgentFileStore` / `FileMemoryStore`, `LoopAgentOptions`, `TodoCompletionLoopEvaluator` |

## 2. Changements réalisés

| Tâche | Lab | Contenu |
|---|---|---|
| T1–T5 | **`Lab14-AIAgentEvaluation`** (projet `AIAgentEvaluation`) | Agent de support e-commerce avec l'outil `get_order_status` (données que le modèle ne peut pas deviner : `TRK-48213`, `ParcelExpress`, `LCK-305`) et un jeu de 4 questions (`EvaluationDataset.cs`). **Scénario 1** : `EvaluateAsync` + `LocalEvaluator` (`NonEmpty`, `ContainsExpected`, `ToolCallArgsMatch`, contrôle personnalisé `is_concise`), résultat par question et par contrôle. **Scénario 2** : les mêmes réponses (évaluation de réponses existantes) notées par `RelevanceEvaluator` et `CoherenceEvaluator` (`ChatConfiguration`, juge = même déploiement, simplification expliquée). **Scénario 3** : instructions de référence contre une version candidate (exigence de sécurité « demander l'e-mail avant de consulter la commande ») ; comparaison avec les contrôles en échec, verdict de régression décidé par le code, constance avec `numRepetitions: 3`. Fichiers fournis : `Tools/OrderTools.cs`, `EvaluationDataset.cs`, `EvaluationConsole.cs`. TODO 1–14, README complet |
| T6–T10 | **`Lab15-AIAgentWithSkills`** (projet `AIAgentWithSkills`) | **Scénario 1** : deux skills `SKILL.md` (`expense-policy` avec la ressource `references/limits.md`, `it-support`), skills annoncées (`AgentFileSkillsSource`), `load_skill` puis `read_skill_resource` pour la question de dépenses (réponse 145 EUR, valeur présente seulement dans la ressource), aucun outil de skill pour une question hors sujet. **Scénario 2** : skill définie en code (`AgentInlineSkill`) avec ressource statique, ressource dynamique (date de validité) et script exécuté dans le programme (identifiant de devis). **Scénario 3** : sans approbation automatique, le premier appel attend une approbation ; avec `ReadOnlyToolsAutoApprovalRule`, seul `run_skill_script` est soumis à la politique `ScriptApprovalPolicy` (2,5 kg approuvé, 120 kg rejeté). Les appels exécutés sont tracés par un middleware d'appel de fonction (Lab03). Fichiers fournis : `skills/`, `ShippingRates.cs`, `ScriptApprovalPolicy.cs`, `SkillsConsole.cs`. TODO 1–14, README complet (format `SKILL.md`, skill/outil/workflow, sécurité) |
| T5, T10 | `Dashboard/LabDashboard/labs.json`, tests | `azureopenai-lab14` (10 attentes) et `azureopenai-lab15` (10 attentes), EN/FR ; sorties réelles et tests négatifs ; catalogue dans l'ordre du parcours |
| T11 | `README.md` racine, `Migration-Plan.md`, plan d'action v2 | Vue d'ensemble, parcours (Lab15 après Lab12, Lab14 après Lab11), nombre de labs, limites du dashboard |

## 3. Écarts et décisions

- **Régression du scénario 3 de Lab14** : la première version candidate (« réponses plus courtes, sans numéros de suivi ») ne cassait pas toujours les mêmes questions (le modèle donnait quand même le transporteur quand la question le demandait : 3 puis 1 échec). Elle a été remplacée par une exigence de sécurité plausible qui empêche la consultation de la commande : 3 échecs identiques sur chaque exécution (règle 5 de la spec).
- **Traçage des appels de skills (Lab15)** : le middleware d'approbation relance l'agent après chaque approbation automatique, et la réponse finale ne garde que les messages de la dernière relance (`load_skill` n'y figurait pas). Les appels sont donc tracés par un middleware d'appel de fonction, qui voit chaque outil réellement exécuté ; un compteur indique le nombre d'outils de skill par question.
- **Scripts de skills en fichiers** exclus (exécuteur de sous-processus présenté par la documentation comme *« for demonstration purposes only »*, Python requis) : le script est montré avec une skill définie en code.
- **Juge du scénario 2 de Lab14** : même déploiement que l'agent (décision du 2026-10-25), simplification expliquée dans le README.
- **Motif du dashboard corrigé** : une première version du motif `scenario2-script` de Lab15 contenait une répétition pouvant correspondre à une chaîne vide (retour arrière catastrophique) ; corrigée avant l'enregistrement.

## 4. Tests effectués et résultats

| # | Vérification | Résultat |
|---|---|---|
| 4 | Build Start + Solution des deux labs | ✅ 0 erreur, **0 warning** |
| 6–7 | Lab14 Solution contre les attentes du dashboard | ✅ **10/10 sur 4 exécutions** (clé API ×3, Entra ID ×1) |
| 6–7 | Lab15 Solution | ✅ **10/10 sur 4 exécutions** (clé API ×3, Entra ID ×1) |
| — | Stabilité de la régression (Lab14, scénario 3) | ✅ 3 questions sur 4 en échec avec la version candidate, sur toutes les exécutions (dont 3 exécutions dédiées du scénario) ; Q1 répétée : 3/3 |
| 9 | Start livrés | ✅ exit 0, tous les en-têtes, seul `config` passe |
| 11 | Start = Solution hors `Program.cs` | ✅ tous les fichiers fournis identiques (dont `skills/`) ; TODO 1–14 du Start = tableaux du README |
| — | Points de dépannage | ✅ `ArgumentException: Found 2 queries but 1 expectedOutput values. Counts must match.` vérifié hors ligne |
| — | `dotnet test` du dashboard | ✅ **218/218** |
| 10 | Start complété en suivant les indices | ⚠️ Non rejoué |

## 5. Suite

- **Agent Harness** : à reprendre quand la compaction, la mémoire de fichiers et la boucle autonome seront stables.
- **Workflows et orchestration** (MultiAgentSystem Lab02 et Lab03) : phase suivante ; l'évaluation de workflows (`run.EvaluateAsync`) s'y rattachera.
