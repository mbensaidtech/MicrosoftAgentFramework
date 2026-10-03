# Lab06 (A2A) — Problèmes à traiter

> Constatés le 2026-09-28 pendant la migration de Lab06_A2AClient et Lab06_A2AServer vers MAF 1.22.0.
> Détails et contexte : [Lab06-Migration-Report.md](Lab06-Migration-Report.md) (§6 et §7).

## 1. Streaming Azure OpenAI cassé — ⚠️ bloquant, externe

- **Symptôme** : tout `RunStreamingAsync` échoue avec `InvalidOperationException: The requested operation requires an element of type 'Object', but the target element has type 'Null'` (dans `OpenAIChatClient.TryGetReasoningDelta`).
- **Cause** : Azure envoie en streaming des annotations de filtre de contenu **sans `delta`**. `Microsoft.Extensions.AI.OpenAI` 10.10.0 (et 10.10.1, dernière version publiée) ne les gère pas. Ticket officiel ouvert : [dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790).
- **Impacts** :
  - **Lab06_A2AServer** : l'hébergement A2A exécute toujours les agents en streaming, donc chaque requête renvoie *« Agent handler did not produce any response events »*.
  - **Lab01** (scénario 5) et **Lab02** (scénario 4) échouent maintenant (exit 134), alors qu'ils étaient validés le 2026-09-26/27. Leurs runs Solution échouent aussi dans le dashboard.
- **État** : contournement temporaire dans Lab06_A2AServer (`StreamingWorkaround.cs`, TODO 4). Lab01 et Lab02 ne sont pas corrigés (hors périmètre).
- **À faire** :
  - [ ] Surveiller le ticket #7790 et la sortie d'une version corrigée de `Microsoft.Extensions.AI.OpenAI`.
  - [ ] Ensuite : supprimer `StreamingWorkaround.cs` et le TODO 4 (Start, Solution, README), puis créer les agents avec `chatClient.AsAIAgent(...)`.
  - [ ] Retester Lab01, Lab02 et Lab06 (CLI et dashboard).
  - [ ] Ou décider maintenant d'appliquer le même contournement à Lab01 et Lab02.

## 2. Client A2A v1 incompatible avec un serveur v0.3

- **Symptôme** : *« Invalid JSON-RPC request: 'method' field is not a valid A2A method »*.
- **Cause** : le SDK A2A 1.0.0-preview2 n'a pas de mode de compatibilité v0.3.
- **État** : résolu en migrant aussi le serveur (hors périmètre initial, décision validée pendant la session).
- **À faire** :
  - [ ] Valider le rapport unique pour les deux labs.

## 3. Port 5000 occupé sur macOS (AirPlay)

- **Symptôme** : quand le serveur du lab ne tourne pas, le client reçoit `403 Forbidden` sur `localhost:5000`.
- **Cause** : le récepteur AirPlay (ControlCenter) écoute sur `*:5000`. Kestrel arrive quand même à se lier à `localhost:5000`.
- **État** : documenté dans les deux README (dépannage). Le dashboard utilise un port dédié (5071).
- **À faire** :
  - [ ] Décider s'il faut changer le port par défaut du lab (5000, comme les samples officiels).

## 4. Écarts entre la documentation Learn et le comportement réel

- Le guide « A2A SDK v1 Migration Guide » annonce HTTP+JSON comme liaison par défaut. Or, avec les cartes du lab, le client utilise **JSON-RPC**. HTTP+JSON n'est utilisé qu'avec `A2AClientOptions.PreferredBindings = [ProtocolBindingNames.HttpJson]` (vérifié dans les journaux du serveur).
- Le guide dit qu'un hôte ne sert qu'une carte. Or `MapWellKnownAgentCard(card, path)` sert une carte par agent, à `<path>/.well-known/agent-card.json`.
- `A2ACardResolver` exige un `/` final sur l'URL de l'agent ; sans lui, on obtient 404.
- **État** : les labs suivent le comportement vérifié, et c'est consigné dans le manuel (§4.12).
- **À faire** :
  - [ ] Revérifier ces points à la prochaine version du SDK A2A.

## 5. Noms de types ambigus

- `A2A.AgentSkill` et `Microsoft.Agents.AI.AgentSkill` coexistent : ambiguïté `CS0104`.
- Le namespace du projet `A2AClient` masque le type `A2A.A2AClient`.
- **État** : les noms complets sont écrits dans le code, avec un commentaire.
- **À faire** :
  - [ ] Éventuellement, renommer le namespace du projet client pour simplifier le code enseigné.

## 6. Incidents d'environnement pendant la session

