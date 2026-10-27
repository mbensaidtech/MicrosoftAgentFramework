# Déplacer les labs vers un nouveau dépôt GitHub

Procédure préparée le 27/10/2026 pour le déplacement du 28/10. Les numéros de ligne cités correspondent au commit `5a7cd5c` de `main`.

**État de départ**
- Dépôt actuel : `mbensaidtech/MicrosoftAgentFramework`.
- Catalogue publié sur l'admin : 17 labs, commit `5a7cd5c`.
- Jeton `CATALOG_PUBLISH_TOKEN` : configuré dans Vercel (production de `labs-admin-dash`) et dans les secrets de ce dépôt.

## Résumé (checklist)

- [ ] 0. **Ce soir** : sauvegarder le jeton dans un gestionnaire de mots de passe.
- [ ] 1. Créer le nouveau dépôt GitHub, **vide**.
- [ ] 2. Y ajouter la variable `ADMIN_DASHBOARD_URL` et le secret `CATALOG_PUBLISH_TOKEN`, **avant** le premier push.
- [ ] 3. Exporter `main` sans historique, puis supprimer ce qui ne concerne pas les élèves.
- [ ] 4. Corriger les liens vers les fichiers supprimés.
- [ ] 5. Vérifier : liens, `dotnet test`, dry-run du catalogue.
- [ ] 6. Premier commit, puis push.
- [ ] 7. Vérifier l'exécution **Publish lab catalog** (attendu : `200 unchanged`).
- [ ] 8. Ancien dépôt : supprimer le secret et la variable, désactiver le workflow, puis l'archiver ou le passer en privé.
- [ ] 9. Ton poste : reprendre `Dashboard/.data` et basculer sur le nouveau dossier.
- [ ] 10. Envoyer la nouvelle URL de clonage aux élèves.

---

## 0. Ce soir : sauvegarder le jeton

Le jeton se trouve dans un fichier temporaire :

```
/private/tmp/claude-501/-Users-developper-Documents-01-Development-AI-MicrosoftAgentFramework/d3304e50-bf3d-44b0-a544-5efda1e81ce0/scratchpad/catalog-publish-token.txt
```

macOS vide `/private/tmp` au redémarrage. Ni Vercel (variable « sensitive ») ni GitHub (secret) ne peuvent réafficher la valeur.

