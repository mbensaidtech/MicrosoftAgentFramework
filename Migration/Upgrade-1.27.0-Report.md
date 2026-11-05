# Rapport de montée de version — tout le catalogue, 1.26.0 → 1.27.0

> Date : 2026-11-06 · Cible : **Microsoft Agent Framework 1.27.0** (stable, publiée le 2026-10-30) · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§1, **§3 ter**, §6, §7, §8), [Migration-Plan.md](Migration-Plan.md), [Upgrade-1.26.0-Report.md](Upgrade-1.26.0-Report.md) (même procédure). Sources : notes de version GitHub `dotnet-1.27.0`, `Directory.Packages.props` officiel aux tags `dotnet-1.26.0` et `dotnet-1.27.0`, sources `AgentSessionStore.cs`, `Local/InMemoryAgentSessionStore.cs`, `A2AServerServiceCollectionExtensions.cs` aux deux tags, nuspecs NuGet de `Microsoft.Agents.AI.OpenAI` 1.27.0 et `Microsoft.Agents.AI.A2A` 1.27.0-preview.261030.1.

**Statut : fait.** Les 19 entrées du catalogue (16 labs AzureOpenAI dont Lab06 serveur et client, 3 labs MultiAgentSystem ; 38 projets Start et Solution) sont en 1.27.0. Aucune API utilisée par les labs n'a changé : la montée se limite aux versions de paquets et à la documentation.

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| 38 `.csproj` (Start et Solution) | `Microsoft.Agents.AI.OpenAI` 1.26.0 → **1.27.0** | Dernière version stable |
| 4 `.csproj` (MAS-Lab02, MAS-Lab03) | `Microsoft.Agents.AI.Workflows` 1.26.0 → **1.27.0** | Idem |
| 4 `.csproj` (Lab06 client et serveur) | `Microsoft.Agents.AI.A2A` / `.Hosting.A2A.AspNetCore` → **1.27.0-preview.261030.1** | Préversion alignée (aucune version stable) ; SDK `A2A` 1.0.0-preview3 inchangé |
| 12 `.csproj` (Lab01, Lab02, Lab06_A2AServer, Lab13, MAS-Lab02, MAS-Lab03) | `Microsoft.Extensions.AI.OpenAI` 10.11.0 **conservé**, commentaire « Agent Framework 1.27.0 still brings 10.10.x » | MAF 1.27.0 n'apporte toujours que 10.10.1 (manuel §8) |
| READMEs des labs (Lab01 à Lab15, MAS-Lab01 à 03) | Colonne « Now (1.27.0) » des encadrés « Coming from an older version » ; liens des samples vers `tree/dotnet-1.27.0` (11 liens vérifiés, tous 200) ; Lab06 : préversions 1.27.0 ; Lab11 : `User-Agent` réel (`agent-framework-dotnet/1.27.0`) ; Lab06 serveur : source citée en 1.27.0 (fichier identique entre les tags) | Cohérence avec les `.csproj` |
| `Lab06_A2AServer/Solution/Program.cs` | Commentaire : `MAAI001` « in Agent Framework 1.27.0 » | `AgentSessionStore` / `InMemoryAgentSessionStore` toujours `[Experimental]` en 1.27.0 (vérifié dans les sources) |
| `README.md` racine | Version des labs (1.27.0), préversions A2A | Idem |
| `Migration-Manual.md` | §1, **§3 ter**, §4.17 (`User-Agent`), §4.19, §6 règle 1, §8 (cadence) | Référence commune |
| `Migration-Plan.md` | Version figée, checklist | Idem |

## 2. Notes de version prises en compte

