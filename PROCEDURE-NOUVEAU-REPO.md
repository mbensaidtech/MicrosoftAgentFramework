# Déplacer les labs vers un nouveau dépôt GitHub

Procédure mise à jour le 06/11/2026. Les numéros de ligne cités correspondent au commit `18d414b` de `main` (= `origin/main`). Ils ne servent que de repère : **chercher toujours le texte cité**, pas le numéro de ligne.

**But** : créer un nouveau dépôt qui ne contient que ce dont les développeurs participants ont besoin pour suivre les labs : les labs, `CommonUtilities`, le Lab Bench et la publication du catalogue. Il ne contient ni les rapports de migration, ni les specs, ni les audits, ni les plans de travail internes.

**État de départ**
- Dépôt actuel : `mbensaidtech/MicrosoftAgentFramework`, branche `main` au commit `18d414b`.
- Microsoft Agent Framework **1.27.0** pour les 19 labs.
- `Dashboard/LabDashboard/labs.json` : **19 labs**, soit 16 AzureOpenAI et 3 MultiAgentSystem (Lab01, Lab02, Lab03). Tous sont migrés.
- Jeton `CATALOG_PUBLISH_TOKEN` : configuré dans Vercel (production de `labs-admin-dash`) et dans les secrets de l'ancien dépôt. **Ni Vercel ni GitHub ne peuvent réafficher sa valeur.**

---

## Répartition des rôles (à lire en premier si tu es un agent IA)

| Qui | Étapes |
|---|---|
| **Humain** (propriétaire du compte GitHub) | 0, 1, 2 (réglages GitHub, jeton), validation avant le push (6), 7, 8, 10 |
| **Agent IA** | 3 (export et suppressions), 4 (corrections), 5 (vérifications), 6 (commit local et dry-run), 9 (copie des données locales) |

Règles pour l'agent :
- **Ne jamais modifier l'ancien dossier** `$OLD`, sauf `git switch main` et `git pull --ff-only`. Tout le travail se fait dans `$NEW`.
- **Ne pas pousser** (`git push`) sans le feu vert explicite de l'humain. Le push publie le dépôt et déclenche la publication du catalogue sur l'admin.
- **Ne jamais afficher ni écrire le jeton** `CATALOG_PUBLISH_TOKEN` dans un fichier versionné, un log ou une réponse. Pas de `set -x` avec le script de publication.
- **Ne jamais renommer l'`id` d'un lab** dans `labs.json` (voir la section 11).
- **S'arrêter et demander** dans les cas suivants : un `grep` de vérification renvoie un résultat non prévu dans cette procédure, `dotnet test` échoue, le dry-run n'annonce pas 19 labs, ou un fichier local (`bin/`, `obj/`, `.data/`, `appsettings.Local.json`) apparaît dans `git status`.
- Lancer `dotnet test` **depuis `Dashboard/`**. Son `global.json` sélectionne Microsoft.Testing.Platform, et depuis un autre dossier le SDK 10 refuse la commande.

## Résumé (checklist)

- [ ] 0. **Humain** : retrouver le jeton de publication. Il n'est plus dans `/private/tmp`. S'il est perdu, voir l'[annexe](#annexe--régénérer-le-jeton).
- [ ] 1. **Humain** : créer le nouveau dépôt GitHub, **vide**.
- [ ] 2. **Humain** : y ajouter la variable `ADMIN_DASHBOARD_URL` et le secret `CATALOG_PUBLISH_TOKEN`, **avant** le premier push.
- [ ] 3. **Agent** : exporter `main` sans historique, puis supprimer les fichiers internes.
- [ ] 4. **Agent** : corriger les liens vers les fichiers supprimés et les mentions « migrated / not migrated » devenues fausses.
- [ ] 5. **Agent** : vérifier les références restantes, lancer `dotnet test` et le dry-run du catalogue.
- [ ] 6. **Agent** : premier commit local. **Humain** : relecture, puis push.
- [ ] 7. **Humain** : vérifier l'exécution **Publish lab catalog**. Résultat attendu : `200 unchanged`, ou `201 published`.
- [ ] 8. **Humain** : sur l'ancien dépôt, supprimer le secret et la variable, désactiver le workflow, puis archiver le dépôt ou le passer en privé.
- [ ] 9. **Agent ou humain** : copier `Dashboard/.data` et `appsettings.Local.json`, puis basculer sur le nouveau dossier.
- [ ] 10. **Humain** : envoyer la nouvelle URL de clonage aux participants.

