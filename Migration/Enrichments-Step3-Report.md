# Rapport d'enrichissement — étape 3 du plan d'action (A5 à A10)

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.26.0** · **.NET 10** (`net10.0`)
> Références : plan d'action [`AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md`](../AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md) (§3, actions A5 à A10), [Migration-Manual.md](Migration-Manual.md) (règles §6, tests §7), [Upgrade-1.26.0-Report.md](Upgrade-1.26.0-Report.md)

**Statut : fait.** Six labs enrichis sur des concepts fondamentaux qui manquaient ou étaient mal enseignés : API Responses (Lab01), outils sûrs (Lab03), historique borné (Lab05), sécurité du RAG (Lab07), garde-fous par middleware (Lab09), niveau de confiance de la mémoire (Lab12). Aucun nouveau lab, aucune nouvelle dépendance en préversion.

---

## 1. Changements réalisés

| Action | Lab | Changement | Pourquoi |
|---|---|---|---|
| **A5** | `Lab01-FirstBasicAIAgent` | **Scénario 6** « Responses API » : le même agent créé avec `client.GetResponsesClient().AsAIAgent(model: …)` (`using OpenAI.Responses;`, `#pragma OPENAI001` ciblé) ; affichage de la réponse, du `ResponseId` (`resp_…`, stocké par le service) et de l'usage. README : TODO 22-23, section **« Chat Completions or Responses? »** (tableau de choix), ligne Foundry, dépannage | *« Responses is the recommended primary client »* (page Azure OpenAI de Learn) ; jusqu'ici seulement en « Going Further » |
| **A6** | `Lab03-AIAgentWithFunctionTools` | **Scénario 2 réécrit** : `MeetingRoomPlugin` (fin de `Program.cs`) **déclare** ses outils (`AsAITools()`, pattern `Agent_Step12_Plugins`) et **vérifie la date** choisie par le modèle (format, pas dans le passé) ; la réservation passée est refusée, celle de demain confirmée. `AgentConsole.WriteToolResults` (fourni) affiche ce que les outils ont renvoyé. Scénario 4 : date relative (`Today + 2`). README : section **« Designing safe tools »**, tableau des trois niveaux de middleware, piège de `Terminate` | L'ancien scénario 2 exposait toutes les méthodes publiques par réflexion (moindre privilège) ; aucune validation des arguments ([Agent Safety](https://learn.microsoft.com/agent-framework/concepts/agents/safety?pivots=programming-language-csharp)) ; les dates codées en dur étaient passées |
| **A7** | `Lab09-AIAgentWithFunctionToolsHumanApproval` | Lab renommé **« Control an Agent: Human Approval and Guardrails »** (titre seulement, dossier inchangé). **Scénario 3** : l'agent RH enveloppé par `AsBuilder().Use(runFunc: HrGuardrail.RunAsync, runStreamingFunc: null)` (classe `HrGuardrail` en fin de `Program.cs`) : question interdite traitée sans appeler le modèle, données personnelles masquées dans la requête et vérifiées dans la réponse (en place, sur les seuls `TextContent` : les demandes d'approbation sont préservées). README : TODO 18-23, concepts, sortie attendue, « Going Further » (streaming, Purview, trois niveaux de middleware) | Le middleware est une brique fondamentale (overview, Journey) et le moyen .NET recommandé pour les contrôles à l'exécution ; un lab dédié a été évité |
| **A8** | `Lab05-AIAgentWithThreads` | **Scénario 2 remplacé** (historique dans un vector store, redondant avec MongoDB) par **« Bound the chat history with a chat reducer »** : `InMemoryChatHistoryProvider` + `MessageCountingChatReducer(2)`, trois tours, nombre de messages gardés et tokens affichés (`SessionConsole.WriteTurn`) ; la dernière réponse connaît Rome mais a oublié « Ada ». Suppression de `VectorChatHistoryProvider.cs` et de `CommunityToolkit.VectorData.InMemory`. Constantes affichées au démarrage (corrige les `CS0219` du Start). README : sections **« When the service keeps the conversation (Responses API) »** et **« Security of stored sessions »** | Conversations longues = coût et dépassement de contexte ; *« bind each stored session to the authenticated user »* (Learn, Storage) |
| **A9** | `Lab12-AIAgentWithAIContextProvider` | Les souvenirs passent de `AIContext.Instructions` à **`AIContext.Messages`** (message utilisateur présenté comme des données) ; `StorageInputRequestMessageFilter` les tient hors de l'historique de la session (sinon, une copie de plus à chaque run). Console : « added to the request ». README : objectifs, TODO 6 et 13, concepts (`Messages` contre `Instructions`, filtre), sécurité | *« Do not place end-user input into system-role messages »* ([Agent Safety](https://learn.microsoft.com/agent-framework/concepts/agents/safety?pivots=programming-language-csharp)) ; par défaut, l'historique stocke les messages des providers (filtre par défaut `DefaultExcludeChatHistoryFilter`, vérifié dans le code source 1.26.0) |
| **A10** | `Lab07-AgenticRAG-VectorStore` | README : section **« Security: the retrieved content is untrusted »** (injection de prompt indirecte, contrôle de l'indexation, sources, limitation des actions) et **exercice** guidé avec une entrée de FAQ piégée ; objectif ajouté au dashboard. Pas de nouveau scénario ni de check : le résultat dépend du modèle | Le contenu récupéré n'est pas fiable ; absent du lab |
| — | `Dashboard/LabDashboard/labs.json` | Titres, résumés, objectifs (EN et FR) ; checks : Lab01 +3, Lab03 scénario 2 réécrit (+1), Lab05 scénario 2 remplacé, Lab09 +4, Lab12 3 motifs mis à jour | Le dashboard vérifie les nouveaux comportements, pas la formulation du modèle |
| — | `Dashboard/LabDashboard.Tests/OutputAnalyzerTests.cs` | Sorties de référence et tests négatifs de Lab03, Lab05, Lab09, Lab12 | Alignés sur les sorties réelles |
| — | `README.md` racine | Vue d'ensemble de Lab01, Lab03, Lab05, Lab09 (nouveau titre), Lab12 | Cohérence |

## 2. Écart au plan

- **A8** prévoyait un `ChatHistoryProvider` écrit à la main (API stables). Le scénario utilise finalement le **réducteur officiel** `MessageCountingChatReducer`, marqué `[Experimental("MEAI001")]`, comme l'exemple officiel `Agent_Step13_ChatReduction`, avec un `#pragma` ciblé et commenté. Raison : la règle 12 du manuel (« pas d'abstraction maison ») ; un provider qui tronque n'apparaît dans aucun exemple officiel, le réducteur si. Le caractère expérimental est signalé dans le code, le README et les concepts.
- **A10** : exercice guidé dans le README plutôt qu'un scénario avec checks (non déterministe : l'effet d'une injection dépend du modèle).

