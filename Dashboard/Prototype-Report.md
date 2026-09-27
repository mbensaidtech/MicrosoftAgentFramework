# Rapport — prototype du dashboard local (Lab01)

> Date : 2026-09-26 · Périmètre : `Lab01-FirstBasicAIAgent` uniquement · Documentation technique : [README.md](README.md)

## 1. Faisabilité : ce qu'une page HTML peut faire seule

| # | Opération | HTML/JS seul (ouvert en `file://` ou servi statiquement) | Pourquoi |
|---|---|---|---|
| 1 | Lancer un processus .NET | ❌ Impossible | Le navigateur exécute la page dans un bac à sable : aucune API web ne permet de créer un processus. C'est une garantie de sécurité volontaire (une page web ne doit pas exécuter de programmes) |
| 2 | Exécuter un exercice | ❌ | conséquence de 1 |
| 3 | Lire stdout / stderr | ❌ | pas de processus, donc pas de flux |
| 4 | Logs en temps réel | ❌ | idem |
| 5 | Code de retour | ❌ | idem |
| 6 | Afficher un résultat | ✅ si un résultat lui est fourni | rendu DOM |
| 7 | Tokens | ❌ | ils sont dans la sortie du programme |
| 8 | Détecter succès / échec | ❌ | idem |

La File System Access API permet au mieux de **lire** des fichiers choisis par l'utilisateur, jamais d'exécuter.
Tout contournement (extension de navigateur, `ms-*`/protocoles personnalisés, ActiveX…) serait fragile ou dangereux : exclu.

**Conclusion : il faut un processus local qui lance `dotnet` et parle au navigateur.**

## 2. Architectures comparées

| Critère | A — HTML/JS seul | **B — HTML + petit runner local (retenue)** | C1 — Extension VS Code | C2 — App de bureau (Electron/MAUI) | C3 — Aspire Dashboard / Test Explorer |
|---|---|---|---|---|---|
| Lance réellement les exercices | ❌ | ✅ | ✅ | ✅ | ⚠️ partiel (logs/traces, pas de parcours pédagogique) |
| Logs en direct | ❌ | ✅ SSE | ✅ | ✅ | ✅ |
| Complexité | très faible | faible (1 projet ASP.NET Core, 1 dépendance) | moyenne (API VS Code, packaging `.vsix`) | élevée (runtime embarqué, packaging) | moyenne (AppHost par lab, modification des labs) |
| Dépendances | 0 | .NET 10 (déjà requis) + Markdig | Node + toolchain VS Code | Node/Chromium ou MAUI | Aspire + adaptation des labs |
| Sécurité | maximale | bonne si verrouillé (voir §4) | bonne | bonne | bonne |
| Maintenance | — | même stack que le lab (C#) | seconde stack (TypeScript) | lourde | liée à Aspire |
| Expérience développeur | ❌ ne répond pas au besoin | un `dotnet run`, un onglet de navigateur | très bonne mais impose VS Code | installation lourde | orientée observabilité, pas apprentissage |
| Respect « labs inchangés » | ✅ | ✅ | ✅ | ✅ | ❌ (projets à orchestrer) |

**Choix : B.** C'est la seule option qui lance réellement les exercices tout en restant dans la stack du lab (.NET) :
aucune seconde chaîne d'outils, aucun cloud, un seul package tiers (Markdig, pour le README). Le runner utilise uniquement
des API natives de .NET 10 : minimal API, `TypedResults.ServerSentEvents`, `Process`.

## 3. Fonctionnement

1. `cd Dashboard/LabDashboard && dotnet run` → serveur sur `http://127.0.0.1:5057`.
2. Le navigateur charge l'UI statique (`wwwroot/`, HTML/CSS/JS natifs, aucun CDN).
3. **Run** → `POST /api/labs/{id}/runs` ; le runner exécute :
   - `dotnet build <projet> -nologo -v minimal` (phase *Build*) ;
   - `dotnet run --project <projet> --no-build` (phase *Run*) — la commande de l'apprenant ;
   - l'analyse de la sortie (phase *Checks*).
4. Le navigateur suit `GET /api/runs/{id}/events` (Server-Sent Events) : événements `phase`, `output`, `progress`, `result`.
   Le serveur garde tous les événements du run : un navigateur rechargé **rejoue** le run puis continue en direct.
5. Le résultat est ajouté à `Dashboard/.data/history.json` (git-ignoré, écriture atomique, 30 runs par lab).

### Récupération des logs

- stdout et stderr sont lus en parallèle, caractère par caractère, par `ConsoleStreamDecoder`, qui applique la sémantique d'un terminal :
  - `\n` / `\r\n` termine une ligne ;
  - texte suivi d'un `\r` seul = ligne réécrite (le `ConsoleSpinner` de CommonUtilities) → événement `progress` (affiché comme ligne d'état « ⠋ Running agent… [00:02] », limité à 4/s) au lieu de 70 lignes parasites ;
  - texte sans fin de ligne (réponse en streaming) → transmis **immédiatement**. Mesuré : 92 fragments pour le scénario 5, affichés au fil de l'eau.
