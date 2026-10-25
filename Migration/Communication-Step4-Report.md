# Rapport — étape 4 du plan d'action : communication entre agents (A13 à A15)

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.26.0** · **.NET 10** (`net10.0`)
> Références : plan d'action [`AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md`](../AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md) (§3 actions A13 à A15, §4 communication entre agents), [Migration-Manual.md](Migration-Manual.md) (règles §6, tests §7), [Enrichments-Step3-Report.md](Enrichments-Step3-Report.md)

**Statut : fait.** `MultiAgentSystem/Lab01-UseAgentAsTool` est refondu en API stables 1.26.0 (« Delegate a Task to Another Agent ») et enregistré dans le dashboard ; le serveur A2A de Lab06 garde les conversations (magasin de sessions par agent) et le client A2A mène une conversation avec un agent distant. Aucune orchestration ni aucun workflow.

---

## 1. Changements réalisés

| Action | Lab | Changement | Pourquoi |
|---|---|---|---|
| **A13** | `MultiAgentSystem/Lab01-UseAgentAsTool` (même dossier, même projet `UseAgentAsTool`) | **Refonte complète** en 1.26.0 : `OpenAIClient` v1 comme les labs AzureOpenAI, `AsAIAgent`, `AgentResponse<T>`, `Azure.Identity` 1.21.0, configuration `Microsoft.Extensions.Configuration.*` + user secrets. Supprimés : `AgentFactory`, `AgentBuilder`, `Configuration/AgentSettings.cs`, `AgentType.cs`, `Models/OrchestratorResponse.cs`, la configuration par agent et par déploiement, `Azure.AI.OpenAI` (beta), `Microsoft.Extensions.Hosting`, l'agent « reformulateur ». **Scénario 1** — délégation par le code : l'expert après-vente répond, le code demande une revue typée (`RunAsync<QualityReview>`) à un agent relecteur et décide avec une règle fixe (score ≥ 7, score hors 1–10 refusé) ; une réponse fausse écrite à la main est retenue. **Scénario 2** — délégation par le modèle : un agent d'accueil reçoit un spécialiste des commandes (qui a son propre outil `get_order`) et l'expert après-vente via `AsAIFunction(new AIFunctionFactoryOptions { Name, Description })` ; affichage du schéma (un seul paramètre `query`), des requêtes écrites par le modèle et des textes renvoyés. **Scénario 3** — session d'un spécialiste : le même agent « ticket » en outil sans session (nouvelle session par appel) et avec `AsAIFunction(session: …)`. Nouveaux fichiers fournis : `AgentConsole.cs`, `Models/QualityReview.cs`, `Tools/OrderTools.cs` (dates relatives à aujourd'hui). README réécrit : TODO 1–19, tableau des deux façons de déléguer, contrat de l'agent-outil, limites (tokens invisibles, approbations qui ne remontent pas, latence, réponse non fiable), exercice « description vague », sortie attendue réelle | Seul lab qui enseigne la délégation dans un même processus (étape *Agents as tools* du parcours Learn), mais API supprimées, fabrique maison et « juges » appelés comme outils par l'agent qu'ils évaluent (plan §4.1) |
| **A14** | `Lab06_A2AServer` | **TODO 8** (nouveau, TODO 8–11 renumérotés 9–12) : un `InMemoryAgentSessionStore` par agent, enregistré sous le nom de l'agent (`builder.Services.AddKeyedSingleton<AgentSessionStore>(authAgent.Name, …)`), `#pragma MAAI001` ciblé et commenté, message « Session store: in memory (development only), one per agent. ». README : section **« Conversations: the session store »** (comportement par défaut vérifié, développement uniquement, expérimental), section **Security** réécrite d'après le guide d'authentification officiel (`RequireAuthorization`, `UseClaimsBasedAgentIsolation`, autorisation dans les outils, `contextId` ≠ preuve d'identité) ; concepts, espaces de noms, sortie attendue, « Coming from an older version » | Sans magasin, le serveur ne garde **aucune** conversation (démontré, §3.2) |
| **A15** | `Lab06_A2AClient` | **Scénario 4** « A conversation with a remote agent » (TODO 13–16) : `CreateSessionAsync`, deux tours dans la session (« génère une clé », puis « redonne la clé que tu viens de générer et vérifie-la »), le même message sans session ; le code vérifie que la réponse contient **exactement** la clé du tour 1, et affiche le `contextId` des deux tours (`AgentConsole.WriteContextId`, fourni). README : scénario 4, point 5 « Conversation », sections **« Direct call or agent as a tool? »** (renvoi vers MAS Lab01) et **« What a network changes »** (latence, pannes, délais, état de conversation côté serveur, authentification de l'agent appelant avec le code du guide officiel, réponse non fiable), dépannage, « Going further » | Exploiter un agent distant au-delà d'une question isolée exige le contexte de conversation (exemple officiel `A2AClientServer`) |
| — | `Dashboard/LabDashboard/labs.json` | Nouveau lab **`multiagent-lab01`** (piste « Multi-Agent System » / « Système multi-agents », niveau Intermédiaire, 13 attentes, EN et FR) ; Lab06 client +4 attentes (`scenario4-*`), Lab06 serveur +2 (`session-store`, `conversation`) ; résumés et objectifs | Le dashboard vérifie les comportements, pas la formulation du modèle |
| — | `Dashboard/LabDashboard.Tests` | Catalogue (14 labs) ; sorties réelles de MAS Lab01 (Solution, Start) ; Lab06 avec scénario 4 ; tests négatifs : MAS Lab01 (le code ne décide pas, sessions inversées) et **Lab06 sans magasin de sessions** (sortie réelle) ; libellés de consommation de tokens de MAS Lab01 | Non-régression |
| — | `README.md` racine, `Migration-Plan.md`, `Migration-Manual.md` | MAS Lab01 migré (Lab02/Lab03 toujours non migrés), parcours, Lab06 scénario 4 et magasin de sessions ; manuel : sessions A2A (`MAAI001`), réducteurs enseignés depuis l'étape 3 | Cohérence |

## 2. Faits vérifiés (code source et documentation 1.26.0)

| Fait | Source |
|---|---|
| `AsAIFunction(options, session)` : un seul paramètre `query` (*« Input query to invoke the agent. »*), renvoie `response.Text`, nouvelle session à chaque appel sans `session` ; le contexte d'appel (`FunctionInvokingChatClient.CurrentContext`) est lu de façon optionnelle : l'outil peut être invoqué depuis le code (scénario 3) | `AgentExtensions.cs` (`dotnet-1.26.0`), XML de `Microsoft.Agents.AI` 1.26.0 ; schéma affiché par le scénario 2 |
| Le nom et la description de l'outil viennent de l'agent quand les options sont absentes | Code source ; scénario 3 (`Tool TicketSpecialist: Keeps the support ticket…`) |
| Hébergement A2A : `GetKeyedService<AgentSessionStore>(agent.Name)`, repli sur `NoopAgentSessionStore` ; sans `AgentIsolationKeyProvider`, isolation **non stricte** (indexation par le seul `contextId`) | `A2AServerServiceCollectionExtensions.cs` (`dotnet-1.26.0`) |
| `AgentSessionStore` et `InMemoryAgentSessionStore` sont `[Experimental("MAAI001")]` | Compilation (`error MAAI001`) |
| Le `contextId` est choisi par le **serveur** à la première réponse ; le client le renvoie ensuite (`session.ContextId ??= contextId`) | `A2AAgent.cs` (`dotnet-1.26.0`) ; scénario 4 (même id aux deux tours) |
| *« It doesn't automatically inherit the outer agent's conversation history or context »*, *« If you need observability into inner agent behavior, use tracing »*, *« if the tool description is vague, the model may call the wrong agent or skip it entirely »* | [Agents as tools](https://learn.microsoft.com/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp) |
| *« The remote agent manages its own conversation state (keyed by context ID) […] If the remote agent restarts and loses state, your conversation context may be lost »* ; *« handle network failures, timeouts, retries, and versioning »* | [Agent-to-Agent](https://learn.microsoft.com/agent-framework/journey/agent-to-agent?pivots=programming-language-csharp) |
| *« Never use a caller-supplied context or task ID as proof of ownership »* ; *« Agent instructions and tool descriptions do not enforce permissions »* ; client : `HttpClient` porteur du jeton passé à `A2ACardResolver` puis à `card.AsAIAgent(httpClient)` | [Guide d'authentification `A2AClientServer`](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/05-end-to-end/A2AClientServer/AUTHENTICATION.md) |

## 3. Écarts au plan

- **A14 — API expérimentale** : le plan ne signalait pas que `AgentSessionStore` / `InMemoryAgentSessionStore` sont `[Experimental]` (`MAAI001`). Le magasin est indispensable à la conversation A2A et le paquet d'hébergement est déjà en préversion : il est conservé avec un `#pragma` ciblé et commenté (règle 11 du manuel), signalé dans le code, le README et le manuel.
- **A13, scénario 3** : les outils sont appelés **par le code** (`AIFunction.InvokeAsync` avec l'argument `query`, ce que fait la boucle d'appel de fonctions) plutôt que par un modèle : un modèle qui a l'historique réécrit souvent toute l'affaire dans sa requête, ce qui masquerait l'effet de la session. Le README l'explique.
- **A13, description vague** : exercice guidé dans le README plutôt qu'un scénario avec attentes (effet non déterministe).
- **A15, preuve de la mémoire** : le plan prévoyait « valide la clé que tu viens de générer ». Sans magasin, l'agent **génère une nouvelle clé et la déclare valide** (§3.2) : une réponse « valid » ne prouve rien. Le second tour demande donc la clé elle-même, et le code vérifie qu'elle est identique à celle du tour 1.
- **Dashboard** : `multiagent-lab01` est ajouté en fin de catalogue ; l'ordre du parcours relève de l'action A12 (étape 5).

## 4. Tests effectués et résultats

| # | Vérification (manuel §7) | Résultat |
|---|---|---|
| 4 | Build Start + Solution (MAS Lab01, Lab06 serveur, Lab06 client) | ✅ 0 erreur, **0 warning** (6 projets) |
| 6–7 | MAS Lab01 Solution contre les attentes du dashboard | ✅ **13/13 sur 5 exécutions** : clé API ×3, Entra ID ×2 (13 à 16 s) |
| 6–7 | Lab06 serveur + client (port 5071, comme le dashboard) | ✅ serveur **12/12**, client **13/13** sur 3 exécutions : clé API ×2, Entra ID ×1 |
| 9 | Start livrés | ✅ MAS Lab01, Lab06 client (contre le serveur de référence) et Lab06 serveur : exit 0, tous les en-têtes, aucune exception ; seul `config` passe |
| 11 | Start = Solution hors `Program.cs` | ✅ MAS Lab01 (`AgentConsole.cs`, `Models/`, `Tools/`, `MockedData/`, configuration, `.csproj`), Lab06 client (`AgentConsole.cs`), Lab06 serveur |
| — | `dotnet test` du dashboard | ✅ **211/211** |
| 10 | Start complété en suivant les indices | ⚠️ Non rejoué : les indices reprennent le code de la Solution |

### 4.1 Comportement par défaut du serveur A2A (avant / après A14)

Même client (scénario 4), même question au second tour :

| Serveur | `contextId` des deux tours | Réponse du second tour (même session) | Clé du tour 1 redonnée |
|---|---|---|---|
| Sans magasin (avant) — 2 exécutions | identique | *« The generated API key is: **Meknes63GQ…** It is valid. »* — une **nouvelle** clé | ❌ non (2/2) |
| `InMemoryAgentSessionStore` par agent (après) — 5 exécutions | identique | *« Here is the API key you requested, exactly as it is: **Meknes89JC…** The key is valid. »* | ✅ oui (5/5) |

Sans la session (troisième appel), l'agent génère toujours une autre clé : il ne refuse pas, il répond à une autre question — d'où la vérification par le code.

### 4.2 Outil de vérification

Le script local qui confronte les journaux aux expressions régulières de `labs.json` lisait les fichiers en mode texte Python, qui convertit chaque `\r` des images du spinner en fin de ligne. Il lit désormais les octets tels quels et ne garde, comme le décodeur du dashboard, que le texte qui suit le dernier `\r` d'une ligne. Les journaux de l'étape 3 donnent le même résultat (135/135 clé API et Entra ID). Les sorties de référence des tests du dashboard ont été produites avec ce décodage (aucune image de spinner).

## 5. Points d'attention

- **MAS Lab01, scénario 1** : le score du relecteur vient d'un modèle ; la grille de notation dans ses instructions l'a stabilisé (10/10 et 1/10 sur les 5 exécutions), mais un 6/10 reste possible pour la bonne réponse.
- **MAS Lab01, scénario 2** : l'accueil appelle parfois les deux spécialistes dans le même appel de modèle (appels parallèles) ; les attentes ne dépendent pas de l'ordre.
- **Magasin en mémoire** : perdu au redémarrage du serveur, non partagé entre instances ; le README le dit et renvoie vers une implémentation persistante pour la production.
- **Sécurité A2A** : le serveur du lab reste sans authentification (développement local) ; la production est décrite d'après le guide officiel, sans être exécutée dans le lab.

## 6. Points pour la suite

- Étape 5 du plan : A11 (Lab11 OpenTelemetry — y compris la visibilité des appels des spécialistes de MAS Lab01), A16 (Lab10, encadré MCP ou A2A), NL1 (lab d'hébergement ASP.NET Core), A12 (ordre du parcours, dont la place de `multiagent-lab01` dans le dashboard).