**Copie-le dès ce soir dans ton gestionnaire de mots de passe**, puis supprime le fichier. Si tu l'as perdu, voir l'[annexe : régénérer le jeton](#annexe--régénérer-le-jeton).

## 1. Principe

Le nouveau dépôt part d'un **export propre de `main`, sans historique** (`git archive`).
- Les rapports de migration, les specs et les audits ne seront donc **pas non plus dans l'historique** visible par les élèves.
- `git archive` n'exporte que les fichiers versionnés. Rien de local ne part :
  - les `bin/` et `obj/` ;
  - `Dashboard/.data/` (historique des exécutions, identité) ;
  - `appsettings.Local.json` (clé d'API admin éventuelle) ;
  - les `_StartCompleted/` ;
  - `.playwright-mcp/` ;
  - `Dashboard/TestResults/`.

> Pourquoi pas un simple `git clone` + `git rm` ? Les fichiers supprimés resteraient lisibles dans l'historique du nouveau dépôt. L'historique complet reste disponible dans l'ancien dépôt, qu'on archive (étape 8).

## 2. Ce qu'il ne faut PAS mettre dans le nouveau dépôt

### À supprimer

| Chemin | Contenu | Raison |
|---|---|---|
| `Migration/` (21 fichiers) | Rapports de migration Lab01→Lab12, plan, manuel, prompt de migration, rapports d'étapes, montée en 1.26.0 | Travail interne, sans intérêt pour les élèves |
| `specs/` (7 fichiers) | Specs du Lab Bench et de l'admin (suivi de progression, réglages Azure OpenAI, reset, saisie interactive, parcours, configuration des modèles, évaluation/skills) | Conception interne |
| `AGENT_FRAMEWORK_LABS_ACTION_PLAN.md` | Plan d'action v1 | Interne |
| `AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md` | Plan d'action v2 | Interne |
| `AGENT_FRAMEWORK_LABS_AUDIT.md` | Audit des labs | Interne |
| `AZUREOPENAI_LABS_READINESS_REPORT.md` | Rapport de préparation des labs Azure OpenAI | Interne |
| `Dashboard/Prototype-Report.md` | Rapport du prototype du Lab Bench | Interne |
| `PROCEDURE-NOUVEAU-REPO.md` | Ce fichier | Interne |

### À décider : MultiAgentSystem Lab02 et Lab03

| Chemin | État |
|---|---|
| `LearningLabs/MultiAgentSystem/Lab02_OrchestrationSequential/` (13 fichiers) | **Non migrés** : preview `1.0.0-preview.251204.1`, API qui n'existent plus (`CreateAIAgent`, `AgentRunResponse`…) |
| `LearningLabs/MultiAgentSystem/Lab03_OrchestrationConcurrent/` (12 fichiers) | Absents de `labs.json` et du Lab Bench ; le README dit déjà « do not follow them for now » |

**Recommandation : les retirer.** Un élève qui les ouvre tombe sur un projet qui ne compile pas avec la version actuelle. Ils restent dans l'ancien dépôt pour une migration future. Si tu les retires, il faut aussi modifier le README (voir 4.b).

### À garder

| Chemin | Pourquoi |
|---|---|
| `LearningLabs/` (16 labs AzureOpenAI + MultiAgentSystem Lab01) | Les labs (`Start/`, `Solution/`, README EN/FR) |
| `CommonUtilities/` | Référencé par les labs |
| `Dashboard/` (y compris `LabDashboard.Tests/` et `LabInputHook/`) | Le Lab Bench. Les tests servent à vérifier `labs.json` et les checks quand tu modifies un lab |
| `.github/` | Publication automatique du catalogue vers l'admin (workflow + script) |
| `README.md`, `.gitignore` | — |

## 3. Créer le nouveau dépôt et ses réglages (GitHub)

1. **New repository** : choisis le propriétaire, le nom et la visibilité. Public si les élèves clonent sans être invités.
   **Ne coche rien** : pas de README, pas de `.gitignore`, pas de licence. Le dépôt doit être vide.
2. **Tout de suite, avant le premier push**, va dans le nouveau dépôt, **Settings → Secrets and variables → Actions** (dans la section *Security* ; le menu se déplie).
   - Onglet **Variables**, partie **Repository variables**, bouton **New repository variable** :
     - Name : `ADMIN_DASHBOARD_URL`
     - Value : `https://labs-admin-dash.vercel.app` (sans `/` final)
   - Onglet **Secrets**, partie **Repository secrets**, bouton **New repository secret** :
     - Name : `CATALOG_PUBLISH_TOKEN`
     - Secret : le jeton sauvegardé à l'étape 0, soit 64 caractères hexadécimaux. Ce n'est **pas** la clé d'API admin ni un code d'invitation.
   - Liens directs, en remplaçant `<owner>/<repo>` :
     - `https://github.com/<owner>/<repo>/settings/variables/actions/new`
     - `https://github.com/<owner>/<repo>/settings/secrets/actions/new`
   - ⚠️ N'utilise **pas** les parties *Environment variables / Environment secrets*, qui demandent de créer un environnement. N'utilise pas non plus les pages *Codespaces* ou *Dependabot* : le workflow ne les lit pas.
3. **Settings → Actions → General** : *Allow all actions* doit être sélectionné. C'est le réglage par défaut, à vérifier si le propriétaire est une organisation.

## 4. Préparer le contenu propre (sur ton poste)

### 4.a Export et suppressions

Adapte `NEW` :

```bash
OLD=~/Documents/01_Development/AI/MicrosoftAgentFramework
NEW=~/Documents/01_Development/AI/<nouveau-dossier>

git -C "$OLD" switch main && git -C "$OLD" pull --ff-only
mkdir "$NEW"
git -C "$OLD" archive --format=tar main | tar -x -C "$NEW"
cd "$NEW"

rm -rf Migration specs \
  AGENT_FRAMEWORK_LABS_ACTION_PLAN.md AGENT_FRAMEWORK_LABS_ACTION_PLAN_V2.md \
  AGENT_FRAMEWORK_LABS_AUDIT.md AZUREOPENAI_LABS_READINESS_REPORT.md \
  Dashboard/Prototype-Report.md PROCEDURE-NOUVEAU-REPO.md

# Seulement si tu retires les deux labs non migrés :
rm -rf LearningLabs/MultiAgentSystem/Lab02_OrchestrationSequential \
       LearningLabs/MultiAgentSystem/Lab03_OrchestrationConcurrent
```

### 4.b Liens à corriger

Sans ces corrections, des liens restent cassés.

| Fichier | Ligne | Texte actuel | Modification |
|---|---|---|---|
| `README.md` | 48 | `See [Migration/Migration-Plan.md](Migration/Migration-Plan.md).` | Supprimer cette phrase. Facultatif : supprimer aussi l'historique « (migrated from the previews to 1.22.0, then upgraded to 1.26.0 on 2026-10-25) » |
| `README.md` | 242 | `The API contract between the two applications is described in [specs/admin-dashboard-progress-tracking/spec.md](…).` | Supprimer la phrase |
| `Dashboard/README.md` | 131 | `(full specification: [specs/dashboard-model-configuration/spec.md](../specs/…))` | Supprimer la parenthèse |
| `Dashboard/README.md` | 199 | `summarized in the [spec](../specs/dashboard-model-configuration/spec.md#admin-api-contract-v1)` | Remplacer par `summarized below` |

Facultatif : deux commentaires de code citent un chemin `specs/…`. C'est sans effet sur le build.
- `Dashboard/LabDashboard/Reset/LabResetService.cs:10`
- `Dashboard/LabDashboard/TrainerModels/AdminModelConfigs.cs:10`

**Si tu retires MultiAgentSystem Lab02/Lab03**, modifie aussi `README.md` :
- l.48 : fin de phrase « Lab 02 and Lab 03 are not migrated yet (see Part 2) » ;
- l.94 : « Environment variables, required for the labs that are **not migrated yet** ». La section *Environment Variables Configuration* (l.536) reste utile : elle sert à surcharger la config de tous les labs. Seule cette mention change ;
- l.464 : l'avertissement « Lab 02 and Lab 03 are not migrated yet » ;
- l.480 et l.490 : les sections *Lab 02 - Orchestration Sequential* et *Lab 03 - Orchestration Concurrent* ;
- l.626 : la phrase sur Lab 02 et Lab 03 dans le parcours essentiel.

### 4.c Vérifications

```bash
cd "$NEW"
# Aucune référence restante aux fichiers supprimés :
grep -rnE 'Migration/|specs/|AGENT_FRAMEWORK_LABS|AZUREOPENAI_LABS_READINESS|Prototype-Report' --include='*.md' --include='*.cs' --include='*.json' .
# Si Lab02/Lab03 MultiAgentSystem retirés :
grep -rnE 'Lab0[23]_Orchestration|OrchestrationSequential|OrchestrationConcurrent' .

# Tests du Lab Bench : à lancer DEPUIS Dashboard/ (son global.json sélectionne Microsoft.Testing.Platform)
cd Dashboard && dotnet test && cd ..
```

## 5. Premier commit et push

```bash
cd "$NEW"
git init -b main
git add -A
git status --short | grep -E '/(bin|obj)/|\.data/|appsettings\.Local\.json' && echo "STOP : fichiers locaux ajoutés"
git commit -m "Microsoft Agent Framework learning labs"

# Adresse SSH : reprends l'alias de ~/.ssh/config du compte propriétaire
# (l'ancien dépôt utilise git@github.com-mbensaidtech:…)
git remote add origin git@github.com-<alias>:<owner>/<repo>.git

# Dry-run du catalogue : doit afficher 17 labs et source.repository = <owner>/<repo>
bash .github/scripts/publish-lab-catalog.sh --dry-run >/dev/null

git push -u origin main
```

## 6. Vérifier la publication du catalogue

Le push déclenche **Actions → Publish lab catalog**, car le workflow fait partie de ses propres chemins surveillés.

| Ce que montre l'exécution | Signification |
|---|---|
| Étape *Publish the catalog* exécutée, résumé `200 unchanged` | ✅ Attendu : le nouveau dépôt publie les mêmes 17 labs |
| `201 published` | ✅ Les labs ont changé pendant le nettoyage (titres, ordre…) : c'est le nouveau catalogue |
| Étape sautée + avis *« Catalog not published: set the ADMIN_DASHBOARD_URL variable and the CATALOG_PUBLISH_TOKEN secret »* | ❌ Réglage absent ou mal rangé (environnement, onglet Variables au lieu de Secrets, faute de nom) : reprendre l'étape 3, puis **Run workflow** |
| `401 unauthorized` | ❌ Le secret ne contient pas le bon jeton : le recoller. Ne pas relancer en boucle : 10 refus par minute bloquent l'IP |
| Erreur « must be 64 hexadecimal characters » | ❌ Le secret n'est pas le jeton de publication (par exemple, la clé d'API admin) |

Avec `200 unchanged`, l'admin garde comme source l'ancien dépôt et le commit `5a7cd5c`. C'est normal : la source change à la prochaine modification de `labs.json` publiée depuis le nouveau dépôt.

Vérification depuis ton poste, facultative : voir la section *Reference catalog on the admin dashboard* de `Dashboard/README.md` (`--check`).

## 7. ⚠️ Ne jamais renommer l'`id` d'un lab pendant le nettoyage

La progression des élèves sur l'admin est rattachée à l'`id` des entrées de `Dashboard/LabDashboard/labs.json`. Les dossiers peuvent bouger, à condition de mettre `path` à jour. Les `id`, eux, ne doivent jamais changer.

## 8. Ancien dépôt : couper la publication

**Important.** Si l'ancien dépôt garde son secret, un futur push sur son `main` qui modifie `labs.json` remplacerait le catalogue de l'admin, puisque ses commits seraient plus récents.

1. Ancien dépôt → **Settings → Secrets and variables → Actions** :
   - onglet *Secrets* : supprimer `CATALOG_PUBLISH_TOKEN` ;
   - onglet *Variables* : supprimer `ADMIN_DASHBOARD_URL`.
2. **Actions → Publish lab catalog → menu « … » → Disable workflow**.
3. Puis, au choix :
   - **Settings → General → Danger Zone → Archive this repository** : lecture seule, l'historique de migration est conservé ;
   - ou **Change visibility → Private**, pour que les élèves ne voient plus les documents internes.

## 9. Ton poste

- Le dossier `$NEW` devient ta copie de travail. Renomme l'ancien, par exemple en `MicrosoftAgentFramework-OLD`, pour ne plus y travailler par erreur.
- **Données locales du Lab Bench** : elles ne sont pas versionnées et sont propres à chaque dossier. Pour garder ton historique d'exécutions et ton identité de reporting, copie-les **avant** de lancer le Lab Bench depuis le nouveau dossier :
  ```bash
  cp -R "$OLD/Dashboard/.data" "$NEW/Dashboard/.data"
  [ -f "$OLD/Dashboard/LabDashboard/appsettings.Local.json" ] && cp "$OLD/Dashboard/LabDashboard/appsettings.Local.json" "$NEW/Dashboard/LabDashboard/"
  ```
- Les réglages Azure OpenAI ne changent pas : ils sont dans les user-secrets du poste, id `microsoft-agent-framework-learninglabs`, partagés par tous les dossiers.

## 10. Ce qui ne change pas

- **Admin dashboard** : URL, jeton Vercel, catalogue, progression des élèves (les `id` restent les mêmes).
- **Lab Bench** : `Dashboard:Reporting:ServerUrl` dans `appsettings.json`, codes d'invitation, clés d'atelier.
- **Seule l'URL de clonage change pour les élèves** : mets à jour le mail ou les instructions que tu leur envoies.

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
4. Vérifie avec **Run workflow** : attendu `200 unchanged`.