- Les lignes complètes servent à l'analyse et à l'historique (2 000 lignes max).

### Récupération des tokens

- **Source unique : ce que l'exercice imprime** (`Input tokens: 56`, `Output tokens: …`, `Reasoning tokens (…)`, `Total tokens: …`), regroupé par bloc (`Token Usage`, `Token Usage (streaming)`).
- Aucune valeur n'est inventée : si rien n'est imprimé (ex. Start non complété), l'UI affiche **« Token usage: Not available »**.
- L'UI précise que le total ne couvre que les appels dont l'usage est imprimé (dans Lab01, les scénarios 1 à 3 n'impriment pas le leur) et qu'aucun coût n'est estimé (pas de grille tarifaire fiable).

### Détection du succès

`Passed` = build OK **et** code de retour 0 **et** toutes les vérifications du lab satisfaites.
Le code de retour seul ne suffit pas : le Start livré se termine avec 0 alors qu'il ne fait rien (mesuré).
Les vérifications de Lab01 (7 expressions régulières dans `labs.json`) contrôlent que la configuration est chargée, que
chaque scénario a produit une réponse, et que l'usage des tokens est affiché (scénarios 4 et 5).
`Failed` précise l'étape : `build`, `run` (code ≠ 0, avec le message d'exception) ou `checks`.

## 4. Sécurité

| Risque | Mesure |
|---|---|
| Accès réseau | écoute sur `127.0.0.1` uniquement (vérifié : `lsof` → `127.0.0.1:5057 (LISTEN)`) |
| Exécution arbitraire | liste blanche `labs.json` ; chemins résolus et confinés au dépôt ; `ArgumentList` sans shell |
| Site malveillant déclenchant un run (CSRF) | `POST` exige l'en-tête `X-Lab-Dashboard: 1` (force un preflight CORS jamais accordé) et une origine identique |
| DNS rebinding | en-tête `Host` différent de `127.0.0.1`/`localhost` → 400 |
| Contenu du README | rendu serveur avec HTML brut désactivé ; liens `javascript:` neutralisés |
| Secrets | aucun stocké par le dashboard ; les labs lisent leurs user-secrets/variables comme en CLI |

## 5. Fichiers ajoutés

```
Dashboard/
├── README.md                      documentation technique
├── Prototype-Report.md            ce rapport
├── Dashboard.slnx                 solution (dotnet build / dotnet test)
├── global.json                    active Microsoft.Testing.Platform pour dotnet test (.NET 10)
├── LabDashboard/
│   ├── LabDashboard.csproj        Microsoft.NET.Sdk.Web, net10.0, Markdig 1.4.0
│   ├── Program.cs                 hôte, garde de sécurité, API
│   ├── DashboardOptions.cs        options (port, racine, historique)
│   ├── appsettings.json
│   ├── labs.json                  catalogue (Lab01 + ses 7 vérifications)
│   ├── Catalog/LabCatalog.cs      liste blanche, validation des chemins et des regex
│   ├── Execution/ConsoleStreamDecoder.cs  sémantique terminal (\r, streaming)
│   ├── Execution/ProcessRunner.cs         processus sans shell, kill de l'arbre
│   ├── Execution/LabRunner.cs             build → run → checks, un run à la fois, timeout
│   ├── Execution/LabRun.cs                journal d'événements rejouable
│   ├── Execution/OutputAnalyzer.cs        checks, tokens, diagnostics de build
│   ├── Execution/RunModels.cs
│   ├── History/RunHistoryStore.cs         JSON local, écriture atomique
│   └── wwwroot/ (index.html, css/dashboard.css, js/dashboard.js)
└── LabDashboard.Tests/            13 tests xUnit v3
```