---

## 0. Le jeton de publication

L'ancien fichier temporaire `/private/tmp/claude-501/…/catalog-publish-token.txt` n'existe plus.

- Si tu as copié le jeton dans ton gestionnaire de mots de passe, il suffit pour l'étape 2.
- Sinon, **régénère-le** : voir l'[annexe](#annexe--régénérer-le-jeton). L'ancien jeton cesse alors de fonctionner, y compris dans l'ancien dépôt, ce qui va dans le sens de l'étape 8.

## 1. Principe

Le nouveau dépôt part d'un **export propre de `main`, sans historique** (`git archive`). Les rapports de migration, les specs et les audits ne figurent donc **pas non plus dans l'historique** visible par les participants.

`git archive` n'exporte que les fichiers versionnés. Rien de local ne part, en particulier :
- `bin/`, `obj/` ;
- `Dashboard/.data/` (historique des exécutions, identité de reporting) ;
- `Dashboard/LabDashboard/appsettings.Local.json` (clé d'API admin éventuelle) ;
- `_StartCompleted/`, `.tmp-*/` ;
- `.playwright-mcp/`, `Dashboard/TestResults/` ;
- `graphify-out/`, `disco-architecture.html`, `.claude/`.

> Pourquoi pas un simple `git clone` suivi de `git rm` ? Les fichiers supprimés resteraient lisibles dans l'historique du nouveau dépôt. L'historique complet reste disponible dans l'ancien dépôt, qui est archivé à l'étape 8.

## 2. Ce qu'il ne faut PAS mettre dans le nouveau dépôt

### À supprimer

| Chemin | Contenu | Raison |
|---|---|---|
| `Migration/` (24 fichiers) | Rapports de migration Lab01→Lab12 et MultiAgentSystem Lab02/Lab03, plan, manuel, prompt de migration, rapports d'étapes, montées en 1.26.0 et 1.27.0 | Travail interne |
| `specs/` (8 fichiers) | Specs du Lab Bench et de l'admin : suivi de progression, réglages Azure OpenAI, reset, saisie interactive, parcours, configuration des modèles, évaluation et skills, textes fournis des agents | Conception interne |
| `AGENT_FRAMEWORK_LABS_ACTION_PLAN.md` | Plan d'action v1 | Interne |
| `AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md` | Plan d'action v2 | Interne |
| `AGENT_FRAMEWORK_LABS_AUDIT.md` | Audit des labs | Interne |
| `AZUREOPENAI_LABS_READINESS_REPORT.md` | Rapport de préparation des labs Azure OpenAI | Interne |
| `Dashboard/Prototype-Report.md` | Rapport du prototype du Lab Bench | Interne |
| `PROCEDURE-NOUVEAU-REPO.md` | Ce fichier | Interne |

### À garder : tout le reste

| Chemin | Pourquoi |
|---|---|
| `LearningLabs/AzureOpenAI/` (16 labs) et `LearningLabs/MultiAgentSystem/` (Lab01, Lab02, Lab03) | Les labs : `Start/`, `Solution/`, `README.md`. **Les 19 labs sont migrés en 1.27.0 et référencés par `labs.json` : n'en retirer aucun.** |
| `CommonUtilities/` | Référencé par les labs |
| `Dashboard/` (y compris `LabDashboard.Tests/` et `LabInputHook/`), sauf `Prototype-Report.md` | Le Lab Bench. Les tests vérifient `labs.json` et les checks quand un lab change |
| `.github/` | Publication automatique du catalogue vers l'admin (workflow et script) |
| `README.md`, `.gitignore` | — |

> L'ancienne version de cette procédure proposait de retirer MultiAgentSystem Lab02 et Lab03, alors non migrés. **Ce n'est plus le cas** : ils sont migrés (commits `ce686b4`, `aa3c1ef`, `63b74d3`) et présents dans `labs.json`. Les retirer casserait le Lab Bench et les tests.

## 3. Créer le nouveau dépôt et ses réglages (GitHub, humain)

1. **New repository** : choisis le propriétaire, le nom et la visibilité. Choisis *Public* si les participants clonent sans être invités.
   **Ne coche rien** : pas de README, pas de `.gitignore`, pas de licence. Le dépôt doit être vide.
2. **Tout de suite, avant le premier push**, ouvre dans le nouveau dépôt **Settings → Secrets and variables → Actions** (section *Security*, le menu se déplie).
   - Onglet **Variables**, partie **Repository variables**, bouton **New repository variable** :
     - Name : `ADMIN_DASHBOARD_URL`
     - Value : `https://labs-admin-dash.vercel.app`, sans `/` final.
   - Onglet **Secrets**, partie **Repository secrets**, bouton **New repository secret** :
     - Name : `CATALOG_PUBLISH_TOKEN`
     - Secret : le jeton de l'étape 0, soit 64 caractères hexadécimaux. Ce n'est **pas** la clé d'API admin, ni un code d'invitation.
   - Liens directs, en remplaçant `<owner>/<repo>` :
     - `https://github.com/<owner>/<repo>/settings/variables/actions/new`
     - `https://github.com/<owner>/<repo>/settings/secrets/actions/new`
   - ⚠️ N'utilise **pas** les parties *Environment variables* et *Environment secrets*, qui demandent de créer un environnement. N'utilise pas non plus les pages *Codespaces* ou *Dependabot* : le workflow ne les lit pas.
3. **Settings → Actions → General** : vérifie que *Allow all actions* est sélectionné. C'est le réglage par défaut, à contrôler si le propriétaire est une organisation. Le workflow utilise `actions/checkout@v7`.

## 4. Préparer le contenu propre (poste, agent)

### 4.a Export et suppressions

L'humain fournit `NEW`, le nom du dossier :

```bash
OLD=~/Documents/01_Development/AI/MicrosoftAgentFramework
NEW=~/Documents/01_Development/AI/<nouveau-dossier>

# Seul PROCEDURE-NOUVEAU-REPO.md peut apparaître ; tout autre fichier : s'arrêter et demander.
# (git archive exporte le contenu commité de main, jamais le dossier de travail.)
git -C "$OLD" status --short | grep -v 'PROCEDURE-NOUVEAU-REPO.md'
git -C "$OLD" switch main && git -C "$OLD" pull --ff-only
git -C "$OLD" log -1 --oneline        # noter le commit exporté (18d414b ou plus récent)

[ -e "$NEW" ] && { echo "STOP : $NEW existe déjà"; exit 1; }
mkdir "$NEW"
git -C "$OLD" archive --format=tar main | tar -x -C "$NEW"
cd "$NEW"

rm -rf Migration specs \
  AGENT_FRAMEWORK_LABS_ACTION_PLAN.md AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md \
  AGENT_FRAMEWORK_LABS_AUDIT.md AZUREOPENAI_LABS_READINESS_REPORT.md \
  Dashboard/Prototype-Report.md PROCEDURE-NOUVEAU-REPO.md
```

Si `main` a avancé depuis `18d414b`, relance d'abord `git -C "$OLD" ls-files | grep -vE '^(LearningLabs|CommonUtilities|Dashboard|\.github)/'` et compare le résultat avec le tableau de la section 2. Un nouveau fichier à la racine doit être classé : à garder ou à supprimer.

### 4.b Liens vers les fichiers supprimés (obligatoire)

Sans ces corrections, des liens restent cassés.

| Fichier | Ligne (repère) | Texte actuel | Modification |
|---|---|---|---|
| `README.md` | 50 | `…target the stable release of Microsoft Agent Framework **1.27.0** (migrated from the previews to 1.22.0, then upgraded to 1.26.0 on 2026-10-25 and to 1.27.0 on 2026-11-05). The 3 **MultiAgentSystem** labs (…) target 1.27.0 too. See [Migration/Migration-Plan.md](Migration/Migration-Plan.md).` | Supprimer la parenthèse d'historique et la phrase `See [Migration/…]`. Résultat : `> **Agent Framework version:** the 16 **AzureOpenAI** labs and the 3 **MultiAgentSystem** labs (*Delegate a Task to Another Agent*, *Sequential Orchestration*, *Concurrent Orchestration*) target the stable release of Microsoft Agent Framework **1.27.0**.` |
| `README.md` | 276 | `The API contract between the two applications is described in [specs/admin-dashboard-progress-tracking/spec.md](…).` | Supprimer la phrase |
| `Dashboard/README.md` | 135 | `(full specification: [specs/dashboard-model-configuration/spec.md](../specs/dashboard-model-configuration/spec.md))` | Supprimer la parenthèse et garder le `:` final |
| `Dashboard/README.md` | 203 | `summarized in the [spec](../specs/dashboard-model-configuration/spec.md#admin-api-contract-v1)` | Remplacer par `summarized below` |
| `Dashboard/LabDashboard/Reset/LabResetService.cs` | 10 | commentaire `(specs/dashboard-exercise-reset)` | Supprimer la parenthèse (sans effet sur le build) |
| `Dashboard/LabDashboard/TrainerModels/AdminModelConfigs.cs` | 10 | commentaire citant `specs/dashboard-model-configuration/spec.md` | Supprimer la référence (sans effet sur le build) |

### 4.c Mentions « migrated » devenues fausses (recommandé)

Les 19 labs sont migrés : la distinction « migrated / not migrated » n'a plus de sens pour un participant, et certaines phrases sont désormais fausses.

| Fichier | Ligne (repère) | Texte actuel | Modification |
|---|---|---|---|
| `README.md` | 51 | `> Only the migrated labs are available in the dashboard; every lab runs with the CLI.` | `> Every lab is available in the dashboard and runs with the CLI.` |
| `README.md` | 66 | `quick feedback on the migrated labs` | `quick feedback on the labs` |
| `README.md` | 75 | `shared by all migrated labs` | `shared by all labs` |
| `README.md` | 88 | `from any migrated lab project folder` | `from any lab project folder` |
| `README.md` | 99 | `a migrated lab stops with` | `a lab stops with` |
| `README.md` | 144 | `a migrated client cannot call an old v0.3 server` | `an A2A v1 client cannot call an A2A v0.3 server` |
| `README.md` | 260 | `- Only the migrated labs are listed (AzureOpenAI Lab01–Lab15, MultiAgentSystem Lab01–Lab03), in the order of the [learning path](#learning-path): run the other labs with the CLI.` | `- Every lab is listed (AzureOpenAI Lab01–Lab15, MultiAgentSystem Lab01–Lab03), in the order of the [learning path](#learning-path).` |
| `README.md` | 654-655 | `> Migrated labs also read **user secrets**…` et `(run from any migrated lab project folder)` | `> The labs also read **user secrets**…` et `(run from any lab project folder)` |
| `Dashboard/README.md` | 6 | `> Prototype: registered labs are \`Lab01-…\` … \`Lab12-AIAgentWithAIContextProvider\` (the migrated labs).` (liste incomplète : 13 labs sur 19) | `> Every lab of \`LearningLabs/\` is registered in \`LabDashboard/labs.json\` (19 labs).` |
| `Dashboard/README.md` | 159 | `from any migrated lab folder` | `from any lab folder` |
| `Dashboard/README.md` | 539 | `…follow the convention since their migration (…)` | `…follow the convention too (…)` |
| `Dashboard/LabDashboard/wwwroot/js/i18n.js` | 19 | `"rail.note": "Only the migrated labs are listed. Every lab still runs from the command line with dotnet run."` | `"rail.note": "Every lab also runs from the command line with dotnet run."` |
| `Dashboard/LabDashboard/wwwroot/js/i18n.js` | 694 | `"rail.note": "Seuls les labs migrés sont listés. Chaque lab reste exécutable en ligne de commande avec dotnet run."` | `"rail.note": "Chaque lab est aussi exécutable en ligne de commande avec dotnet run."` |
| `LearningLabs/MultiAgentSystem/Lab02_OrchestrationSequential/README.md` | 464 | `concurrent (\`BuildConcurrent\`, Lab 03 of this folder, not migrated yet)` | `concurrent (\`BuildConcurrent\`, Lab 03 of this folder)` |

Ne pas toucher aux sections *Coming from…* des README des labs : elles expliquent les changements d'API aux développeurs qui viennent d'une ancienne version, ce qui leur est utile.

### 4.d Lien cassé existant (recommandé)

Les 19 README de labs pointent vers `../../../Dashboard/README.md#azure-openai-settings`. Cette ancre n'existe plus : la section s'appelle désormais *AI models settings* (`#ai-models-settings`). Le texte du lien, *Azure OpenAI settings*, est lui aussi daté.

```bash
cd "$NEW"
grep -rl 'Dashboard/README.md#azure-openai-settings' LearningLabs | xargs sed -i '' \
  -e 's#the \*Azure OpenAI settings\* of the optional#the *AI models* settings of the optional#' \
  -e 's#Dashboard/README.md\#azure-openai-settings#Dashboard/README.md\#ai-models-settings#'
grep -rn 'azure-openai-settings' LearningLabs   # doit être vide
```

## 5. Vérifications (agent)

```bash
cd "$NEW"

# 1. Aucune référence restante aux fichiers supprimés. Un seul faux positif est attendu :
#    l'URL opentelemetry.io/docs/specs/semconv/gen-ai/ dans le README de Lab11.
grep -rnE 'Migration/|specs/|AGENT_FRAMEWORK_LABS|AZUREOPENAI_LABS_READINESS|Prototype-Report|PROCEDURE-NOUVEAU' \
  --include='*.md' --include='*.cs' --include='*.json' --include='*.js' --include='*.html' \
  --include='*.yml' --include='*.sh' --include='*.csproj' . | grep -v 'opentelemetry.io/docs/specs'

# 2. Plus de mention « migrated » obsolète. Résultat attendu : rien, ou seulement des phrases
#    sur la migration A2A v0.3 → v1 dans les sections Coming from… de Lab06.
grep -rniE 'migrated|not migrated|migration' README.md Dashboard LearningLabs \
  --include='*.md' --include='*.js' | grep -viE 'a2a|v0\.3|migration guide'

# 3. Les 19 chemins de labs.json existent.
jq -r '.labs[].path' Dashboard/LabDashboard/labs.json | while read -r p; do [ -d "$p" ] || echo "MANQUANT : $p"; done

# 4. Tests du Lab Bench, à lancer DEPUIS Dashboard/. Attendu : 0 échec.
(cd Dashboard && dotnet test)

# 5. Nettoyer ce que le build a produit, pour que l'export reste propre.
find . -type d \( -name bin -o -name obj -o -name TestResults \) -prune -exec rm -rf {} +
```

## 6. Premier commit et push

**Agent** :

```bash
cd "$NEW"
git init -b main
git add -A
git status --short | grep -E '/(bin|obj)/|\.data/|appsettings\.Local\.json|TestResults/' && echo "STOP : fichiers locaux ajoutés"
git commit -m "Microsoft Agent Framework learning labs"

# Adresse SSH : reprendre l'alias de ~/.ssh/config du compte propriétaire
# (l'ancien dépôt utilise git@github.com-mbensaidtech:…). L'humain fournit <alias>/<owner>/<repo>.
git remote add origin git@github.com-<alias>:<owner>/<repo>.git

# Dry-run du catalogue : doit afficher 19 labs et source.repository = <owner>/<repo>.
bash .github/scripts/publish-lab-catalog.sh --dry-run >/dev/null
```

Le dry-run lit le dépôt depuis le remote `origin` : ajoute-le **avant** le dry-run. Sur ce premier commit, sans parent, le contrôle des `id` supprimés par rapport au commit précédent est simplement sauté. C'est normal.

L'agent s'arrête ici et présente à l'humain : le commit exporté depuis `$OLD`, le nombre de fichiers (`git ls-files | wc -l`), le résultat des vérifications et du dry-run.

**Humain**, après relecture : `git push -u origin main`.

## 7. Vérifier la publication du catalogue (humain)

Le push déclenche **Actions → Publish lab catalog**, car le workflow fait partie de ses propres chemins surveillés.

| Ce que montre l'exécution | Signification |
|---|---|
| Étape *Publish the catalog* exécutée, résumé `200 unchanged` | ✅ L'admin avait déjà ces 19 labs, publiés depuis l'ancien dépôt au push de `18d414b` |
| `201 published` | ✅ Le catalogue a changé : soit l'admin n'avait pas encore la version de `18d414b`, soit le nettoyage a modifié un titre ou l'ordre. Le nouveau dépôt devient la source |
| Étape sautée et avis *« Catalog not published: set the ADMIN_DASHBOARD_URL variable and the CATALOG_PUBLISH_TOKEN secret »* | ❌ Réglage absent ou mal rangé (environnement, onglet Variables au lieu de Secrets, faute dans le nom) : reprendre l'étape 3, puis **Run workflow** |
| `401 unauthorized` | ❌ Le secret ne contient pas le bon jeton : le recoller. **Ne pas relancer en boucle** : 10 refus par minute bloquent l'IP |
| `409 conflict` | ❌ Improbable, car le commit du nouveau dépôt est plus récent. Vérifier l'horloge du poste au moment du commit (`git log -1 --format=%cI`) |
| `503 unavailable` | ❌ Le jeton manque côté Vercel : voir l'annexe |
| Erreur « must be 64 hexadecimal characters » | ❌ Le secret n'est pas le jeton de publication (par exemple, la clé d'API admin) |