## 3. Tests effectués et résultats

| # | Vérification (manuel §7) | Résultat |
|---|---|---|
| 4 | Build Start + Solution des labs modifiés | ✅ 0 erreur, **0 warning** (Lab05/Start compris : les 4 `CS0219` hérités sont résolus) |
| 6–7 | Exécution des 13 Solutions, clé API **et** Entra ID, contre les checks du dashboard | Voir §3.1 |
| 9 | Start livrés des 6 labs | ✅ exit 0, tous les en-têtes de scénarios (y compris les nouveaux), aucune exception ; seul le check `config` passe, comme attendu |
| 11 | Start = Solution hors `Program.cs` | ✅ `AgentConsole.cs`, `SessionConsole.cs`, `MemoryConsole.cs`, `CompanyTools.cs`, `.csproj` identiques |
| — | `dotnet test` du dashboard | ✅ 207/207 |
| 10 | Start complété en suivant les indices | ⚠️ Non rejoué : les indices des TODO ajoutés reprennent le code de la Solution, mais aucune copie de Start n'a été complétée et exécutée |

### 3.1 Exécutions réelles (attentes du dashboard)

| Lab | Clé API | Entra ID |
|---|---|---|
| Lab01 (dont 3 nouveaux checks du scénario 6) | ✅ 10/10 | ✅ 10/10 |
| Lab02 | ✅ 9/9 | ✅ 9/9 |
| Lab03 (scénario 2 réécrit) | ✅ 13/13 | ✅ 13/13 |
| Lab04 | ✅ 9/9 | ✅ 9/9 |
| Lab05 (scénario 2 remplacé) | ✅ 11/11 | ✅ 11/11 |
| Lab06 serveur / client | ✅ 10/10 · 9/9 | ✅ 10/10 · 9/9 |
| Lab07 | ✅ 12/12 | ✅ 12/12 |
| Lab08 | ✅ 11/11 | ✅ 11/11 |
| Lab09 (dont 4 nouveaux checks du scénario 3) | ✅ 13/13 | ✅ 13/13 |
| Lab10 | ✅ 9/9 | ✅ 9/9 |
| Lab11 | ✅ 12/12 | ✅ 12/12 |
| Lab12 (3 motifs mis à jour) | ✅ 7/7 | ✅ 7/7 |
| **Total** | ✅ **135/135** | ✅ **135/135** |

Les 13 Solutions sortent en exit 0 dans les deux modes. Conditions identiques à l'étape 2 ([Upgrade-1.26.0-Report.md](Upgrade-1.26.0-Report.md) §3) : exécution depuis le dossier de la Solution, entrées scriptées (Lab09 : `Y` ; Lab12 : message vide), Lab06 serveur + client sur le port 5071, MongoDB de Lab05 et Lab12 démarrés pour les tests puis arrêtés, sorties vérifiées avec les expressions régulières de `labs.json`.

## 4. Points d'attention

- **Lab05 scénario 2** dépend du comportement du modèle pour le check `scenario2-forgotten` (la réponse doit donner Rome sans connaître « Ada ») : le nom n'étant plus dans le contexte, le modèle ne peut pas le donner ; la formulation peut varier, le motif ne vérifie que « Rome » et l'absence de « Ada ».
- **Lab09 scénario 3** : les motifs de `MaskPersonalData` sont volontairement simples (e-mails, numéros internationaux en `+…`) ; le README renvoie vers un service de détection dédié (Purview).
- **Lab01 scénario 6** : nécessite un déploiement qui accepte l'API Responses (ligne de dépannage ajoutée).

## 5. Points pour la suite

- Étape 4 du plan : A13 (refonte de `MultiAgentSystem/Lab01-UseAgentAsTool`), A14 (magasin de sessions de Lab06 serveur), A15 (conversation avec un agent distant, Lab06 client).