Modifié : `.gitignore` (`Dashboard/.data/`), `README.md` racine (section courte). **Aucun fichier de lab modifié.**

## 6. Tests réalisés

| # | Test | Résultat |
|---|---|---|
| 1 | Build `Dashboard.slnx` | ✅ 0 erreur, 0 warning |
| 2 | Tests unitaires (`dotnet test`) | ✅ 13/13 |
| 3 | Écoute réseau | ✅ `127.0.0.1:5057` uniquement |
| 4 | `Host: evil.example` | ✅ 400 |
| 5 | `POST` sans en-tête / avec `Origin` étrangère | ✅ 403 / 403 |
| 6 | Lab inconnu (`../../etc`) | ✅ 404 |
| 7 | Run Solution via API (clé API réelle) | ✅ Passed, 7/7 checks, build 0,6 s, run 7,6 s, tokens 96 + 148 = 244, 2 warnings de build hérités remontés |
| 8 | Deuxième run pendant un run | ✅ 409 |
| 9 | Run Start livré (navigateur) | ✅ Failed à l'étape `checks`, 6/7 checks KO nommés, « Token usage: Not available », progression « In progress » |
| 10 | Run Solution (navigateur, thème sombre) | ✅ lignes affichées progressivement (34 → 82 pendant le run), indicateur de progression pendant les appels au modèle, Passed |
| 11 | Annulation après 3 s | ✅ `cancelled`, aucun processus orphelin |
| 12 | Configuration absente (2ᵉ instance sans variables Azure) | ✅ Failed étape `run`, code 134, message d'exception exact du lab en résumé |
| 13 | Rechargement de la page | ✅ dernier run rejoué depuis l'historique (log, étapes, résultat) |
| 14 | Onglets | ✅ README rendu (11 tableaux, 8 blocs de code, 0 script), historique, À propos (packages, commandes CLI), solution masquée puis affichée (`Solution/Program.cs`) |
| 15 | Console navigateur | ✅ 0 erreur, 0 warning |
| 16 | **Parité CLI** : `dotnet run --project Solution` vs run du dashboard | ✅ même structure de sortie (16/16 lignes-repères : en-têtes, blocs de tokens, configuration) ; les réponses du modèle varient d'un run à l'autre |
| 17 | Labs inchangés | ✅ 0 fichier de Lab01 modifié depuis la création du dashboard, 0 référence au dashboard dans les labs |

Non testé en réel : un **échec de build** (le catalogue n'autorise que des projets qui compilent). La branche est couverte par
le test unitaire du parseur de diagnostics et par la revue du code.

## 7. Limitations

- **Tokens** : seulement ceux que l'exercice imprime, donc un total partiel (scénarios 4 et 5 dans Lab01). Pas d'estimation de coût.
- **Couleurs** perdues : .NET n'émet pas les couleurs de `ColoredConsole` quand la sortie est redirigée.
- **Un run à la fois** : volontaire, les labs partagent `CommonUtilities` et des builds parallèles entreraient en conflit.
- **Pas d'entrée interactive** : stdin est fermé (aucun lab actuel n'en a besoin ; un futur lab « chat » en aurait besoin).
- **Vérifications déclaratives** : les regex vérifient la *forme* de la sortie (chaque scénario a répondu), pas la *justesse* de la réponse d'un LLM, par nature non déterministe.
- Le dashboard dépend de la sortie console des labs : si un lab change ses titres de scénarios, ses vérifications dans `labs.json` doivent suivre.

## 8. Améliorations avant généralisation