Avec `200 unchanged`, l'admin garde comme source l'ancien dépôt. C'est normal : la source change à la prochaine modification de `labs.json` publiée depuis le nouveau dépôt.

Vérification facultative depuis le poste, avec le jeton : `ADMIN_DASHBOARD_URL=https://labs-admin-dash.vercel.app CATALOG_PUBLISH_TOKEN=… bash .github/scripts/publish-lab-catalog.sh --check`. Voir aussi la section *Reference catalog on the admin dashboard* de `Dashboard/README.md`.

## 8. Ancien dépôt : couper la publication (humain)

**Important.** Si l'ancien dépôt garde son secret, un futur push sur son `main` qui modifie `labs.json` remplacerait le catalogue de l'admin, puisque ses commits seraient plus récents.

1. Ancien dépôt → **Settings → Secrets and variables → Actions** :
   - onglet *Secrets* : supprimer `CATALOG_PUBLISH_TOKEN` ;
   - onglet *Variables* : supprimer `ADMIN_DASHBOARD_URL`.
2. **Actions → Publish lab catalog → menu « … » → Disable workflow**.
3. La branche distante `lab01-responses-follow-up` n'a aucun commit en plus de `main` : tu peux la supprimer avant l'archivage.
4. Puis, au choix :
   - **Settings → General → Danger Zone → Archive this repository** : le dépôt passe en lecture seule et l'historique de migration est conservé ;
   - ou **Change visibility → Private**, pour que les participants ne voient plus les documents internes. C'est à préférer si l'ancien dépôt était public, car l'archivage seul laisse les documents visibles.

