---
title: Dashboard AI Model Configuration - Trainer and Custom Models
status: implemented (T12 pending: hosted routes)
priority: high
author: PM Agent
created: 2026-10-25
updated: 2026-10-26
jira_project: _pending_
jira_epic: _pending_
---

# Dashboard AI Model Configuration - Trainer and Custom Models

## Summary

The Lab Bench dashboard (the candidate's local dashboard, `Dashboard/LabDashboard`) lets the candidate configure **two AI models independently**: the main **LLM** (chat) and the **embedding** model. For each one, the candidate chooses between the **trainer's configuration** (retrieved from the trainer's Admin Dashboard through a new API route, unlocked with a 6-digit code given by the trainer) and **their own configuration** (their own Azure OpenAI deployment and credentials, typed in a form). Both paths save the result in the **shared .NET user-secrets file** that every lab already reads, with the same keys and conventions, so the labs use it transparently from the dashboard and from `dotnet run`.

The Admin Dashboard routes are defined by the **Admin API contract v1** (OpenAPI `Lab Bench Admin — AI model configurations (local dashboard API)` 1.0.0, received on 2026-10-25), summarized under [Admin API contract v1](#admin-api-contract-v1). The routes were not deployed on the hosted admin yet on that date (404). Phase 1 built everything that did not depend on the contract, with the trainer side behind an isolated integration layer and a mock. Phase 2 plugs the contract in, and is tested against the official test vector and a fake admin server until the hosted routes are deployed.

## Problem Statement

- Today the candidate must obtain the endpoint, the deployment name and the API key out of band (mail, slide…) and type them in the *Azure OpenAI* settings form. Nothing in the repository says where these values come from (see `AZUREOPENAI_LABS_READINESS_REPORT.md`, § 7).
- The form only covers the LLM (endpoint, chat deployment, API key). The embedding deployment of Lab07 (`AzureOpenAI:EmbeddingDeploymentName`) can only be set with `dotnet user-secrets` or an environment variable: a candidate who only uses the dashboard is stuck at Lab07.
- Lab07 builds its embedding client from the same endpoint and key as the LLM: a candidate cannot use the trainer's LLM with their own embedding resource, or the opposite.
- A trainer who wants to hand out a shared Azure OpenAI resource has no channel to do it safely: keys travel in clear text in chats or slides.

## Goals

- [x] A candidate can configure the LLM and the embedding model **independently**, each in *trainer* or *custom* mode, from the Settings dialog only (no file to edit, no command to type).
- [x] Every configuration, trainer or custom, ends in the **shared user-secrets file** with the keys the labs read; the labs need no dashboard to use it.
- [x] Switching modes, or retrieving / refreshing the trainer's configuration, **never loses** the candidate's custom configuration.
- [x] The trainer retrieval path (review, Unlock, access code, apply — v1.1) is complete in the UI and testable end to end with a **mock**, behind an integration layer that phase 2 replaced without touching the UI or the storage.
- [x] Lab07 runs with an embedding resource different from the LLM resource.
- [x] No API key, decrypted trainer secret or access code ever appears in an API response, a log, the run history, the run output or the reporting to the admin dashboard.
- [x] Every existing feature keeps working: the current settings, the CLI runs, the dashboard tests (601 on 2026-10-26).

## Non-Goals

- **Providers other than Azure OpenAI** (OpenAI, DeepSeek, Anthropic…). The labs only call Azure OpenAI (v1 endpoint, API key or Microsoft Entra ID). The form has a provider selector ready for more, with Azure OpenAI as the only choice. Other providers need a dedicated spec, because they change the client creation code of every lab (TODO 2).
- **Changing the Admin Dashboard contract or its encryption protocol.** The dashboard implements contract v1 exactly; anything else is a change of the admin repository first.
- **Choosing among several trainer configurations of a kind.** The dashboard uses the configuration the trainer designates for each kind (`defaults.llm` / `defaults.embedding`); the others are listed for information.
- **A secrets vault.** The candidate dashboard runs on the candidate's own machine and only listens on `127.0.0.1`. .NET user secrets are designed for exactly this local development use. They are plain JSON in the user profile, protected by file permissions (`0700` folder, `0600` file on macOS/Linux), not encrypted. Hosting the dashboard would require another store (for example Azure Key Vault with a managed identity), which is out of scope.
- **An API version field.** The labs use the Azure OpenAI v1 API, which has no `api-version`; the form explains it instead of offering a field (same as today).
- **Reporting the new settings to the admin dashboard.** The existing `settings.changed` event (authentication mode only) does not change. Extending it would change the reporting contract.
- **Changing labs other than Lab07.**

## Changelog

- **v1.0** (2026-10-25) — Initial release. Phase 1: tasks T1..T9. Phase 2 (blocked until the Admin Dashboard contract is delivered): tasks T10..T12.
- **v1.1** (2026-10-25) — Admin API contract v1 received. T1, T2 and T4 delivered under v1.0, unchanged. Changes:
  - Business Rules 13–16 rewritten (5-character access code, `X-API-Key` + dev token, one held envelope, no decrypted data waiting); Rules 17–20 added (default configuration per kind, applicability, contract crypto, unlock errors).
  - The section "Contract-dependent points" is replaced by [Admin API contract v1](#admin-api-contract-v1); Edge Cases and UI / UX updated.
  - T5 and T6 rewritten to the contract flow (review before the code). T10 and T11 unblocked; T12 waits for the hosted routes.
  - Order of the remaining tasks: T10 → T11 → T5 → T6 → T7 → T8 → T9, then T12.
- **v1.2** (2026-10-26) — Distribution of the admin API key decided: a single **invitation code** sent by the trainer by e-mail carries the admin API key and the workshop key (Business Rules 21–23, task T13). Order of the remaining tasks: T10 → T11 → T13 → T5 → T6 → T7 → T8 → T9, then T12.
- **v1.3** (2026-10-26) — Contract amendment from the owner: at most 5 envelopes per minute (429 `tooManyEnvelopes`) and the 410 reason `configChanged` (Rules 19 and 20, Admin API contract v1). No new task: T10, T5 and T6 cover them.
- **v1.4** (2026-10-26) — Delivered: T3, T5–T11 and T13 (T1, T2, T4 earlier); T12 waits for the hosted routes. Decisions taken during the implementation:
  - Local routes of T5: `GET /api/trainer-models` (overview + unlock under way), `POST /api/trainer-models/unlock` `{ kinds }`, `POST /api/trainer-models/code` `{ code }`, `POST /api/trainer-models/cancel` (`TrainerUnlockFlow`). The code entry and the unlock run to completion even if the page goes away, so that an envelope the admin released is never lost half-way.
  - Rule 20 "at 0 the next attempt fetches a new envelope" and `exhausted` / `used`: the ticked kinds stay held and the new envelope is fetched lazily, by the next code. The hold of an envelope is `expiresAt − serverTime` from the local retrieval (10 minutes at most), so that a skewed clock does not shorten it.
  - T6 layout: one *Trainer's configuration* bar above the cards (Refresh, errors with the action that helps, Unlock, the code entry with a countdown) and, in each card, the trainer's default with its *Use the trainer's configuration* tick. A refused switch to *Trainer configuration* ticks the kind. In trainer mode, an offer identical to what is in use is not repeated; the bar then says the configuration is up to date.
  - T13: the trainer's command is `dotnet run --project Dashboard/LabDashboard -- invitation` (no dashboard started). Whitespace inside a pasted code (a line wrapped by a mail client) is ignored; keys must be visible ASCII (they travel as HTTP headers). The admin API key is read at each call by the reporting and the trainer flow.
  - The `Dashboard/README.md#azure-openai-settings` anchor is kept as an alias of *AI models settings*: 17 lab READMEs link to it and the other labs are out of scope.

---

## Reference

### Data Model

No database. The single store is the existing shared user-secrets file (`UserSecretsId` = `microsoft-agent-framework-learninglabs`, macOS/Linux `~/.microsoft/usersecrets/microsoft-agent-framework-learninglabs/secrets.json`, Windows `%APPDATA%\Microsoft\UserSecrets\…\secrets.json`). Writes stay atomic (temporary file + move) and private to the user. They keep every other entry of the file, as the current store does. Entries use the same flat `Section:Name` spelling that `dotnet user-secrets set` writes, and nested spellings are still read.

**Active configuration: read by the labs.** Only Lab07 reads the new keys.

| Key | Written from | Read by |
|---|---|---|
| `AzureOpenAI:Endpoint` | active LLM profile | every lab (existing) |
| `AzureOpenAI:ChatDeploymentName` | active LLM profile | every lab (existing) |
| `AzureOpenAI:APIKey` | active LLM profile (absent → Microsoft Entra ID) | every lab (existing) |
| `AzureOpenAI:EmbeddingDeploymentName` | active embedding profile | Lab07 (existing) |
| `AzureOpenAI:EmbeddingEndpoint` | active embedding profile, only when it uses its own resource | Lab07 (**new**, optional) |
| `AzureOpenAI:EmbeddingAPIKey` | active embedding profile, only when it uses its own resource | Lab07 (**new**, optional) |

**Dashboard-owned entries.** They live in the same file, are ignored by the labs (which only bind the `AzureOpenAI` section), and use the same flat convention.

```text
LabBench:Models:Chat:Mode                    custom | trainer
LabBench:Models:Chat:Custom:Provider         azure-openai
LabBench:Models:Chat:Custom:Endpoint
LabBench:Models:Chat:Custom:Deployment
LabBench:Models:Chat:Custom:APIKey           optional (absent → Microsoft Entra ID)
LabBench:Models:Chat:Trainer:Provider
LabBench:Models:Chat:Trainer:Endpoint
LabBench:Models:Chat:Trainer:Deployment
LabBench:Models:Chat:Trainer:APIKey
LabBench:Models:Chat:Trainer:RetrievedAt     ISO 8601 date of the last retrieval
LabBench:Models:Chat:Trainer:ConfigId        (v1.1) id of the trainer configuration (contract `configs[].id`)
LabBench:Models:Chat:Trainer:ConfigName      (v1.1) its name, for display
LabBench:Models:Chat:Trainer:ConfigUpdatedAt (v1.1) its `updatedAt`, to show "updated by the trainer since your last unlock"

LabBench:Models:Embedding:Mode               custom | trainer
LabBench:Models:Embedding:Custom:Provider
LabBench:Models:Embedding:Custom:SameResourceAsLlm   true | false
LabBench:Models:Embedding:Custom:Endpoint    only when SameResourceAsLlm = false
LabBench:Models:Embedding:Custom:Deployment
LabBench:Models:Embedding:Custom:APIKey      only when SameResourceAsLlm = false; optional
LabBench:Models:Embedding:Trainer:Provider
LabBench:Models:Embedding:Trainer:Endpoint
LabBench:Models:Embedding:Trainer:Deployment
LabBench:Models:Embedding:Trainer:APIKey
LabBench:Models:Embedding:Trainer:RetrievedAt
LabBench:Models:Embedding:Trainer:ConfigId / ConfigName / ConfigUpdatedAt   (v1.1)
```

The access code, the envelope, the proof, the unlock key and the decrypted plaintext are **never** written anywhere. Only the opened API key of a trainer profile is stored, in this file.

### Business Rules

1. **Two model kinds, independent.** The LLM and the embedding model each have a mode, `custom` or `trainer`. Changing one never changes the other.
2. **Phase 1 provider.** The only accepted provider is `azure-openai`. The server rejects any other value (`unsupportedProvider`), even if the browser sends it.
3. **Custom Azure OpenAI fields** reuse the current validations:
   - Endpoint: absolute `https` URL, no query, fragment or user info, and not a `YOUR-…` placeholder. The `/openai/v1/` suffix is added by the labs.
   - Deployment: letters, digits, `.`, `_`, `-`, up to 64 characters, and not a placeholder.
   - API key: no whitespace, at least the redactor's minimum length.
4. **LLM API key optional.** An empty key means Microsoft Entra ID, as today: the labs use `DefaultAzureCredential` when `AzureOpenAI:APIKey` is absent.
5. **Embedding resource.** A custom embedding profile either uses the **same resource as the LLM in use** (default; only the deployment is entered) or **its own resource** (endpoint, deployment, optional key). "Same resource as the LLM" follows the active LLM, whether custom or trainer.
6. **Applying a profile.** The selected profile of each kind is written to the active `AzureOpenAI:*` keys of the Data Model.
   - LLM: `Endpoint`, `ChatDeploymentName` and `APIKey`; an absent key removes `APIKey`.
   - Embedding on the same resource: `EmbeddingDeploymentName` is written, and `EmbeddingEndpoint` and `EmbeddingAPIKey` are removed.
   - Embedding on its own resource: all three embedding keys are written; an absent key removes `EmbeddingAPIKey`, so Lab07 uses Microsoft Entra ID for the embeddings.
7. **Lab07 resolution.**
   - `AzureOpenAI:EmbeddingEndpoint` set: the embedding client uses that endpoint, with `EmbeddingAPIKey` if set, otherwise Microsoft Entra ID. It **never** reuses the LLM key on another endpoint.
   - `EmbeddingEndpoint` absent: the embedding client uses the LLM endpoint and key (today's behavior).
8. **Switching mode** never deletes a profile. Switching a kind to a mode whose profile is incomplete is refused (`profileIncomplete`), and the UI opens what is missing: the form, or the trainer retrieval.
9. **Trainer retrieval and refresh** only write the trainer profiles of the kinds the trainer provides.
   - Custom profiles are never touched.
   - The active keys of a kind change only if that kind is in trainer mode, or if the candidate chooses "use it" for that kind while confirming.
10. **Backward compatibility.** When no `LabBench:Models:*` entry exists yet:
    - the existing `AzureOpenAI:Endpoint`, `ChatDeploymentName` and `APIKey` become the custom LLM profile, in `custom` mode;
    - an existing `AzureOpenAI:EmbeddingDeploymentName` becomes the custom embedding profile, on the same resource as the LLM.

    Nothing is lost and the labs keep working without any action.
11. **Environment variables win.** An `AzureOpenAI__*` variable, including the new embedding ones, overrides the user secrets for the labs. Each field shows it, as the current form does.
12. **Keys are write-only.**
    - API responses only say whether a key is stored, with the last 4 characters at most.
    - Every stored key of every profile is masked in the run output and the history.
    - Keys never appear in logs or in the reporting.
13. **The access code (v1.1):**
    - format: **5 characters** from the alphabet `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (no 0, O, 1, I or L);
    - normalization before validation: trim, invariant upper-case, remove spaces and dashes (`k7qx-9` and ` K7QX9 ` both mean `K7QX9`);
    - checked in the browser and again on the server, before any call (`0OIL1` is refused locally);
    - **never sent to the admin**: only the HMAC proof of Rule 18 travels;
    - kept in memory only for one unlock request, including the single automatic retry of Rule 20;
    - never persisted, never logged, never returned to the browser, never reported.
14. **What stays in memory (v1.1):**
    - At most **one envelope**, held by the dashboard server from the "Unlock" click until success, cancel or its `expiresAt` (10 minutes). Without the code and the unlock key, the envelope cannot be decrypted.
    - Once the envelope is opened, the keys of the selected kinds are **written immediately** to the user-secrets file and every buffer is wiped. Nothing decrypted waits for a confirmation: the candidate confirms *before* typing the code (Rule 17).
15. **Integration layer (v1.1).** The contract is implemented behind the T4 seam (source + decryptor), so that the UI and the storage do not depend on it.
    - **Real adapter**: used whenever the mock is off.
    - **Mock**: unchanged, for development and tests only, off by default, with its "Simulated trainer configuration" banner.
    - **Error kinds**:
      - `notConfigured` (missing: `serverUrl`, `apiKey` or `workshop`);
      - `unauthorized` (`invalidApiKey` or unknown dev token);
      - `forbidden` (archived developer);
      - `rateLimited` (`retryAfterSeconds`, `reason` `lockedOut` / `accessCodeFrozen`);
      - `unavailable` (server not configured for keys);
      - `sealedUnavailable` (contract `sealedUnavailableReason`);
      - `invalidCode` (`attemptsLeft`);
      - `envelopeExpired` (contract `reason`);
      - `notFound` (route absent on the server: 404);
      - `network`, `invalidResponse`, `unknown`.
16. **Authentication (contract v1).** Every admin call sends:
    - `X-API-Key`, with the reporting's resolution (`LABS_ADMIN_API_KEY`, else `Dashboard:Reporting:ApiKey`);
    - `Authorization: Bearer <dev token>`, from the identity created when the candidate joined the workshop.

    The `devId` of the protocol is that identity's `userId`, lowercase as registered. Base URL: `Dashboard:Reporting:ServerUrl`.

    Without a URL, an API key or an identity, trainer mode shows `notConfigured`. The UI then says what is missing ("Join the workshop first" leads to the Workshop tab).
17. **Which configuration, and is it usable (v1.1).**
    - **Selection**: for each kind, the dashboard uses `defaults.llm` / `defaults.embedding`. The other configurations are listed for information.
    - **Provider**: a default whose provider is not `azure-openai` is shown as **not applicable** (the labs only call Azure OpenAI) and cannot be applied. Unknown providers are ignored.
    - **Unsealed**: a configuration listed in `unsealedConfigs` is unusable ("tell the trainer").
    - **Dimensions**: an embedding configuration whose `embedding.dimensions` is not **1536** shows a warning, because Lab07 builds its vector store with 1536 dimensions.
    - **Mapping**:
      - `settings.endpoint` → endpoint; `settings.deployment` → deployment;
      - envelope `configs[<id>].apiKey` → API key; a configuration without `apiKey` in `secretFields` gives a profile without a key (Microsoft Entra ID);
      - `settings.apiVersion`, `model` and `parameters` are informational: the labs use the v1 API.
    - **Review before the code**: the candidate sees the default configurations (name, provider, endpoint host, deployment, applicability, warnings) and ticks the kinds to use *before* clicking Unlock.
    - **Change detection**: the stored `ConfigUpdatedAt` is compared with `updatedAt` to show "updated by the trainer since your last unlock".
18. **Envelope protocol (contract v1, exact).**
    - **Validate** before deriving anything: `version` 1, `cipher` `AES-256-GCM`, `kdf.name` `PBKDF2-HMAC-SHA256`, 600000 ≤ `iterations` ≤ 10000000, salt of 16 bytes, `iv` of 12 bytes.
    - **Code key**: `K_code = PBKDF2-HMAC-SHA256(code, salt, iterations, 32)`.
    - **Proof**: `proof = base64url(HMAC-SHA256(K_code, "labs-admin/model-unlock/v1|<envelopeId>|<devId>"))`, sent to `POST /api/v1/model-configs/unlock`.
    - **Envelope key**: `K_env = HKDF-SHA256(K_code ‖ unlockKey, salt ∅, info "labs-admin/model-envelope/v1|<envelopeId>|<devId>", 32)`.
    - **Decrypt** with AES-256-GCM: the last 16 bytes are the tag; associated data `"labs-admin/model-envelope/v1|<envelopeId>|<devId>|<expiresAt>"`, with `expiresAt` used verbatim as received.
    - **Validate the plaintext**: `v` = 1 and `envelopeId` equal to the envelope's.
    - **Wipe**: `K_code`, the unlock key, `ikm`, `K_env` and the plaintext buffer, after deserializing it.

    Official test vector: code `K7QX9`, devId `8d2b6c1e-4f3a-4e7b-b2a9-5c0d1e2f3a4b`, the example envelope and unlock key of the contract → proof `Pl-wK3wMsxjqkLcphHjD6UuRxNzCtLH9f9Q0Hg8rGlE`, plaintext with the key `test-vector-api-key-not-a-real-secret`. Checked on 2026-10-25.
19. **Calling the admin politely (contract limits per developer: 30 GET / min, of which at most 5 `?sealed=1` / min; 20 unlock / min).**
    - The overview (`GET /api/v1/model-configs`, no `sealed`) is called when the AI models tab opens, or on an explicit **Refresh**. It is never polled.
    - `?sealed=1` is called only when the candidate clicks **Unlock**: never polled, never retried in a loop.
20. **Unlock errors (contract v1).**
    - `invalidAccessCode`: "Wrong code (N attempts left)". At 0, the next attempt fetches a new envelope first.
    - `410 envelopeExpired`:
      - `expired`, `unknown`, `keyRotated`: fetch a new envelope and retry **once** with the same code, without asking again;
      - `exhausted`, `used`: fetch a new envelope and ask for the code again;
      - `configChanged` (contract amendment of 2026-10-26: a configuration of the envelope was deleted, disabled, re-targeted or got a new key): reload the overview, so that the candidate reviews the new configuration, then unlock again with a new envelope and the code;
      - other reasons: show the reason ("ask the trainer…").
    - `429 rateLimited`: show the wait (`Retry-After`) and do not retry before. The reason picks the message: `lockedOut` and `accessCodeFrozen` mean too many wrong codes; `tooManyEnvelopes` (on Unlock) means too many unlocks started; no reason means too many calls.
    - `403`: "your participant account is archived".
    - `503 unavailable`, `500`: "tell the trainer" / "try again later".
    - A GCM tag failure after a 200: treated as a wrong code or tampering; nothing is used.
21. **Invitation code (v1.2).** The trainer sends, by e-mail, one invitation code that carries the admin API key and the workshop key.
    - **Format**: `LB1.` followed by the base64url (no padding) of the UTF-8 JSON `{"apiKey":"<admin API key>","workshopKey":"<workshop key>"}`.
    - **Size**: 1,024 characters at most.
    - **Refused**:
      - a code that cannot be decoded, or that lacks one of the two values: `invalid`;
      - another version prefix (`LB2.`…): `unsupportedVersion`.
    - **Where it is typed**: in the field of the welcome dialog *Join the workshop* and of the Settings *Workshop* tab, where the workshop key is typed today. A plain workshop key, without the prefix, is still accepted as before.
22. **Storing and resolving the admin API key (v1.2).**
    - The admin API key of an invitation is stored as `Dashboard:Reporting:ApiKey` in the shared user-secrets file; the workshop key is stored where it is today.
    - **Resolution order**:
      1. `LABS_ADMIN_API_KEY`;
      2. the user-secrets value;
      3. `appsettings.Local.json` / `appsettings.json`.
    - The resolution is used by the reporting and by the trainer configuration alike. It applies **without restarting the dashboard**.
    - A new invitation clears a previous `invalidApiKey` halt of the reporting.
    - The admin API key and the workshop key are never displayed back (only "configured" and their source), never logged, never reported. They are as sensitive as each other: the e-mail must not be posted in a public channel.
23. **Generating an invitation (v1.2).**
    - The repository provides the trainer with a cross-platform command that prints an invitation code from an admin API key and a workshop key.
    - It works without network, writes nothing to disk, and never echoes the keys.
    - It refuses empty values and values with whitespace.

### Admin API contract v1

Received on 2026-10-25 (OpenAPI `Lab Bench Admin — AI model configurations (local dashboard API)` 1.0.0; algorithm details, C# reference and test vector in the admin repository's `specs/ai-model-configurations/local-dashboard-contract.md`). Server-to-server only: the browser page never calls the admin.

| Route | Use in the dashboard | Notes |
|---|---|---|
| `GET /api/v1/model-configs` | Overview when the AI models tab opens or on Refresh (Rule 19) | `configs[]` (id, name, kind `llm`/`embedding`, provider, settings, parameters, secretFields, isDefault, embedding {model, dimensions, space}, updatedAt), `defaults` {llm, embedding}, `sealed` null, `sealedUnavailableReason` `notRequested`. Never a key |
| `GET /api/v1/model-configs?sealed=1` | When the candidate clicks Unlock (at most 5 envelopes per minute per developer, else 429 `rateLimited` `tooManyEnvelopes`) | Same, plus `sealed` (envelope valid 10 min: version, envelopeId, expiresAt, cipher, iv, ciphertext, kdf {name, iterations, salt}, code {length, alphabet}, attemptsAllowed), `sealedConfigIds`, `unsealedConfigs`; or `sealed` null with `sealedUnavailableReason` (`noSecrets`, `noWorkshop`, `workshopClosed`, `noAccessCode`, `accessCodeExpired`, `accessCodeUnreadable`, `secretsUnreadable`, `unavailable`) |
| `POST /api/v1/model-configs/unlock` | With the proof of Rule 18 | Body exactly `{ envelopeId, proof }`; 200 `{ envelopeId, unlockKey }` once; errors 400 `validation`, 401 `invalidAccessCode` (`attemptsLeft`) / `invalidApiKey` / `unauthorized`, 403 `forbidden`, 410 `envelopeExpired` (`reason`, including `configChanged` since the contract amendment of 2026-10-26), 429 `rateLimited` (`retryAfterSeconds`, `reason` `lockedOut` / `accessCodeFrozen`), 500 `internal`, 503 `unavailable` |

Common: headers of Rule 16 on every call; HTTPS only; JSON UTF-8; base64url without padding; error body `{ error, message, details? }` (the `message` is for logs: user messages are built from `error` and `details`); `Cache-Control: no-store`; ignore unknown properties, providers and settings keys.

Client security requirements of the contract (all binding): decrypt only in the .NET server process; never log or persist the code, `K_code`, the proof, the unlock request/response bodies, `unlockKey`, the plaintext or the keys; validate the envelope before deriving and the plaintext after decrypting; wipe derived keys and buffers; register opened keys with the output redaction; store opened values only in the shared user-secrets file.

Status on 2026-10-25: the routes answer 404 on the hosted admin (not deployed yet). Until then, phase 2 is tested with the official test vector and an in-process fake admin server.

### Edge Cases

| Case | Expected behavior |
|---|---|
| First launch after the update, with an existing configuration | Rule 10: values shown in the custom LLM profile; Lab01 still passes without any action |
| Fresh machine, nothing configured | Both kinds in custom mode, empty forms; the labs report "not configured" as today |
| `AzureOpenAI__*` environment variable set | The field shows "set on this computer, wins over the value saved here"; saving still works but the variable stays effective (Rule 11) |
| Admin URL or admin API key missing, or the candidate has not joined the workshop | Trainer mode shows `notConfigured` with what is missing ("Join the workshop first" leads to the Workshop tab); custom mode works |
| Admin routes not deployed (404) | `notFound`: "The trainer configuration is not available on this server yet"; custom mode works |
| Network down or timeout | `network` error with a retry action; nothing is written |
| Admin API refuses the key (401 `invalidApiKey`) or the dev token (401 `unauthorized`) | `unauthorized`, saying which one (wrong project API key / join the workshop again); nothing is written |
| `sealedUnavailableReason` on Unlock (`noAccessCode`, `accessCodeExpired`, `workshopClosed`, `noWorkshop`, `noSecrets`, server-side reasons) | The matching message ("Ask the trainer for an access code", "…expired: ask the trainer for a new one", "This workshop is closed", "Join a workshop first", "No key to unlock", "Tell the trainer"); no code prompt |
| Default configuration of a kind is not Azure OpenAI, or is in `unsealedConfigs` | Shown as not applicable / unusable for that kind; it cannot be ticked |
| Embedding default with `dimensions` ≠ 1536 | Warning "Lab07 expects 1536 dimensions"; it can still be applied |
| Too many wrong codes (429 `lockedOut` / `accessCodeFrozen`) | "Too many wrong codes: try again in N minutes" (from `Retry-After`); no retry before |
| Trainer provides only the LLM (no embedding default) | Only the LLM can be ticked and unlocked; embedding trainer mode stays unavailable (`profileIncomplete`) |
| Code not made of 5 characters of the alphabet after normalization (`0OIL1`, `K7QX`, `K7QX99`) | Inline error, submit disabled; the dashboard server also rejects it, and nothing is sent to the admin |
| Wrong code | `invalidCode` with the attempts left on this envelope; at 0 the next attempt fetches a new envelope; the code is never kept |
| Envelope expired, used or invalidated (410) | Rule 20: one automatic retry with a new envelope for `expired` / `unknown` / `keyRotated`, a new prompt for `exhausted` / `used`, the reason otherwise |
| Candidate closes the dialog or waits more than 10 minutes before typing the code | The held envelope is dropped (Rule 14); the next Unlock fetches a new one; nothing written |
| Switch to trainer mode without a retrieved trainer profile | Refused (`profileIncomplete`); the UI offers to retrieve it |
| Switch back to custom mode | The custom profile reappears as typed before; active keys rewritten from it |
| User-secrets file is not valid JSON | Same as today: nothing is overwritten; the error shows the file path |
| A lab is running while the configuration changes | The running lab keeps its configuration; the next run uses the new one (as today) |
| Embedding on its own resource without a key | Lab07 uses Microsoft Entra ID for the embeddings, never the LLM key (Rule 7) |

### UI / UX

| View | Where | Purpose |
|---|---|---|
| Settings → **AI models** tab (replaces the *Azure OpenAI* tab) | Settings dialog, opened from the top bar chip | Two cards, **LLM** and **Embedding model**, each with its mode switch, its form or trainer summary, and its effective source |
| Trainer retrieval flow | Inside the Settings dialog | Retrieve → enter the 6-digit code → review the non-sensitive information → confirm, per kind |

- Each card has a two-choice mode switch: **Trainer configuration** / **My configuration**. The switch is keyboard accessible and announces the active mode.
- **My configuration** shows the provider selector (Azure OpenAI only in phase 1) and the Azure OpenAI fields. The embedding card adds the choice "Same resource as the LLM / Its own resource". The current hints are kept: deployment, not the model name; no API version, the labs use the v1 API; no key means Entra ID.
- **Trainer configuration** (v1.1) shows the trainer's default configuration for the kind, read from the overview without any code: name, provider, endpoint host, deployment, model, "key provided", applicability and warnings (Rule 17), "updated by the trainer since your last unlock" when relevant, and the date of the last unlock. Actions: **Refresh** (reloads the overview) and, for an applicable configuration, a tick "use it" per kind.
- **Unlock** (v1.1): one **Unlock** button for the ticked kinds. It fetches the envelope (or shows the `sealedUnavailableReason` message), then opens the **access code** entry: 5 characters, upper-cased as typed, pasting `k7qx-9` works, inline format error, and submit disabled until the format is valid. The code is cleared from the page after each attempt. On success: "Models unlocked", the ticked kinds switch to trainer mode, and the keys appear only as "provided".
- **States**: loading spinner on Retrieve and Unlock; success toast "Settings saved · used by the next run"; errors in a notice, with retry when it makes sense; a "Simulated trainer configuration" banner when the mock is on.
- Keys are masked after saving (`••••` plus the last 4 characters) and can be removed.
- **The top bar chip** keeps showing the LLM authentication status, as today.
- All texts exist in **English and French** (existing i18n).

### User Stories

#### Story 1: Candidate uses the trainer's models
**As a** candidate in a workshop, **I want to** retrieve the trainer's LLM and embedding configuration with the code the trainer gives me, **So that** I can run every lab without typing endpoints and keys.

#### Story 2: Candidate uses their own models
**As a** candidate with my own Azure OpenAI resource, **I want to** type my own deployments and keys for the LLM and the embedding model, **So that** I use my own quota.

#### Story 3: Candidate mixes both
**As a** candidate, **I want to** use the trainer's LLM with my own embedding deployment (or the opposite), **So that** I can work around a missing or saturated deployment.

#### Story 4: Candidate switches back and forth
**As a** candidate, **I want to** switch a model between trainer and custom mode without retyping my own values, **So that** I can compare or fall back quickly.

#### Story 5: Existing candidate updates the dashboard
**As a** candidate who already configured Azure OpenAI, **I want** my configuration to keep working after the update, **So that** nothing breaks during the workshop.

---

## Tasks

> Phase 1: T1..T9. Phase 2: T10..T12, blocked until the Admin Dashboard contract is delivered.

### T1 — [backend] Independent LLM and embedding profiles saved in the user secrets

**Jira**: _pending_
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: The dashboard keeps, for the LLM and for the embedding model, a custom profile, a trainer profile and the selected mode. It writes the selected profiles to the keys the labs read. A candidate who already had a configuration finds it as their custom LLM profile, and their labs keep working without doing anything.

**Business constraints**:
- Data Model, Business Rules 1–2, 3–6, 8–12
- Same file, same flat convention, same atomic and private writes as today; other entries untouched
- Keys never returned: only "stored" plus a 4-character hint

**Success criteria**:
- [ ] After the update, a machine with an existing `AzureOpenAI:*` configuration still runs Lab01 to Passed without any action, and the settings show these values as the custom LLM profile
- [ ] Saving a custom embedding profile on its own resource writes `AzureOpenAI:EmbeddingEndpoint`, `EmbeddingDeploymentName` and `EmbeddingAPIKey`; switching it to "same resource as the LLM" removes the endpoint and key entries
- [ ] Switching the LLM to trainer mode and back restores the custom values in `AzureOpenAI:*`, unchanged
- [ ] Switching to a mode whose profile is incomplete is refused with `profileIncomplete`, and nothing is written
- [ ] A provider other than `azure-openai` is refused with `unsupportedProvider`
- [ ] No API response of the dashboard contains a stored key

**References**: Data Model, Business Rules 1–6, 8–12, Edge Cases (first launch, fresh machine, environment variable, invalid file, switch), Stories 2, 4, 5

---

### T2 — [backend] Lab07 can use an embedding resource different from the LLM

**Jira**: _pending_
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: A candidate can run Lab07 with the embeddings on another Azure OpenAI resource than the LLM. A candidate who configures nothing new sees no difference. The Lab07 README explains the two optional settings and how they fall back.

**Business constraints**:
- Business Rule 7 (never reuse the LLM key on another endpoint)
- Start and Solution stay identical outside the TODO areas; the TODO hints and the README stay consistent with the code
- No other lab changes

**Success criteria**:
- [ ] With only today's settings, Lab07 Solution passes 12/12 in the dashboard, as before
- [ ] With `AzureOpenAI:EmbeddingEndpoint` and `EmbeddingAPIKey` set (even to the same resource), Lab07 passes 12/12, and the printed configuration shows the embedding endpoint in use
- [ ] With `EmbeddingEndpoint` set and no `EmbeddingAPIKey`, Lab07 uses Microsoft Entra ID for the embeddings
- [ ] The delivered Lab07 Start still builds with 0 warnings and runs with exit code 0

**References**: Data Model (active keys), Business Rules 5–7, Edge Cases (embedding without a key), Story 3

---

### T3 — [frontend] "AI models" settings with a mode per model and the custom forms

**Jira**: _pending_
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T1

**User outcome**: In Settings, the candidate opens **AI models** and sees two cards, **LLM** and **Embedding model**. In each card, they choose **My configuration** and fill in the Azure OpenAI form (provider, endpoint, deployment, key; for the embedding, the same resource as the LLM or its own). They save it and see the effective value and its source for every field. The keys are masked after saving and can be removed.

**Business constraints**:
- Business Rules 2–5, 8, 11, 12; UI / UX section
- Inline validation errors identical to the server's
- Works at phone width, keyboard accessible, English and French

**Success criteria**:
- [ ] The candidate can save an LLM profile and an embedding profile separately; reopening Settings shows them as saved
- [ ] Invalid endpoint, deployment or key show the same error as the server, on the right field, and nothing is saved
- [ ] After saving, a key is shown as `••••` plus its last 4 characters, never in clear, and **Remove** deletes it
- [ ] With an `AzureOpenAI__*` variable set, the field says it is set on this computer and wins
- [ ] Lab01 and Lab07 then pass from the dashboard with values entered only through this form

**References**: UI / UX, Business Rules 2–5, 8, 11, 12, Edge Cases (environment variable, fresh machine), Stories 2, 4

---

### T4 — [backend] Trainer configuration integration layer with a mock

**Jira**: _pending_
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: Behind the scenes, the dashboard can "retrieve" and "unlock" a trainer configuration through one internal integration point. By default it honestly answers that the trainer configuration is not available yet, without any network call. With the mock enabled in the configuration, it returns a simulated configuration, so that the whole flow can be built and tested before the Admin Dashboard route exists.

**Business constraints**:
- Business Rules 13–16; Contract-dependent points (nothing beyond them is assumed)
- No URL, field name, payload format or cryptographic protocol of the real route is encoded
- The mock is off by default, cannot be enabled from the browser, and only returns obviously fake values

**Success criteria**:
- [ ] Without the mock, "retrieve" returns the `notAvailable` state and no outgoing HTTP request is made
- [ ] With the admin URL or API key missing, "retrieve" returns `notConfigured`, naming what is missing
- [ ] With the mock on, "retrieve" then "unlock" with the mock's code returns the simulated LLM and embedding profiles; a wrong code returns `invalidCode`
- [ ] The mock can simulate each error kind of Rule 15 for tests

**References**: Business Rules 13–16, Contract-dependent points, Edge Cases (phase 1 without the mock, network, 401, wrong code)

---

### T5 — [security] Unlock the trainer's keys with the access code and apply them safely (v1.1)

**Jira**: _pending_
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T1, T4, T10, T11

**User outcome**: In the AI models tab, the candidate sees the trainer's default configurations, with no code needed. They tick the kinds to use and click Unlock. They type the access code announced by the trainer. The keys are opened, saved for the ticked kinds, and those kinds switch to trainer mode, while their own profiles stay untouched. A wrong, expired or refused code gives a clear message and saves nothing.

**Business constraints**:
- Business Rules 9, 12–20; Edge Cases; Admin API contract v1 (client security requirements)
- Local routes for the page: overview, unlock start (fetches and holds the envelope), code submit, cancel. Same CSRF protection as every state-changing request
- Nothing decrypted waits in memory; at most one envelope held, dropped at success, cancel or `expiresAt`
- The code, the proof, the unlock key, the plaintext and the keys never reach the browser, a log, the history or the reporting

**Success criteria**:
- [ ] With the fake admin server: overview → tick LLM and embedding → Unlock → right code: both trainer profiles are saved with the opened keys, the two kinds are in trainer mode, the labs read the trainer values, and the custom profiles are unchanged
- [ ] A wrong code shows the attempts left; at 0 the next attempt uses a new envelope; `expired` / `unknown` / `keyRotated` are retried once automatically; `exhausted` / `used` ask for the code again
- [ ] Every `sealedUnavailableReason`, `429` (with the wait), `403`, `401` and `503` gives its message; nothing is written
- [ ] A code that is not 5 alphabet characters after normalization is refused by the dashboard server without any call to the admin
- [ ] No response to the browser and no log line contains the code, the proof, the unlock key, the plaintext or a key; opened keys are masked in lab run output

**References**: Admin API contract v1, Business Rules 9, 12–20, Edge Cases, Stories 1, 3

---

### T6 — [frontend] Trainer configurations and unlock flow in the AI models tab (v1.1)

**Jira**: _pending_
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T3, T5

**User outcome**: Each model card shows the trainer's configuration for that kind: name, provider, endpoint host, deployment, applicability, warnings, and "updated since your last unlock" when it changed. The candidate ticks the kinds to use and clicks Unlock. They type the 5-character code and see "Models unlocked", or a clear message: wrong code with attempts left, wait time, ask the trainer, join the workshop. Loading states and the "simulated configuration" banner are visible.

**Business constraints**:
- UI / UX section; Business Rules 13, 17, 19, 20; Edge Cases
- The code field accepts the alphabet only (upper-cased as typed, pasting `k7qx-9` works), is cleared after each attempt, and is never stored by the page
- English and French; keyboard and screen-reader accessible; phone width

**Success criteria**:
- [ ] Opening the tab shows the trainer's default configurations without any code, or the reason they are not available (not joined, not configured, route not deployed)
- [ ] With the fake admin server, a candidate goes from Unlock to an active trainer LLM in under a minute, then back to their own configuration with the values typed before
- [ ] Each error of Rule 20 and each `sealedUnavailableReason` shows its message; retry is offered only when it makes sense
- [ ] Submit stays disabled until 5 valid characters are entered
- [ ] A non-Azure default or a non-1536 embedding shows its notice; a non-applicable configuration cannot be ticked
- [ ] No key, proof or unlock key is ever present in the page

**References**: UI / UX, Business Rules 13, 17, 19, 20, Edge Cases, Stories 1, 3, 4

---

### T7 — [test] Automated tests of the profiles, the Lab07 fallback and the trainer flow with the mock

**Jira**: _pending_
**Agent hint**: unit-testing:test-automator
**Depends on**: T1, T2, T4, T5

**User outcome**: The maintainers can run `dotnet test` (from `Dashboard/`) and get a regression safety net for everything phase 1 adds, including the security rules. The flows are tested without the real Admin Dashboard route.

**Business constraints**:
- Cover Business Rules 1–16 and the Edge Cases table; existing tests keep passing
- Tests never touch the real user-secrets file of the machine, nor the network

**Success criteria**:
- [ ] `dotnet test` passes, with the existing tests and the new ones
- [ ] A test fails if a stored key, the code or the decrypted payload appears in an API response or a log
- [ ] A test fails if switching modes or refreshing the trainer configuration changes a custom profile
- [ ] A test fails if the integration layer makes a network call without the real adapter
- [ ] The Lab07 fallback (Rule 7) is checked by a real run or by a test of its configuration resolution

**References**: Business Rules 1–16, Edge Cases

---

### T8 — [glue] Texts, documentation and the list of contract-dependent points

**Jira**: _pending_
**Agent hint**: code-improver
**Depends on**: T3, T6

**User outcome**: The candidate reads in English and French what each setting does and where the values come from: the trainer gives a code, or they use the values of their own Azure resource (*Keys and Endpoint*). The maintainers find, in one place, what phase 2 must plug in once the Admin Dashboard contract is known.

**Business constraints**:
- Dashboard README (settings and API sections), root README (*Configure Azure OpenAI*, with a short "Where do I get these values?"), Lab07 README (T2), this spec's contract table kept up to date
- The previous spec `dashboard-azure-openai-settings` gets a changelog line pointing to this spec

**Success criteria**:
- [ ] Every new label, hint and error exists in English and French
- [ ] The root README says where the endpoint, deployment and key come from, and how the trainer configuration is retrieved
- [ ] The Dashboard README documents the new settings and the integration layer (default, mock, how to enable the mock for tests), and lists the contract-dependent points

**References**: UI / UX, Contract-dependent points, Non-Goals

---

### T9 — [test] End-to-end check of phase 1 in a browser

**Jira**: _pending_
**Agent hint**: unit-testing:test-automator
**Depends on**: T2, T6, T7, T8

**User outcome**: The owner gets evidence that phase 1 works as a candidate experiences it, on a machine without `AzureOpenAI__*` variables:
- the custom configuration of each model;
- the trainer flow with the mock;
- the mix of trainer LLM and custom embedding;
- the labs passing after each configuration.

**Business constraints**:
- Run with a temporary user profile, never the owner's real secrets, and with reporting disabled
- Real Azure calls only for the lab runs

**Success criteria**:
- [ ] Custom LLM plus custom embedding entered in the browser: Lab01 and Lab07 pass
- [ ] Trainer flow with the mock: retrieve, code, review and apply work, and the custom profiles are intact afterwards
- [ ] No console error in the browser; the existing tests and settings still work

**References**: Stories 1–5, Edge Cases

---

### T10 — [backend] Admin API client for the model configurations (contract v1)

**Jira**: _pending_
**Agent hint**: backend-development:backend-architect
**Depends on**: T4

**User outcome**: When the mock is off, the dashboard reads the trainer's configurations from the real Admin Dashboard: the overview, and the envelope on Unlock. It uses the project's admin API key and the candidate's dev token. Every documented answer, success or error, becomes a clear state for the page.

**Business constraints**:
- Admin API contract v1 (routes, headers, error bodies, `Cache-Control`, forward compatibility); Business Rules 15–17, 19
- Same base URL, API key resolution and identity (dev token, `userId`) as the reporting; HTTPS only, except a `localhost` admin used for local development
- No retry loop; `Retry-After` honoured; nothing secret logged

**Success criteria**:
- [ ] Against a fake admin server: the overview maps `configs`, `defaults`, applicability and the 1536 warning; `?sealed=1` returns the envelope or the `sealedUnavailableReason`
- [ ] 401 (`invalidApiKey` / `unauthorized`), 403, 404, 429, 500, 503, timeouts and malformed bodies each give the right error kind; nothing is written
- [ ] Without an identity (not joined), no call is made and the state is `notConfigured` (`workshop`)

**References**: Admin API contract v1, Business Rules 15–17, 19

---

### T11 — [security] Envelope opening exactly as the contract defines it

**Jira**: _pending_
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T10

**User outcome**: The access code announced by the trainer opens the envelope with exactly the protocol of the Admin Dashboard. The code never leaves the candidate's machine: only a proof does. A wrong code, a tampered envelope or a dubious parameter is refused, and nothing is used.

**Business constraints**:
- Business Rules 13, 18, 20; Admin API contract v1 (client security requirements)
- .NET base library only (PBKDF2, HMAC-SHA256, HKDF, AES-GCM); no home-made cryptography; buffers wiped

**Success criteria**:
- [ ] The official test vector opens with `K7QX9`, `k7qx-9` and ` K7QX9 `, with the expected proof
- [ ] A wrong code, a wrong unlock key, another `devId` or a modified `expiresAt` fail on the tag, and nothing is used
- [ ] `iterations` 1000, a 15-byte salt, another cipher or version are refused before any derivation; `0OIL1` is refused locally
- [ ] The unlock request body is exactly `{ envelopeId, proof }`; the plaintext is validated (`v` 1, same `envelopeId`) before use

**References**: Admin API contract v1, Business Rules 13, 18, 20

---

### T12 — [test] Integration tests against the hosted Admin Dashboard

**Jira**: _pending_
**Agent hint**: unit-testing:test-automator
**Depends on**: T5, T6, T10, T11, **hosted routes deployed**

**User outcome**: The owner gets end-to-end evidence that a candidate unlocks the trainer configuration from the real Admin Dashboard with the access code, and passes the labs with it.

**Business constraints**:
- A test workshop and a test access code on the Admin Dashboard, never production data; registered with a test identity
- No secret is printed in the test output

**Success criteria**:
- [ ] Overview, Unlock and code work against the hosted routes; Lab01 and Lab07 pass afterwards
- [ ] Wrong code (attempts left), wrong admin API key and network failure give the expected messages
- [ ] The phase 1 tests still pass

**References**: Stories 1, 3, Admin API contract v1

---

### T13 — [security] Join the workshop with one invitation code sent by the trainer (v1.2)

**Jira**: _pending_
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T10

**User outcome**: The trainer generates one invitation code with a command of the repository and e-mails it. The candidate pastes it in *Join the workshop*, or later in the *Workshop* tab. The dashboard then knows both the admin API key and the workshop key: reporting starts and the trainer's model configurations become available, without editing any file or restarting. A candidate who still types a plain workshop key, with the API key configured as before, sees no change.

**Business constraints**:
- Business Rules 16, 21–23
- The admin API key stays write-only: never returned to the browser, logged or reported; the status only says it is configured and where it comes from
- Same CSRF protection as every state-changing request; existing reporting behaviour unchanged otherwise

**Success criteria**:
- [ ] On a fresh machine without `appsettings.Local.json`, pasting a valid invitation code in *Join the workshop* registers the candidate; the reporting status turns connected, and the AI models tab can read the trainer's configurations, without a restart
- [ ] A malformed code, a code with a missing value or a `LB2.` code is refused with a clear message on the field, and nothing is stored
- [ ] `LABS_ADMIN_API_KEY` still wins over the invitation; a plain workshop key still works as before
- [ ] After a 401 `invalidApiKey`, pasting a new invitation resumes the reporting without a restart
- [ ] The trainer's command prints a code that the dashboard accepts; it refuses empty values or values with whitespace
- [ ] No API response, log line or reporting event contains the admin API key or the workshop key

**References**: Business Rules 16, 21–23, Edge Cases ("Admin URL or admin API key missing…")

---

## Test Plan

> Status: ready (2026-10-26), T12 excepted. Run it with a **temporary user profile**, never your real secrets, and without reporting. From `Dashboard/`: `dotnet test` (601 tests), `dotnet build`, then in `Dashboard/LabDashboard` (macOS/Linux; on Windows set `APPDATA` instead of `HOME`):
>
> ```bash
> HOME=$(mktemp -d) NUGET_PACKAGES=~/.nuget/packages Dashboard__Port=5091 Dashboard__DataDirectory=$(mktemp -d) Dashboard__Reporting__ServerUrl= \
>   Dashboard__TrainerModels__Mock__Enabled=true dotnet bin/Debug/net10.0/LabDashboard.dll
> ```
>
> Unset the `AzureOpenAI__*` variables in that terminal first (`unset ${!AzureOpenAI__*}` in bash). Open <http://127.0.0.1:5091>, choose *Work on my own*, then click the *Azure OpenAI* status of the top bar (*AI models* tab). Verified on 2026-10-26 in Chrome (Playwright): steps 1–8 (the 10-minute expiry by the automated tests only), the `network` error of step 9 and step 10; steps 4, 5 and 7 with real runs of Lab01 and Lab07 (the values set through the dashboard routes, never typed in a transcript). Step 11 is covered by the automated tests (the trainer's command run as a real process, the join against an in-process fake admin), not against a live admin dashboard.

1. **Fresh machine (Edge Cases)** — both cards show *My configuration*, empty forms, *Not configured*. The *Trainer's configuration* bar shows the pill *Simulated trainer configuration (fake values)*; each card shows *From the trainer:* with its name, provider, deployment, endpoint host and *key provided*, and a *Use the trainer's configuration* tick. **Unlock** is disabled until a kind is ticked. No request reached an admin dashboard (mock).
2. **Story 1 — trainer's models** — click *Trainer configuration* on the LLM card: refused (nothing unlocked yet), the LLM gets ticked and the focus goes to **Unlock**. Tick the embedding card too, click **Unlock**: the code entry appears with *5 characters, for LLM and Embedding model. Type it within 09:59* and the focus in the field.
3. **The access code (Rules 13, 20)** — type `0oil1`: the field keeps nothing, *Only the letters and digits of the code: there is no 0, O, 1, I or L*, submit disabled. Type `m4prs` + Enter: *Wrong code.*, the field is emptied. Paste `k7qx-9`: it shows `K7QX9`; Enter: toast *Models unlocked*, both cards in *Trainer configuration* with *Configuration*, *Last unlock*, the key *Provided*; the bar says the configuration is up to date and hides Unlock.
4. **Story 2 — own models** — on *My configuration* of the LLM, enter your endpoint, deployment and key, **Save**; on the embedding card, *Same resource as the LLM*, your embedding deployment, **Save**. The key shows *Stored ••••xxxx* and **Remove**. Run Lab01 and Lab07 (*Reference solution*): **Passed**, 10/10 and 12/12.
5. **Lab07 on its own resource (Rule 7)** — embedding card: *Its own resource*, endpoint and key (the same resource is fine), **Save**. Run Lab07: **Passed** 12/12, and the output shows `Embedding endpoint: https://…/ (API key)` after `Embedding deployment`. Without a key there, it says `(Microsoft Entra ID)` and the LLM key is never used.
6. **Story 3 — mix** — tick only the LLM, **Unlock**, `K7QX9`: the LLM is in *Trainer configuration*, the embedding stays in *My configuration* with its values.
7. **Story 4 — back and forth** — click *My configuration* on the LLM card: *LLM: My configuration in use*, the values typed in step 4 are back (key hint unchanged); click *Trainer configuration*: back to the trainer's without a new code. Run Lab01 after coming back: **Passed**.
8. **Cancel and expiry (Rule 14)** — tick, **Unlock**, then **Cancel** (or close the dialog, or press Escape): `curl http://127.0.0.1:5091/api/trainer-models` shows `"unlock":{"status":"idle"…}`. Unlock and wait 10 minutes: *The unlock has expired: click Unlock again.*
9. **Errors** — restart with `Dashboard__TrainerModels__Mock__RetrieveError=network`: the bar shows *The admin dashboard cannot be reached…* with **Try again**; with `notConfigured`: *No admin dashboard is configured…*; with `unauthorized`: the message and **Open the Workshop tab**. Without the mock and without `Dashboard__Reporting__ServerUrl`: same *not configured* message, *My configuration* works.
10. **FR, keyboard, phone width** — switch to FR: every text of the bar, the offers, the code entry and the errors is translated. At 390 px: no horizontal scroll. Tab reaches the ticks, Unlock, the code field, its buttons; Enter submits the code, never the dialog.
11. **Invitation code (T13)** — `dotnet run --project Dashboard/LabDashboard -- invitation`: it asks for both keys without echoing them and prints `LB1.…` only. Start a dashboard **with** reporting on a profile without `appsettings.Local.json` and without `LABS_ADMIN_API_KEY`, paste the code in *Join the workshop*: the reporting pill turns *Reporting* without a restart; the *Workshop* tab shows *Admin API key: from the trainer's invitation*. A code starting with `LB2.` gives *…made for a newer Lab Bench*, a truncated one *Paste the invitation code… exactly…*; nothing is stored.
12. **Nothing secret anywhere** — after the steps above: `grep` your key, `K7QX9` and the invitation code in the data folder (`history.json`, `outbox.json`, `identity.json`) and in the dashboard console: no match. `GET /api/settings/models` shows `stored` and 4-character hints only.
13. **T12 (pending)** — once the routes are deployed on the hosted admin dashboard: a test workshop with an access code, steps 2–3 and 6 without the mock, then Lab01 and Lab07 pass with the trainer's keys; a wrong code shows the attempts left; a wrong admin API key shows *refused the project's API key*.

## Dependencies

- **Blocked by**: the Admin Dashboard contract (phase 2 only: T10..T12)
- **Blocks**: support of providers other than Azure OpenAI (future spec)
- **External**: Admin Dashboard (separate repository, hosted), Azure OpenAI for the lab runs
- **Builds on**: `specs/dashboard-azure-openai-settings` (current settings form and store), `specs/admin-dashboard-progress-tracking` (admin URL and API key)

## Out of Scope / Future Considerations

- OpenAI, DeepSeek, Anthropic and other providers: they need a provider-aware client creation in every lab, and some of them have no embedding models.
- Expiry or automatic refresh of the trainer configuration, if the contract provides it.
- A "test connection" button per model.
- Encrypting the user-secrets file itself; using an OS keychain.

## Open Questions

- [x] ~~Contract — authentication~~: `X-API-Key` **and** the dev token (contract v1).
- [x] ~~Contract — protection of the payload~~: the code is never sent; the server checks a proof online with attempt limits (3 per envelope, 10 per developer per 15 min, 100 per workshop code per 30 min, 1,000 per code), and the envelope key needs the server's one-time unlock key.
- [x] ~~Contract — content~~: several configurations per kind with a default; providers azure-openai, openai, deepseek, anthropic; keys in the envelope.
- [x] ~~Contract — code scope~~: one code per workshop, announced by the trainer; a new code invalidates the open envelopes (`accessCodeChanged`).
- [x] ~~Deployment~~: the owner runs T12 against the hosted admin once the routes are deployed.
- [x] ~~Distribution of the admin API key~~: an invitation code e-mailed by the trainer (v1.2, Business Rules 21–23, T13).
