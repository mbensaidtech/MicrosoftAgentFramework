# Rapport de montée de version — 13 labs AzureOpenAI, 1.22.0 → 1.26.0

> Date : 2026-10-25 · Cible : **Microsoft Agent Framework 1.26.0** (stable, publiée le 2026-10-23) · **.NET 10** (`net10.0`)
> Références : [Migration-Manual.md](Migration-Manual.md) (§1, §3 bis, §6, §7, §8), [Migration-Plan.md](Migration-Plan.md), plan d'action [`AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md`](../AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md) (étape 2, action A4)

**Statut : fait.** Les 13 labs AzureOpenAI (Start et Solution) sont en 1.26.0. Aucune API utilisée par les labs n'a changé : la montée se limite aux versions de paquets, à la documentation et à une correction d'affichage de Lab07 (culture), découverte pendant les tests. Hors périmètre : les 3 labs MultiAgentSystem, toujours non migrés.

---

## 1. Changements réalisés

| Fichier | Changement | Pourquoi |
|---|---|---|
| 26 `.csproj` (Start et Solution des 13 labs) | `Microsoft.Agents.AI.OpenAI` 1.22.0 → **1.26.0** | Dernière version stable |
| `Lab06_A2AClient/*/A2AClient.csproj`, `Lab06_A2AServer/*/A2AServer.csproj` | `Microsoft.Agents.AI.A2A` / `.Hosting.A2A.AspNetCore` → **1.26.0-preview.261023.1** (SDK `A2A` 1.0.0-preview3) | Préversion alignée sur la version cible (aucune version stable n'existe) |
| `Lab01`, `Lab02`, `Lab06_A2AServer` (`.csproj`) | `Microsoft.Extensions.AI.OpenAI` 10.11.0 **conservé**, commentaire mis à jour | MAF 1.26.0 n'apporte que 10.10.1, sans le correctif du streaming (manuel §8) |
| `Lab07-AgenticRAG-VectorStore/*/RagConsole.cs` | Score de la recherche vectorielle écrit avec `CultureInfo.InvariantCulture` | Sur un poste en culture française, le score s'affichait `0,3953` : le check `scenario2-*` du dashboard et le README attendent `0.3953` (règle du manuel §8 « Culture du poste ») |
| 13 `README.md` des labs | Colonne « Now (1.26.0) » des tableaux « Coming from an older version » ; Lab06 : préversions 1.26.0 et SDK `A2A` 1.0.0-preview3 ; Lab01/Lab02/Lab06 serveur : commentaire MEAI ; Lab11 : `User-Agent` réel et liens vers `dotnet-1.26.0` | Cohérence avec les `.csproj` |
| `README.md` racine | Version des labs AzureOpenAI (1.26.0) ; préversions A2A alignées sur 1.26.0 | Idem |
| `Migration-Manual.md` | §1 (versions cibles), **§3 bis** (montée 1.22.0 → 1.26.0), §4.17 (`User-Agent`), §6 règle 1, §7 critère 2, §8 (cadence de release) | Référence commune |
| `Migration-Plan.md` | Version figée, checklist | Idem |

## 2. Changements des notes de version pris en compte

Voir le tableau du manuel §3 bis. En résumé :
- **Approbations (Lab09)** : liaison des réponses aux demandes renforcée (#8641, #8692), rejet automatique des demandes sans réponse (#9152), rejet des identifiants dupliqués (#9542) → aucun changement de code, comportement vérifié.
- **Outils (Lab03, Lab09)** : meilleure prise en charge des changements d'outils entre deux exécutions (#8754) → aucun changement de code.
- **A2A (Lab06)** : SDK 1.0.0-preview3 (#9386) → aucun changement de code ; la liaison utilisée par le client reste JSON-RPC (ordre des interfaces de la carte), vérifié dans les journaux du serveur.
- **Sans effet sur les labs** : #8200 et #8403 (workflows déclaratifs), #8801 (`AgentSessionStore`, non utilisé).

## 3. Tests effectués et résultats

| # | Vérification (manuel §7) | Résultat |
|---|---|---|
| 1–2 | Restauration et graphe de dépendances | ✅ MAF 1.26.0 dans les 13 labs ; `OpenAI` 2.14.0 ; pas d'`Azure.AI.OpenAI` |
| 3 | Vulnérabilités | ✅ aucune |
| 4 | Build des 26 projets | ✅ 0 erreur ; 0 warning sauf 4 `CS0219` dans `Lab05/Start` (constantes non utilisées tant que les TODO sont vides), **déjà présents en 1.22.0** (vérifié en compilant le commit précédent) |
| 5 | Anciennes API | ✅ aucun changement de code ; aucun warning `CS0618` (obsolète) |
| 6 | Exécution des 13 Solutions — **clé API** | ✅ toutes en exit 0. Premier passage : 124/127 attentes du dashboard → Lab07 (culture, corrigé, puis 12/12) et Lab08 `scenario2-csv` (le modèle a mal trié le CSV ; 11/11 sur les deux passages suivants) |
| 7 | Exécution des 13 Solutions — **Entra ID** | ✅ **127/127** |
| 9 | Start livrés | ✅ les 13 s'exécutent (exit 0, en-têtes de scénarios, aucune exception) |
| 11 | Start = Solution hors `Program.cs` | ✅ `.csproj` identiques ; `RagConsole.cs` identique |
| — | `dotnet test` du dashboard | ✅ 207/207 |

Conditions : Solutions exécutées depuis leur dossier (`dotnet bin/Debug/net10.0/<projet>.dll`), entrées scriptées pour les labs interactifs (Lab09 : `Y` ; Lab12 : message vide), Lab06 serveur + client sur le port 5071 comme dans le dashboard, MongoDB de Lab05 et Lab12 démarrés pour les tests puis arrêtés. Les sorties ont été vérifiées avec les expressions régulières de `labs.json` (pas depuis l'interface du dashboard).

## 4. Problèmes rencontrés

1. **Lab07 — affichage dépendant de la culture** (existant avant la montée de version) : `{result.Score:F4}` suit la culture du poste. Les tests d'exécution de Lab07 n'avaient pas pu être joués lors de sa migration (identifiants Azure indisponibles) : le défaut n'était pas apparu. Corrigé avec `string.Create(CultureInfo.InvariantCulture, …)`, comme Lab08.
2. **Lab08 — variabilité du modèle** : 1 exécution sur 4 a renvoyé un CSV mal trié (un hôtel à 35 USD avant celui à 25 USD). C'est le comportement que le lab enseigne (un format demandé seulement dans les instructions n'est pas garanti) ; le check `scenario2-csv` du dashboard peut donc échouer occasionnellement.

## 5. Décisions à valider

- [ ] Garder `Microsoft.Extensions.AI.OpenAI` 10.11.0 explicite dans Lab01, Lab02 et Lab06_A2AServer tant que MAF n'apporte pas 10.11.0 ou plus (recommandé).
- [ ] Lab08 : accepter l'échec occasionnel de `scenario2-csv`, ou assouplir ce check (par exemple ne vérifier que l'en-tête CSV et le nombre de lignes).
- [ ] Lab05/Start : laisser les 4 `CS0219` (constantes utilisées par les TODO) ou les supprimer en déplaçant les constantes dans les indices du README.

## 6. Points pour la suite

- Prochaine montée de version : même procédure (une vague, tous les labs, clé API et Entra ID, attentes du dashboard) ; garder la version figée entre deux vagues (manuel §8).
- Étape 3 du plan d'action : enrichissements des labs existants (A5 à A10).