## 9. Ton poste

- `$NEW` devient ta copie de travail. Renomme l'ancien dossier, par exemple en `MicrosoftAgentFramework-OLD`, pour ne plus y travailler par erreur.
- **Données locales du Lab Bench** : elles ne sont pas versionnées et sont propres à chaque dossier. Pour garder ton historique d'exécutions et ton identité de reporting, copie-les **avant** de lancer le Lab Bench depuis le nouveau dossier :
  ```bash
  [ -d "$OLD/Dashboard/.data" ] && cp -R "$OLD/Dashboard/.data" "$NEW/Dashboard/.data"
  [ -f "$OLD/Dashboard/LabDashboard/appsettings.Local.json" ] && cp "$OLD/Dashboard/LabDashboard/appsettings.Local.json" "$NEW/Dashboard/LabDashboard/"
  ```
- Les réglages Azure OpenAI et `Storage:Mode` (Lab05, Lab12) ne changent pas : ils sont dans les user-secrets du poste, sous l'id `microsoft-agent-framework-learninglabs`, partagés par tous les dossiers.
- Les conteneurs MongoDB de Lab05 et Lab12 portent un nom de projet Docker Compose fixe, qui ne dépend pas du dossier. Si tu les relances depuis `$NEW`, arrête d'abord ceux de `$OLD` (`docker compose down` dans chaque dossier `MongoDB/`).