- `timeout` et `gh` ne sont pas installés : contournés (`curl` sur l'API GitHub).
- Playwright a écrit des instantanés dans `.playwright-mcp/` à la racine du dépôt : **supprimés**.
- Un `git rm --cached` a été lancé par erreur sur `RemoteAuthAgentSettings.cs` : **annulé** tout de suite (`git reset`). L'index n'est pas modifié.
- **À faire** :
  - [ ] Envisager d'ajouter `.playwright-mcp/` au `.gitignore`.

## 7. Ancien secret HMAC dans l'historique git

- L'ancienne valeur de `APIKeySettings:SecretKey` était versionnée dans `appsettings.json`. Elle a été retirée des fichiers, mais elle reste dans l'historique git.
- **À faire** :
  - [ ] Ne plus utiliser cette valeur (le secret est maintenant facultatif, en user-secrets, ou aléatoire).

## 8. Vulnérabilités héritées de CommonUtilities

- `SharpCompress` 0.30.1 (moderate) et `Snappier` 1.0.0 (high), via `MongoDB.Driver` 2.30.0 : warnings NU1902/NU1903 sur tous les labs.
- **À faire** :
  - [ ] Phase 0 du plan (avec Lab07 ou Lab12) : monter `CommonUtilities` en `MongoDB.Driver` 3.12.0.

## Décisions à valider

- [ ] Contournement streaming dans le serveur (point 1).
- [ ] Secret de signature facultatif (aléatoire par défaut).
- [ ] Ajouts de contenu : scénario 3 du client (`AsAIFunction`), validation d'une clé modifiée, skill `DetectTone`, agent renommé `AuthAgent`.
- [ ] Mécanisme `companion` ajouté au code du dashboard (port dédié 5071).

---

## Analyse du 2026-09-29 (session interrompue : problème de souscription Azure à régler d'abord)

### A. Accès Azure OpenAI cassé (nouveau, bloquant, indépendant du code)

- **Symptôme** : Lab01 Solution échoue dès le scénario 1 avec `HTTP 401 Access denied due to invalid subscription key or wrong API endpoint` (avant même d'atteindre le streaming). Un `curl` direct avec la clé de `~/.zshrc` renvoie `401` sur `/openai/v1/chat/completions` et sur le chemin legacy `/openai/deployments/...`. Un jeton Entra ID (`az account get-access-token --scope https://ai.azure.com/.default`) renvoie `SubscriptionNotRegistered` (`Microsoft.CognitiveServices` non enregistré sur la souscription).
- **Cause** : problème côté souscription/ressource Azure (constaté aussi par l'utilisateur dans le portail). Aucun lien avec la migration.
- **À faire** :
  - [ ] Rétablir la souscription / la ressource `mbensaid-project-alpha-resource`, puis vérifier la clé API de `~/.zshrc` (`AzureOpenAI__APIKey`) ou passer par Entra ID.
  - [ ] Relancer Lab01 Solution : il doit repasser le scénario 1 avant de retester le streaming.

### B. Problème 1 (streaming) : cause racine précisée, elle est aussi côté Azure

- **Chaîne complète** : mode **« Asynchronous Filter »** du filtre de contenu activé sur le déploiement → Azure émet des chunks d'annotation **sans `delta`** (`content_filter_offsets` + `content_filter_results`, y compris après `finish_reason: "stop"`) → le SDK OpenAI 2.13/2.14 expose l'absence de `delta` comme `"delta": null` via `JsonPatch` → `OpenAIChatClient.TryGetReasoningContent` (MEAI.OpenAI 10.10.x, helper ajouté par dotnet/extensions#7726) appelle `TryGetProperty` sur un élément `Null` → `InvalidOperationException`.
- **Preuves** :
  - Doc Learn « Content streaming » : les messages d'annotation sans tokens avec `content_filter_offsets` sont le format du mode Asynchronous Filter, **opt-in**, appliqué au niveau du déploiement via une configuration de filtre de contenu (Foundry portal → filtre → section Streaming). Le mode Default n'émet pas ces chunks.
  - Issue dotnet/extensions#7790 (ouverte, `untriaged`) et PR **#7792** « Guard against a null choice or delta in TryGetReasoningContent » (ouverte, **non fusionnée**, aucune version publiée au 2026-09-29). Dernière version NuGet de `Microsoft.Extensions.AI.OpenAI` : 10.10.1 (cassée). MAF 1.23.0 dépend encore de MEAI.OpenAI 10.10.1 : monter MAF ne corrige rien.
  - Source `A2AAgentHandler` (tag dotnet-1.22.0) : `HandleNewMessageAsync` appelle toujours `RunStreamingAsync`, y compris pour `message/send` non streaming (agrégation ensuite). Confirme que le contournement est nécessaire côté serveur tant que le flux Azure est en mode asynchrone.
- **Non vérifié (à faire dès que l'accès Azure est rétabli)** :
  - [ ] Capturer un flux SSE réel (`curl -N` sur `/openai/v1/chat/completions` avec `"stream": true`) et confirmer la présence de chunks `choices[0]` sans `delta` avec `content_filter_offsets`.
  - [ ] Lire la configuration de filtre de contenu du déploiement `gpt-4o-mini-paris-chat` (`az cognitiveservices account deployment show ... --query properties.raiPolicyName`, puis `raiPolicies` via `az rest`) et vérifier le mode Streaming.
- **Corrections possibles (durables)** :
  1. **Côté Azure** : repasser le filtre du déploiement en mode Streaming « Default » (ou créer une configuration Default). Supprime la cause pour Lab01, Lab02 et Lab06 sans toucher au code ; `StreamingWorkaround.cs` et le TODO 4 deviennent inutiles.
  2. **Côté bibliothèque** (si on veut garder le filtre asynchrone) : attendre la publication du correctif de #7792, puis ajouter une `PackageReference` directe sur la version corrigée de `Microsoft.Extensions.AI.OpenAI` dans chaque csproj (MAF 1.22.0 accepte `>= 10.10.0`, pas besoin de monter MAF).
  3. Ne pas appliquer `WithNonStreamingResponses()` à Lab01/Lab02 : le scénario 5 de Lab01 et le scénario 4 de Lab02 enseignent justement le streaming, le contournement en détruirait la valeur pédagogique.
- **Vérifications après correction** : Lab01 scénario 5, Lab02 scénario 4, Lab06 client (3 scénarios) et serveur, en CLI et dans le dashboard ; puis retirer `StreamingWorkaround.cs`, le TODO 4 (Start, Solution, README serveur), la ligne du manuel §8 et le point 8 du rapport Lab06.

### C. Problème 4 : la liaison JSON-RPC n'est pas un écart de la doc, c'est l'ordre de la carte

- **Cause confirmée** (source `A2AClientFactory.Create`, SDK A2A) : le client parcourt `AgentCard.SupportedInterfaces` **dans l'ordre déclaré par la carte** et retient la première liaison présente dans `A2AClientOptions.PreferredBindings` (défaut `[HttpJson, JsonRpc]`, spec A2A §8.3.1 : la préférence de l'agent gagne). Les cartes du lab listent JSON-RPC en premier → JSON-RPC.
- **Conséquence** : le commentaire de `AgentCards.CreateInterfaces` (« clients prefer HTTP+JSON and fall back to JSON-RPC ») est faux, ainsi que la formulation « écart Learn » du manuel §4.12 et du README client (« Going further »).
- **Correction proposée** : corriger les trois textes (Start/Solution `AgentCards.cs`, manuel §4.12, README client). Option : mettre `HttpJson` en premier dans `CreateInterfaces` si l'on veut HTTP+JSON par défaut.
- **Risque de régression** si l'on réordonne : le check `scenario1-card` du dashboard (`labs.json`, entrées lab06-server et lab06-client, et `OutputAnalyzerTests`) attend la ligne `Interface: JSONRPC` **avant** la ligne `Interface: HTTP+JSON` ; les sorties attendues des README aussi. À mettre à jour ensemble.

### D. `/` final de `A2ACardResolver` : comportement standard, pas un bug

- Source SDK : `_agentCardPath = new Uri(baseUrl, agentCardPath.TrimStart('/'))`. Résolution d'URI relative (RFC 3986) : sans `/` final, le dernier segment est remplacé (`/a2a/authAgent` → `/a2a/.well-known/agent-card.json` → 404).
- **Correction durable possible** : normaliser l'URL dans `ConfigurationHelper.GetRemoteAgentSettings` (ou une propriété `CardResolverUrl`) au lieu du `$"{url}/"` en ligne dans `Program.cs` (Start et Solution).

### E. Problème 3 (port 5000) : confirmé sur cette machine

- `lsof -nP -iTCP:5000 -sTCP:LISTEN` : `ControlCenter` (AirPlay) écoute sur `*:5000` IPv4 et IPv6. Lab10 documente aussi `localhost:5000` (README) : choisir une convention de ports par lab pour tout le dépôt si le port par défaut change. Le test `LabCatalogTests` vérifie déjà que le dashboard n'utilise pas 5000.

### F. Autres constats

- Problème 7 : l'ancien `SecretKey` est présent dans les commits `52725fb` → `a58b374` (`Lab06_A2AServer/*/appsettings.json`), dépôt poussé sur `origin`. Réécrire l'historique n'est pas recommandé ; considérer la valeur comme brûlée (elle ne signe que des clés de démonstration).
- Problème 8 : `MongoDbHealthCheck` n'est utilisé que par Lab12 (Start/Solution) ; Lab07 et Lab12 référencent `MongoDB.Driver` 2.30.0, Lab05 3.12.0. La montée de `CommonUtilities` en 3.12.0 doit se faire avec Lab07 et Lab12 (sinon `NU1605`).
- Problème 5 : `Microsoft.Agents.AI.AgentSkill` existe bien dans MAF 1.22.0 (skills d'agent, sans rapport avec A2A). Correction durable : `<RootNamespace>` distinct (ou renommer le namespace des 6 fichiers Start/Solution du client) et alias `using AgentSkill = A2A.AgentSkill;`.

### Reprise : ordre des étapes

1. Rétablir l'accès Azure (A), relancer Lab01 Solution.
2. Vérifier le mode Streaming du filtre de contenu du déploiement et capturer un flux SSE (B, « non vérifié »).
3. Décider : filtre en mode Default (corrige tout de suite) ou attendre le correctif MEAI (#7792).
4. Retester Lab01/Lab02/Lab06 ; retirer le contournement.
5. Corriger les textes du problème 4 (C) et, si souhaité, l'ordre des liaisons avec les checks du dashboard.
6. Traiter D, E, 5, 6, 7, 8 comme prévu dans le plan.
