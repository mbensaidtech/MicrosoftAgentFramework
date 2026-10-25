# Rapport de préparation : labs AzureOpenAI

| | |
|---|---|
| **Date** | 25/10/2026 |
| **Périmètre** | `LearningLabs/AzureOpenAI` : 16 labs (Lab01 à Lab15, le Lab06 étant en deux parties, serveur et client), soit 32 projets `Start` et `Solution`, plus les éléments communs nécessaires pour les suivre : README racine, `CommonUtilities` et dashboard Lab Bench |
| **Version testée** | commit `9c78d6c` (main), **plus 22 fichiers corrigés par cet audit, pas encore commités** (voir [§ 5](#5-problèmes-détectés-et-corrections-effectuées)) |
| **Méthode** | les tests ont été réellement exécutés. Les résultats ci-dessous viennent des exécutions faites ; ce qui n'a pas pu être testé figure au [§ 8](#8-vérifications-impossibles-ou-non-réalisées) |

---

## Conclusion

**Les labs AzureOpenAI sont prêts pour la formation, sous réserve des actions de préparation du [§ 7](#7-blocages-restants-à-résoudre-avant-la-formation)** : 5 sont prioritaires, 2 optionnelles. La première est bloquante : **committer et pousser les corrections de cet audit**, sinon les participants clonent la version qui contient encore les défauts corrigés.

- **Aucun lab n'est « non prêt ».** Tous ont été vérifiés de bout en bout contre Azure OpenAI.
- **11 labs sont « prêts ». 5 sont « prêts sous réserve »** :
  - 4 dépendent d'un élément à préparer en plus d'Azure OpenAI : Docker et MongoDB pour Lab05 et Lab12, le déploiement d'embeddings pour Lab07, le serveur MCP public de Hugging Face pour Lab04 ;
  - 1 a un **échec intermittent dû au modèle**, désormais documenté : le tri du Lab08 fait échouer une vérification du dashboard environ 1 fois sur 6.
- **Lab02 passe en mode strict (option B, validée avec vous)** : son plantage intermittent (1 exécution sur 15) est supprimé. Après la modification : 10/10 en CLI et 2/2 sur le dashboard.
- **Les 32 projets compilent sans erreur ni avertissement**, y compris sur un clone neuf avec un cache NuGet vide.
- **Les 16 Solutions passent toutes leurs vérifications automatiques** du dashboard. Lab02 et Lab08 ont dû être relancés une fois dans la seconde campagne (voir § 3.4).
- **Les 16 exercices**, complétés en suivant uniquement le README et les indices du code, passent **100 % des vérifications**. Ce résultat a été recontrôlé indépendamment sur les sorties sauvegardées.
- **17 corrections ont été apportées.** Deux d'entre elles corrigent des défauts qui auraient pénalisé les participants :
  - **Lab02** : en suivant les indices, un participant obtenait **5/9** sur le dashboard.
  - **Lab11** : l'extrait de code du README faisait échouer une vérification du scénario 3.

  S'y ajoute une erreur de prérequis dans le README racine : « Visual Studio 2022+ », alors que cette version ne sait pas cibler `net10.0`.

---

## 1. Environnement de test

| Élément | Valeur |
|---|---|
| OS | macOS (Darwin 27.0), Apple Silicon |
| .NET | SDK 10.0.402, runtimes Microsoft.NETCore.App et Microsoft.AspNetCore.App 10.0.13 |
| Agent Framework | `Microsoft.Agents.AI.OpenAI` 1.26.0 ; A2A `1.26.0-preview.261023.1` |
| Azure OpenAI | ressource `mbensaid-project-alpha-resource` (AIServices, France Central) |
| Déploiement chat | `gpt-4o-mini-paris-chat` : gpt-4o-mini 2024-07-18, GlobalStandard, capacité 100 |
| Déploiement embeddings | `text-embedding-3-small-paris` : text-embedding-3-small, GlobalStandard, capacité 150 |
| Authentification | clé API (variables `AzureOpenAI__*`), user-secrets et Microsoft Entra ID (`az login`) : les trois ont été testées |
| Docker | conteneurs `lab05-mongodb` (127.0.0.1:27017) et `lab12-mongodb` (127.0.0.1:27018), lancés avec les `docker-compose.yml` des labs |
| Dashboard | Lab Bench lancé avec le reporting désactivé (`Dashboard__Reporting__ServerUrl=""`) et un répertoire de données temporaire, pour ne polluer ni le tableau de bord du formateur ni l'historique local |

Avant les tests, j'ai vérifié les identifiants : `GET /openai/v1/models` répond 200, un appel chat de test répond 200 et un appel embeddings de test répond 200.

---

## 2. Inventaire des labs analysés

Chaque lab contient un `README.md` (instructions du participant), un dossier `Start/` (exercice avec des TODO) et un dossier `Solution/` (référence).

**Tous les TODO sont dans `Start/Program.cs`.** J'ai comparé les deux dossiers avec `diff -r` dans les 16 labs : les autres fichiers sont identiques entre `Start` et `Solution`, et dans `Program.cs` les différences se limitent aux zones des TODO.

| # | Lab | Projet | TODO | Scénarios | Vérifs dashboard | Dépendances en plus d'Azure OpenAI |
|---|---|---|---|---|---|---|
| 01 | Lab01-FirstBasicAIAgent | FirstBasicAIAgent | 1–23 | 6 | 10 | — |
| 02 | Lab02-AIAgentWithSO | AIAgentWithSO | 1–19 | 4 | 9 | — |
| 03 | Lab03-AIAgentWithFunctionTools | AIAgentWithFunctionTools | 1–23 | 4 | 13 | — |
| 04 | Lab04-AIAgentWithMCPClient | AIAgentWithMCPClient | 1–18 | 3 | 9 | Internet : `https://huggingface.co/mcp` (anonyme) |
| 05 | Lab05-AIAgentWithThreads | AIAgentWithThreads | 1–22 | 3 | 11 | Docker, MongoDB sur 27017 |
| 06 | Lab06_A2AServer | A2AServer | 1–12 | 1 | 12 | Port 5000 |
| 06 | Lab06_A2AClient | A2AClient | 1–16 | 4 | 13 | Serveur Lab06 démarré |
| 07 | Lab07-AgenticRAG-VectorStore | AgenticRAG | 1–19 | 4 | 12 | Déploiement d'embeddings |
| 08 | Lab08-DataFormatComparison *(optionnel)* | DataFormatComparison | 1–13 | 2 + comparaison | 11 | — |
| 09 | Lab09-AIAgentWithFunctionToolsHumanApproval | AIAgentWithFunctionToolsHumanApproval | 1–23 | 3 | 13 | Saisie console (Y/N) |
| 10 | Lab10_ExposeAIAgentAsMCPTool *(optionnel)* | ExposeAIAgentAsMCPTool | 1–18 | 3 | 9 | Port 5100 |
| 11 | Lab11-AIAgentWithCustomHttpTransport | AIAgentWithCustomHttpTransport | 1–19 | 3 | 18 | — |
| 12 | Lab12-AIAgentWithAIContextProvider | AIAgentWithAIContextProvider | 1–16 | 2 | 7 | Docker, MongoDB sur 27018, saisie console (chat) |
| 13 | Lab13-AIAgentInAspNetCoreApi | AIAgentWebApi | 1–10 | 4 | 8 | Port 5200 |
| 14 | Lab14-AIAgentEvaluation | AIAgentEvaluation | 1–14 | 3 | 10 | — |
| 15 | Lab15-AIAgentWithSkills | AIAgentWithSkills | 1–14 | 3 | 10 | — |

Fichiers associés vérifiés : `appsettings.json`, les `docker-compose.yml` (Lab05 et Lab12), les données (`Data/sav-faq.json`, `Data/hotels.json`), les skills (`skills/**/SKILL.md`, `references/limits.md`) et `CommonUtilities` (`AzureOpenAIEndpoint`, `ColoredConsole`, `ConsoleSpinner`).

**Tous les fichiers nécessaires sont versionnés.** J'ai comparé les fichiers présents sur disque avec ceux de `git ls-files` : aucun fichier utile n'est ignoré ni non suivi.

---

## 3. Tests exécutés et résultats

### 3.1 Compilation

| Test | Résultat |
|---|---|
| `dotnet build` des 32 projets (Start et Solution) | ✅ 32/32, 0 erreur, 0 avertissement |
| `dotnet build --no-incremental` des 32 projets, pour faire remonter tous les avertissements | ✅ 0 erreur, 0 avertissement |
| **Clone neuf** (`git clone`) et compilation des 32 projets avec un **cache NuGet vide** (`NUGET_PACKAGES` temporaire) | ✅ 32/32, 0 avertissement. Taille des packages téléchargés : **337 Mo** |
| Recompilation complète (`--no-incremental`) **après les corrections** | ✅ 32/32, 0 erreur, 0 avertissement |
| Parité `Start`/`Solution` après les corrections (`diff -rq`) | ✅ seul `Program.cs` diffère |

### 3.2 Packages NuGet

- **Source unique : nuget.org.** Aucun `nuget.config` ni flux privé.
- **Les 15 packages directs** existent sur nuget.org dans la version référencée, sont listés et ne sont pas dépréciés : `Microsoft.Agents.AI.OpenAI` 1.26.0, `Microsoft.Extensions.AI.OpenAI` 10.11.0, `Azure.Identity` 1.21.0, `ModelContextProtocol(.AspNetCore)` 2.2.0, `MongoDB.Driver` 3.12.0, `OpenTelemetry` 1.20.1, etc.
- **Seuls les deux packages A2A sont en préversion** (`1.26.0-preview.261023.1`) ; le README l'indique.
- **`dotnet list package --vulnerable --include-transitive`** : aucune vulnérabilité, ni directe ni transitive, dans les 16 labs.

### 3.3 Tests automatisés existants

Les labs n'ont pas de projet de test. Le seul projet de test du dépôt concerne le dashboard.

| Test | Résultat |
|---|---|
| `dotnet test` (dans `Dashboard/`) | ✅ **218/218 réussis**, 0 échec. Il y a des avertissements d'analyseur xUnit (xUnit1051, xUnit2029), sans incidence |

### 3.4 Exécution réelle des Solutions (via le dashboard, qui applique les vérifications de `labs.json`)

Deux campagnes complètes ont été lancées : la première au début de l'audit, la seconde après toutes les corrections. Chaque exécution fait un `dotnet build`, puis un `dotnet run --no-build` contre Azure OpenAI ; le dashboard applique ensuite les regex de `labs.json` à la sortie. Lab09 a reçu `Y` sur son entrée interactive ; Lab12 a reçu un message de chat suivi d'une ligne vide.

| Lab | Vérifs | Campagne 1 | Campagne 2 (après corrections) | Durée d'exécution (campagne 2) |
|---|---|---|---|---|
| Lab01 | 10 | ✅ 10/10 | ✅ 10/10 | 9,8 s |
| Lab02 | 9 | ✅ 9/9 | ❌ **plantage (code 134)** au scénario 3 : le modèle a renvoyé `headChefName` au lieu de `chefName` (`JsonException`, propriété requise manquante). **Relance : ✅ 9/9** | 8,3 s |
| Lab03 | 13 | ✅ 13/13 | ✅ 13/13 | 12,4 s |
| Lab04 | 9 | ✅ 9/9 | ✅ 9/9 | 9,3 s |
| Lab05 | 11 | ✅ 11/11 | ✅ 11/11 | 8,2 s |
| Lab06 serveur (avec le client comme compagnon) | 12 | ✅ 12/12 | ✅ 12/12 | 21,0 s |
| Lab06 client (avec le serveur comme compagnon) | 13 | ✅ 13/13 | ✅ 13/13 | 19,7 s |
| Lab07 | 12 | ❌ arrêt explicite : `AzureOpenAI:EmbeddingDeploymentName` manquait **dans mon environnement** (voir § 3.7). Après ajout de la variable : ✅ 12/12 | ✅ 12/12 | 6,1 s |
| Lab08 | 11 | ✅ 11/11 | ❌ 10/11 : `scenario2-csv` échoue, car le modèle a placé un hôtel à 35 USD avant celui à 25 USD (tri imparfait ; le code est correct). **Deux relances : ✅ 11/11** | 20,5 s |
| Lab09 | 13 | ✅ 13/13 | ✅ 13/13 | 6,2 s |
| Lab10 | 9 | ✅ 9/9 | ✅ 9/9 | 23,9 s |
| Lab11 | 18 | ✅ 18/18 | ✅ 18/18 | 9,5 s |
| Lab12 | 7 | ✅ 7/7 | ✅ 7/7 | 9,3 s |
| Lab13 | 8 | ✅ 8/8 | ✅ 8/8 | 5,3 s |
| Lab14 | 10 | ✅ 10/10 | ✅ 10/10 | 28,1 s |
| Lab15 | 10 | ✅ 10/10 | ✅ 10/10 | 11,1 s |

**Les 16 Solutions fonctionnent.** Il existait cependant **deux sources d'échec intermittent, dues au modèle et non au code**. Je les ai mesurées sur toutes les exécutions observées : dashboard, exercices complétés et 10 exécutions supplémentaires du Lab02 en CLI.

- **Lab02, scénarios 3 et 4 (1 exécution sur 15)** :
  - en mode JSON schema non strict, le modèle peut renommer une propriété requise ; le programme s'arrête alors sur une exception non gérée ;
  - **le mode strict élimine le problème** : je l'ai testé dans une copie temporaire en ajoutant `AdditionalProperties = new() { ["strict"] = true }` aux `ChatOptions` du scénario 3 ;
  - résultat : 5 exécutions sur 5 réussies, schéma exactement respecté, sans même la propriété `description` en trop ;
  - **corrigé (option B)** : le mode strict est maintenant activé aux scénarios 3 et 4 (voir la correction n° 17 au [§ 5](#5-problèmes-détectés-et-corrections-effectuées)).
- **Lab08, vérifications *cheapest hotel first* (1 exécution sur 6)** : le tri fait par le modèle n'est pas toujours parfait. Seul le verdict du dashboard est concerné.

Dans les deux cas, le symptôme est documenté dans le README (Troubleshooting).

### 3.5 Exécution des `Start` livrés tels quels (sans compléter les TODO)

J'ai lancé les deux mêmes campagnes sur les projets `Start/` tels qu'ils sont livrés aux participants.

| Résultat (identique dans les 2 campagnes) | Détail |
|---|---|
| ✅ **16/16 compilent** et **16/16 se terminent avec le code 0**, sans plantage | Chaque programme lit la configuration, puis affiche les en-têtes de scénario sans contenu tant que les TODO ne sont pas faits |
| ✅ Les vérifications échouent, comme prévu | Seule la vérification `config` passe (2/8 pour Lab13, dont l'API démarre déjà) ; c'est le comportement annoncé par le README (*« The delivered exercise fails them until its TODOs are completed »*) |
| ℹ️ Lab06 serveur | Le serveur livré s'arrête tout de suite, car le démarrage de l'hôte est un TODO ; le dashboard l'indique (*« exited with code 0 before it was ready »*) |
| ℹ️ Lab13 | Le Start non complété répond `404` à tous les scénarios, comme l'annonce le README (« until TODO 9 ») |

### 3.6 Parcours du participant : exercices complétés en suivant uniquement les README

Pour chaque lab, voici ce qui a été fait :
1. copie du `Start` dans un dossier temporaire ignoré par git ;
2. complétion des TODO **en suivant uniquement le README et les indices du code**, sans recopier la Solution ;
3. compilation, puis exécution réelle contre Azure OpenAI ;
4. contrôle de la sortie avec les vérifications de `labs.json`.

J'ai ensuite **recontrôlé moi-même toutes les sorties sauvegardées** avec un portage du décodeur de console du dashboard : sortie standard seule, trames du spinner (texte suivi d'un `\r` nu) retirées, regex appliquées en mode Multiline.

| Lab | Vérifs (exercice complété) | Recontrôle indépendant | Le README suffisait-il ? | Commentaire |
|---|---|---|---|---|
| 01 | 10/10 | ✅ 10/10 | Oui | Les libellés d'affichage n'étaient que dans l'Expected Output (corrigé) |
| 02 | **5/9 au départ**, puis 9/9 | ✅ 9/9 | **Non avant correction** | Les libellés exigés par les vérifications manquaient dans les indices (corrigé) |
| 03 | 13/13 | ✅ 13/13 | Oui | — |
| 04 | 9/9 | ✅ 9/9 | Oui | Serveur MCP Hugging Face joignable, 4 outils |
| 05 | 11/11 | ✅ 11/11 | Oui | Commande `mongosh` du README vérifiée |
| 06 serveur | 12/12 | ✅ 12/12 | Oui | **Testé sur le port par défaut 5000** malgré AirPlay (voir § 3.8) |
| 06 client | 13/13 (2 exécutions) | ✅ 13/13 | Oui | — |
| 07 | 12/12 | ✅ 12/12 | Oui | Classement des recherches identique sur 2 exécutions |
| 08 | 11/11 (2 exécutions) | ✅ 11/11 | Oui | Les valeurs attendues du README étaient fausses pour gpt-4o-mini (corrigé) |
| 09 | 13/13 avec `Y` ; 12/13 avec `N` (attendu) | ✅ 13/13 | Presque | Indice du TODO 21 incomplet (corrigé) |
| 10 | 9/9 | ✅ 9/9 | Oui | Modes `--stdio` et `--http`, MCP Inspector en `--cli` |
| 11 | 18/18 | ✅ 18/18 | Oui | Extrait de `SendAsync` incomplet (corrigé) |
| 12 | 7/7 | ✅ 7/7 | Oui | Persistance d'une exécution à l'autre vérifiée |
| 13 | 8/8 | ✅ 8/8 | Oui | Mode `--serve` testé avec `curl -N` (SSE) |
| 14 | 10/10 | ✅ 10/10 | Oui | Durée d'exécution d'environ 41 s |
| 15 | 10/10 | ✅ 10/10 | Oui | Skills bien copiées dans `bin/` |

### 3.7 Configuration et authentification

Tests faits sur le Lab01. La configuration commune `ConfigurationHelper` est la même dans tous les labs.

| Cas | Résultat |
|---|---|
| Aucune configuration : placeholders `YOUR-…`, ni user-secrets ni variables | ⚠️ Arrêt avec le message explicite *"'AzureOpenAI:Endpoint' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set …', or with the environment variable …"*. Il s'affiche sous forme d'exception non gérée, avec la pile d'appels et le code 134 : bruyant mais compréhensible |
| **User-secrets seuls**, avec les commandes `dotnet user-secrets set` du README dans un `HOME` temporaire pour ne pas toucher aux secrets réels | ✅ code 0, les 6 scénarios s'exécutent |
| **Variables d'environnement** `AzureOpenAI__*` | ✅ c'est le mode utilisé par toutes les exécutions de l'audit |
| **Microsoft Entra ID** : sans clé API, avec `az login` (`DefaultAzureCredential`) | ✅ code 0, les scénarios s'exécutent |
| Lab07 sans `AzureOpenAI:EmbeddingDeploymentName` | ⚠️ Arrêt explicite *"'AzureOpenAI:EmbeddingDeploymentName' is not configured"*. **Le formulaire Azure du dashboard ne gère pas cette clé**, comme l'indique le README du dashboard |
| API `GET /api/settings/azure-openai` du dashboard | ✅ source effective de chaque valeur et mode d'authentification ; la clé n'est jamais renvoyée |

### 3.8 Infrastructure et cas particuliers

| Vérification | Résultat |
|---|---|
| MongoDB : `docker compose up -d` avec les fichiers de Lab05 et Lab12 | ✅ conteneurs démarrés (27017 et 27018 sur 127.0.0.1), `docker compose config -q` OK |
| Lab05 et Lab12 sans MongoDB joignable | ✅ message *"MongoDB is not reachable…"* après environ 5 s, conforme au Troubleshooting |
| **Lab06 sur macOS, port 5000** : AirPlay (ControlCenter) écoute sur `*:5000` | ✅ Kestrel se lie quand même à `127.0.0.1:5000` et `[::1]:5000` ; les `curl` d'agent card du README répondent 200 ; le client passe 13/13. Serveur arrêté : le client reçoit `403 Forbidden` (réponse d'AirPlay), comme le décrit le Troubleshooting |
| Lab10 : modes `--stdio` (stdout réservé au JSON-RPC), `--http`, MCP Inspector `--cli` (stdio et HTTP), port occupé | ✅ conforme au README, après correction de la phrase sur l'environnement du serveur stdio |
| Lab13 : `--serve` et `curl -N` (SSE, 401, 400 problem+json, 404 sur un id invalide, isolation Alice/Bob), port occupé | ✅ conforme au README |
| Lab09 interactif : `Y` puis `N` sur stdin | ✅ `Y` : approuvé et exécuté ; `N` : *"Function call rejected by user"*, l'agent le dit |
| Lab12 interactif : fin du chat par ligne vide et par `exit` | ✅ |
| Serveur MCP Hugging Face (Lab04) en anonyme | ✅ `initialize` répond 200 ; outils `hf_whoami`, `hub_repo_search`, `hub_repo_details`, `hf_fs` |
| Liens externes des README | ✅ tous répondent 200 après correction (un lien du Lab07 renvoyait 404) |
| Interface du dashboard dans un navigateur (Playwright) | ✅ la page se charge : 17 labs listés, statut « Azure OpenAI · API key », fenêtre d'accueil ; **aucune erreur ni avertissement dans la console** |
| Admin dashboard du formateur : `GET https://labs-admin-dash.vercel.app/api/v1/health` | ✅ `{"status":"ok","db":"ok"}` |

### 3.9 Consommation et capacité Azure OpenAI

Tokens affichés par les Solutions lors d'une exécution complète, par lab : environ 0,3 k (Lab01) ; 1,3 k (Lab02) ; 4,1 k (Lab03) ; **7,3 k (Lab04)** ; 0,3 k (Lab05) ; 1,1 k (Lab06) ; 1,4 k (Lab07) ; **8,3 k (Lab08)** ; 1,9 k (Lab09) ; 0,7 k (Lab10) ; 0,5 k (Lab11) ; 0,3 k (Lab12).

**Total : environ 27 k tokens.** Lab13, Lab14 et Lab15 n'affichent pas leur consommation, donc ils ne sont pas comptés ; Lab14 fait de nombreux appels (juge LLM, répétitions).

Le déploiement chat a une capacité de **100 unités GlobalStandard, soit 100 000 tokens par minute**. Les ordres de grandeur à retenir pour le jour J sont au [§ 7](#7-blocages-restants-à-résoudre-avant-la-formation).

---

## 4. État de chaque lab

| Lab | État | Justification |
|---|---|---|
| Lab01 – First Basic AI Agent | ✅ **Prêt** | Solution et exercice complété à 10/10 ; clé API, user-secrets et Entra ID testés |
| Lab02 – Structured Output | ✅ **Prêt** (après corrections) | 9/9 ; sans la correction des indices, un participant obtenait 5/9. Le plantage intermittent des scénarios 3 et 4 (non strict) est supprimé par le mode strict : 10/10 en CLI, 2/2 sur le dashboard. Le scénario 2 (`RunAsync<T>`) reste non strict : risque théorique, jamais observé ([§ 6](#6-problèmes-signalés-et-non-corrigés)) |
| Lab03 – Function Tools | ✅ **Prêt** | 13/13, README suffisant |
| Lab04 – MCP Client | 🟡 **Prêt sous réserve** | 9/9, mais le lab dépend du serveur public `huggingface.co/mcp`, utilisé en anonyme : disponibilité, limites de débit d'une salle derrière une même IP, et nom de l'outil `hub_repo_search` |
| Lab05 – Sessions | 🟡 **Prêt sous réserve** | 11/11 ; le scénario 3 exige Docker et le conteneur MongoDB sur le poste de chaque participant |
| Lab06 – A2A Server | ✅ **Prêt** | 12/12, port 5000 vérifié sur macOS |
| Lab06 – A2A Client | ✅ **Prêt** | 13/13 contre le serveur démarré au port 5000 |
| Lab07 – Agentic RAG | 🟡 **Prêt sous réserve** | 12/12, mais il faut fournir un déploiement d'embeddings et le configurer hors du formulaire du dashboard |
| Lab08 – Data Format Comparison *(optionnel)* | 🟡 **Prêt sous réserve** | 11/11, valeurs attendues du README corrigées. Le verdict du dashboard échoue parfois (1 exécution sur 6) parce que le modèle trie mal la liste, alors que le code est correct : c'est maintenant indiqué dans le Troubleshooting |
| Lab09 – Human Approval and Guardrails | ✅ **Prêt** | 13/13 (Y), comportement N conforme |
| Lab10 – Expose an Agent as MCP Tool *(optionnel)* | ✅ **Prêt** | 9/9, modes stdio et HTTP testés |
| Lab11 – HTTP Transport and OpenTelemetry | ✅ **Prêt** (après correction) | 18/18, extrait `SendAsync` corrigé |
| Lab12 – AI Context Provider | 🟡 **Prêt sous réserve** | 7/7 ; exige Docker et le conteneur MongoDB (port 27018) sur chaque poste |
| Lab13 – Agent in an ASP.NET Core API | ✅ **Prêt** | 8/8, mode `--serve` testé |
| Lab14 – Evaluate an Agent | ✅ **Prêt** | 10/10 ; c'est le lab le plus long (environ 26 à 41 s) |
| Lab15 – Agent Skills | ✅ **Prêt** | 10/10 |

**Synthèse : 11 prêts, 5 prêts sous réserve, 0 non prêt.**

---

## 5. Problèmes détectés et corrections effectuées

Gravités :
- **Majeur** : un participant qui suit les instructions obtient un échec, ou se retrouve bloqué.
- **Mineur** : information inexacte ou confuse, sans blocage.

Après chaque correction, la compilation du Start livré a été vérifiée, puis l'exercice complété ou la Solution ont été réexécutés quand c'était pertinent ([§ 3.1](#31-compilation), [§ 3.4](#34-exécution-réelle-des-solutions-via-le-dashboard-qui-applique-les-vérifications-de-labsjson)).

| # | Gravité | Lab / fichier | Problème | Correction | Vérification |
|---|---|---|---|---|---|
| 1 | **Majeur** | Lab02 `README.md`, `Start/Program.cs` | Les libellés exigés par les vérifications du dashboard n'apparaissaient que dans l'Expected Output, pas dans les indices : `Successfully parsed structured response:`, `Structured response:`, `JSON response:`, `Deserialized response:`. **En suivant les indices, un participant obtenait 5/9** | Libellés ajoutés aux indices des TODO 6, 10, 14 et 19 (README et commentaires du Start), ainsi que le message `Failed to parse JSON:` cité par le Troubleshooting | Exercice complété : 5/9 avant, **9/9** après |
| 2 | **Majeur** | Lab11 `README.md` | L'extrait « Complete `SendAsync` » omettait le bloc fourni `if (!WriteTraffic) { … }`. Un participant qui recopie la méthode entière réaffiche le log HTTP au scénario 3, ce qui fait échouer la vérification `scenario3-no-http-log` (déduit du regex, non exécuté) | Bloc ajouté ; l'extrait est maintenant identique à la Solution | 18/18 |
| 3 | **Majeur** | README racine, prérequis | « Visual Studio 2022+ », alors que **VS 2022 ne sait pas cibler `net10.0`** : il faut Visual Studio 2026 (18.0+) ([doc Microsoft](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)) | « Visual Studio Code (C# Dev Kit), Visual Studio 2026 (18.0+) or Rider », avec une mention explicite pour VS 2022 | Relecture |
| 4 | Mineur | Lab01 `README.md`, `Start/Program.cs` | Les libellés `Input tokens: …` et `Response id (stored by the service): …`, exigés par les vérifications, n'étaient pas dans les indices des TODO 18 et 23 | Ajoutés aux indices | 10/10 |
| 5 | Mineur | Lab09 `README.md`, `Start/Program.cs` | L'indice du TODO 21 utilisait `maskedResponse` sans la définir et sans donner le format `Agent: …` attendu par la vérification `scenario3-answer` ; le commentaire du Start écrivait `[response]` | Code complet donné sur le modèle du TODO 20 ; commentaire aligné | 13/13 |
| 6 | Mineur | Lab05 `README.md`, `Start/Program.cs` | Renvois de TODO erronés (TODO 17 → « agent of TODO 11 » au lieu de 10 ; Troubleshooting « TODO 9, 15, 22 » et « TODO 11, 17 »), séparateur manquant avant le scénario 3, ligne d'espace de noms en double | Numéros corrigés (10 ; 9, 22 ; 10, 17), séparateur ajouté, lignes fusionnées | 11/11 |
| 7 | Mineur | Lab07 `README.md` | Expected Output du scénario 2 obsolète (faq-010 0.5xxx, faq-004, faq-005) ; mesuré : faq-010 0.3953, faq-005, faq-012, identique sur 2 exécutions. Lien `microsoft-extensions-vectordata-overview` en 404 | Expected Output mis à jour, avec la mention du modèle d'embeddings ; lien remplacé par `dotnet/ai/vector-stores/overview` (200) | 12/12 |
| 8 | Mineur | Lab08 `README.md` | Valeurs attendues fausses pour gpt-4o-mini : « 43 hotels » au lieu de 35 à 37 mesurés, ratios « half / a third » au lieu de -66 % en entrée, et ordres de grandeur des tokens | « up to 43 hotels », ratios et ordres de grandeur conformes aux mesures | 11/11 sur 2 exécutions |
| 9 | Mineur | Lab10 `README.md` (étape 5, optionnelle) | Le README affirmait que le serveur stdio hérite de l'environnement de l'hôte. C'est faux avec MCP Inspector, qui le lance avec un environnement minimal : le serveur s'arrête alors avec *'AzureOpenAI:Endpoint' is not configured* | Phrase corrigée, en indiquant où déclarer les variables (Inspector, `-e`, `"env"` de `mcp.json`) | Inspector `--cli` OK avec `-e` |
| 10 | Mineur | Lab12 `Start/Program.cs`, `Start/MemoryConsole.cs`, **`Solution/Program.cs`, `Solution/MemoryConsole.cs`** | Les commentaires disaient que les mémoires sont ajoutées « aux instructions », ce que contredisent le code et la section Security du README (elles sont ajoutées dans un message, jamais dans les instructions) | 3 commentaires corrigés dans le Start et les 4 équivalents dans la Solution, pour garder Start et Solution identiques hors TODO | Compilation des deux projets ; Solution 7/7 |
| 11 | Mineur | Lab12 `README.md` | Expected Output « Input tokens: 119 », alors que le texte du README et la mesure donnent 132. Liens vers les samples sur `tree/main` au lieu du tag de la version utilisée | 132/103/235 ; liens figés sur `tree/dotnet-1.26.0` | Liens : 200 |
| 12 | Mineur | Lab06 serveur, `Start/AgentCards.cs` et `Solution/AgentCards.cs` (commentaire) | « clients prefer HTTP+JSON and fall back to JSON-RPC » est inexact. D'après la doc du SDK A2A 1.0.0-preview3, la version utilisée, l'ordre de `SupportedInterfaces` est respecté et la première entrée est préférée ; ici c'est JSON-RPC, ce que le README client observe | Commentaire corrigé dans le Start et la Solution | Compilation ; Solution 12/12 et 13/13 |
| 13 | Mineur | Lab06 client `README.md` | Le README disait que la nouvelle conversation génère une autre clé « and calls it valid » ; observé deux fois : « not valid » | Formulation neutre | — |
| 14 | **Majeur** | Lab02 `README.md` | Le README affirmait qu'en mode non strict, le modèle peut seulement *ajouter* une propriété, sans conséquence. Observé : il peut aussi en **renommer** une (`headChefName`), d'où une `JsonException` non gérée et l'arrêt du programme avant le scénario 4 (1 exécution sur 15) | Note complétée ; nouvelle ligne de Troubleshooting (relancer, ou activer le mode strict) | Mode strict testé dans une copie temporaire : 5 sur 5 |
| 15 | Mineur | Lab08 `README.md` | Rien n'indiquait que le tri fait par le modèle peut être imparfait. Les vérifications *cheapest hotel first* du dashboard échouent alors alors que le code est correct (observé 1 fois sur 6) | Phrase sur la variabilité complétée ; ligne de Troubleshooting ajoutée | Relances : 11/11 |
| 16 | Mineur | README racine | Le tableau *Common Utilities* citait `MongoDbHealthCheck`, supprimé depuis, et omettait `AzureOpenAIEndpoint`. La section reporting disait que `ServerUrl` est vide par défaut et que le participant doit le renseigner : il est en fait fixé dans `appsettings.json`, et une clé API d'admin est aussi nécessaire | Tableau et section reporting corrigés, conformément au README du dashboard | Relecture |
| 17 | **Majeur** | Lab02 `Solution/Program.cs`, `Start/Program.cs`, `README.md`, README racine | **Option B, décidée avec vous** : le mode strict est activé aux scénarios 3 et 4 pour supprimer le plantage intermittent (correction 14). Les changements : `AdditionalProperties = new() { ["strict"] = true }` dans les `ChatOptions` (scénario 3) et dans les `AgentRunOptions` (scénario 4) de la Solution ; indices des TODO 12 et 17 dans le Start et le README ; *What You Will Learn*, *Key Concepts*, note, Troubleshooting et *Going Further* (mode strict pour `RunAsync<T>`) ; description des scénarios dans le README racine | Requêtes HTTP observées : `"strict": true` est bien envoyé aux scénarios 3 et 4 (scénario 4 via `AgentRunOptions.AdditionalProperties`, vérifié contre un témoin sans l'option). Solution : 10/10 en CLI, sans propriété hors schéma (contre 10/10 avec `description` avant) ; dashboard : 9/9 deux fois. Start : compile, code 0 |

**Fichiers modifiés (22) :**
- README racine ;
- `README.md` des Lab01, 02, 05, 06 client, 07, 08, 09, 10, 11 et 12 ;
- `Start/Program.cs` des Lab01, 02, 05, 09 et 12 ;
- `Start/MemoryConsole.cs` et `Solution/{Program,MemoryConsole}.cs` du Lab12 ;
- `Solution/Program.cs` du Lab02 ;
- `{Start,Solution}/AgentCards.cs` du Lab06 serveur.

Toutes les modifications portent sur des textes, des indices ou des commentaires, **sauf une, décidée avec vous** : le mode strict dans la Solution du Lab02 (correction 17). `Migration/Lab-Migration-Prompt.md` était déjà modifié avant l'audit ; je n'y ai pas touché.

---

## 6. Problèmes signalés et non corrigés

Ces problèmes sont mineurs, ou leur correction sort du périmètre « Start et README ». Aucun ne bloque un participant.

| Lab / fichier | Constat | Pourquoi non corrigé |
|---|---|---|
| `labs.json`, Lab02 `scenario3` et `scenario4` | La regex `\{[^\n]*\}` exige un JSON sur une seule ligne. Si le modèle renvoie du JSON indenté, la vérification échoue alors que le code est correct (gpt-4o-mini passe) | Fragilité liée au modèle ; à surveiller si vous utilisez un autre modèle |
| Lab02, scénario 2 (`RunAsync<T>`) | Le schéma part toujours en mode non strict (vérifié dans la requête HTTP) : le même renommage de propriété est théoriquement possible, mais il n'a jamais été observé sur ce scénario | Hors de l'option B décidée (scénarios 3 et 4) ; le README indique en *Going Further* comment l'activer (`options: new AgentRunOptions { AdditionalProperties = new() { ["strict"] = true } }`, testé) |
| `labs.json`, Lab03 `scenario3` | L'id de la notification et son message doivent figurer sur la même ligne de la réponse libre du modèle | Idem |
| Lab04 | Les vérifications et le README supposent que l'outil `hub_repo_search` existe | Dépendance externe ; le Troubleshooting la mentionne déjà |
| Lab07 `README.md`, indice du TODO 15 | `" \| "` dans un tableau Markdown : le rendu est correct, mais copier le Markdown brut donne du C# invalide (CS1009) | Correct en rendu GitHub et dans le dashboard |
| Lab09 `README.md` | L'Expected Output du scénario 3 montre des nombres réels au lieu de `xxx` ; séparateur `---` manquant avant le scénario 3 | Cosmétique |
| Lab12 `README.md` | Les séparateurs de l'Expected Output font 40 tirets, contre 79 dans la sortie réelle | Cosmétique |
| Lab14 `README.md` (Going Further) | `Microsoft.Extensions.AI.Evaluation.Safety` est cité sans préciser qu'il est en préversion | Hors parcours |
| Lab15 `{Start,Solution}/SkillsConsole.cs` | `<summary>` mal placé (deux résumés consécutifs) ; ligne `;` isolée dans `Solution/Program.cs` | Cosmétique ; à corriger dans les deux à la fois |
| Tous les labs, `ConfigurationHelper` | Sans configuration, le message est clair mais sort en exception non gérée, avec la pile d'appels | Comportement de conception, commun à tous les labs |
| Dashboard, tests | Avertissements d'analyseur xUnit (xUnit1051, xUnit2029) | Sans incidence ; hors périmètre |
| `Lab02-AIAgentWithSO/_StartCompleted/` | Reste local antérieur à l'audit (`obj/` du 27/09), ignoré par git | Local uniquement ; le dashboard le supprime avec *Reset exercises* |

---

## 7. Blocages restants à résoudre avant la formation

Aucun défaut des labs ne reste bloquant. Il reste des **actions de préparation**, par ordre de priorité.

1. **🔴 Committer et pousser les corrections** (22 fichiers, non commités à ce jour).
   - Sans cela, les participants clonent la version d'origine : Lab02 à 5/9 et sans mode strict, extrait Lab11 incomplet, prérequis VS 2022 erroné.
   - **C'est le seul vrai blocage.**
2. **🟠 Accès Azure OpenAI des participants.** À communiquer à l'avance : l'endpoint, le **nom du déploiement chat**, une clé API (ou le rôle *Cognitive Services OpenAI User* pour Entra ID) et le **nom du déploiement d'embeddings** pour le Lab07.
   - Ce dernier se configure avec `dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "<déploiement>"`.
   - Le formulaire du dashboard ne gère pas cette clé.
3. **🟠 Capacité Azure si la ressource est partagée.**
   - La capacité est de 100 000 tokens/min pour `gpt-4o-mini-paris-chat`.
   - Une exécution du Lab08 consomme environ 8,3 k tokens, celle du Lab04 environ 7,3 k.
   - **Une douzaine de participants qui lancent le Lab08 dans la même minute dépassent la capacité.** Le service répond alors en 429 : le SDK réessaie, d'où des ralentissements, voire des échecs.
   - Avant le jour J : augmenter la capacité, ou répartir les participants sur plusieurs déploiements.
   - Ce risque est **déduit des mesures, pas testé en charge**.
4. **🟠 Postes des participants** (à faire vérifier avant la session) :
   - .NET 10 SDK, et un IDE compatible `net10.0` (pas VS 2022) ;
   - **Docker**, et `docker pull mongo:8.0` pour Lab05 et Lab12 ;
   - **pré-restauration des packages NuGet (337 Mo)** : un `dotnet build` des labs avec une bonne connexion ;
   - accès à nuget.org et à `huggingface.co` (Lab04) ;
   - ports 5000, 5100, 5200, 27017 et 27018 libres. Sur macOS, AirPlay sur le port 5000 ne gêne pas (vérifié).
5. **🟠 Prévenir les formateurs et les participants des échecs intermittents dus au modèle.** Ils ne viennent pas du code ; la consigne est de **relancer**.
   - **Lab08** : verdict *cheapest hotel first* (tri imparfait).
   - **Lab06 client** : le modèle peut mal expliquer pourquoi une clé est rejetée.
   - **Vérifications fragiles** du Lab02 (JSON indenté) et du Lab03 (id et message sur la même ligne), si vous changez de modèle.
6. **🟢 Optionnel, reporting vers l'admin dashboard.**
   - Le serveur répond (`health` OK).
   - Pour que les participants remontent leur progression, il faut leur donner la **clé de workshop** et la **clé API d'admin**. Cette dernière vient de `appsettings.Local.json`, ignoré par git, ou de `LABS_ADMIN_API_KEY`.
   - Sans la clé API, le reporting reste désactivé (« API key missing ») ; les labs fonctionnent quand même.
7. **🟢 Recommandé : une répétition sur Windows**, si des participants sont sous Windows ([§ 8](#8-vérifications-impossibles-ou-non-réalisées)).

---

## 8. Vérifications impossibles ou non réalisées

| Vérification | Raison |
|---|---|
| Exécution sous **Windows** (PowerShell, chemins, Docker Desktop) et **Linux** | Seul un poste macOS était disponible. Les commandes PowerShell du README n'ont pas été exécutées |
| **Visual Studio 2026 / Rider** : ouverture et exécution des projets dans l'IDE | Non disponibles ; seuls le CLI `dotnet` et le dashboard ont été utilisés |
| **Test de charge** : N participants simultanés sur le même déploiement | Pas de moyen de simuler une salle. Le risque est estimé à partir des mesures ([§ 7](#7-blocages-restants-à-résoudre-avant-la-formation), point 3) |
| Entra ID sur les **15 autres labs** | Testé sur Lab01 seulement. Le code d'authentification (`BearerTokenPolicy` + `DefaultAzureCredential`) est commun |
| Commande `dotnet user-secrets set` dans le magasin **réel** de l'utilisateur | Volontairement non fait pour ne pas modifier vos secrets ; testée à l'identique dans un `HOME` temporaire |
| Formulaire Azure OpenAI du dashboard (enregistrement) | Non soumis, pour ne pas écrire dans vos user-secrets ; l'API de lecture a été testée, et les tests automatisés couvrent l'écriture |
| Clic **Run** dans l'interface et saisie dans la zone d'entrée | L'interface a été chargée dans un navigateur ; les exécutions sont passées par l'API HTTP du dashboard, le même backend, y compris la saisie interactive des Lab09 et Lab12 |
| `docker compose down/up` à froid | Les conteneurs existaient déjà (arrêtés) et ont été relancés avec les fichiers des labs ; pas de premier téléchargement de l'image |
| Lab04 avec un jeton Hugging Face | Seul le mode anonyme a été testé |
| Lab10 : interface graphique de MCP Inspector, `mcp.json` de VS Code / Copilot | Seul le mode `--cli` d'Inspector a été testé |
| Lab13 : `Ctrl+C` en terminal interactif | Non reproductible dans un shell non interactif ; l'arrêt par SIGTERM a été vérifié |
| Sections *Going Further* et exercices facultatifs (par exemple la *poisoned FAQ entry* du Lab07) | Hors du parcours principal |
| Labs `MultiAgentSystem` | Hors périmètre (demande limitée à `AzureOpenAI`) |

---

## 9. Annexes

**Commandes principales utilisées :**

```bash
# Compilation
dotnet build <Lab>/<Start|Solution>/<Projet>.csproj [--no-incremental]
# Tests du dashboard
cd Dashboard && dotnet test
# Vulnérabilités
dotnet list <projet> package --vulnerable --include-transitive
# Exécutions via le dashboard (reporting désactivé, données temporaires)
Dashboard__Reporting__ServerUrl="" Dashboard__DataDirectory=<tmp> dotnet run   # dans Dashboard/LabDashboard
POST /api/labs/<id>/runs {"target":"solution"|"start"}  puis  GET /api/runs/<runId>/events (SSE)
# Exercices complétés (copie temporaire, supprimée ensuite)
dotnet build <Lab>/.tmp-start-check/<Projet>.csproj && dotnet run --no-build
```

**Les sorties brutes** (exécutions du dashboard, exercices complétés, journaux de compilation) se trouvent dans le scratchpad temporaire de la session. Elles ne sont pas versionnées et disparaîtront avec lui.

**Nettoyage :**
- les copies temporaires `.tmp-start-check` sont supprimées, sans `obj/` recréé ;
- aucun processus de lab ne tourne ;
- les conteneurs MongoDB et le dashboard sont arrêtés à la fin de l'audit.