## 10. Ce qui ne change pas

- **Admin dashboard** : URL, jeton Vercel (sauf régénération), catalogue, progression des participants (les `id` restent les mêmes).
- **Lab Bench** : `Dashboard:Reporting:ServerUrl` dans `appsettings.json`, codes d'invitation, clés d'atelier.
- **Seule l'URL de clonage change pour les participants** : mets à jour le mail ou les instructions que tu leur envoies.

## 11. ⚠️ Ne jamais renommer l'`id` d'un lab

La progression des participants sur l'admin est rattachée à l'`id` des entrées de `Dashboard/LabDashboard/labs.json` (`azureopenai-lab01` … `multiagent-lab03`). Les dossiers peuvent bouger, à condition de mettre `path` à jour. Les `id`, eux, ne doivent jamais changer.

---

## Annexe : régénérer le jeton

À faire seulement si le jeton est perdu. Une fois l'admin redéployé, **l'ancien jeton ne fonctionne plus**.

```bash
cd ~/Documents/01_Development/AI/labs-admin-dash
(umask 077 && openssl rand -hex 32 | tr -d '\n' > ~/catalog-publish-token.txt)
vercel env add CATALOG_PUBLISH_TOKEN production --sensitive --force --yes < ~/catalog-publish-token.txt

# Redéployer la production pour que le serveur lise le nouveau jeton :
vercel inspect labs-admin-dash.vercel.app        # repérer l'url du déploiement de production (ligne « url »)
vercel redeploy <url-du-deploiement> --target production
```

Ensuite :
1. Colle le contenu de `~/catalog-publish-token.txt` dans le secret `CATALOG_PUBLISH_TOKEN` du nouveau dépôt.
2. Sauvegarde-le dans ton gestionnaire de mots de passe.
3. Supprime le fichier : `rm ~/catalog-publish-token.txt`.
4. Vérifie avec **Run workflow** : résultat attendu `200 unchanged`.