1. **Usage complet des tokens sans modifier les labs** : le SDK OpenAI .NET publie la métrique OpenTelemetry `gen_ai.client.token.usage` (expérimentale). Le runner pourrait la collecter via EventPipe (`Microsoft.Diagnostics.NETCore.Client`) pour obtenir le total réel de tous les appels. À évaluer : complexité et statut expérimental.
2. **Convention de vérification commune** : ajouter aux labs migrés des titres de scénarios uniformes (déjà le cas) et écrire les vérifications de chaque lab en même temps que sa migration (checklist du plan).
3. **Catalogue généré** : dériver `labs.json` des README (titre, objectifs) pour éviter les doublons, en gardant les vérifications explicites.
4. **Labs web** (Lab06 A2A, Lab10 MCP) : ce sont des serveurs longue durée, avec un client et un serveur. Il faudra un mode « service » (démarrer / arrêter, lien vers l'URL) distinct du mode « run jusqu'à la fin ».
5. **Labs avec prérequis** (MongoDB pour Lab05/12) : afficher et vérifier les prérequis (Docker actif, conteneur démarré) avant le run.
6. **Diff Start ↔ Solution** dans l'onglet Solution (aujourd'hui : fichiers complets de la solution qui diffèrent).
7. Si l'équipe le souhaite : lancer le navigateur automatiquement au démarrage (option `--open`).

---

## 9. Addendum (2026-09-26) — refonte UI, thèmes et bilinguisme

**Référence visuelle** : `disco-architecture.html`. Repris : système de tokens (neutres froids, un seul accent bleu, couleurs sémantiques avec fonds teintés), typographie système 14 px à interlettrage serré, panneaux plats à bordure 1 px (rayon 10 px), barre d'en-tête de panneau (titre en petites capitales + actions), bande d'étapes numérotées en monospace avec indicateur coloré de 2 px, contrôles segmentés, bouton de thème, toast, `kbd`, `prefers-reduced-motion`. **Non repris** : la structure de la page (sections de schémas SVG) ; l'organisation du dashboard (rail des labs, onglets, terminal) est conservée.

**Thèmes** : clair / sombre ; la préférence système s'applique tant que l'utilisateur n'a pas choisi ; le choix est enregistré dans le navigateur et appliqué avant le premier rendu (pas de flash).

**Langues** : anglais / français (`EN | FR`), 126 clés par langue, aucune clé manquante (vérifié par script). Traduits : navigation, onglets, boutons, statuts, étapes, titres du terminal, verdicts, **résumés** (recomposés côté client à partir des données structurées du run, le résumé serveur restant en anglais pour l'API), vérifications, tokens, historique, À propos, solution, toasts, messages d'erreur, formats de date et de nombre (`7,1 s`). Textes des labs : bloc `translations.fr` de `labs.json` (Lab01 traduit). README : `README.fr.md` servi s'il existe, sinon README anglais avec une notice.

**Ce qui reste en anglais, volontairement** : la sortie des programmes (y compris l'indicateur « Running agent… » du `ConsoleSpinner` et les libellés « Token Usage » des rapports, qui sont imprimés par l'exercice) et le README de Lab01 (pas de version française à ce jour).

**Fichiers modifiés** : `wwwroot/index.html`, `wwwroot/css/dashboard.css`, `wwwroot/js/dashboard.js`, **nouveau** `wwwroot/js/i18n.js` ; backend : `Catalog/LabCatalog.cs` (traductions, `LocalizedReadmePath`), `Program.cs` (`?lang=`, `readmeLanguage`, `timeoutSeconds`, `translations`), `labs.json` (traduction FR de Lab01). Aucun fichier de lab modifié.

**Tests** : build 0 warning ; **17/17 tests** (4 nouveaux : sécurité du paramètre `lang`) ; navigateur : clair/EN, sombre/FR, bascule en direct avec conservation du terminal, run réel en FR (titre « En cours · Solution de référence », toast, annulation traduite, verdict « Réussi »), notice README, verrou solution, historique traduit, mobile 390 px sans débordement horizontal, console 0 erreur / 0 avertissement.
