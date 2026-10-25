# Plan d'action essentiel — Labs Microsoft Agent Framework (v2)

| | |
|---|---|
| **Date** | 2026-10-25 (révision : intégration de la communication entre agents, §4) |
| **Version de référence** | Microsoft Agent Framework .NET **1.26.0** — dernière version stable (2026-10-23) |
| **Objectif** | Couvrir les concepts **indispensables** pour débuter et construire une **solution agentique complète**, y compris un agent qui **fait appel à d'autres agents** — sans viser l'exhaustivité |
| **Hors périmètre** | Orchestration multi-agent et workflows MAF (dont `MultiAgentSystem/Lab02_OrchestrationSequential` et `Lab03_OrchestrationConcurrent`). `MultiAgentSystem/Lab01-UseAgentAsTool` **réintègre le périmètre** (§4) : il relève de la délégation entre agents, pas de l'orchestration |
| **Statut** | Plan uniquement — aucun lab n'est modifié ni créé. Remplace pour l'exécution [`AGENT_FRAMEWORK_LABS_ACTION_PLAN.md`](AGENT_FRAMEWORK_LABS_ACTION_PLAN.md) (v1), conservé pour référence |

---

## 1. Ce qui est essentiel

Une solution agentique complète doit savoir : **(1)** raisonner sur un modèle et produire du texte ou des données typées ; **(2)** appeler des outils (locaux et MCP) ; **(3)** tenir et persister une conversation ; **(4)** s'appuyer sur des connaissances (RAG) et une mémoire ; **(5)** être **contrôlée** (approbation humaine, garde-fous) ; **(6)** **confier une tâche à un autre agent** et exploiter sa réponse, dans le même processus ou à distance (A2A) ; **(7)** être **observable** ; **(8)** être **exposée à des utilisateurs**.

