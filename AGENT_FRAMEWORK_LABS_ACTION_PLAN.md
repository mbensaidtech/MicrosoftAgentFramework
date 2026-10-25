# Plan d'action : évolution des labs Microsoft Agent Framework (phase 1, consolidation)

| | |
|---|---|
| **Date** | 2026-10-25 |
| **Point de départ** | [`AGENT_FRAMEWORK_LABS_AUDIT.md`](AGENT_FRAMEWORK_LABS_AUDIT.md), révisé selon deux règles : (1) améliorer l'existant avant de créer ; (2) laisser de côté l'orchestration multi-agent et les workflows |
| **Labs concernés** | Les 13 labs de la piste `LearningLabs/AzureOpenAI` (Lab01 à Lab12, dont Lab06 en serveur et client) |
| **Labs exclus** | Les 3 labs `LearningLabs/MultiAgentSystem`, ainsi que tout sujet d'orchestration, de coordination entre agents ou de workflow MAF (§1.2) |
| **Versions** | Aujourd'hui : 1.22.0. Visée : la dernière version stable au moment de la mise à jour (1.26.0 le 2026-10-25) |
| **Nature du document** | Plan uniquement. **Aucun lab n'a été modifié ni créé.** |

---

## Sommaire

1. [Résumé des décisions et stratégie retenue](#1-résumé-des-décisions-et-stratégie-retenue)
2. [Tableau A : labs existants à modifier](#2-tableau-a--labs-existants-à-modifier)
3. [Tableau B : nouveaux labs à créer](#3-tableau-b--nouveaux-labs-à-créer)
4. [Pourquoi ces sujets ne peuvent pas être intégrés aux labs existants](#4-pourquoi-ces-sujets-ne-peuvent-pas-être-intégrés-aux-labs-existants)
5. [Plan d'exécution par étapes](#5-plan-dexécution-par-étapes)
6. [Références officielles](#6-références-officielles)

---

## 1. Résumé des décisions et stratégie retenue

### 1.1 Stratégie : consolider avant d'étendre

1. **D'abord, ne plus induire en erreur (P0).** On corrige ce qui peut échouer chez l'apprenant ou le mettre sur une fausse piste.
   - Le streaming et le contournement temporaire de Lab06.
   - Le README racine, qui oriente vers des labs non migrés et emploie encore le terme « Threads ».
2. **Ensuite, mettre l'existant à jour et le compléter (P1).**
   - Passer les 13 labs à la version actuelle du framework, en une seule fois.
   - Combler dans les labs existants les manques sur des concepts fondamentaux : API Responses, sécurité, observabilité, évaluation, réduction de l'historique.
3. **Puis enrichir et ne créer que l'indispensable (P2).** On ajoute d'abord des scénarios secondaires aux labs existants. On crée ensuite **3 nouveaux labs seulement**, sur des sujets qu'aucun lab existant ne peut accueillir proprement.
4. **Enfin, les compléments (P3).** Scénarios optionnels et un 4ᵉ nouveau lab.

À priorité égale, **une modification de lab existant passe toujours avant une création**.

### 1.2 Ce qui est dans le périmètre et ce qui ne l'est pas

| Dans le périmètre | Hors périmètre (étude et phase séparées) |
|---|---|
| Les fonctionnalités d'un agent seul : client, exécution, streaming, sortie structurée, outils, MCP, sessions, historique, context providers, RAG, approbations, middleware, observabilité, évaluation, hébergement d'un agent, Agent Harness, Skills | **MultiAgentSystem/Lab01-UseAgentAsTool** (un orchestrateur coordonne des agents spécialisés) |
| L'API `AIAgent.AsAIFunction()` en tant que telle : elle reste enseignée dans Lab10 et Lab06_A2AClient | **MultiAgentSystem/Lab02_OrchestrationSequential** et **Lab03_OrchestrationConcurrent** |
| A2A et MCP comme **protocoles d'exposition et de consommation** d'un agent (Lab06, Lab04, Lab10) | Workflows MAF : executors, edges, événements, human-in-the-loop et checkpoints de workflow, workflows comme agents, sous-workflows, workflows déclaratifs, hébergement de workflows (`AddWorkflow`) |
| | Orchestrations Handoff, Group Chat et Magentic |
| | Coordination d'agents secondaires par un agent principal (*Background agents* du Harness) |

**Pourquoi Lab01-UseAgentAsTool est exclu.**
- Son objectif déclaré est un système multi-agent coordonné par un orchestrateur, avec des agents juges. C'est bien une stratégie de coordination.
- L'API qu'il utilise, `AsAIFunction`, reste couverte dans le périmètre par Lab10 et Lab06_A2AClient (voir Tableau A, P1).

**Point de vigilance sur les labs exclus.**
- Les trois labs MultiAgentSystem enseignent des API **qui n'existent plus** : `CreateAIAgent`, `AgentRunResponse`, `InProcessExecution.StreamAsync`. C'est vérifié dans le code source au commit `67ba500`, voir l'audit §4.
- Ces labs ne sont pas modifiés. Seul le README racine les signalera comme non migrés (P0-4), pour qu'aucun apprenant ne les suive en croyant qu'ils sont à jour.
- **Décision à prendre par l'équipe :** ajouter ou non le même avertissement en tête des trois README MultiAgentSystem. C'est un effort faible, mais cela modifie des labs exclus du périmètre.

### 1.3 Décisions principales

| # | Décision | Ce qui change par rapport à l'audit |
|---|---|---|
| D1 | **Lab11 devient le lab « observabilité »**. Il garde le transport HTTP et ajoute OpenTelemetry. | L'audit prévoyait un nouveau lab Observabilité et faisait de Lab11 un module optionnel. |
| D2 | **Lab08 devient le lab « évaluation »**. Les variantes JSON et CSV restent, comme objet de l'évaluation. | L'audit prévoyait un nouveau lab Évaluation et remplaçait Lab08 par un lab Compaction. |
| D3 | **La réduction de l'historique entre dans Lab05.** Elle remplace le scénario « historique dans un vector store », qui faisait doublon. La compaction est traitée dans le lab Harness. | L'audit prévoyait un nouveau lab « Contexte et coûts ». |
| D4 | **L'API Responses entre dans Lab01 et Lab05.** | Pas de changement. |
| D5 | **La sécurité (*Agent Safety*) est traitée dans les labs concernés** : Lab03, Lab05, Lab07, Lab12. | Pas de changement. |
| D6 | **Le multimodal entre dans Lab02**, sous la forme image puis sortie structurée. | Pas de changement. |
| D7 | **La montée de version 1.22 → 1.26.x se fait en P1, avant les enrichissements**, pour ne réécrire chaque lab qu'une fois. | L'audit la plaçait en P2, après les refontes. |
| D8 | **Seulement 4 nouveaux labs** : Middleware et garde-fous (P2), Héberger un agent dans ASP.NET Core (P2), Agent Harness (P2), Agent Skills (P3). | L'audit en proposait 10, dont 5 sur les workflows ou l'orchestration, aujourd'hui exclus. |

### 1.4 Synthèse par priorité

| Priorité | Labs existants (lots) | Nouveaux labs | Effort cumulé indicatif |
|---|---|---|---|
| **P0** | 3 lots (Lab01, Lab02, Lab06_A2AServer) + 1 action transverse (README racine) | — | ≈ 1 jour |
| **P1** | 9 lots (montée de version + Lab01, Lab03, Lab05, Lab06_A2AClient, Lab07, Lab08, Lab11, Lab12) | — | ≈ 13 à 18 jours |
| **P2** | 8 lots (Lab02, Lab04, Lab05, Lab06_A2AServer, Lab07, Lab09, Lab10 + ordre du parcours) | 3 | ≈ 12 à 16 jours |
| **P3** | 6 lots (Lab01, Lab02, Lab04, Lab09, Lab10, Lab12) | 1 | ≈ 5 à 7 jours |

Échelle d'effort pour un lot ou un lab : **Faible** < ½ jour, **Moyen** ≈ 1 à 2 jours, **Élevé** ≥ 3 jours.
- Ces estimations couvrent le projet Start, le projet Solution, le README et les auto-tests du dashboard.
- Elles s'appuient sur la taille des labs actuels (12 à 27 TODO par projet Start). Ce sont des estimations, pas des mesures.

---

## 2. Tableau A : labs existants à modifier

**Comment lire le tableau.** Chaque ligne est un **lot de modifications**. Un lab peut apparaître dans plusieurs priorités. Les noms de dossier sont exacts. Valeurs de la colonne *État actuel* : **Pertinent**, **Incomplet**, **Partiellement obsolète**, **Fortement obsolète**.

### 2.1 P0 : critique

| Lab existant | État actuel | Modifications nécessaires | Contenu à ajouter | Contenu à corriger ou supprimer | Priorité | Justification | Effort estimé |
|---|---|---|---|---|---|---|---|
| `Lab01-FirstBasicAIAgent` | Pertinent | Relancer le scénario 5 (streaming) avec une clé API puis avec Entra ID. S'il échoue, ajouter une référence explicite à `Microsoft.Extensions.AI.OpenAI` **10.11.0** dans le `.csproj`. | Si la référence est ajoutée, une ligne dans le README qui explique pourquoi. | Rien d'autre. | **P0** | Depuis le 2026-09-28, le streaming Azure OpenAI échouait à cause du bug [dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790). Le bug est fermé depuis le 2026-10-12 et cité dans les notes de version de MEAI 10.11.0, mais MAF 1.26.0 référence encore 10.10.1. L'apprenant risque de voir le scénario échouer. | Faible |
| `Lab02-AIAgentWithSO` | Pertinent | Relancer le scénario 4 (streaming + `ResponseFormat`). Même correction que Lab01 si nécessaire. | — | — | **P0** | Même cause. | Faible |
| `Lab06_A2AServer` | Pertinent | Relancer le serveur **sans** `StreamingWorkaround.WithNonStreamingResponses()` (`Program.cs:54`), avec MEAI.OpenAI 10.11.0 si nécessaire, et vérifier les scénarios de Lab06_A2AClient. | — | Supprimer `StreamingWorkaround.cs` et son usage dans Start et Solution. Mettre à jour le README et `Migration/Migration-Manual.md` §8. | **P0** | Le contournement montre une manipulation temporaire (forcer le non-streaming dans un middleware `IChatClient`) comme si c'était une partie normale de l'hébergement A2A. C'est une pratique à ne pas reproduire une fois le bug corrigé. | Faible |

**Action transverse P0 (hors lab).** Elle figure aussi au §5 (étape 0, P0-4).

| Cible | Modification | Justification | Effort |
|---|---|---|---|
| `README.md` racine, section *Learning Path* et partie 2 *MultiAgentSystem* | 1) Marquer les 3 labs MultiAgentSystem comme « non migrés, API obsolètes, hors maintenance pour cette phase ». 2) Remplacer le libellé « Threads » (ligne 576) par « Sessions ». | Le README racine envoie aujourd'hui l'apprenant vers des labs qui utilisent des API supprimées, et « Threads » désigne un concept renommé (`AgentThread` → `AgentSession`, PR [#3430](https://github.com/microsoft/agent-framework/pull/3430)). | Faible |

### 2.2 P1 : élevée

| Lab existant | État actuel | Modifications nécessaires | Contenu à ajouter | Contenu à corriger ou supprimer | Priorité | Justification | Effort estimé |
|---|---|---|---|---|---|---|---|
| **Les 13 labs AzureOpenAI** | Pertinent (version 1.22.0) | Passer **en une seule vague** à la dernière version stable (1.26.x au 2026-10-25), en suivant le processus de `Migration/Migration-Manual.md` : même version partout, Start = Solution, tests du §7. Points à re-tester :<br>• approbations (Lab09) ;<br>• appels d'outils successifs (Lab03, Lab09) ;<br>• A2A preview3 (Lab06) ;<br>• `OpenAI` 2.14.0 et état de `OPENAI001` (tous). | Un tableau « changements 1.23 → 1.26 » dans le manuel de migration. | Les mentions « 1.22.0 » dans les README et `labs.json`. | **P1** | L'objectif est un parcours aligné sur la version actuelle. Changements notables :<br>• [#8641](https://github.com/microsoft/agent-framework/pull/8641) [BREAKING] *Enforce approval response binding consistently* ;<br>• [#8754](https://github.com/microsoft/agent-framework/pull/8754) [BREAKING] *tool changes between runs* ;<br>• [#8730](https://github.com/microsoft/agent-framework/pull/8730) `OpenAI` 2.14.0 ;<br>• [#9152](https://github.com/microsoft/agent-framework/pull/9152) rejet des approbations sans réponse ;<br>• [#9542](https://github.com/microsoft/agent-framework/pull/9542) rejet des IDs dupliqués ;<br>• [#9386](https://github.com/microsoft/agent-framework/pull/9386) A2A 1.0.0-preview3.<br>[#8200](https://github.com/microsoft/agent-framework/pull/8200) et [#8403](https://github.com/microsoft/agent-framework/pull/8403) ne concernent que les workflows déclaratifs, qui sont hors périmètre. | Moyen |
| `Lab01-FirstBasicAIAgent` | Incomplet | Ajouter un scénario 6 « Le même agent sur l'API Responses », avec un `#pragma warning disable OPENAI001` limité à cette instruction et commenté, comme la règle 13 du manuel l'exige pour Entra ID. | • `client.GetResponsesClient().AsAIAgent(model: …, instructions: …, name: …)`.<br>• Un tableau Chat Completions / Responses : historique local ou côté service, outils hébergés, background responses.<br>• Une explication : la suite du parcours reste sur Chat Completions parce que les sessions, la persistance et les context providers ont besoin d'un historique local.<br>• Un encadré « Foundry » : le Get Started officiel utilise `AIProjectClient.AsAIAgent`, et le reste du code ne change pas grâce à `AIAgent`. | — (scénarios 1 à 5 conservés) | **P1** | Learn : *« Responses is the recommended primary client »*. Responses n'apparaît aujourd'hui qu'en « Going Further ». | Moyen |
| `Lab03-AIAgentWithFunctionTools` | Incomplet | Remplacer le scénario 2 (découverte par réflexion de **toutes** les méthodes publiques, `Program.cs:101`) par le pattern « plugin » : la classe expose elle-même ses outils et elle est résolue depuis le conteneur. | • Une section « Concevoir des outils sûrs » : arguments non fiables, validation, outils qui écrivent → approbation (renvoi vers Lab09).<br>• **Exercice** : `BookMeetingRoom` refuse une date passée ou une salle inconnue et renvoie un message d'erreur exploitable par le modèle.<br>• Un tableau des trois niveaux de middleware (run, function, `IChatClient`) qui annonce le futur lab Middleware.<br>• Le piège `FunctionInvocationContext.Terminate`. | Le scénario « réflexion » : à supprimer, ou à garder en option avec un avertissement (moindre privilège). | **P1** | [Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) : traiter les arguments du modèle comme non fiables. [Add tools](https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools?pivots=programming-language-csharp) : décider avant tout quels outils exigent une confirmation. Pattern officiel : [`Agent_Step12_Plugins`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step12_Plugins). | Moyen |
| `Lab05-AIAgentWithThreads` | Incomplet | Remplacer le scénario 2 (historique stocké dans un vector store, qui fait doublon avec le scénario MongoDB) par un scénario **« borner l'historique »**. Utiliser `#pragma warning disable MEAI001` : `MessageCountingChatReducer` est `[Experimental]`. | • **Scénario de réduction** : `new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions { ChatReducer = new MessageCountingChatReducer(n) })`, puis observer la baisse des tokens et la perte d'un fait ancien (« quel est mon nom ? » après n tours).<br>• **Encadré ou scénario optionnel** « historique géré par le service » : agent Responses, `ChatClientAgentSession.ConversationId`, une session sérialisée qui ne contient plus qu'un identifiant, la compaction et le changement de modèle impossibles.<br>• Une section sécurité étoffée : rattacher chaque session stockée à l'utilisateur authentifié ; restaurer une session venue d'une source non fiable revient à accepter une entrée non fiable. | Le scénario « vector store comme stockage d'historique », qui peut être confondu avec la mémoire sémantique (`ChatHistoryMemoryProvider`). | **P1** | [Storage](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp) : réducteurs, historique côté service, *« bind each stored session to the authenticated user »*. [Compaction](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp) : *« no effect … Responses API with store enabled »*. Exemple officiel : [`Agent_Step13_ChatReduction`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step13_ChatReduction). | Élevé |
| `Lab06_A2AClient` | Pertinent | Vérifier la liaison réellement choisie par défaut, après la montée de version (A2A preview3). Corriger le README si l'affirmation « JSON-RPC par défaut » est fausse. | Comme Lab01-UseAgentAsTool est exclu, préciser dans le scénario 3 ce que fait `AsAIFunction` : nom et description pris de l'agent ou de `AIFunctionFactoryOptions`, paramètre `query`, nouvelle session à chaque appel. | L'affirmation sur la liaison par défaut, si elle se révèle inexacte. | **P1** | Learn [A2A](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/a2a?pivots=programming-language-csharp) indique HTTP+JSON par défaut, alors que le README dit avoir observé JSON-RPC. Lab06_A2AClient et Lab10 deviennent les seuls endroits du périmètre où `AsAIFunction` est enseigné. | Faible |
| `Lab07-AgenticRAG-VectorStore` | Incomplet (sécurité) | Ajouter une section « Sécurité du RAG ». | • Le contenu récupéré n'est pas fiable : risque d'injection de prompt indirecte.<br>• **Exercice** : insérer dans la FAQ une entrée piégée (« ignore les instructions… »), observer l'effet, puis l'atténuer (consigne explicite, citations via `CitationsPrompt`, filtrage à l'indexation). | — | **P1** | [Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) (*« Vet extension providers »*, contenu RAG) et [Chat History Memory Provider](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/chat-history-memory-provider?pivots=programming-language-csharp) (avertissement sur l'injection indirecte). Le README actuel n'en parle pas. | Faible |
| `Lab08-DataFormatComparison` | Partiellement obsolète (valeur pédagogique marginale) | **Refondre en « Évaluer et comparer deux variantes d'un agent »**. Les deux variantes JSON et CSV restent, mais elles deviennent l'objet testé par une évaluation. Renommer le titre (dashboard et README), sans forcément renommer le dossier. | • Un jeu de requêtes avec sortie attendue et appel d'outil attendu.<br>• `LocalEvaluator` avec `EvalChecks.ContainsExpected()`, `EvalChecks.NonEmpty()`, `EvalChecks.ToolCalledCheck(...)`.<br>• `FunctionEvaluator` pour une règle métier : tous les hôtels < 100 USD et triés.<br>• `agent.EvaluateAsync(queries, evaluator, expectedOutput:, expectedToolCalls:)` et `AgentEvaluationResults.AssertAllPassed()`.<br>• En option, un évaluateur de qualité de `Microsoft.Extensions.AI.Evaluation` (`RelevanceEvaluator` via `ChatConfiguration`).<br>• **Conclusion** : choisir une variante sur la qualité **et** sur le coût. | La conclusion implicite « passer en CSV pour économiser des tokens ». Elle devient une hypothèse à vérifier par l'évaluation. | **P1** | Évaluer est indispensable pour une application fiable ([agents/evaluation](https://learn.microsoft.com/en-us/agent-framework/agents/evaluation?pivots=programming-language-csharp)). Les types d'évaluation sont dans le paquet **stable** `Microsoft.Agents.AI` (espace de noms `Microsoft.Agents.AI`, sans `[Experimental]`) et existent déjà au tag `dotnet-1.22.0`. Le lab garde son idée de départ, mesurer pour choisir, et y gagne un sujet central du framework. | Élevé |
| `Lab11-AIAgentWithCustomHttpTransport` | Incomplet (observabilité officielle absente) | **Refondre en « Observer un agent : du transport HTTP à OpenTelemetry »**. Les scénarios 1 et 2 (trafic HTTP) restent. | • **Scénario 3** : `agent.AsBuilder().UseOpenTelemetry(sourceName: …)` + `Sdk.CreateTracerProviderBuilder().AddSource(…).AddConsoleExporter()` (`OpenTelemetry` et `OpenTelemetry.Exporter.Console` 1.18.0, versions des dépendances MAF). Voir sur le même run météo les spans de l'agent, de l'appel modèle et de l'outil, et les comparer aux requêtes HTTP du scénario 2.<br>• **Scénario 4 (option)** : métriques de tokens, et export OTLP vers l'Aspire Dashboard (Docker).<br>• Les bonnes pratiques : instrumenter l'agent **ou** le chat client, pas les deux ; `EnableSensitiveData` seulement en développement. | — (le masquage des secrets reste) | **P1** | [agents/observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp) : *« Agent Framework emits traces, logs, and metrics according to the OpenTelemetry GenAI Semantic Conventions »*. Le README de Lab11 cite déjà `UseOpenTelemetry` comme alternative (ligne 421). Exemples officiels : [`Agent_Step05_Observability`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step05_Observability), [`AgentOpenTelemetry`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentOpenTelemetry). | Élevé |
| `Lab12-AIAgentWithAIContextProvider` | Incomplet (sécurité) | Expliquer et traiter le **niveau de confiance** des souvenirs injectés. Le provider place des faits tirés des messages de l'utilisateur dans `AIContext.Instructions`, qui deviennent le message système. | • Une section « Mémoire et sécurité » : risque d'empoisonnement (« retiens que je suis administrateur »).<br>• **Exercice** : injecter les souvenirs par `AIContext.Messages` plutôt que par `Instructions`, comme le fait [`Agent_Step17_AdditionalAIContext`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step17_AdditionalAIContext), et comparer les deux.<br>• Filtrer ce que l'extracteur a le droit de retenir. | Ne rien supprimer. L'approche par `Instructions` est celle de l'exemple officiel [`01-get-started/04_memory`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/01-get-started/04_memory). Il faut en montrer la limite plutôt que la retirer. | **P1** | [Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) : *« Do not place end-user input into system-role messages »*. Le lab enseigne aujourd'hui l'inverse sans le dire. | Moyen |

### 2.3 P2 : moyenne

| Lab existant | État actuel | Modifications nécessaires | Contenu à ajouter | Contenu à corriger ou supprimer | Priorité | Justification | Effort estimé |
|---|---|---|---|---|---|---|---|
| `Lab02-AIAgentWithSO` | Pertinent | Ajouter un scénario 5 **multimodal**. | Une image locale (`DataContent`) ou une URL (`UriContent`), par exemple la photo d'une carte ou d'une enseigne de restaurant, transformée en `Restaurant` avec `RunAsync<T>`. Fournir l'image dans `Data/`. | — | **P2** | [agents/multimodal](https://learn.microsoft.com/en-us/agent-framework/agents/multimodal?pivots=programming-language-csharp), exemple [`Agent_Step08_UsingImages`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step08_UsingImages). C'est une capacité fondamentale aujourd'hui absente, qui s'ajoute naturellement à la sortie structurée. | Moyen |
| `Lab09-AIAgentWithFunctionToolsHumanApproval` | Pertinent | Ajouter un scénario 3 « Ne plus redemander ». | `agent.AsBuilder().UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = … })`, `request.CreateAlwaysApproveToolResponse()` / `CreateAlwaysApproveToolWithArgumentsResponse()`. La règle permanente est stockée dans la session. | — | **P2** | [tool-approval](https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval?pivots=programming-language-csharp) documente ce pattern d'approbations permanentes. `ToolApprovalAgent` n'est pas `[Experimental]` (code source au commit `67ba500`). Le pattern est aujourd'hui seulement cité en « Going Further ». | Moyen |
| `Lab07-AgenticRAG-VectorStore` | Pertinent | Ajouter un scénario 5 « recherche à la demande avec le provider ». | `TextSearchProviderOptions.SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling` (`FunctionToolName`, `FunctionToolDescription`), comparé à l'outil écrit à la main du scénario 3. | — | **P2** | [agents/rag](https://learn.microsoft.com/en-us/agent-framework/agents/rag?pivots=programming-language-csharp) décrit les deux modes. C'est la version « framework » du RAG agentique. | Faible |
| `Lab04-AIAgentWithMCPClient` | Pertinent | Compléter la section *Security* du README. | Comment demander une approbation avant un outil MCP qui écrit (envelopper le `McpClientTool` dans `ApprovalRequiredAIFunction`, avec renvoi vers Lab09), et que faire si le serveur public Hugging Face est indisponible. | — | **P2** | Learn [tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/?pivots=programming-language-csharp) : l'approbation *« works with providers whose clients invoke tools locally »*. Un outil MCP client est exécuté localement : c'est une déduction de l'auditeur, que la page Learn ne détaille pas. | Faible |
| `Lab05-AIAgentWithThreads` | Incomplet (nom obsolète) | Renommer le dossier en `Lab05-AIAgentWithSessions`. | — | Le nom « Threads » dans le dossier, les liens, `labs.json` et les tests du dashboard. | **P2** | Le concept s'appelle `AgentSession` depuis la PR [#3430](https://github.com/microsoft/agent-framework/pull/3430). Ce n'est pas en P0 parce que le renommage touche le dashboard et l'historique des exécutions. | Faible |
| `Lab06_A2AServer` | Pertinent | Ajouter une variante utilisant le **builder d'hébergement** du framework. | `builder.AddAIAgent("AuthAgent", …)` + `agentBuilder.AddA2AServer()` + `app.MapA2AJsonRpc(agentBuilder, path)`. Cette syntaxe est déjà décrite en « Going Further » et vient de `Microsoft.Agents.AI.Hosting`, que le paquet A2A du lab installe déjà. | — | **P2** | [get-started/hosting](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp), [self-hosting/a2a/dotnet](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/a2a/dotnet?pivots=programming-language-csharp). Prépare le nouveau lab d'hébergement (NL2). | Moyen |
| `Lab10_ExposeAIAgentAsMCPTool` | Pertinent | Mettre le README à jour. | • `AsAIFunction` présenté comme la notion centrale (Lab01-UseAgentAsTool étant exclu).<br>• Un encadré « Évolution prévue » : Learn annonce *« Self-hosting MCP tool support in .NET is coming soon »*.<br>• Un lien vers le futur lab d'hébergement. | — | **P2** | [hosting/self-hosting/mcp](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/mcp?pivots=programming-language-csharp). L'assemblage manuel avec `ModelContextProtocol.AspNetCore` reste correct (exemple [`Agent_Step07_AsMcpTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step07_AsMcpTool)). | Faible |
| **Ordre du parcours** (`README.md` racine, `labs.json`) | — | Recommander un ordre de suivi **sans renuméroter les dossiers** : Lab01 → Lab02 → Lab03 → Lab04 → Lab05 → Lab09 → Lab07 → Lab12 → Lab11 (observabilité) → Lab08 (évaluation) → Lab10 → Lab06 serveur → Lab06 client → nouveaux labs. | Le schéma *Learning Path* mis à jour, et l'emplacement des nouveaux labs. | L'ancien schéma linéaire Lab01 → Lab12. | **P2** | Rapprocher les approbations des sessions dont elles dépendent. Placer A2A après MCP serveur et agent comme outil, comme dans le [Journey](https://learn.microsoft.com/en-us/agent-framework/journey/?pivots=programming-language-csharp) (*Agents as Tools* puis *A2A*). Renuméroter coûterait cher : ids du dashboard, historique, liens. | Faible |

### 2.4 P3 : faible

| Lab existant | État actuel | Modifications nécessaires | Contenu à ajouter | Contenu à corriger ou supprimer | Priorité | Justification | Effort estimé |
|---|---|---|---|---|---|---|---|
| `Lab01-FirstBasicAIAgent` | Pertinent | Ajouter un scénario bonus « réponse en arrière-plan ». | `AgentRunOptions { AllowBackgroundResponses = true }`, `ContinuationToken`, interrogation périodique, sur l'agent Responses du scénario 6. | — | **P3** | [background-responses](https://learn.microsoft.com/en-us/agent-framework/agents/background-responses?pivots=programming-language-csharp) : *« only agents that use the OpenAI Responses API support background responses »*. Utile mais spécialisé. | Moyen |
| `Lab02-AIAgentWithSO` | Pertinent | Ajouter un « Going Further » sur l'agent déclaratif. | Un agent défini en YAML avec `outputSchema` (`ChatClientPromptAgentFactory.CreateFromYamlAsync`, paquet `Microsoft.Agents.AI.Declarative`, **RC** : `1.22.0-rc1`). | — | **P3** | [agents/declarative](https://learn.microsoft.com/en-us/agent-framework/agents/declarative?pivots=programming-language-csharp). Le paquet n'est pas stable. | Faible |
| `Lab04-AIAgentWithMCPClient` | Pertinent | Ajouter un scénario stdio (serveur MCP local). | `StdioClientTransport`, qui ne dépend plus d'un service public. | — | **P3** | [local-mcp-tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/local-mcp-tools?pivots=programming-language-csharp) montre le stdio. Prérequis : Node.js, ou un serveur .NET fourni. | Moyen |
| `Lab09-AIAgentWithFunctionToolsHumanApproval` | Pertinent | Ajouter un scénario « attendre la décision d'une personne ». | Sérialiser la session pendant qu'une approbation est en attente (`SerializeSessionAsync`), la restaurer, puis répondre. | — | **P3** | Exemple officiel [`Agent_Step03_PersistedConversations`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step03_PersistedConversations). Les demandes enregistrées voyagent avec la session. | Moyen |
| `Lab10_ExposeAIAgentAsMCPTool` | Pertinent | Ajouter un exercice « protéger l'endpoint MCP ». | `app.MapMcp(path).RequireAuthorization(...)` avec une politique simple, et le client qui envoie le jeton. | — | **P3** | Le README de Lab10 recommande déjà `RequireAuthorization()` avant toute exposition réseau ; le [README 04-hosting](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/04-hosting/README.md) l'applique. | Moyen |
| `Lab12-AIAgentWithAIContextProvider` | Pertinent | Ajouter une comparaison avec la mémoire intégrée. | Le même besoin traité avec `ChatHistoryMemoryProvider` (recherche sémantique dans l'historique, portées `UserId` et `SessionId`), puis une discussion « faite main ou fournie ». | — | **P3** | [chat-history-memory-provider](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/chat-history-memory-provider?pivots=programming-language-csharp). Demande un modèle d'embeddings, déjà utilisé dans Lab07. | Moyen |

### 2.5 Fiches par lab

Les lots du tableau ci-dessus, rassemblés lab par lab, avec en plus ce qu'il faut conserver.

| Lab | À conserver | Mise à jour de version | Manquant (à ajouter) | Obsolète ou déconseillé | Compétences visées après modification |
|---|---|---|---|---|---|
| `Lab01-FirstBasicAIAgent` | Scénarios 1 à 5, client `OpenAIClient` sur `/openai/v1/` (identique à l'exemple officiel `Agent_With_AzureOpenAIChatCompletion`), avertissement `DefaultAzureCredential` | 1.26.x ; re-tester le streaming | API Responses (P1), encadré Foundry (P1), background responses (P3) | — | Créer un agent, l'exécuter et le streamer ; **choisir entre Chat Completions et Responses** en connaissant les conséquences |
| `Lab02-AIAgentWithSO` | Les 4 scénarios, y compris le scénario 1 montré comme contre-exemple | 1.26.x ; re-tester le scénario 4 | Multimodal (P2), agent déclaratif (P3, en mention) | — | Obtenir une sortie typée, y compris à partir d'une image |
| `Lab03-AIAgentWithFunctionTools` | Scénarios 1, 3 et 4 | 1.26.x ; re-tester avec #8754 | Pattern plugin, outils sûrs, carte des middlewares (P1) | Découverte de toutes les méthodes publiques par réflexion | Concevoir des outils **sûrs**, injecter des services, tracer les appels |
| `Lab04-AIAgentWithMCPClient` | Les 3 scénarios, la section sécurité | 1.26.x (MCP 2.2.0, la version de MAF 1.26.0) | Approbation des outils MCP (P2), stdio (P3) | — | Consommer un serveur MCP en maîtrisant les outils exposés et la confiance |
| `Lab05-AIAgentWithThreads` | Scénarios 1 (en mémoire et sérialisation) et 3 (MongoDB), `StateBag`, `ProviderSessionState<T>` | 1.26.x | Réduction de l'historique, historique côté service, sécurité des sessions (P1) ; renommage (P2) | Scénario « historique dans un vector store » ; libellé « Threads » | Gérer des conversations longues et persistantes **en toute sécurité** |
| `Lab06_A2AServer` | Hébergement A2A v1 avec deux liaisons, cartes d'agent, section sécurité | 1.26.x : A2A 1.0.0-preview3 (préversion signalée) | Variante avec le builder d'hébergement (P2) | Contournement de streaming (P0) | Exposer un agent en A2A avec l'hébergement du framework |
| `Lab06_A2AClient` | Les 3 scénarios | 1.26.x : A2A preview3 | Explication complète d'`AsAIFunction` (P1) | Affirmation sur la liaison par défaut, à vérifier | Découvrir et appeler un agent distant, l'utiliser comme outil |
| `Lab07-AgenticRAG-VectorStore` | Les 4 scénarios et la comparaison outil / provider | 1.26.x | Sécurité du RAG (P1), recherche à la demande (P2) | — | Construire un RAG **résistant à l'injection indirecte** |
| `Lab08-DataFormatComparison` | Les données (50 hôtels) et les deux variantes JSON et CSV | 1.26.x | **Évaluation complète** (P1) | La conclusion implicite « CSV = économie » | **Évaluer un agent** et choisir une variante sur la qualité et le coût |
| `Lab09-AIAgentWithFunctionToolsHumanApproval` | Les 2 scénarios et la boucle d'approbation | 1.26.x : **re-tester** (#8641, #9152, #9542) | Approbations permanentes (P2), approbation en attente persistée (P3) | — | Mettre en place une validation humaine et des règles permanentes |
| `Lab10_ExposeAIAgentAsMCPTool` | Les 3 scénarios (HTTP, stdio, agent consommateur), la section sécurité | 1.26.x | Mise à jour du README (P2), protection de l'endpoint (P3) | — | Exposer un agent comme outil MCP sécurisé |
| `Lab11-AIAgentWithCustomHttpTransport` | Scénarios 1 et 2, le masquage des secrets | 1.26.x | **OpenTelemetry** : traces, métriques, export (P1) | — | **Observer un agent** avec les outils standard, et comprendre ce qui passe sur le réseau |
| `Lab12-AIAgentWithAIContextProvider` | Le provider, `ProviderSessionState<T>`, MongoDB, les 2 scénarios | 1.26.x | Niveau de confiance de la mémoire (P1), `ChatHistoryMemoryProvider` (P3) | — | Écrire un context provider **sans élever des données de l'utilisateur au niveau système** |

---

## 3. Tableau B : nouveaux labs à créer

Les labs ci-dessous ne portent ni sur l'orchestration ni sur les workflows. Chacun couvre un concept important absent du parcours, que l'enrichissement d'un lab existant ne permet pas de traiter correctement (voir §4).

| Nouveau lab proposé | Objectif pédagogique | Concepts couverts | Justification | Prérequis | Priorité | Effort estimé | Sources officielles |
|---|---|---|---|---|---|---|---|
| **NL1 — Middleware et garde-fous d'un agent** | À la fin, le développeur sait **intercepter et contrôler** chaque exécution de l'agent sans toucher à l'agent, et choisir le bon niveau d'interception. | • Middleware d'exécution d'agent (`AsBuilder().Use(runFunc:, runStreamingFunc:)`), en version streaming et non streaming.<br>• Garde-fou d'entrée qui court-circuite en renvoyant une `AgentResponse`.<br>• Réécriture ou masquage de la sortie.<br>• Repli en cas d'exception.<br>• Middleware `IChatClient` (`clientFactory:`).<br>• Contexte d'exécution : `AgentRunOptions.AdditionalProperties`, `AgentSession.StateBag`, `AIAgent.CurrentRunContext`.<br>• Ordre agent puis exécution.<br>• `UseToolApproval` vu comme un middleware intégré. | Le middleware est l'un des cinq *foundational building blocks* (vue d'ensemble) et l'étape 5 du Journey. C'est aussi la voie recommandée en .NET pour les contrôles à l'exécution : la page Agent Hooks dit *« Use agent middleware, tool approval, and agent safety to add runtime controls to .NET agents »*. Aujourd'hui, seul le middleware de fonction est montré (Lab03, scénario 4). | Lab03, Lab05 ; Lab09 utile | **P2** | Moyen | [middleware](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/?pivots=programming-language-csharp), [agent-pipeline](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/agent-pipeline?pivots=programming-language-csharp), [termination](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/termination?pivots=programming-language-csharp), [result-overrides](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/result-overrides?pivots=programming-language-csharp), [exception-handling](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/exception-handling?pivots=programming-language-csharp), [runtime-context](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/runtime-context?pivots=programming-language-csharp), [agent-vs-run-scope](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/agent-vs-run-scope?pivots=programming-language-csharp), [safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) ; exemple [`Agent_Step11_Middleware`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step11_Middleware) |
| **NL2 — Héberger un agent dans une application ASP.NET Core** | À la fin, le développeur sait **exposer un agent comme service web** : injection de dépendances, endpoint, sessions persistées par utilisateur, authentification. | • Enregistrer l'agent dans le conteneur (`Host.CreateApplicationBuilder` + `AddSingleton(agent)` / services à clé, **API stables**).<br>• Endpoint HTTP minimal qui crée, sérialise et restaure les sessions par utilisateur.<br>• `RequireAuthorization`.<br>• **Scénario optionnel en préversion** : `builder.AddAIAgent(...)` (`Microsoft.Agents.AI.Hosting`, préversion), `WithInMemorySessionStore`, `UseClaimsBasedAgentIsolation`, puis `AddOpenAIResponses` / `MapOpenAIResponses` (`Microsoft.Agents.AI.Hosting.OpenAI`, **alpha**).<br>• En « Going Further » : Durable Extension, Foundry Hosted Agents. | C'est l'étape 7 du Get Started. Le parcours ne montre l'hébergement que derrière deux protocoles spécialisés (A2A, MCP), assemblés à la main, et jamais le passage d'une application console à un service. | Lab01, Lab03, Lab05 ; Lab06_A2AServer utile | **P2** | Élevé | [get-started/hosting](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp), [hosting/self-hosting](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/?pivots=programming-language-csharp), [openai-endpoints](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/openai-endpoints?pivots=programming-language-csharp), [session](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp) ; exemples [`Agent_Step06_DependencyInjection`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step06_DependencyInjection), [`04-hosting/README.md`](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/04-hosting/README.md), [`AgentWebChat`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/AgentWebChat) |
| **NL3 — Agent Harness** | À la fin, le développeur sait **construire un agent prêt pour les tâches longues** avec `AsHarnessAgent`, comprendre ce que le harness active pour lui et le configurer. | • `chatClient.AsIChatClient().AsHarnessAgent()` / `new HarnessAgent(chatClient, HarnessAgentOptions)`.<br>• Composants par défaut : liste de tâches, modes plan et exécution, mémoire de fichiers, approbations permanentes, Skills, OpenTelemetry. Désactivation : `Disable*`.<br>• Ajout d'outils et de context providers.<br>• **Compaction** : `MaxContextWindowTokens` + `MaxOutputTokens` (`[Experimental("MAAI001")]`).<br>• Persistance de session.<br>• **Exclu** : *Background agents* (coordination d'agents secondaires). | C'est l'un des trois piliers (*« Agents and building on that: Workflows and Harness Agents »*) et l'étape 6 du Get Started. Le paquet `Microsoft.Agents.AI.Harness` est **stable depuis 1.14.0**. Le harness réutilise ce que le parcours a enseigné (approbations, context providers, réduction, observabilité) : c'est un lab de synthèse naturel. | Lab05, Lab09, Lab11, Lab12 ; NL1 utile | **P2** | Moyen | [concepts/harness](https://learn.microsoft.com/en-us/agent-framework/concepts/harness?pivots=programming-language-csharp), [get-started/harness](https://learn.microsoft.com/en-us/agent-framework/get-started/harness?pivots=programming-language-csharp), [planning-and-todos](https://learn.microsoft.com/en-us/agent-framework/agents/planning-and-todos?pivots=programming-language-csharp), [compaction](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp) ; exemples [`02-agents/Harness`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness) (`Harness_Step01` à `Step05`), [`Agent_Step22_AgentMode`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step22_AgentMode), [`Agent_Step23_TodoList`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step23_TodoList) |
| **NL4 — Agent Skills** | À la fin, le développeur sait **packager un savoir-faire réutilisable** (instructions, ressources, scripts) et le donner à un agent, chargé par divulgation progressive et soumis à approbation. | • Format `SKILL.md` (frontmatter `name`, `description`).<br>• `AgentSkillsProvider` (un `AIContextProvider`), `AgentSkillsProviderBuilder`.<br>• Skills sous forme de fichiers, en code (`AgentInlineSkill`) et sous forme de classe (`AgentClassSkill<T>`).<br>• Outils `load_skill`, `read_skill_resource`, `run_skill_script`.<br>• Approbation par défaut et `ReadOnlyToolsAutoApprovalRule`.<br>• Règle de choix entre skill et outil. | C'est l'étape 4 du Journey (*Adding Skills*), et Skills est activé par défaut dans le harness. Il apporte un concept nouveau, le savoir-faire packagé et chargé à la demande, qui n'est ni un outil ni un context provider classique. | Lab03, Lab09, Lab12 ; NL3 utile | **P3** | Moyen | [agents/skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills?pivots=programming-language-csharp), [journey/adding-skills](https://learn.microsoft.com/en-us/agent-framework/journey/adding-skills?pivots=programming-language-csharp) ; exemples [`02-agents/AgentSkills`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentSkills) (`Step01` à `Step07`) |

**Disponibilité dans la version actuelle des labs.** Tous les exemples cités existent déjà au tag `dotnet-1.22.0`. Les types correspondants sont présents dans le code source de cette version : `Microsoft.Agents.AI/Evaluation`, `Compaction`, `Skills`, `Harness/ToolApproval`, `OpenTelemetryAgent`, `Microsoft.Agents.AI.Harness`.

**Statut des API** (vérifié dans le code source au commit `67ba500`, version 1.26) :
- **Non expérimentales** : `ToolApprovalAgent` et `UseToolApproval`, `AgentSkillsProvider`, `TextSearchProvider`, l'extension `UseOpenTelemetry`, les types d'évaluation.
- **`[Experimental("MAAI001")]`** : `CompactionProvider`, `HarnessAgentOptions.MaxContextWindowTokens` et `MaxOutputTokens`, certains membres d'`OpenTelemetryAgent`.
- **`[Experimental("MEAI001")]`** : `MessageCountingChatReducer`.

Conformément aux règles 11 et 13 du manuel de migration, chaque usage d'une API expérimentale doit être couvert par un `#pragma` ciblé et commenté, jamais par un `NoWarn` global.

---

## 4. Pourquoi ces sujets ne peuvent pas être intégrés aux labs existants

### 4.1 Les quatre nouveaux labs : intégrations envisagées puis écartées

| Nouveau lab | Lab existant envisagé | Raison du rejet |
|---|---|---|
| **NL1 Middleware et garde-fous** | `Lab03-AIAgentWithFunctionTools`, qui montre déjà le middleware de fonction | Lab03 est un lab **débutant** (`labs.json`), déjà chargé (4 scénarios, 25 TODO). Les garde-fous ont besoin de sessions (Lab05), qui viennent après Lab03. Le middleware d'exécution demande deux variantes, streaming et non streaming (*« Ideally both runFunc and runStreamingFunc should be provided »*). Lab03 garde donc l'introduction (scénario 4 et carte des trois niveaux) et renvoie à NL1. |
| | `Lab11-AIAgentWithCustomHttpTransport` | Lab11 devient le lab observabilité (D1). Lui ajouter le middleware en ferait un lab fourre-tout sur le pipeline, avec deux objectifs indépendants : observer et contrôler. |
| | `Lab09-AIAgentWithFunctionToolsHumanApproval` | Lab09 traite un mécanisme précis (l'approbation d'outils). Les garde-fous d'entrée et de sortie, la réécriture et le repli sur erreur n'ont rien à voir avec les outils. |
| **NL2 Héberger un agent** | `Lab06_A2AServer` | Lab06 enseigne **un protocole** (A2A, en préversion). L'hébergement générique (injection de dépendances, endpoint, sessions par utilisateur, authentification) est indépendant du protocole. Le mettre dans Lab06 le rendrait dépendant de paquets en préversion et masquerait la partie stable. Lab06 reçoit seulement la variante avec le builder d'hébergement (P2), qui sert de pont. |
| | `Lab10_ExposeAIAgentAsMCPTool` | Même raison : MCP est un protocole d'outils. Learn annonce un support .NET dédié pour l'hébergement MCP (*« coming soon »*), ce qui rend Lab10 susceptible de changer. |
| | `Lab05-AIAgentWithThreads` | Lab05 traite la persistance des sessions en console. Y ajouter une application web doublerait sa taille, alors qu'il est déjà le plus gros lab (27 TODO). |
| **NL3 Agent Harness** | `Lab09-AIAgentWithFunctionToolsHumanApproval`, qui cite déjà `AsHarnessAgent` | Le harness n'est pas une option d'un agent existant. C'est **une autre façon de construire tout l'agent**, qui active plusieurs composants à la fois : tâches, modes, mémoire de fichiers, approbations, Skills, télémétrie, compaction. Le traiter dans Lab09 noierait le sujet des approbations. |
| | `Lab12-AIAgentWithAIContextProvider` | Les composants du harness sont des context providers, mais le sujet de Lab12 est d'**écrire** son propre provider. Le harness, à l'inverse, en **assemble** plusieurs déjà prêts. |
| **NL4 Agent Skills** | `Lab03-AIAgentWithFunctionTools` | Une skill n'est pas un outil. C'est un paquet de fichiers (`SKILL.md`, ressources, scripts) chargé par divulgation progressive, avec ses propres règles d'approbation. Cela demande de créer des fichiers, des scripts et des règles d'approbation, ce qu'un lab débutant sur les outils ne peut pas absorber. |
| | `Lab12-AIAgentWithAIContextProvider` | `AgentSkillsProvider` est techniquement un context provider. Mais l'objectif de Lab12, une mémoire long terme écrite à la main, n'a rien à voir avec le packaging de savoir-faire. |
| | NL3 Agent Harness | Envisageable : Skills est activé par défaut dans le harness. Mais les skills fonctionnent sur **n'importe quel** `ChatClientAgent` et méritent qu'on montre leurs trois formes (fichier, code, classe). C'est pourquoi NL4 est séparé, en P3. Le scénario Skills de NL3 en reste à la découverte. |

### 4.2 Sujets intégrés à des labs existants plutôt que créés

| Sujet (lacune de l'audit) | Lab qui l'accueille | Pourquoi l'intégration est cohérente |
|---|---|---|
| Observabilité OpenTelemetry (G3) | `Lab11-AIAgentWithCustomHttpTransport` | Le but actuel de Lab11 est de « voir ce que fait un run ». OpenTelemetry montre la même chose au niveau de l'agent, avec les outils standard. Comparer requêtes HTTP et spans sur le même run est très parlant. |
| Évaluation (G7) | `Lab08-DataFormatComparison` | Lab08 compare déjà deux variantes d'un agent par la mesure. L'évaluation rend la comparaison rigoureuse en ajoutant la qualité au coût, et redonne au lab un sujet central du framework. |
| Réduction de l'historique (G10, partie stable) | `Lab05-AIAgentWithThreads` | Le réducteur se règle sur le `ChatHistoryProvider`, qui est déjà le sujet de Lab05. Il remplace un scénario redondant. |
| Compaction (G10, partie expérimentale) | NL3 Agent Harness | Le harness la configure (`MaxContextWindowTokens` + `MaxOutputTokens`). Elle reste expérimentale en C# : un lab dédié serait prématuré. |
| API Responses et historique côté service (G4) | `Lab01-FirstBasicAIAgent`, `Lab05-AIAgentWithThreads` | C'est un choix de client (Lab01) dont la conséquence principale porte sur l'historique (Lab05). |
| Sécurité *Agent Safety* (G8) | Lab03, Lab05, Lab07, Lab12 (en plus des sections existantes de Lab04, Lab06 et Lab10) | Les risques sont propres à chaque sujet : arguments d'outils, sessions restaurées, contenu récupéré, mémoire. Les traiter au moment où l'apprenant écrit le code concerné est plus efficace qu'un lab séparé, d'autant que FIDES est réservé à Python. |
| Multimodal (G13) | `Lab02-AIAgentWithSO` | Image puis données typées : un ajout naturel à la sortie structurée. |
| Approbations permanentes (`UseToolApproval`) | `Lab09-AIAgentWithFunctionToolsHumanApproval` | C'est la suite directe du flux d'approbation. |
| Agent exposé comme outil (`AsAIFunction`) | `Lab10_ExposeAIAgentAsMCPTool`, `Lab06_A2AClient` | L'API est déjà utilisée dans ces deux labs. Seul son usage en orchestration (Lab01-UseAgentAsTool) est exclu. |
| Agents déclaratifs, background responses | Lab02, Lab01 (P3) | Ce sont des variantes de fonctionnalités déjà enseignées. La première est en RC, la seconde ne fonctionne qu'avec Responses. |

### 4.3 Sujets écartés ou reportés

| Sujet | Décision | Raison |
|---|---|---|
| Workflows : executors, edges, événements, HITL et checkpoints de workflow, workflows comme agents, sous-workflows, workflows déclaratifs | **Exclu** de cette phase | Décision de périmètre. À reprendre dans la phase « orchestration et workflows », avec la refonte des 3 labs MultiAgentSystem (voir l'audit, G1, G6, G11). |
| Orchestrations séquentielle, concurrente, Handoff, Group Chat, Magentic ; agent orchestrateur | **Exclu** | Décision de périmètre. |
| *Background agents* (Harness) | **Exclu** | Il s'agit de coordonner des agents secondaires depuis un agent principal. C'est aussi une fonctionnalité expérimentale (*« Background agents are experimental »*). |
| AG-UI, DevUI | **Reporté** | Paquets en préversion. DevUI *« is a sample app… not intended for production use »*. AG-UI demande une interface web. On le reconsidérera après NL2. |
| Durable Extension, Foundry Hosted Agents | **Reporté** (mention dans NL2) | Dépendent d'Azure Functions ou de Foundry. La Durable Extension est en préversion et a été déplacée dans un dépôt séparé. |
| Looping, CodeAct | **Reporté** | Le Looping est expérimental. Le paquet .NET de CodeAct dépend d'un NuGet Hyperlight non publié, et Local CodeAct *« is not a security sandbox »*. |
| Agents personnalisés (`AIAgent`), routage multi-modèles | **Reporté** | Ce sont des points d'extension avancés ; `RoutePersistingRoutingChatClient` est expérimental. |
| Agent Hooks, FIDES, Functional Workflow API, *Controlling tool availability*, *Computer use* | **Hors sujet .NET** | D'après Learn, ces fonctionnalités sont réservées à Python au 2026-10-25. |

---

## 5. Plan d'exécution par étapes

**Critères de fin, communs à chaque lot.** Ils reprennent `Migration/Migration-Manual.md` §5 à §7.
1. Les projets Start et Solution compilent sans avertissement nouveau.
2. La solution s'exécute réellement **avec une clé API et avec Entra ID**.
3. Les auto-tests du dashboard passent (`dotnet test` lancé depuis `Dashboard/`).
4. Le tableau des TODO du README est aligné sur le projet Start.
5. Aucune API expérimentale n'est utilisée sans `#pragma` ciblé et commenté.
6. Les nouvelles pratiques viennent de la documentation ou des exemples officiels, sans abstraction maison (règle 12).
7. Un rapport court est ajouté dans `Migration/`.

| Étape | Priorité | Contenu | Dépend de | Effort indicatif | Livrable / critère de fin |
|---|---|---|---|---|---|
| **0 — Stopper les erreurs** | P0 | P0-1 Lab01 streaming · P0-2 Lab02 streaming · P0-3 retrait du contournement de Lab06_A2AServer · P0-4 README racine (avertissement MultiAgentSystem, « Threads » → « Sessions ») | — | ≈ 1 jour | Streaming validé avec les deux types d'authentification ; `StreamingWorkaround.cs` supprimé ; manuel §8 mis à jour ; README racine corrigé. **Décision d'équipe** sur l'avertissement dans les README MultiAgentSystem. |
| **1 — Remise à niveau de version** | P1 | Les 13 labs passent à la dernière version stable (1.26.x), figée pour toute la phase | 0 | ≈ 2 à 3 jours | Même version dans tous les `.csproj` ; Lab09, Lab03 et Lab06 re-testés en priorité ; tableau « 1.23 → 1.26 » dans le manuel. |
| **2 — Compléter les fondamentaux** | P1 | Lab01 (Responses, Foundry) · Lab03 (plugin, outils sûrs, carte des middlewares) · Lab05 (réduction, historique côté service, sécurité) · Lab07 (sécurité du RAG) · Lab12 (niveau de confiance de la mémoire) · Lab06_A2AClient (liaison par défaut, `AsAIFunction`) | 1 | ≈ 5 à 7 jours | Chaque lab enrichi respecte les critères communs. Les nouveaux TODO sont numérotés dans la continuité. |
| **3 — Refondre deux labs** | P1 | Lab11 → observabilité OpenTelemetry · Lab08 → évaluation | 1 | ≈ 6 à 8 jours | Les titres, `labs.json` et les objectifs du dashboard sont mis à jour. Le lab d'évaluation **échoue volontairement** sur une variante, pour montrer qu'une évaluation sert à trancher. |
| **4 — Enrichissements secondaires** | P2 | Lab02 multimodal · Lab09 approbations permanentes · Lab07 recherche à la demande · Lab04 section sécurité · Lab05 renommage du dossier · Lab06_A2AServer builder d'hébergement · Lab10 README · ordre du parcours (`README.md`, `labs.json`) | 2, 3 | ≈ 4 à 5 jours | Le parcours conseillé est publié ; le renommage de Lab05 est sans régression dans le dashboard. |
| **5 — Nouveaux labs essentiels** | P2 | NL1 Middleware et garde-fous → NL2 Héberger un agent → NL3 Agent Harness (dans cet ordre) | 2 à 4 (NL3 après Lab11 et Lab12 refondus) | ≈ 8 à 11 jours | Chaque lab suit le format Start / Solution / README / dashboard. NL2 sépare clairement la partie stable de la partie en préversion ou alpha. |
| **6 — Compléments** | P3 | NL4 Agent Skills · Lab01 background responses · Lab02 agent déclaratif · Lab04 stdio · Lab09 approbation persistée · Lab10 protection de l'endpoint · Lab12 `ChatHistoryMemoryProvider` | 5 | ≈ 5 à 7 jours | Les scénarios sont **optionnels** (désactivés par défaut dans `scenariosToRun`). |

**Ordre de suivi conseillé à la fin de la phase** (sans renuméroter les dossiers) :

```
Lab01 → Lab02 → Lab03 → Lab04 → Lab05 → Lab09 → NL1 Middleware
      → Lab07 → Lab12 → Lab11 Observabilité → Lab08 Évaluation
      → Lab10 → Lab06 serveur → Lab06 client
      → NL2 Hébergement → NL3 Harness → (NL4 Skills)
```

**Risques et parades**

| Risque | Parade |
|---|---|
| Le framework publie à peu près une version par semaine, et des `[BREAKING]` tombent régulièrement sur les approbations, A2A et MCP. | Figer la version choisie à l'étape 1 pour toute la phase, comme pendant la migration. Ne pas monter de version en cours de route. |
| Les labs utilisent des API expérimentales (réducteur, compaction du harness, Responses `OPENAI001`) qui peuvent changer. | Utiliser un `#pragma` ciblé, un encadré « API expérimentale » dans le README, et un re-test à chaque montée de version. |
| Paquets en préversion ou alpha dans NL2 (Hosting, Hosting.OpenAI) et Lab06 (A2A). | Garder ces parties en scénarios séparés ou optionnels, et signaler la préversion dans le README (pratique déjà en place dans Lab06). |
| Le serveur MCP public de Hugging Face (Lab04) peut devenir indisponible. | Parade documentée en P2, scénario stdio en P3. |
| Les labs MultiAgentSystem restent avec des API obsolètes pendant toute la phase. | Avertissement dans le README racine (P0) ; refonte prévue dans la phase « orchestration et workflows ». |

---

## 6. Références officielles

Toutes les pages Learn ont été consultées le 2026-10-25 avec le pivot C# : ajouter `?pivots=programming-language-csharp`.

### 6.1 Microsoft Learn

- Vue d'ensemble et parcours :
  - [overview](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)
  - [get-started](https://learn.microsoft.com/en-us/agent-framework/get-started/?pivots=programming-language-csharp) (`your-first-agent`, `add-tools`, `multi-turn`, `memory`, `harness`, `hosting`)
  - [journey](https://learn.microsoft.com/en-us/agent-framework/journey/?pivots=programming-language-csharp) (`adding-skills`, `adding-middleware`, `agents-as-tools`)
  - [concepts](https://learn.microsoft.com/en-us/agent-framework/concepts/?pivots=programming-language-csharp)
- Agents :
  - [running-agents](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/running-agents?pivots=programming-language-csharp)
  - [agent-pipeline](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/agent-pipeline?pivots=programming-language-csharp)
  - [session](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp)
  - [context-providers](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/context-providers?pivots=programming-language-csharp)
  - [storage](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)
  - [compaction](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp)
  - [chat-history-memory-provider](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/chat-history-memory-provider?pivots=programming-language-csharp)
  - [safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp)
  - [harness](https://learn.microsoft.com/en-us/agent-framework/concepts/harness?pivots=programming-language-csharp)
- Middleware :
  - [middleware](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/?pivots=programming-language-csharp)
  - `defining-middleware`, `chat-middleware`, `agent-vs-run-scope`, `termination`, `result-overrides`, `exception-handling`, `shared-state`, `runtime-context`
- Capacités :
  - [structured-outputs](https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs?pivots=programming-language-csharp)
  - [multimodal](https://learn.microsoft.com/en-us/agent-framework/agents/multimodal?pivots=programming-language-csharp)
  - [background-responses](https://learn.microsoft.com/en-us/agent-framework/agents/background-responses?pivots=programming-language-csharp)
  - [rag](https://learn.microsoft.com/en-us/agent-framework/agents/rag?pivots=programming-language-csharp)
  - [declarative](https://learn.microsoft.com/en-us/agent-framework/agents/declarative?pivots=programming-language-csharp)
  - [observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp)
  - [evaluation](https://learn.microsoft.com/en-us/agent-framework/agents/evaluation?pivots=programming-language-csharp)
  - [skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills?pivots=programming-language-csharp)
  - [planning-and-todos](https://learn.microsoft.com/en-us/agent-framework/agents/planning-and-todos?pivots=programming-language-csharp)
  - [agent-hooks](https://learn.microsoft.com/en-us/agent-framework/agents/agent-hooks?pivots=programming-language-csharp) (réservé à Python)
- Outils :
  - [tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/?pivots=programming-language-csharp)
  - [function-tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp)
  - [tool-approval](https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval?pivots=programming-language-csharp)
  - [local-mcp-tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/local-mcp-tools?pivots=programming-language-csharp)
- Fournisseurs et intégrations :
  - [model-providers](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/?pivots=programming-language-csharp)
  - [azure-openai](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/azure-openai?pivots=programming-language-csharp)
  - [microsoft-foundry](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/microsoft-foundry?pivots=programming-language-csharp)
  - [vector-stores](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/vector-stores/?pivots=programming-language-csharp)
  - [a2a](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/a2a?pivots=programming-language-csharp)
  - [devui](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/ui/devui/?pivots=programming-language-csharp)
- Hébergement :
  - [hosting](https://learn.microsoft.com/en-us/agent-framework/hosting/?pivots=programming-language-csharp)
  - [self-hosting](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/?pivots=programming-language-csharp)
  - [openai-endpoints](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/openai-endpoints?pivots=programming-language-csharp)
  - [self-hosting/a2a/dotnet](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/a2a/dotnet?pivots=programming-language-csharp)
  - [self-hosting/mcp](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/mcp?pivots=programming-language-csharp)
  - [durable-extension](https://learn.microsoft.com/en-us/agent-framework/hosting/durable-extension?pivots=programming-language-csharp)
- Migration : [A2A SDK v1](https://learn.microsoft.com/en-us/agent-framework/migration-guide/agent-to-agent-sdk-v1?pivots=programming-language-csharp)

### 6.2 GitHub, `microsoft/agent-framework`

- Notes de version `dotnet-1.23.0` à `dotnet-1.26.0` : https://github.com/microsoft/agent-framework/releases
- Dépendances 1.26.0 (`Microsoft.Extensions.AI.OpenAI` 10.10.1, `OpenTelemetry` 1.18.0, `A2A` 1.0.0-preview3) : https://github.com/microsoft/agent-framework/blob/dotnet-1.26.0/dotnet/Directory.Packages.props
- Exemples .NET cités (tous présents aux tags `dotnet-1.22.0` et `main`) :
  - Get started : [`01-get-started/04_memory`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/01-get-started/04_memory)
  - Agents :
    - [`Agent_Step03_PersistedConversations`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step03_PersistedConversations)
    - [`Agent_Step05_Observability`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step05_Observability)
    - [`Agent_Step06_DependencyInjection`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step06_DependencyInjection)
    - [`Agent_Step07_AsMcpTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step07_AsMcpTool)
    - [`Agent_Step08_UsingImages`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step08_UsingImages)
    - [`Agent_Step11_Middleware`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step11_Middleware)
    - [`Agent_Step12_Plugins`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step12_Plugins)
    - [`Agent_Step13_ChatReduction`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step13_ChatReduction)
    - [`Agent_Step14_BackgroundResponses`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step14_BackgroundResponses)
    - [`Agent_Step17_AdditionalAIContext`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step17_AdditionalAIContext)
    - [`Agent_Step22_AgentMode`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step22_AgentMode)
    - [`Agent_Step23_TodoList`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step23_TodoList)
  - Fournisseurs : [`AgentProviders/azure/Agent_With_AzureOpenAIResponses`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentProviders/azure/Agent_With_AzureOpenAIResponses)
  - Autres dossiers :
    - [`02-agents/AgentOpenTelemetry`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentOpenTelemetry)
    - [`02-agents/Evaluation`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Evaluation)
    - [`02-agents/Harness`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness)
    - [`02-agents/AgentSkills`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentSkills)
  - Hébergement : [`04-hosting/README.md`](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/04-hosting/README.md), [`05-end-to-end/AgentWebChat`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/AgentWebChat)
- Code source (commit `67ba500`, 2026-10-24) :
  - `dotnet/src/Microsoft.Agents.AI/Evaluation/` : `LocalEvaluator`, `EvalChecks`, `AgentEvaluationExtensions.EvaluateAsync`, sans `[Experimental]`
  - `dotnet/src/Microsoft.Agents.AI/Compaction/CompactionProvider.cs` : `[Experimental("MAAI001")]`
  - `dotnet/src/Microsoft.Agents.AI.Harness/HarnessAgentOptions.cs` : `MaxContextWindowTokens` et `MaxOutputTokens` en `[Experimental("MAAI001")]`
  - `dotnet/src/Microsoft.Agents.AI/Harness/ToolApproval/ToolApprovalAgent.cs`, sans `[Experimental]`
- PR citées :
  - [#3430](https://github.com/microsoft/agent-framework/pull/3430) `AgentThread` → `AgentSession`
  - [#8200](https://github.com/microsoft/agent-framework/pull/8200) workflows déclaratifs, hors périmètre
  - [#8403](https://github.com/microsoft/agent-framework/pull/8403) workflows déclaratifs, hors périmètre
  - [#8641](https://github.com/microsoft/agent-framework/pull/8641) liaison des approbations
  - [#8730](https://github.com/microsoft/agent-framework/pull/8730) `OpenAI` 2.14.0
  - [#8754](https://github.com/microsoft/agent-framework/pull/8754) changements d'outils entre exécutions
  - [#9152](https://github.com/microsoft/agent-framework/pull/9152) approbations sans réponse
  - [#9386](https://github.com/microsoft/agent-framework/pull/9386) A2A preview3
  - [#9542](https://github.com/microsoft/agent-framework/pull/9542) IDs d'approbation dupliqués

### 6.3 Autres sources

- Bug du streaming : [dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790), fermé le 2026-10-12, cité dans la release `v10.11.0` de [dotnet/extensions](https://github.com/dotnet/extensions/releases).
- `MessageCountingChatReducer` `[Experimental]` (identifiant `MEAI001`) : [code source dotnet/extensions](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI/ChatReduction/MessageCountingChatReducer.cs).
- NuGet, versions au 2026-10-25 :
  - `Microsoft.Agents.AI`, `.OpenAI`, `.Harness` : 1.26.0, stables ;
  - `Microsoft.Agents.AI.Hosting` : `1.22.0-preview.260918.1` / `1.26.0-preview.261023.1` ;
  - `Microsoft.Agents.AI.Hosting.OpenAI` : `1.22.0-alpha.260918.1` / `1.26.0-alpha.261023.1` ;
  - `Microsoft.Agents.AI.Declarative` : `1.22.0-rc1` / `1.26.0-rc1` ;
  - `Microsoft.Extensions.AI.OpenAI` : 10.11.0.

### 6.4 Documents du projet

- [`AGENT_FRAMEWORK_LABS_AUDIT.md`](AGENT_FRAMEWORK_LABS_AUDIT.md) : audit de départ
- `Migration/Migration-Manual.md` (règles 1, 11, 12, 13 ; §7 stratégie de test ; §8 risques) et `Migration/Migration-Plan.md`
- `Dashboard/LabDashboard/labs.json` (niveaux et objectifs des labs), `README.md` racine
- `LearningLabs/AzureOpenAI/*/README.md` et `*/Solution/*.cs`