| Changement 1.27.0 | PR | Labs | Constat |
|---|---|---|---|
| [BREAKING] Dépendances AgentServer, `builder.AddFoundryResponses(...)` | #9721 | aucun | `AddFoundryResponses`, `AgentServer` : 0 occurrence dans les labs |
| [BREAKING] `DefaultHttpRequestHandler` : refus des clients de redirection | #9711 | aucun | 0 occurrence (workflows déclaratifs) |
| Corrections Workflows (conversions, alias de portée, revue de plan Magentic, libération après échec d'initialisation) | #9845, #9826, #9830, #9749, #9797, #9585 | MAS-Lab02/03 | Aucun effet observé : attentes du dashboard 13/13 et 9/9 |
| Toolbox, Mem0, Harness, Foundry, LocalCodeAct | divers | aucun | Paquets non utilisés |
| `Directory.Packages.props` | — | aucun | Seuls changent `Azure.AI.AgentServer.*`, `Azure.AI.Projects`, `Azure.AI.Extensions.OpenAI`, `Azure.Core`, `System.ClientModel`, `Microsoft.Identity.Client.Extensions.Msal` : aucun n'est référencé par les labs ; nos versions figées (Azure.Identity 1.21.0, Configuration 10.0.12, MCP 2.2.0, etc.) sont inchangées |

## 3. Tests effectués et résultats

| # | Vérification (manuel §7) | Résultat |
|---|---|---|
| 1–2 | Restauration et graphe | ✅ 38 projets : MAF 1.27.0 (préversion 1.27.0 pour Lab06), `OpenAI` 2.14.0, `Microsoft.Extensions.AI.OpenAI` 10.11.0 (12 projets explicites) ou 10.10.1 ; pas d'`Azure.AI.OpenAI` |
| 3 | Vulnérabilités | ✅ aucune (38/38) |
| 4 | `dotnet build -warnaserror` des 38 projets | ✅ 0 erreur, **0 warning** (les 4 `CS0219` de Lab05/Start signalés en 1.26.0 n'apparaissent plus) |
| 5 | Anciennes API / mentions de 1.26 | ✅ aucune mention de 1.26 dans `LearningLabs/` ; aucun `CS0618` |
| 6 | 19 Solutions — **clé API**, exécutées **par le dashboard** (attentes de `labs.json`) | ✅ **19/19 `passed`**, toutes les attentes. Premier passage de Lab07 : `'AzureOpenAI:EmbeddingDeploymentName' is not configured` (configuration de ma session, pas de la 1.27.0) ; relancé avec le nom de déploiement d'embeddings : 12/12 |
| 7 | 19 Solutions — **Entra ID** (dashboard sans `AzureOpenAI__APIKey`, statut « apiKey: missing / entraId ») | ✅ **19/19 `passed`**, toutes les attentes |
| 9 | 19 Start livrés (CLI) | ✅ exit 0, aucune exception (Lab07 avec le nom de déploiement d'embeddings) ; scénarios écartés car non implémentés, comme attendu |
| 11 | Start = Solution hors `Program.cs` | ✅ mêmes remplacements appliqués aux deux |
| — | `dotnet test` du dashboard | ✅ **670/670** |

Conditions : dashboard lancé en local, isolé (`Dashboard__Reporting__ServerUrl` vide : aucun appel au serveur d'administration ; historique dans un dossier temporaire hors dépôt) ; runs déclenchés par l'API (`POST /api/labs/{id}/runs`, en-tête `X-Lab-Dashboard: 1`), entrées scriptées pour les labs interactifs (Lab09 : `Y` ; Lab12 : ligne vide), serveur compagnon de Lab06 démarré par le dashboard ; stockage de Lab05 et Lab12 en mode **`JsonFile`** (`Storage__Mode`, mode prévu par les labs, sans Docker : les modes MongoDB n'ont pas été rejoués) ; nom du déploiement d'embeddings de Lab07 passé par `AzureOpenAI__EmbeddingDeploymentName`.

Différence de méthode avec la vague 1.26.0 : les attentes sont évaluées par le dashboard lui-même (et non par un script d'expressions régulières), ce qui couvre aussi le découpage par scénario et les tokens.

## 4. Problèmes rencontrés

1. **Script de pilotage** : mon premier passage a traité la réponse `202 Accepted` du démarrage d'un run comme une erreur, puis reçu `409` (run en cours) pour les labs suivants ; corrigé (202 accepté, nouvelle tentative sur 409) avant le passage retenu. Aucun impact sur les labs.
2. **Lab07** : le nom du déploiement d'embeddings n'était pas configuré dans ma session (seule une variable `AzureOpenAI__DefaultEmbeddingDeploymentName`, non lue par le lab, existait) ; message d'erreur clair du lab ; relancé avec la bonne variable.

## 5. Décisions à valider

- [ ] Garder `Microsoft.Extensions.AI.OpenAI` 10.11.0 explicite (12 projets) tant que MAF n'apporte pas 10.11.0 ou plus (recommandé).
- [ ] Rejouer Lab05 et Lab12 en mode MongoDB (Docker) si une validation de ce mode est souhaitée pour cette vague (non fait : mode `JsonFile`).

## 6. Points pour la suite

- Prochaine montée de version : même procédure, désormais entièrement pilotable par l'API du dashboard (script de la vague, à reprendre).
- Tous les labs étant migrés, la **Central Package Management** (`Directory.Packages.props` à la racine) réduirait chaque vague à un seul fichier.