Ce périmètre correspond aux *foundational building blocks* (*« model clients (chat completions and responses), an agent session …, context providers …, middleware …, and MCP clients »* — [overview](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)), au *Get Started* hors workflows ([get-started](https://learn.microsoft.com/en-us/agent-framework/get-started/?pivots=programming-language-csharp)) et aux étapes *Agents as Tools* et *Agent-to-Agent* du [Journey](https://learn.microsoft.com/en-us/agent-framework/journey/?pivots=programming-language-csharp), qui précèdent l'étape *Workflows* (exclue).

**Résultat : 16 actions sur les labs existants, 1 seul nouveau lab** (§5). La communication entre agents est couverte **sans nouveau lab**, en refondant et en enrichissant les trois labs existants qui la traitent déjà (§4).

---

## 2. Stable, expérimental ou en préversion

Le plan s'appuie sur des API **stables** de la 1.26.0, avec quatre exceptions explicites. Statut vérifié dans le code source (tags `dotnet-1.22.0` / `dotnet-1.26.0`, commit `67ba500`), dans [`openai-dotnet` @ OpenAI_2.14.0](https://github.com/openai/openai-dotnet/tree/OpenAI_2.14.0) et sur NuGet.

| Élément | Statut | Usage dans le plan |
|---|---|---|
| `Microsoft.Agents.AI`, `.Abstractions`, `.OpenAI` 1.26.0 — `AsAIAgent`, `RunAsync`/`RunStreamingAsync`/`RunAsync<T>`, `AgentSession`, `ChatHistoryProvider`, `AIContextProvider`, `TextSearchProvider`, `ApprovalRequiredAIFunction`, **`AIAgent.AsAIFunction()`** | **Stable** | Tous les labs, dont la délégation entre agents |
| Middleware `AIAgentBuilder.Use(runFunc:, runStreamingFunc:)`, middleware de fonction ; `UseOpenTelemetry` | **Stable** | Lab09, Lab03, Lab11 |
| `ModelContextProtocol` 2.2.0 ; `Microsoft.Extensions.AI.OpenAI` 10.11.0 | **Stable** | Lab04, Lab10 ; correctif streaming |
| SDK `OpenAI` 2.14.0 : `GetResponsesClient()`, `OpenAIClient(AuthenticationPolicy, …)` | **Expérimental** `OPENAI001` | Lab01 — `#pragma` ciblé et commenté (règle 13 du manuel de migration) |
| **A2A** : `Microsoft.Agents.AI.A2A`, `.Hosting.A2A.AspNetCore` (1.26.0-preview), SDK `A2A` 1.0.0-preview3 ; `InMemoryAgentSessionStore` (`Microsoft.Agents.AI.Hosting`, préversion) | **Préversion** — **aucune version stable n'existe** ; Learn installe avec `--prerelease` | Lab06 serveur/client — préversion **signalée** : c'est le seul protocole agent-à-agent officiel du framework |
| `MessageCountingChatReducer` (`MEAI001`), compaction (`MAAI001`), `BackgroundAgentsProvider` (*« Background agents are experimental »*) | **Expérimental** | Non enseignés (au plus cités) |
| Bibliothèque d'hébergement MAF : `Microsoft.Agents.AI.Hosting` (préversion), `.Hosting.OpenAI` (alpha) | **Préversion / alpha** | Citée seulement (NL1) |

---

## 3. Mises à jour indispensables des labs existants

Effort : **Faible** < ½ j · **Moyen** ≈ 1–2 j · **Élevé** ≥ 3 j (Start + Solution + README + tests du dashboard).

| # | Prio. | Lab | Action | Pourquoi c'est indispensable | Effort |
|---|---|---|---|---|---|
| A1 | **P0** | `Lab01-FirstBasicAIAgent`, `Lab02-AIAgentWithSO` | Re-tester les scénarios de streaming (Lab01 n° 5, Lab02 n° 4), clé API **et** Entra ID ; en cas d'échec, ajouter une référence explicite à `Microsoft.Extensions.AI.OpenAI` **10.11.0** | Le streaming est une fonction de base ; échec possible chez l'apprenant depuis le 2026-09-28 ([dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790), corrigé en 10.11.0 ; MAF 1.26.0 référence encore 10.10.1) | Faible |
| A2 | **P0** | `Lab06_A2AServer` | Supprimer `StreamingWorkaround.cs` et son usage (`Program.cs:54`) après le re-test A1 ; mettre à jour README et `Migration/Migration-Manual.md` §8 | Le lab présente un contournement temporaire comme une étape normale de l'hébergement A2A | Faible |
| A3 | **P0** | `README.md` racine | Signaler les labs MultiAgentSystem comme **non migrés** (API supprimées) jusqu'à la refonte de Lab01-UseAgentAsTool (A13) ; marquer Lab02/Lab03 MultiAgentSystem **hors parcours** ; remplacer « Threads » par « Sessions » (ligne 576) | Le parcours renvoie aujourd'hui vers `CreateAIAgent`, `AgentRunResponse`, `InProcessExecution.StreamAsync`, qui n'existent plus, et vers un concept renommé (`AgentThread` → `AgentSession`) | Faible |
| A4 | **P1** | **Les 13 labs AzureOpenAI** | Passer de 1.22.0 à **1.26.0** en une vague, version figée. Re-tests prioritaires : Lab09 ([#8641](https://github.com/microsoft/agent-framework/pull/8641), [#9152](https://github.com/microsoft/agent-framework/pull/9152), [#9542](https://github.com/microsoft/agent-framework/pull/9542)), Lab03 ([#8754](https://github.com/microsoft/agent-framework/pull/8754)), Lab06 (A2A preview3, [#9386](https://github.com/microsoft/agent-framework/pull/9386)), dont la liaison par défaut annoncée dans le README de Lab06_A2AClient | Le parcours doit reposer sur la dernière version stable ; le faire **avant** les enrichissements évite de réécrire deux fois | Moyen |
| A5 | **P1** | `Lab01-FirstBasicAIAgent` | Scénario « le même agent sur l'API Responses » (`client.GetResponsesClient().AsAIAgent(model: …)`), tableau de choix Chat Completions / Responses, explication du choix Chat Completions pour la suite ; une ligne sur Foundry (`AIProjectClient.AsAIAgent`) | *« Responses is the recommended primary client »* ([azure-openai](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/azure-openai?pivots=programming-language-csharp)) ; les deux clients de modèle font partie des fondamentaux | Moyen |
| A6 | **P1** | `Lab03-AIAgentWithFunctionTools` | Remplacer le scénario 2 (exposition par réflexion de **toutes** les méthodes publiques, `Program.cs:101`) par des outils déclarés explicitement ([`Agent_Step12_Plugins`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step12_Plugins)) ; exercice de validation des arguments | Moindre privilège et arguments du modèle non fiables ([Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp), [add-tools](https://learn.microsoft.com/en-us/agent-framework/get-started/add-tools?pivots=programming-language-csharp)) | Moyen |
| A7 | **P1** | `Lab09-AIAgentWithFunctionToolsHumanApproval` | Étendre en **« Contrôler un agent : approbation humaine et garde-fous »** : garder les 2 scénarios ; ajouter un **middleware d'exécution** (`AsBuilder().Use(runFunc:, runStreamingFunc:)`) — garde-fou d'entrée (court-circuit par une `AgentResponse`) et de sortie (masquage de données personnelles) ; titre mis à jour dans `labs.json` | Le middleware est un *foundational building block* et le moyen .NET recommandé pour les contrôles d'exécution ([middleware](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/?pivots=programming-language-csharp), [termination](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/termination?pivots=programming-language-csharp), [agent-hooks](https://learn.microsoft.com/en-us/agent-framework/agents/agent-hooks?pivots=programming-language-csharp)) | Moyen |
| A8 | **P1** | `Lab05-AIAgentWithThreads` | Remplacer le scénario 2 (historique dans un vector store, redondant) par **« borner l'historique »** — `ChatHistoryProvider` ne renvoyant que les N derniers messages sans couper un appel d'outil et son résultat (API stables ; citer le réducteur intégré, expérimental) ; encadré « historique géré par le service » ; rattacher chaque session stockée à l'utilisateur | Conversations longues = coût et dépassement de contexte ; *« bind each stored session to the authenticated user »* ([storage](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)) | Élevé |
| A9 | **P1** | `Lab12-AIAgentWithAIContextProvider` | Injecter les souvenirs via `AIContext.Messages` plutôt que `Instructions` ([`Agent_Step17_AdditionalAIContext`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step17_AdditionalAIContext)) et expliquer le risque | *« Do not place end-user input into system-role messages »* ([Agent Safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp)) | Faible |
| A10 | **P1** | `Lab07-AgenticRAG-VectorStore` | Section + exercice **injection indirecte** (entrée de FAQ piégée, atténuation) | Le contenu récupéré n'est pas fiable ; absent du README actuel | Faible |
| **A13** | **P1** | `MultiAgentSystem/Lab01-UseAgentAsTool` | **Refondre en « Confier une tâche à un autre agent »** (même dossier, API stables uniquement) — détail en §4.4 | Seul lab qui enseigne la délégation entre agents dans un même processus — étape *Agents as Tools* du Journey — mais il utilise des API **supprimées** (`CreateAIAgent`, `AgentRunResponse<T>`, `AzureOpenAIClient`), une fabrique maison qui masque l'API et des « juges » appelés comme outils par l'agent qu'ils évaluent | Élevé |
| **A14** | **P1** | `Lab06_A2AServer` | Enregistrer un **magasin de sessions** par agent (`builder.Services.AddKeyedSingleton<AgentSessionStore>("AuthAgent", new InMemoryAgentSessionStore())`, idem pour `CustomerToneAgent`) ; README : comportement **par défaut vérifié** (aucune session conservée), magasin en mémoire = développement uniquement, `contextId` ≠ preuve d'identité → isolation (`UseClaimsBasedAgentIsolation`) et `RequireAuthorization` en production | Sans magasin, le serveur ne garde **aucun** contexte entre deux requêtes (`NoopAgentSessionStore` par défaut, code source 1.22.0 et 1.26.0) : un client ne peut pas mener une conversation avec l'agent distant | Faible |
| **A15** | **P1** | `Lab06_A2AClient` | Ajouter le scénario **« conversation avec un agent distant »** (`CreateSessionAsync` → même `contextId` ; ex. « génère une clé » puis « valide la clé que tu viens de générer » sans la redonner) ; README : tableau **appel direct vs agent comme outil** (renvoi vers A13), limites réseau (indisponibilité, délais), authentification de l'agent appelant (`HttpClient` porteur du jeton passé à `A2ACardResolver`, guide officiel) | Exploiter un agent distant au-delà d'une question isolée exige le contexte de conversation ([A2A agent service](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/a2a?pivots=programming-language-csharp) ; exemple [`A2AClientServer`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/A2AClientServer) : *« Create a session so requests share the same A2A protocol context »*) | Moyen |
| A11 | **P2** | `Lab11-AIAgentWithCustomHttpTransport` | Ajouter un scénario **OpenTelemetry** sur le même run (`AsBuilder().UseOpenTelemetry(...)` sur l'agent **ou** le chat client, export console ; `EnableSensitiveData` en développement seulement) → « Observer un agent » ; montrer aussi les spans d'un agent appelé comme outil | Diagnostiquer avec l'outil standard ([observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp)) ; le Journey renvoie au tracing pour voir l'intérieur d'un agent délégué (*« If you need observability into inner agent behavior, use tracing »*) | Moyen |
| **A16** | **P2** | `Lab10_ExposeAIAgentAsMCPTool` | README : encadré **« exposer un agent : MCP ou A2A ? »** (MCP = l'agent devient un outil pour tout hôte MCP, protocole stable ; A2A = protocole agent-à-agent avec carte, contexte de conversation, tâches, en préversion) | Le développeur doit savoir choisir le canal d'exposition de son agent ; Lab10 en est la seule illustration MCP | Faible |
| A12 | **P2** | `README.md` racine, `labs.json` | Publier l'ordre de suivi (§7) sans renuméroter ; enregistrer Lab01-UseAgentAsTool refondu dans le dashboard ; marquer **facultatifs** Lab08 et Lab10 | Rendre visible le chemin essentiel | Faible |

**Labs sans autre mise à jour que A4** : `Lab02-AIAgentWithSO`, `Lab04-AIAgentWithMCPClient`, `Lab08-DataFormatComparison`.

---

## 4. Communication entre agents : réévaluation et décisions

### 4.1 L'agent utilisé comme outil est-il toujours recommandé ?

**Oui, dans un cadre précis.** Fait vérifié :

- C'est l'étape 7 du parcours officiel ([journey/agents-as-tools](https://learn.microsoft.com/en-us/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp)) : *« Agent-as-tool is the lightest multi-agent pattern … the natural next step when one agent isn't enough »*, à utiliser quand *« the delegation is model-driven, not hard-coded »*. La page [tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/?pivots=programming-language-csharp) documente `.AsAIFunction()` (*« Using an Agent as a Function Tool »*), avec les exemples officiels [`Agent_Step09_AsFunctionTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step09_AsFunctionTool) (local) et [`A2AAgent_AsFunctionTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/A2A/A2AAgent_AsFunctionTool) (distant).
- L'API est **stable** : `AIAgentExtensions.AsAIFunction(this AIAgent agent, AIFunctionFactoryOptions? options = null, AgentSession? session = null)` dans `Microsoft.Agents.AI` 1.26.0 ([code source](https://github.com/microsoft/agent-framework/blob/dotnet-1.26.0/dotnet/src/Microsoft.Agents.AI/AgentExtensions.cs)).
- Le cadre recommandé : agents **dans le même processus, maintenus par la même équipe** (*« If your agents all live in the same process … agents as tools is simpler »* — [journey/agent-to-agent](https://learn.microsoft.com/en-us/agent-framework/journey/agent-to-agent?pivots=programming-language-csharp)) ; pour un agent distant exposé en A2A, le même mécanisme s'applique à l'`A2AAgent`.

**Ce qui n'est plus recommandé dans `Lab01-UseAgentAsTool`** : les API supprimées, la fabrique et le builder maison qui masquent l'API enseignée, et l'**auto-évaluation** — les agents juges sont des outils de l'orchestrateur, qui choisit lui-même de se faire évaluer et lit le verdict dans la même exécution. Le concept, lui, reste d'actualité.

### 4.2 Les mécanismes disponibles dans la 1.26.0

Fait de départ : **tout agent est un `AIAgent`** — un `ChatClientAgent` local, un `A2AAgent` distant (*« It wraps any A2A-compliant endpoint as a standard `AIAgent` »*) ou un agent de service (Foundry, Copilot Studio, GitHub Copilot — [agent services](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/?pivots=programming-language-csharp)). Les mêmes mécanismes s'appliquent donc à tous.

| Mécanisme | API | Qui décide de déléguer | Ce que l'agent appelé reçoit / ce qui revient | Statut | Cas d'usage | Limites |
|---|---|---|---|---|---|---|
| **Appel direct** | `other.RunAsync(...)`, `RunAsync<T>(...)`, `RunStreamingAsync(...)` | **Le code** | Les messages que le code construit ; l'`AgentResponse` complète (texte, messages, `Usage`, résultat typé) | Stable (A2A : préversion) | Étape obligatoire et déterministe : faire vérifier, traduire, classer, extraire, puis exploiter un résultat **typé** | Le code fixe l'enchaînement ; au-delà de quelques appels, on construit à la main ce qu'un workflow ferait (exclu) |
| **Agent comme outil** | `other.AsAIFunction(new AIFunctionFactoryOptions { Name, Description }, session?)` | **Le modèle** de l'agent appelant | Un seul paramètre `query` (texte) ; **seul `response.Text`** revient (vérifié dans le code) | Stable (A2A : préversion) | Spécialiste consulté au besoin par un agent généraliste ; décomposer un agent qui a trop d'outils ou d'instructions | Routage dépendant de la description ; latence cumulée ; aucune visibilité sur les étapes internes ni sur leurs tokens (*« use tracing »*) ; **pas d'historique partagé** (*« doesn't automatically inherit the outer agent's conversation history »*) ; **nouvelle session à chaque appel** sauf session fournie ; une demande d'approbation de l'agent appelé **ne remonte pas** (seul le texte est renvoyé) |
| **Conversation avec l'agent** | `CreateSessionAsync()` puis `RunAsync(msg, session)` ; `AsAIFunction(session: …)` | Code ou modèle | Le contexte des échanges précédents (A2A : même `contextId`) | Stable (A2A : préversion) | Échanges en plusieurs tours avec un même agent (suivi, précision, correction) | A2A : le serveur doit conserver les sessions — **aucun magasin par défaut** ; *« If the remote agent restarts and loses state, your conversation context may be lost »* ; une session liée à `AsAIFunction` ne doit pas être utilisée en concurrence |
| **Un outil par compétence A2A** | Un `AIFunction` par `AgentSkill` de la carte ([`A2AAgent_Skills`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/A2A/A2AAgent_Skills)) | Le modèle | Comme l'agent comme outil | Préversion | Agent distant aux compétences multiples bien décrites dans sa carte | Le choix de la compétence n'oblige à rien côté distant (texte libre) ; **non indispensable** |
| **Tâches longues A2A** | `AllowBackgroundResponses` + `ContinuationToken` (interrogation, reconnexion de flux) ; serveur `AgentRunMode.ReturnTask` | Le code | Une tâche suivie dans le temps | Préversion ; Learn indique encore *« not supported yet »* côté hébergement alors que `ReturnTask` existe dans le code 1.22.0 et 1.26.0 | Traitements de plusieurs minutes | Avancé — **non indispensable** pour débuter |
| **Agent exposé en MCP** | `McpServerTool.Create(agent.AsAIFunction())` côté serveur, `McpClient` côté appelant (Lab10) | Le modèle (comme un outil MCP) | Un appel d'outil MCP | Stable (SDK MCP) | Rendre un agent utilisable par **tout hôte MCP** (IDE, assistants, autres agents) | L'agent n'est qu'un outil : pas de carte d'agent, pas de contexte de conversation ni de tâches du protocole A2A |
| *Exclus de cette phase* | `BackgroundAgentsProvider` (expérimental), Handoff, Group Chat, Magentic, workflows | — | — | — | Coordination entre agents | Orchestration / workflows hors périmètre |

### 4.3 Concepts indispensables pour connecter son agent à d'autres agents

1. **Un agent = un `AIAgent`**, local ou distant : mêmes `RunAsync`, `RunStreamingAsync`, `CreateSessionAsync`.
2. **Deux façons de déléguer** : *par le code* (appel direct, résultat complet et typé) ou *par le modèle* (agent comme outil, routage par la description) — et savoir choisir.
3. **Le contrat de l'agent comme outil** : nom et description soignés ; une seule entrée `query` ; seul le texte final revient ; contexte isolé ; nouvelle session par appel sauf session fournie ; coût et latence d'une exécution imbriquée ; pas de visibilité sans tracing.
4. **Exploiter la réponse** : sortie structurée (`RunAsync<T>`) pour un appel direct ; validation avant usage ; une réponse d'agent reste une **donnée non fiable** pour l'agent qui la reçoit.
5. **Local ou distant** : agent comme outil dans le même processus ; **A2A** dès qu'on franchit une frontière de service, d'équipe ou d'organisation.
6. **A2A** : découverte par carte d'agent (`/.well-known/agent-card.json`, catalogue ou URL directe), liaisons de protocole, **conversation par `contextId` qui suppose un magasin de sessions côté serveur**, pannes réseau et délais.
7. **Sécurité entre agents** : authentifier l'agent appelant, protéger l'hôte (`RequireAuthorization`), isoler les sessions par identité (`contextId`/`taskId` ne sont pas des preuves d'identité), autoriser **dans les outils** de l'agent distant (*« Agent instructions and tool descriptions do not enforce permissions »* — [guide d'authentification A2A](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/05-end-to-end/A2AClientServer/AUTHENTICATION.md)) ; garder les outils à approbation humaine sur l'agent qui dialogue avec l'utilisateur.

Hors périmètre : décider d'un enchaînement d'agents, de règles de passage de main ou de coordination (workflows, Handoff, Group Chat).

### 4.4 Décisions pour les labs

| Lab | État par rapport au sujet | Décision |
|---|---|---|
| `MultiAgentSystem/Lab01-UseAgentAsTool` | Concept juste, **fortement obsolète** techniquement, pratiques à corriger | **A13 — refonte**, en API stables, dans le même dossier : <br>• **Migration** : `OpenAIClient` v1 comme Lab01, `AsAIAgent`, `AgentResponse<T>`, `Azure.Identity` 1.21.0, plus de `Microsoft.Extensions.Hosting` ; suppression de `AgentFactory`/`AgentBuilder`. <br>• **Scénario 1 — délégation par le code** : l'agent SAV répond ; le code soumet sa réponse à l'agent de contrôle qualité par **appel direct** `RunAsync<QualityReview>` et décide selon le score (le juge sort des outils de l'orchestrateur). <br>• **Scénario 2 — délégation par le modèle** : un agent d'accueil reçoit les spécialistes via `AsAIFunction(new AIFunctionFactoryOptions { Name, Description })` ; lire les appels (`FunctionCallContent` avec `query`), constater l'isolement du contexte et l'impact d'une description vague. <br>• **Scénario 3 — garder le contexte d'un spécialiste** : `AsAIFunction(session: …)` contre une session par appel. <br>• **README** : tableau §4.2 (lignes locales), limites (approbations non remontées, tokens internes invisibles → Lab11, latence). <br>• Enregistrement dans le dashboard |
| `Lab06_A2AServer` | Pertinent ; n'autorise pas de conversation (aucun magasin de sessions) | **A2** (P0) + **A14** — magasin de sessions par agent, README sécurité et comportement par défaut |
| `Lab06_A2AClient` | Pertinent ; couvre découverte, appel direct, agent distant comme outil ; manque la conversation | **A15** — scénario de conversation, tableau des mécanismes, limites et authentification |
| `Lab10_ExposeAIAgentAsMCPTool` | Pertinent ; autre canal d'exposition | **A16** — encadré MCP ou A2A |

**Pourquoi aucun nouveau lab sur ce sujet.** Les trois mécanismes essentiels (appel direct, agent comme outil, conversation avec un agent distant) et le choix local / distant trouvent chacun leur place dans un lab existant dont c'est déjà le thème : la délégation locale dans `Lab01-UseAgentAsTool`, l'interopérabilité A2A dans `Lab06`, l'exposition MCP dans `Lab10`. Un lab supplémentaire répéterait `AsAIFunction` pour la quatrième fois.

---

## 5. Nouveau lab indispensable

| | |
|---|---|
| **Nouveau lab** | **NL1 — Exposer un agent dans une API ASP.NET Core** |
| **Priorité / effort** | **P2** · Élevé |
| **Objectif** | Transformer l'agent console en **service utilisable par une application** : conversations propres à chaque utilisateur, accès protégé |
| **Contenu (API stables uniquement)** | 1) `AIAgent` enregistré dans l'injection de dépendances d'ASP.NET Core (outils de Lab03) ; 2) endpoint minimal qui streame la réponse (`RunStreamingAsync` → Server-Sent Events) ; 3) sessions par utilisateur : `SerializeSessionAsync` / `DeserializeSessionAsync` vers un stockage indexé par l'utilisateur authentifié ; 4) `RequireAuthorization()` et gestion d'erreur ; 5) en « Going Further » : bibliothèque d'hébergement MAF (`AddAIAgent`, `MapOpenAIResponses`) — **préversion / alpha** |
| **Pourquoi indispensable** | Étape 7 du Get Started ([hosting](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp)) : sans elle, le parcours s'arrête à des applications console. Les identifiants de session ne sont pas une frontière d'autorisation ([session](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp)) |
| **Pourquoi pas un lab existant** | `Lab05` est en console et déjà le plus gros lab (27 TODO) ; `Lab06_A2AServer` expose un agent **à d'autres agents** via un protocole en préversion ; `Lab10` l'expose comme **outil** à des hôtes MCP. Aucun ne sert des **utilisateurs finaux** avec sessions par utilisateur et authentification |
| **Prérequis** | Lab01, Lab03, Lab05 ; Lab09 utile |
| **Sources** | [get-started/hosting](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp), [session](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp), [storage](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp) ; [`Agent_Step03_PersistedConversations`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step03_PersistedConversations), [`Agent_Step06_DependencyInjection`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step06_DependencyInjection) |

Pas de lab dédié au middleware : couvert par A7 (garde-fous), A11 (niveau chat client) et Lab03 (middleware de fonction).

---

## 6. Écarté pour cette phase

| Sujet | Raison |
|---|---|
| Orchestration multi-agent et workflows MAF, `MultiAgentSystem/Lab02` et `Lab03` | Hors périmètre (décision précédente) |
| Coordination d'agents : Handoff, Group Chat, Magentic, `BackgroundAgentsProvider` | Orchestration ; `BackgroundAgentsProvider` est en outre expérimental |
| Un outil par compétence A2A, tâches longues A2A, liaison gRPC | Utiles mais non indispensables ; préversion ; documentation Learn en retard sur le code pour les tâches |
| Agent Harness, Agent Skills, évaluation | Premiers candidats d'une phase suivante. **Mise à jour 2026-10-25** : évaluation et Agent Skills faits (Lab14, Lab15, API stables — [rapport](Migration/Evaluation-Skills-Report.md)) ; Agent Harness reporté (compaction, mémoire de fichiers et boucle expérimentales, `MAAI001`) |
| Multimodal, background responses (hors A2A), agents déclaratifs (RC), compaction et réducteur intégrés (expérimentaux), routage multi-modèles, agents personnalisés | Fonctionnalités avancées ou spécialisées |
| AG-UI, DevUI, CodeAct, Durable Extension, Foundry Hosted Agents | Non indispensables pour débuter ni pour une première solution complète |

---

## 7. Exécution

| Étape | Actions | Effort | Critère de fin |
|---|---|---|---|
| **1 · P0** ✅ *fait le 2026-10-25* | A1, A2, A3 | ≈ 1 j | Streaming validé (deux authentifications) ; contournement Lab06 supprimé ; README racine corrigé. **Résultat** : `Microsoft.Extensions.AI.OpenAI` 10.11.0 ajouté à Lab01, Lab02, Lab06_A2AServer ; Lab01 7/7, Lab02 9/9, Lab06 serveur 10/10 et client 9/9 (clé API et Entra ID) ; `dotnet test` du dashboard 207/207 |
| **2 · P1** ✅ *fait le 2026-10-25* | A4 | ≈ 2–3 j | 13 labs en 1.26.0 ; Lab09, Lab03, Lab06 re-testés. **Résultat** : aucune API changée ; Entra ID 127/127, clé API 127/127 après correction de Lab07 (culture) ; Start livrés OK ; dashboard 207/207 — [rapport](Migration/Upgrade-1.26.0-Report.md) |
| **3 · P1** ✅ *fait le 2026-10-25* | A5 → A10 | ≈ 7–10 j | Labs enrichis, TODO du README alignés, tests du dashboard au vert. **Résultat** : Lab01 (API Responses), Lab03 (outils déclarés et validés), Lab05 (réducteur d'historique), Lab07 (sécurité du RAG), Lab09 (garde-fous par middleware), Lab12 (souvenirs en `Messages`) ; clé API 135/135, Entra ID 135/135 ; Start à 0 warning ; dashboard 207/207 — [rapport](Migration/Enrichments-Step3-Report.md) |
| **4 · P1** ✅ *fait le 2026-10-25* | A13, A14, A15 (communication entre agents) | ≈ 5–6 j | Lab01-UseAgentAsTool refondu en 1.26.0 et enregistré dans le dashboard ; conversation A2A client ↔ serveur démontrée. **Résultat** : MAS Lab01 « Delegate a Task to Another Agent » (délégation par le code, par le modèle, session d'un spécialiste) 13/13 sur 5 exécutions (clé API et Entra ID) ; Lab06 : magasin de sessions par agent (`AgentSessionStore`, **expérimental `MAAI001`**, non signalé dans ce plan) et scénario 4 de conversation — sans magasin, le serveur oublie (vérifié), avec, la clé du tour 1 est redonnée 5/5 ; serveur 12/12, client 13/13 ; dashboard 211/211 — [rapport](Migration/Communication-Step4-Report.md) |
| **5 · P2** ✅ *fait le 2026-10-25* | A11, A16, NL1, A12 | ≈ 5–7 j | « Observer un agent » ; NL1 enregistré ; ordre de suivi publié. **Résultat** : Lab11 « Observe an Agent » (scénario 3 OpenTelemetry, spans d'un agent appelé comme outil) 18/18 ; Lab10 encadré MCP ou A2A ; **Lab13** « Host an Agent in an ASP.NET Core API » (DI, JWT, Server-Sent Events, conversations par utilisateur, API stables) 8/8 ; clé API et Entra ID ; parcours publié dans le README racine et ordre du dashboard (Lab08, Lab10 facultatifs) ; dashboard 214/214 — [rapport](Migration/Production-Step5-Report.md) |

**Total indicatif : 20 à 27 jours** (estimation d'après la taille des labs actuels, pas une mesure).

Critères communs : Start et Solution compilent ; exécution réelle clé API **et** Entra ID ; `dotnet test` depuis `Dashboard/` ; aucune API expérimentale sans `#pragma` ciblé ; aucune abstraction maison (règles de `Migration/Migration-Manual.md`).

**Parcours essentiel en fin de phase :**

```
Lab01 Agent (+ Responses) → Lab02 Sortie structurée → Lab03 Outils → Lab04 Client MCP
→ Lab05 Sessions et historique → Lab09 Contrôle (approbation + garde-fous)
→ Lab07 RAG → Lab12 Mémoire
→ MultiAgentSystem/Lab01 Confier une tâche à un autre agent (local)
→ Lab06 serveur → Lab06 client (agents distants, A2A — préversion)
→ Lab11 Observabilité → NL1 API ASP.NET Core
Facultatifs : Lab10 (agent exposé en MCP) · Lab08 (formats de données)
```

---

## 8. Références

- **Microsoft Learn** (pivot C#) : [overview](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp) · [get-started](https://learn.microsoft.com/en-us/agent-framework/get-started/?pivots=programming-language-csharp) · [hosting](https://learn.microsoft.com/en-us/agent-framework/get-started/hosting?pivots=programming-language-csharp) · [journey](https://learn.microsoft.com/en-us/agent-framework/journey/?pivots=programming-language-csharp) · [journey/agents-as-tools](https://learn.microsoft.com/en-us/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp) · [journey/agent-to-agent](https://learn.microsoft.com/en-us/agent-framework/journey/agent-to-agent?pivots=programming-language-csharp) · [tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/?pivots=programming-language-csharp) · [agent services](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/?pivots=programming-language-csharp) · [A2A agent service](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/agent-services/a2a?pivots=programming-language-csharp) · [A2A hosting](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/a2a/server?pivots=programming-language-csharp) · [A2A hosting .NET](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting/a2a/dotnet?pivots=programming-language-csharp) · [background-agents](https://learn.microsoft.com/en-us/agent-framework/agents/background-agents?pivots=programming-language-csharp) · [azure-openai](https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/azure-openai?pivots=programming-language-csharp) · [session](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp) · [storage](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp) · [middleware](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/middleware/?pivots=programming-language-csharp) · [safety](https://learn.microsoft.com/en-us/agent-framework/concepts/agents/safety?pivots=programming-language-csharp) · [observability](https://learn.microsoft.com/en-us/agent-framework/agents/observability?pivots=programming-language-csharp)
- **GitHub `microsoft/agent-framework`** (tag `dotnet-1.26.0` sauf mention) : [releases](https://github.com/microsoft/agent-framework/releases) · [`AgentExtensions.cs`](https://github.com/microsoft/agent-framework/blob/dotnet-1.26.0/dotnet/src/Microsoft.Agents.AI/AgentExtensions.cs) (`AsAIFunction`) · [`A2AServerServiceCollectionExtensions.cs`](https://github.com/microsoft/agent-framework/blob/dotnet-1.26.0/dotnet/src/Microsoft.Agents.AI.Hosting.A2A/A2AServerServiceCollectionExtensions.cs) (magasin par défaut `NoopAgentSessionStore`, `AgentRunMode.ReturnMessage` ; identique au tag `dotnet-1.22.0`) · [`AgentRunMode.cs`](https://github.com/microsoft/agent-framework/blob/dotnet-1.26.0/dotnet/src/Microsoft.Agents.AI.Hosting.A2A/AgentRunMode.cs) · exemples [`Agent_Step09_AsFunctionTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step09_AsFunctionTool), [`02-agents/A2A`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/A2A) (`A2AAgent_AsFunctionTool`, `A2AAgent_Skills`, `A2AAgent_PollingForTaskCompletion`, `A2AAgent_StreamReconnection`, `A2AAgent_ProtocolSelection`), [`05-end-to-end/A2AClientServer`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/A2AClientServer) et son [`AUTHENTICATION.md`](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/05-end-to-end/A2AClientServer/AUTHENTICATION.md), `Agent_Step03`, `Step06`, `Step12`, `Step17` · PR [#8641](https://github.com/microsoft/agent-framework/pull/8641), [#8754](https://github.com/microsoft/agent-framework/pull/8754), [#9152](https://github.com/microsoft/agent-framework/pull/9152), [#9386](https://github.com/microsoft/agent-framework/pull/9386), [#9542](https://github.com/microsoft/agent-framework/pull/9542)
- **Autres** : [`openai-dotnet` @ OpenAI_2.14.0](https://github.com/openai/openai-dotnet/tree/OpenAI_2.14.0) (`OPENAI001`) · [dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790) (corrigé en `Microsoft.Extensions.AI` 10.11.0)
- **Projet** : [`AGENT_FRAMEWORK_LABS_AUDIT.md`](AGENT_FRAMEWORK_LABS_AUDIT.md) · [`AGENT_FRAMEWORK_LABS_ACTION_PLAN.md`](AGENT_FRAMEWORK_LABS_ACTION_PLAN.md) (v1) · `Migration/Migration-Manual.md` · `LearningLabs/AzureOpenAI/Lab06_*`, `Lab10_*`, `LearningLabs/MultiAgentSystem/Lab01-UseAgentAsTool`
