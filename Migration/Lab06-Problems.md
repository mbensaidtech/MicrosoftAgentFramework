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
