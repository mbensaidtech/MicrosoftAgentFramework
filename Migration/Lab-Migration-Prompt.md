# Prompt générique — migration d'un lab

> Copier le bloc ci-dessous dans une nouvelle session, en remplaçant **uniquement** `<NOM_DU_LAB>`
> par le nom du dossier du lab (ex. `Lab03-AIAgentWithFunctionTools`).
> Tout le reste (méthode, règles, tests, livrables) se déduit du dépôt.

---

```markdown
Migre le lab suivant vers la dernière version stable de Microsoft Agent Framework retenue pour ce dépôt :

**Lab à migrer : `Lab05-AIAgentWithThreads`**

Ne migre aucun autre lab.

## 1. Références à lire avant toute modification

Dans le dossier `Migration/` du dépôt :

- `Migration-Manual.md` : version cible, breaking changes, mapping ancien → nouveau, bonnes pratiques (§6), stratégie de test (§7), risques (§8). **Il fait foi.**
- `Migration-Plan.md` : vague du lab, changements attendus et points d'attention propres à ce lab, pratiques à appliquer (§5 bis).
- Les rapports des labs déjà migrés (`Lab*-Migration-Report.md`) : décisions prises, problèmes rencontrés, recommandations pour la suite.
- Les labs déjà migrés (au minimum `Lab01-FirstBasicAIAgent`) : ils servent de référence pour le socle (`.csproj`, `ConfigurationHelper.cs`, `AzureOpenAISettings.cs`, `appsettings.json`, bloc « Setup » de `Program.cs`, structure du README).

En cas de doute, les sources officielles priment sur les anciens patterns du dépôt :
- https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp
- https://github.com/microsoft/agent-framework (tag correspondant à la version cible, dossier `dotnet/samples`)

## 2. Périmètre et autonomie

- Tu es autonome : commandes shell, restauration, build, tests, exécution des exercices, création/modification/suppression de fichiers **dans le dépôt**.
- **Aucune lecture ni modification de fichier hors du dépôt** (hors caches d'outils utilisés normalement par `dotnet`). En cas de doute sur l'emplacement d'un fichier, considère-le hors périmètre.
- Ne jamais lire, afficher ou recopier un secret (clé API, variables `AzureOpenAI__*`) : vérifier seulement leur présence.
- Informations externes : uniquement la documentation Microsoft et le dépôt officiel.
- Ne pas commiter.

## 3. Méthode

1. Analyser le code, le README, le Start et la Solution du lab ; lister APIs, packages et concepts utilisés ; faire un build de référence (baseline).
2. Vérifier chaque API/package dans les sources officielles de la version cible (samples, `PublicAPI.Shipped.txt`, code source au tag) ; repérer les APIs supprimées, renommées, dépréciées ou devenues expérimentales.
3. Identifier les changements, y compris **conceptuels** : si un concept a changé, adapter l'exercice pour enseigner la manière actuelle, sans reproduire l'ancien comportement. Signaler clairement tout élément supprimé ou remplacé.
4. Appliquer la migration complète : socle identique aux labs migrés, code, commentaires d'aide, TODO, README, configuration, dépendances.
5. Valider (§5), puis produire le rapport (§6).

## 4. Règles à respecter (résumé du manuel, §6)

- Version MAF unique et stable pour Start et Solution ; aucune préversion sauf exception documentée dans le manuel (à signaler et à faire valider).
- Référencer le package de plus haut niveau utile ; pas de dépendance ni d'abstraction inutile ; aucune bibliothèque tierce non officielle.
- Programmer contre `AIAgent` ; client `OpenAIClient` sur l'endpoint Azure OpenAI v1 (clé API ou Entra ID) ; Chat Completions par défaut.
- Ne pas conserver une ancienne API parce qu'elle fonctionne encore.
- **Configuration** : aucun secret dans `appsettings.json` ; le `.csproj` déclare le `UserSecretsId` partagé `microsoft-agent-framework-learninglabs` (obligatoire : le dashboard l'exige pour tous les labs de son catalogue).
- Start et Solution partagent exactement les mêmes fichiers d'infrastructure ; seul `Program.cs` diffère.
- Le Start livré compile **sans warning C#** et s'exécute (TODO en commentaires) ; les helpers non utilisés par le Start vont dans un fichier fourni, pas en fonctions locales.
- Aucun `NoWarn` global ; suppression ciblée et commentée d'un diagnostic expérimental uniquement si l'API est réellement enseignée.
- Commentaires d'aide et README ne nomment que les APIs actuelles ; les anciennes n'apparaissent que dans l'encadré « Coming from an older version of the lab? ».
- Chaque TODO a un indice dans le README qui mène exactement au code de la Solution ; conserver l'objectif pédagogique ; ajouter un scénario seulement s'il est démontré par un sample ou la doc officiels (et le justifier).
- Le lab reste exécutable avec la seule CLI `dotnet`.
- **Versioning** : ne jamais versionner les fichiers de travail locaux exclus par `.gitignore`, et ne jamais introduire de référence à un projet tiers non officiel dans le contenu versionné (code, commentaires, README, docs, configuration, dashboard).

## 5. Validation (stratégie de test du manuel, §7)

Le lab n'est migré que si tout est vérifié et consigné :

1. Restauration Start + Solution ; graphe de dépendances conforme (version cible, pas de package retiré) ; vulnérabilités : aucune introduite par le lab.
2. `dotnet build -warnaserror` Start + Solution (seuls les warnings NU19xx hérités de `CommonUtilities` sont tolérés tant qu'ils ne sont pas corrigés).
3. Recherche des anciens noms d'API (manuel §5) dans `Start/`, `Solution/`, `README.md` : 0 occurrence hors encadré.
4. Exécution réelle de la Solution avec clé API **et** avec Microsoft Entra ID ; chaque scénario produit un résultat cohérent.
5. Configuration absente/placeholder : message d'erreur clair.
6. Start livré : compile et s'exécute.
7. Copie temporaire du Start (dans le dossier du lab, supprimée ensuite) complétée **uniquement** d'après les indices du README : même comportement que la Solution.
8. `diff` Start/Solution hors `Program.cs` : aucune différence.
9. Relecture README ↔ TODO ↔ Solution.
10. Si `CommonUtilities` est modifié : non-régression des autres labs.

## 6. Intégration au dashboard (`Dashboard/`)

- Ajouter le lab à `Dashboard/LabDashboard/labs.json` (métadonnées EN + traduction FR, projets Start/Solution, timeout, checks).
- Écrire des checks qui **échouent sur le Start livré** et **passent sur la Solution** ; vérifier le résultat réel de chaque scénario (pas seulement la présence d'une ligne) ; pour l'usage de tokens d'un scénario, s'arrêter à l'en-tête suivant : `(?is)^=== Scenario N:(?:(?!^=== Scenario).)*?input tokens:?\s*\d+`.
- Ajouter l'id du lab au test `LabCatalogTests` ; ajouter un test des checks du lab sur une sortie Solution et une sortie Start ; `dotnet test` dans `Dashboard/` doit passer.
- Modifier le code du dashboard seulement si le lab l'exige réellement (et le justifier) ; les labs déjà enregistrés doivent continuer à fonctionner.
- Vérifier via l'API ou l'UI du dashboard : run Solution `passed`, run Start `failed` attendu, tokens affichés ; nettoyer tout artefact de test créé dans le dépôt.

## 7. Livrables

- Code, README et configuration du lab migrés.
- `Migration/<NOM_DU_LAB>-Migration-Report.md`, sur le modèle des rapports existants : changements effectués (fichier → changement → pourquoi), APIs et packages modifiés, changements conceptuels, éléments supprimés ou remplacés, tests effectués et résultats (tableau), problèmes rencontrés, décisions à valider, points pour les prochains labs, intégration au dashboard.
- Mises à jour associées : `Migration-Manual.md` (nouveau mapping ou breaking change découvert), `Migration-Plan.md` (statut), `README.md` racine (liste des labs migrés, scénarios du lab), `Dashboard/README.md` si nécessaire.

## 8. Contrôle final avant de rendre la main

- `git grep --cached` et les fichiers suivis ou non ignorés ne contiennent aucune référence interdite (§4, Versioning), et les fichiers de travail locaux restent non suivis.
- Aucun artefact temporaire (copie du Start, captures, historique de test) n'est laissé dans le dépôt en dehors des dossiers ignorés.
- Rapport final dans la conversation : résumé des changements, résultats des tests (réussites **et** échecs, avec la sortie), points à valider.
```
