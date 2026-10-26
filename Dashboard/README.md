# Lab Bench — local dashboard for the labs

An optional, local web UI to follow the labs and run them without typing `dotnet` commands.
It is a convenience layer: **every lab keeps working exactly the same with the CLI**, and no lab depends on the dashboard.

> Prototype: registered labs are `Lab01-FirstBasicAIAgent`, `Lab02-AIAgentWithSO`, `Lab03-AIAgentWithFunctionTools`, `Lab04-AIAgentWithMCPClient`, `Lab05-AIAgentWithThreads`, `Lab06_A2AServer`, `Lab06_A2AClient`, `Lab07-AgenticRAG-VectorStore`, `Lab08-DataFormatComparison`, `Lab09-AIAgentWithFunctionToolsHumanApproval`, `Lab10_ExposeAIAgentAsMCPTool`, `Lab11-AIAgentWithCustomHttpTransport` and `Lab12-AIAgentWithAIContextProvider` (the migrated labs).
> The two Lab06 labs are run in pairs: each run of one starts the reference solution of the other as its **companion** (see [Labs run in pairs](#labs-run-in-pairs-companion)), on port 5071.
> Scenario 3 of Lab05 needs its MongoDB container (`docker compose up -d` in the lab's `MongoDB/` folder): without it, the Solution run fails with *"MongoDB is not reachable"*.
> Lab07 needs an **embedding** deployment (`AzureOpenAI:EmbeddingDeploymentName`, not part of the settings form): without it, the run stops with *"'AzureOpenAI:EmbeddingDeploymentName' is not configured"*.
> Lab09 is the first **interactive** lab of the catalog: scenario 1 waits for your decision in the input box (`Y` approves; the checks expect the approval), scenario 2 needs no input.
> Lab10 starts its own MCP servers during the run and stops them (no companion): an HTTP server on `McpServer:BaseUrl` (`http://localhost:5100` by default, the port must be free) and a stdio server, a child process of the lab whose stderr the lab prints as `[server]` lines.
> Lab11 prints the whole HTTP traffic of its agents (about 600 lines per run, every header and JSON body); the lab masks the `Authorization` header itself, and a check fails if a credential header is printed in clear.
> Lab12 needs its MongoDB container (`docker compose up -d` in the lab's `MongoDB/` folder, port 27018): without it, the Solution run fails with *"MongoDB is not reachable"*. It is **interactive**: scenario 2 is a chat that waits for your messages in the input box (any message, or none); end it with an empty line, `exit` or **End input**. Its checks read scenario 1 and the greeting of scenario 2, not your messages.

## Start it

```bash
cd Dashboard/LabDashboard
dotnet run
```

Then open <http://127.0.0.1:5057>. Stop it with `Ctrl+C`.

The labs read their configuration as usual (`appsettings.json`, user secrets, `AzureOpenAI__*` environment variables):
the dashboard starts them as child processes of your own user, so they see the same settings as in a terminal.
To configure them without a terminal, use the **Azure OpenAI settings** (see below).

## Interface

- **Dark theme only**, whatever the system preference.
- **English / Français**: switch with the `EN | FR` selector in the top bar (saved in the browser; the default follows the browser language).
  Every UI text is translated (`wwwroot/js/i18n.js`). Program output is shown as printed.
  Lab texts (title, summary, objectives, check descriptions) come from the optional `translations` of `labs.json`;
  a lab can also provide `README.fr.md`, otherwise the English README is shown with a notice.
- Responsive down to phone width; respects `prefers-reduced-motion`.

## What it does

| Feature | How |
|---|---|
| Lab list, progress, last result | `labs.json` (catalog) + local history |
| Instructions | the lab `README.md`, rendered to HTML on the server (Markdig, raw HTML disabled) |
| Run the exercise (`Start`) or the reference (`Solution`) | `dotnet build <project> -nologo -v minimal`, then `dotnet run --project <project> --no-build` — the exact commands a developer types |
| Live logs | stdout/stderr read as they are produced and pushed to the browser with Server-Sent Events |
| Progress indicator | lines the program overwrites with `\r` (the `ConsoleSpinner` of CommonUtilities) are shown as a live status line instead of flooding the log |
| Passed / Failed | build succeeded **and** exit code 0 **and** every check of the lab matched the output |
| Errors | build errors (`error XX0000`) or stderr of the program |
| Token usage | parsed from the usage **printed by the exercise** (`Input tokens: 56`, …), one report per block, labelled with the current scenario (`Scenario 2 · Token Usage`). Shown as *Not available* when the program prints none |
| History | `Dashboard/.data/history.json` (git-ignored), 30 runs per lab |
| Solution | the solution files that differ from the exercise, behind a "try it first" gate |
| Cancel / timeout | kills the whole process tree |
| Interactive labs (`Console.ReadLine`) | labs with `"interactive": true` keep stdin open: an input box under the output shows *Waiting for input #n* exactly when the program reads, sends the line, echoes it, and **End input** closes stdin (`ReadLine` returns `null`). Waiting does not count against the lab timeout — see [Interactive labs](#interactive-labs) |
| Labs run in pairs (companion) | a lab can declare a `companion` project (the reference solution of another lab): a server started before the lab and stopped after it, or a client run against the lab once it is ready. Its output is shown as `companion` lines — see [Labs run in pairs](#labs-run-in-pairs-companion) |
| AI models | top-bar button (status of the LLM: *API key* / *Microsoft Entra ID* / *Not configured*) opening the **AI models** settings: the LLM and the embedding model, each with the **trainer's configuration** (unlocked with the access code announced by the trainer) or **my configuration** (an Azure OpenAI form), shared by every migrated lab — see [AI models settings](#ai-models-settings) |
| Reset exercises | **Reset exercises…** under the lab list, **Reset this exercise…** in the History tab (shown once the exercise was started): one exercise, several or all back to *Not started* (their runs and their `_StartCompleted/` folder), after confirmations proportional to the risk — see [Reset exercises](#reset-exercises) |
| Journey | a column left of the lab list: overall progress, the state of every exercise, XP, rank, milestones, distinctions and the next mission, recomputed from the local history and refreshed by itself — see [Journey](#journey) |

## Architecture

```
Browser (HTML + CSS + JS, no framework, no CDN)
   │  fetch /api/...            EventSource /api/runs/{id}/events
   ▼
LabDashboard (ASP.NET Core minimal API, 127.0.0.1 only)
   ├─ LabCatalog      whitelist of labs and projects (labs.json)
   ├─ LabRunner       one run at a time: build → run → checks
   │   └─ ProcessRunner + ConsoleStreamDecoder   (no shell, terminal semantics)
   │   └─ RunInput + InputSignalParser          (stdin of interactive labs)
   ├─ OutputAnalyzer  checks, token usage, build diagnostics
   ├─ RunHistoryStore JSON file, atomic writes
   ├─ LabResetService reset of exercises (runs + _StartCompleted), single-use confirmations
   ├─ LearningJourney states, XP, rank, milestones, distinctions (computed from the history, read-only)
   ├─ AzureOpenAISettingsStore  AI models: profiles and modes in the shared user secrets
   ├─ TrainerUnlockFlow → TrainerConfigurationService → admin dashboard (contract v1) or mock
   └─ ReportingClient progress and help requests to the admin dashboard (outbox, background loop)
   ▼
dotnet build / dotnet run  →  LearningLabs/.../Start|Solution
                              └─ LabInputHook (startup hook, interactive labs only)
```

| API | Purpose |
|---|---|
| `GET /api/labs` | catalog with progress and last runs |
| `GET /api/labs/{id}?lang=fr` | details: README (HTML, `README.fr.md` when present), objectives, packages, commands, checks, history |
| `GET /api/labs/{id}/solution` | solution files that differ from the exercise |
| `GET /api/journey` | the journey: per lab `state` (`notStarted` / `inProgress` / `completed` / `toResume`), `running`, attempts, best checks, XP and XP still within reach; totals, `rank` (and next rank), `milestones`, `badges`, `next` mission. Read-only, recomputed on every call |
| `POST /api/labs/{id}/runs` `{ "target": "start" \| "solution" }` | start a run (409 if one is in progress) |
| `GET /api/runs/{runId}/events` | Server-Sent Events: `phase`, `output` (streams `stdout`, `stderr`, `system`, `stdin`), `progress`, `input-request`, `input-sent`, `input-closed`, `result` (replayed from the start on reconnection) |
| `POST /api/runs/{runId}/input` `{ "text": "…" }` | send one line to an interactive run (400 `newline` / `tooLong`; 409 `notInteractive`, `notAccepting` (build or exited), `closed`, `tooManyPending`) |
| `POST /api/runs/{runId}/input/close` | close the standard input of an interactive run |
| `POST /api/runs/{runId}/cancel` | cancel a run |
| `POST /api/labs/reset/preview` `{ "labIds": [...] }` | what a reset would delete (runs per target, `_StartCompleted/` files, size and source files, `blocked` reason) and, unless a lab is blocked, a single-use `confirmationId` valid 10 minutes, plus a 5-character `code` for several labs. **Deletes nothing** (400 `labIds`: `required` / `unknown`) |
| `POST /api/labs/reset` `{ "confirmationId", "names"?, "code"? }` | reset the labs of that confirmation (`names`: the folder name of the lab when there is one; `code`: when there are several, names not needed): per-lab result `reset` / `nothingToReset` / `failed` (runs and files removed, `verified`, error). 400 `names` / `code` (`mismatch`), 409 `running` / `busy`, 410 `expired` (unknown, expired or already used) / `tooManyAttempts` (3rd wrong code) — a refused request deletes nothing |
| `GET /api/settings/models` | the AI models: per kind (`chat` = the LLM, `embedding`) the mode (`custom` / `trainer`), the custom and trainer profiles (a key is only `stored` + its last 4 characters; a trainer profile also has its `configId`, `configName`, `configUpdatedAt` and the date of the last unlock) and what the labs use (`active`), each value with its source — **never a key** |
| `PUT /api/settings/models/{kind}/custom` `{ "provider", "endpoint", "deployment", "apiKey", "sameResourceAsLlm"? }` | save my configuration of a kind; applied at once in custom mode (400 with field errors; `unsupportedProvider` for anything but `azure-openai`; an empty key keeps the stored key) |
| `DELETE /api/settings/models/{kind}/custom/api-key` | remove the key of my configuration (Microsoft Entra ID) |
| `PUT /api/settings/models/{kind}/mode` `{ "mode": "custom" \| "trainer" }` | select the profile the labs use for a kind (409 `profileIncomplete` with `missing`, or `unsupportedProvider`: nothing written) |
| `GET /api/settings/azure-openai` · `PUT` · `DELETE …/api-key` | the former single form, kept for compatibility (no longer used by the page): `GET` gives what the labs use for the LLM, `PUT` and `DELETE` change my LLM configuration |
| `GET /api/trainer-models` | the trainer's configurations, read without any code (one call to the admin dashboard, when the AI models tab opens or on Refresh): per kind the default (`state` `applicable` / `notApplicable` / `unusable` / `none`, `reason`, `warnings`, `updatedSinceUnlock`), or the `error` that prevents reading them; `simulated` when the mock answers; the unlock under way (`unlock`) |
| `POST /api/trainer-models/unlock` `{ "kinds": ["chat", "embedding"] }` | start an unlock for the ticked kinds: the dashboard server fetches **one** envelope and holds it (at most one, 10 minutes at most); answers `codeRequired` (code length and alphabet, attempts, expiry) or `idle` with the `error` (400 `kinds`: one or two distinct kinds) |
| `POST /api/trainer-models/code` `{ "code" }` | open the held envelope with the access code: `unlocked` (the keys of the ticked kinds are saved, those kinds switch to trainer mode, the new `models` status), or the error (`invalidCode` with `attemptsLeft`, `envelopeExpired` with its `reason`, `rateLimited`…). A code out of format is refused without any call. The code is never returned, stored or logged |
| `POST /api/trainer-models/cancel` | drop the held envelope (the page also calls it when the settings dialog closes) |
| `GET /api/reporting/status` | reporting to the admin dashboard: enabled, state (`disabled` / `registering` / `connected` / `offline` / `rejected`), server host, username, `userId`, outbox size, help request, whether the workshop key and the admin API key are configured and where from (`environment` / `dashboard` / `appsettings` / `missing`) — **never the workshop key, the admin API key nor the dev token** |
| `POST /api/identity` `{ "username", "workshopKey"? }` | first launch: creates the identity (`userId`) and registers it (400 with field errors). `workshopKey` takes a plain workshop key or the trainer's **invitation code** (`LB1.…`: 400 `invalid` / `unsupportedVersion`) |
| `PUT /api/reporting/settings` `{ "username"?, "workshopKey"? }` | change the username, the workshop key or paste a new invitation code (write-only); an empty field keeps the value |
| `GET /api/help` · `POST /api/help` `{ "labId"?, "message"? }` · `POST /api/help/cancel` · `POST /api/help/dismiss` | the help request of the developer: read, send ("Help / Je suis bloqué", 409 `active` while one is active), cancel ("I'm unblocked"), hide a closed one |

## Security

It runs processes, so it is locked down to a local, single-user tool:

- listens on **127.0.0.1 only** (not reachable from the network);
- only the projects declared in `labs.json` can be run; paths are resolved and must stay inside the repository;
- no shell: `ProcessStartInfo.ArgumentList`, never a command string;
- requests with a non-local `Host` header are rejected (DNS rebinding);
- `POST` requests need the `X-Lab-Dashboard: 1` header and a same-origin `Origin` (CSRF: a web page cannot trigger a run);
- no CORS, no authentication secrets, no cloud dependency. The README is rendered with raw HTML disabled;
- every API key of the AI models (custom and trainer profiles, both kinds) is **write-only**: the API never returns it (only "stored" and its
  last 4 characters), the form clears it after saving, it is never logged, and any occurrence of it in a run output (live stream, result,
  history, copied output) is replaced by `••••`;
- the trainer's keys are opened **in the dashboard server only** (never in the browser), with the access code the candidate types: the code
  is used for one request and never stored, logged or returned; only an HMAC proof leaves the machine. The envelope held between Unlock and
  the code cannot be opened without the code and the server's one-time unlock key, and is dropped at success, cancel or expiry;
- the admin API key and the workshop key (typed, or carried by the trainer's invitation code) are never displayed back, logged or reported;
- a reset of exercises only deletes the history of the selected labs and their `<lab folder>/_StartCompleted` (fixed name, never a path from the request), after a confirmation the server checks itself (see [Reset exercises](#reset-exercises)).

<a id="azure-openai-settings"></a>

## AI models settings

The labs use two models, configured separately in the **AI models** tab of the settings (full specification: [specs/dashboard-model-configuration/spec.md](../specs/dashboard-model-configuration/spec.md)):

| Model | Used by | Entries the labs read |
|---|---|---|
| **LLM** (`chat`) | every lab | `AzureOpenAI:Endpoint`, `AzureOpenAI:ChatDeploymentName`, `AzureOpenAI:APIKey` (absent: Microsoft Entra ID) |
| **Embedding model** (`embedding`) | Lab07 | `AzureOpenAI:EmbeddingDeploymentName`; on its own resource also `AzureOpenAI:EmbeddingEndpoint` and `AzureOpenAI:EmbeddingAPIKey` (absent: Microsoft Entra ID, **never** the key of the LLM on another endpoint) |

Each card has a mode switch:

- **My configuration**: the candidate's own Azure OpenAI deployment — provider (Azure OpenAI only), endpoint, deployment, optional key. The embedding model uses *the same resource as the LLM in use* (only its deployment is entered) or *its own resource*.
- **Trainer configuration**: the trainer's configuration, read from the admin dashboard and unlocked with the access code the trainer announces — see [The trainer's configuration](#the-trainers-configuration).

Both profiles of each model are kept: switching mode never loses one, and switching back to *My configuration* restores the values typed before.

| Question | Answer |
|---|---|
| Where is it stored? | In the **shared user secrets** of the labs (`UserSecretsId` = `microsoft-agent-framework-learninglabs`, read from the lab projects): `~/.microsoft/usersecrets/microsoft-agent-framework-learninglabs/secrets.json` (macOS/Linux) or `%APPDATA%\Microsoft\UserSecrets\microsoft-agent-framework-learninglabs\secrets.json` (Windows). **Outside the repository**; created private to the user (`600`/`700`) on macOS/Linux. |
| What is written? | The selected profile of each model goes to the active `AzureOpenAI:*` entries above, the only ones the labs read. The profiles and the modes live next to them under `LabBench:Models:{Chat\|Embedding}:{Mode\|Custom:*\|Trainer:*}` (a trainer profile also keeps the id, name and `updatedAt` of the trainer configuration and the date of the last unlock), ignored by the labs. Flat format of `dotnet user-secrets set`; other entries of the file are kept; a file that is not valid JSON is never overwritten. |
| A configuration already exists? | It becomes *My configuration* of the LLM (an `EmbeddingDeploymentName` becomes that of the embedding model, on the LLM's resource), in custom mode, without any write: the labs keep working. |
| How do the labs get it? | Unchanged: their `ConfigurationHelper` reads `appsettings.json` → user secrets → environment variables. The values apply to runs from the dashboard **and** to `dotnet run` in a terminal, without restarting anything. |
| What wins? | `appsettings.json` of the lab < these settings < `AzureOpenAI__*` environment variables of the dashboard process (including `AzureOpenAI__EmbeddingEndpoint` and `AzureOpenAI__EmbeddingAPIKey`). A value set on the computer is shown in the field, read-only, with a *Set on this computer* badge (a key only as its last 4 characters). |
| A value is missing? | No endpoint or deployment anywhere: the lab stops with *"'AzureOpenAI:Endpoint' is not configured"* and the run result links to the settings. No API key: Microsoft Entra ID (`az login` + *Cognitive Services OpenAI User* role). |
| API version? | Not needed: the labs use the Azure OpenAI **v1** API (`…/openai/v1/`), which has no `api-version`. |

The CLI remains fully equivalent: `dotnet user-secrets set "AzureOpenAI:APIKey" "<key>"` from any migrated lab folder writes the same file; in custom mode, a value set this way is *My configuration*.
Other lab settings are not managed here: for example the optional Hugging Face token of Lab04 is set with `dotnet user-secrets set "MCPServers:HuggingFace:BearerToken" "<token>"` (same file). The dashboard keeps these entries; only the keys of the AI models are masked in the run output — Lab04 never prints its token.

### The trainer's configuration

1. **Review, without any code.** When the AI models tab opens (or on **Refresh**, never polled), the dashboard reads the trainer's configurations from the admin dashboard. Each card shows the trainer's default for its model: name, provider, deployment, endpoint host, *key provided*; whether it can be used (the labs only call Azure OpenAI; a configuration whose key cannot be read is *not usable: tell the trainer*); the warning *Lab07 expects 1536 dimensions*; and *Updated by the trainer since your last unlock*.
2. **Unlock.** The candidate ticks **Use the trainer's configuration** in the cards to take and clicks **Unlock**: the dashboard server fetches one sealed envelope and holds it (one at most, 10 minutes at most; dropped on **Cancel**, when the dialog closes, or at its expiry).
3. **Access code.** The candidate types the code announced by the trainer: 5 characters of `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (no 0, O, 1, I or L), upper-cased as typed, `k7qx-9` works. The dashboard server derives a proof from it (the code never leaves the machine), the admin dashboard releases a one-time unlock key, and the server opens the envelope: the keys of the ticked models are saved at once and those models switch to *Trainer configuration*. *My configuration* is never touched.

It needs the admin dashboard URL (HTTPS), the admin API key (carried by the trainer's [invitation code](#invitation-code)) and the workshop identity (the candidate joined the workshop): otherwise the tab says what is missing and leads to the *Workshop* tab.
What the candidate sees on a refusal (Business Rule 20 of the spec): a wrong code gives the attempts left (3 per envelope; after the last one, the next code starts with a new envelope); an expired envelope is replaced and the code tried once more without asking again; too many wrong codes or unlocks give the wait time, and nothing is tried before; a configuration changed by the trainer asks for a new review; an archived account, a closed workshop, an access code that expired or changed each have their message. Nothing is ever written on a refusal.

### Integration layer

Everything that depends on the admin dashboard stays behind one integration point (`TrainerModels/`), so that the page and the storage never depend on it:

| Piece | Role |
|---|---|
| `TrainerConfigurationService` | the integration point: overview, retrieve, unlock. Chooses its adapters at startup; reads the admin API key and the identity at each call (an invitation or joining the workshop applies without a restart) |
| `AdminModelConfigsClient`, `AdminModelConfigs` | the admin API (contract v1): routes, headers, answers, error bodies, rate limits, `Retry-After` |
| `AdminEnvelopeDecryptor` | the envelope protocol: validation before any derivation, PBKDF2 → HMAC proof → unlock key → HKDF → AES-256-GCM, plaintext validation, one retry for `expired` / `unknown` / `keyRotated`, buffers wiped |
| `MockTrainerConfiguration` | a simulated trainer, for development and tests: fake values (`*.trainer-mock.invalid`, keys starting with `mock-`), no network call |
| `TrainerUnlockFlow` | the flow of the page: one held envelope, the code, the profiles saved for the ticked models |

The adapters, in this order: the **mock** when `Dashboard:TrainerModels:Mock:Enabled` is set; otherwise *not configured* when `Dashboard:Reporting:ServerUrl` is absent or not HTTPS (`http://` only for `localhost`); otherwise the **admin dashboard** adapter.
The mock is project configuration only, never reachable from the browser, and the tab shows *Simulated trainer configuration* while it answers:

| Setting (`Dashboard:TrainerModels:Mock`) | Default |
|---|---|
| `Enabled` | `false` |
| `Code` — the code that unlocks the simulated configuration (not a secret) | `K7QX9` |
| `ProvideEmbedding` — `false`: the simulated trainer provides the LLM only | `true` |
| `RetrieveError`, `UnlockError` — an error kind to simulate (`network`, `unauthorized`, `rateLimited`, `notFound`…) | none |

To try the whole flow without touching your own user secrets, run a second dashboard with a temporary home folder (macOS/Linux; on Windows set `APPDATA` instead of `HOME`), no reporting and its own data folder:

```bash
cd Dashboard/LabDashboard && dotnet build
HOME=$(mktemp -d) Dashboard__Port=5091 Dashboard__DataDirectory=$(mktemp -d) Dashboard__Reporting__ServerUrl= \
  Dashboard__TrainerModels__Mock__Enabled=true dotnet bin/Debug/net10.0/LabDashboard.dll
```

#### What depends on the admin dashboard contract

The dashboard implements the **Admin API contract v1** (`Lab Bench Admin — AI model configurations (local dashboard API)` 1.0.0, amended on 2026-10-26), summarized in the [spec](../specs/dashboard-model-configuration/spec.md#admin-api-contract-v1). A change of the contract only changes these points:

| Point | Where |
|---|---|
| Routes: `GET /api/v1/model-configs` (overview), `?sealed=1` (envelope), `POST /api/v1/model-configs/unlock` (body exactly `{ envelopeId, proof }`) | `AdminModelConfigsContract` |
| Headers: `X-API-Key` and `Authorization: Bearer <dev token>`; `devId` = the `userId` of the identity, lower-case | `AdminModelConfigsClient` |
| Answers and errors: `configs`, `defaults`, `sealedConfigIds`, `unsealedConfigs`, `sealedUnavailableReason`; `{ error, message, details }`; limits 30 GET / 5 envelopes / 20 unlocks per minute | `AdminModelConfigs` (parser), `AdminModelConfigsClient` |
| Envelope protocol: version 1, AES-256-GCM, PBKDF2-HMAC-SHA256 (600,000 to 10,000,000 iterations, 16-byte salt), the labels `labs-admin/model-unlock/v1` and `labs-admin/model-envelope/v1`, the official test vector | `AdminEnvelopeDecryptor` (tests: `AdminEnvelopeDecryptorTests`) |
| Plaintext `{ v, envelopeId, configs: { <id>: { apiKey } } }`; `settings.endpoint` / `settings.deployment` of the default configuration of each kind | `AdminEnvelopeDecryptor` |

Status: deployed on the hosted admin dashboard and checked end to end on 2026-10-26 (unlock with an access code, then Lab01 and Lab07 with the trainer's keys). A server without these routes answers 404: the tab then says *The trainer configuration is not available on this server yet*, and *My configuration* works as usual. The automated tests use the official test vector and an in-process fake of the admin dashboard (`FakeModelConfigServer`).

## Interactive labs

A lab that reads the console (`Console.ReadLine`) declares `"interactive": true` in `labs.json` (optionally `"inputIdleTimeoutSeconds"`, default 600).
The labs themselves are not modified, and **`dotnet run` in a terminal is unchanged**.

| Question | Answer |
|---|---|
| How does the dashboard know the program waits? | For interactive labs only, the runner sets `DOTNET_STARTUP_HOOKS` (the `LabInputHook` assembly, built next to the dashboard) and `LAB_DASHBOARD_RUN=1` on the `dotnet run` process. The hook runs inside the lab program (not in the `dotnet` CLI host), replaces `Console.In`, and writes an invisible signal `ESC ] lab;input;<n> BEL` on stderr just before each `ReadLine`. The runner removes it from the output and publishes `input-request`. The prompt is shown exactly when the program reads — never on a mere silence (a model call is silent too). |
| Encoding | UTF-8 end to end (the hook reads stdin as UTF-8, also on Windows). A line ends with `\n` on every platform. |
| Several reads | Numbered 1, 2, 3…; lines answer them in order. A line sent before the program asks waits in the pipe and answers the next read (type-ahead, at most 8 lines). |
| End of input | **End input** closes stdin: the next `ReadLine` returns `null` (like `Ctrl+D`). |
| Long waits | While a read is pending, the lab timeout (`timeoutSeconds`) is paused. A read unanswered for `inputIdleTimeoutSeconds` stops the run: *Timed out — no input received*. Cancel works at any moment. |
| History and secrets | Typed lines are echoed (stream `stdin`) and saved in the history, masked like any output (a stored API key shows as `••••`). The checks still read the program's stdout only. |
| Other labs | Stdin stays closed: `ReadLine` returns `null` at once, as before. |

Not supported: `Console.ReadKey` (it throws when stdin is redirected) and programs that read `Console.OpenStandardInput()` directly or replace `Console.In` themselves (input reaches them, but no prompt is detected).

## Labs run in pairs (companion)

An A2A client needs a running server, and a server never exits: `Lab06_A2AClient` and `Lab06_A2AServer` declare a `companion` in `labs.json`.
The labs are not modified, and **`dotnet run` in a terminal is unchanged**.

| `role` | Run |
|---|---|
| `server` (Lab06_A2AClient) | build the lab and the companion → start the companion (`dotnet run --project <companion> --no-build`) → wait for a stdout line matching `readyPattern` (at most `readyTimeoutSeconds`) → run the lab → stop the companion (process tree killed). The checks and the token usage read the output of the lab. |
| `client` (Lab06_A2AServer) | build both → start the lab → wait for `readyPattern` in the output of the lab → run the companion client to the end → stop the lab. The checks and the token usage read the output of the lab **then** of the companion (the server itself prints no model answer). |

| Question | Answer |
|---|---|
| Which companion? | Always a project of the repository (validated like the lab projects), here the `Solution` of the other lab: your `Start` is checked against the reference. |
| Ports | `environment` sets variables on **both** processes of the run (`A2AServer__BaseUrl`, `RemoteAgents__*__Url` = `http://localhost:5071`): no conflict with a server you started yourself on port 5000 (or with the macOS AirPlay Receiver, which also listens on 5000). They are shown in the log. |
| Verdict | *Failed* at the run step when the companion server is not ready (*"The companion failed"*), when the lab (a server) exits or times out before it is ready (*"The lab (a server) never became ready"*, e.g. the delivered `Start`), or when the companion client exits with an error. |
| Not supported | A companion for an interactive lab. |

## Reset exercises

To undo a mistake (an exercise run by accident, a `_StartCompleted/` folder left in a lab folder) or to start the course over.
**Reset exercises…** (under the lab list) or **Reset this exercise…** (History tab of a lab) opens a dialog: choose **One exercise** or **Several or all**.
Only the exercises with something to reset are listed: **in progress** or **completed** (a run of *My exercise*), or holding a `_StartCompleted/` folder. A never-started exercise is not listed (a lab with only *Reference solution* runs is *Not started*), and its History tab has no reset button.
Each exercise is shown with its name (its lab folder, e.g. `Lab01-FirstBasicAIAgent`) and a **copy** button.

| Question | Answer |
|---|---|
| What is deleted? | For each selected lab: **all its runs** (Start and Solution) in `Dashboard/.data/history.json`, and its `<lab folder>/_StartCompleted/` folder (git-ignored completed copy of `Start/`, often only MSBuild `obj/` leftovers). The lab then shows *Not started*, *No run yet* and an empty history. |
| What is never touched? | `Start/` (your code, `bin/`, `obj/`), `Solution/`, the README files, `labs.json`, the settings (user secrets, `appsettings*.json`), `identity.json` / `outbox.json` / `standalone`, the other labs, any other file of the lab folder (`.tmp-*`…), the Docker containers of Lab05 / Lab12. |
| How is it confirmed? | Before anything, the dialog shows what will be deleted per lab: runs per target, files, size and **source files** of `_StartCompleted/` (files outside `bin/` and `obj/`, with a warning: that completed copy of your code is deleted too). **One exercise**: type (or paste) its folder name (e.g. `Lab01-FirstBasicAIAgent`, case-sensitive), then confirm a second screen. **Several or all**: retype the random 5-character code shown on screen (no name to type), then confirm a recap of the consequences. The red button names the action and is never focused by default. |
| Checked by the server too? | Yes: the preview issues a confirmation for exactly those labs, valid **10 minutes** and usable **once**; the reset needs the typed name for one lab, the code for several. Three wrong codes cancel it; a double click or a replayed request resets nothing twice; a lab whose run is in progress cannot be reset; one reset at a time. |
| Which `_StartCompleted` is deleted? | Only a real folder named exactly `_StartCompleted` directly inside the lab folder of the catalog. A symbolic link or a junction is never followed nor deleted (the lab is *blocked*, remove the link by hand), nor a folder that contains a project of the catalog; links inside the folder are removed, never followed. |
| Order and errors | The folder is deleted first, the runs only once it is gone (a lab never looks reset while its folder remains); the history is rewritten in one atomic write. A permission error is shown for that lab with the path, its runs are kept, and the other labs are reset. |
| Verified? | After the reset, each lab is checked again (no run left, no folder, `Start` and `Solution` projects present): the summary says *Reset and verified*, *Already at zero* or *Not reset* with the reason. |
| Trainer's dashboard | When you joined a workshop, a new `progress.snapshot` (authoritative) is sent, so the trainer sees *Not started* too. Nothing is sent when working alone. |

## Journey

The column left of the lab list (a band above it below 1280 px wide) turns the progress into a route through the course.
Everything is computed by the server from `Dashboard/.data/history.json` (`Journey/LearningJourney.cs`): nothing else is stored, a reset of exercises lowers it, and nothing is sent to the trainer.

| Part | What it shows |
|---|---|
| Core | One arc per exercise in catalog order (wider gaps between levels): green *completed*, amber *in progress* (the share of checks of its best run), red dashed *to resume*, blue pulse *running*; a blue dot marks the lab open on the right. Inside: the percentage completed and, in blue, the XP toward the next rank. |
| Rank and XP | *Foundations* → *Tooling* (15 % of the course base XP) → *Integration* (35 %) → *Orchestration* (60 %) → *Mastery* (85 %). XP: 100 per Beginner exercise, 150 per Intermediate one, 200 per Advanced one; +50 when it passes on the first attempt, +25 when its solution was not run before; an exercise in progress earns `base × 0.5 × best checks / checks`. Running a solution never earns XP. |
| Trajectory | The exercises as waypoints, one group per level (the gate lights up when every exercise of the level is completed), joined by a trail; selecting one opens the lab. *To resume* = passed before, but the latest attempt (a Start run not cancelled) failed or timed out: it still counts as completed, like in the lab list and on the trainer's dashboard. |
| Next mission | The exercise in progress run most recently, else the first one to resume, else the next one to start, with **Continue** and the XP still within reach. |
| Distinctions | *First steps*, *First try*, *No safety net* (completed without running its solution before), *Persistence* (3 unsuccessful attempts or more before passing), *Milestone cleared*, *Summit*; each insignia shows how to earn it. |

It refreshes when a run starts or ends, after a reset, when the page comes back to the front and every 20 seconds while it is visible (a run from another tab). A completed exercise, a new rank, a cleared milestone or a distinction is celebrated once, when it happens on the open page (short card, also announced to screen readers); never on load, on a language switch or after a reset. `prefers-reduced-motion` turns every animation off.

## Reporting to the admin dashboard

Optional. During a workshop, the trainer runs the **Admin Dashboard** (a separate application in its own repository, hosted on the web) and e-mails every developer an **invitation code** (or, as before, a plain workshop key).
Once the developer joined, the local dashboard reports their progress to it, and the **Help / Je suis bloqué** button lets them raise a hand that the trainer sees at once.

### First launch: join the workshop, or work on your own

The first time the dashboard opens, a welcome dialog offers two choices:

| Choice | What the developer enters | Result |
|---|---|---|
| **Join the workshop** | a **username** (2–32 characters, letters, digits, `.`, `_`, `-`; not necessarily a real name) and the **invitation code** e-mailed by the trainer, or a plain workshop key (the field is hidden when both the workshop key and the admin API key are already configured). The admin dashboard URL is shown, not asked: it is fixed in the project configuration | the dashboard generates a stable `userId`, registers on the server and starts reporting |
| **Work on my own** | nothing | nothing is ever sent: no status pill, no Help button, no network call. The choice is remembered (`Dashboard/.data/standalone`) and the dialog does not come back |

Both can be changed later in the *Workshop* tab of the settings: a developer working alone joins the workshop by entering a username and the invitation code (or the key); a username can be changed at any time, and a new invitation code can be pasted there too.
Nothing has to be typed on the command line by the developer.

### Configure it (trainer)

The admin dashboard URL is **fixed in the project**: `Dashboard:Reporting:ServerUrl` in `LabDashboard/appsettings.json` (committed, so every developer gets it; `Dashboard__Reporting__ServerUrl` overrides it for a local test). It is not shown to developers and can never be changed in the UI; when it is empty, only *Work on my own* is offered.
#### Invitation code

The trainer sends **one invitation code** by e-mail: it carries the admin API key and the workshop key, so that a developer joins without editing any file nor restarting anything. Its format is `LB1.` followed by the base64url (no padding) of the UTF-8 JSON `{"apiKey":"…","workshopKey":"…"}`, 1,024 characters at most. The trainer creates it with:

```bash
dotnet run --project Dashboard/LabDashboard -- invitation
```

The command asks for the admin API key and the workshop key without echoing them (or reads them, one per line, from a pipe), prints the code alone on the standard output, and exits: it starts no dashboard, calls no network, writes nothing to disk, never takes the keys as arguments (they would stay in the shell history) and refuses empty values or values with whitespace.
The code is as sensitive as the two keys: e-mail it to the participants, never post it in a public channel.

Pasted in *Join the workshop* or in the *Workshop* tab, the code stores the admin API key as `Dashboard:Reporting:ApiKey` and the workshop key as `Dashboard:Reporting:WorkshopKey` in the shared user-secrets file, in one write; reporting and the trainer's AI model configurations use them at once. A malformed code is refused on the field (`invalid`), and so is a code of another version (`LB2.…`: `unsupportedVersion`, update the repository); nothing is stored then. A plain workshop key, without the prefix, still works as before.

The workshop key can also be distributed as a command:

```bash
cd Dashboard/LabDashboard
dotnet user-secrets set "Dashboard:Reporting:WorkshopKey" "<key given by the trainer>" --id microsoft-agent-framework-learninglabs
```

The admin API also requires a shared **API key**: the dashboard sends it as the `X-API-Key` header on every `/api/v1/*` request (all but `/api/v1/health`), never in the URL.
It comes **on top of** the workshop key (sent as `X-Workshop-Key` at registration) and of the dev token (`Authorization: Bearer`), never instead of them: the API key alone is rejected everywhere.
It normally comes with the [invitation code](#invitation-code); it is never shown in the UI (only *configured* and where from) nor written to the logs. It is resolved at each pass of the reporting loop, in this order:

1. `LABS_ADMIN_API_KEY` (environment variable, for a local test);
2. the shared user-secrets file (`Dashboard:Reporting:ApiKey`, written by an invitation code);
3. `appsettings.Local.json` (git-ignored) or `appsettings.json`:

```json
// Dashboard/LabDashboard/appsettings.Local.json — git-ignored, next to appsettings.json
{ "Dashboard": { "Reporting": { "ApiKey": "<API key of the admin dashboard>" } } }
```

Without a key, reporting stays off: one warning is logged and the status pill says *API key missing*; pasting an invitation code starts it without a restart.
A key refused by the server (`401 invalidApiKey`) stops reporting at once, without retrying (30 failures per minute block the machine on the server side), until a new invitation code is pasted in the settings (or the dashboard is restarted with a valid key).

| Setting (`Dashboard:Reporting`) | Where | Default |
|---|---|---|
| `ServerUrl` | `appsettings.json` (fixed by the project; `Dashboard__Reporting__ServerUrl` overrides it for a local test). Never editable in the UI | `https://labs-admin-dash.vercel.app` (the hosted admin dashboard) |
| `ApiKey` | `LABS_ADMIN_API_KEY` > the shared user-secrets file (`Dashboard:Reporting:ApiKey`, written by an invitation code) > `appsettings.Local.json` (git-ignored) / `appsettings.json`. Sent as `X-API-Key`; never displayed in the UI | none (reporting off) |
| `WorkshopKey` | `Dashboard__Reporting__WorkshopKey` > the shared user-secrets file (`Dashboard:Reporting:WorkshopKey`, written by the UI, same file as the Azure OpenAI settings) > `appsettings.json` | none |
| `HeartbeatSeconds`, `FlushIntervalSeconds`, `RequestTimeoutSeconds` | `appsettings.json` | 60, 5, 10 |

When the developer joins, the dashboard generates a stable **`userId`** (UUID), registers the pair on the server and stores the identity and the **dev token** returned by the server in `Dashboard/.data/identity.json` (git-ignored, private to the user).
The top bar shows a status pill: green *Reporting* (connected), amber *offline, retrying* (events are kept and sent later), red *rejected* (wrong workshop key or identity: check the settings; API key missing or refused: paste the trainer's invitation code in the *Workshop* tab).

### What is sent, what is not

| Sent (`POST /api/v1/events`, `userId` + `username` in every request) | Never sent |
|---|---|
| `catalog.synced` (the labs of `labs.json`), `progress.snapshot` (state of each lab computed from the local history: not started / in progress / completed, run counts, last result, tokens reported) | source files, the solution, the run output |
| `lab.opened` (once per lab per 5 min), `run.started`, `run.finished` (status, failure stage, summary, durations, exit code, checks passed/failed, tokens reported), `solution.viewed` | the AI models (endpoints, deployments, **API keys**, trainer configurations, access code); the workshop key (only as a header at registration); the admin API key (only as the `X-API-Key` header) |
| `settings.changed` (`authMode` only: `apiKey` / `entraId` / `notConfigured`), `heartbeat` every 60 s (`browserConnected`, `activeRunId`) | real name, e-mail, machine name, paths (only the platform, e.g. `macOS`, and the dashboard version) |
| help requests (`POST /api/v1/help-requests`: lab, optional message) and their cancellation | typed input of interactive labs |

### Reliability

- Reporting **never delays or fails a run**: the runner only enqueues; a background loop sends batches (100 events at most) every `FlushIntervalSeconds`.
- Server down: events wait in `Dashboard/.data/outbox.json` (500 at most, the oldest are dropped; the next `progress.snapshot` restores a consistent state) and are sent, in order, when it is back (exponential backoff 5 s → 60 s; `Retry-After` honoured on every route; `503 unavailable` retried with backoff).
- A refused API key (`401 invalidApiKey`) or workshop key (`401 invalidWorkshopKey`) stops all requests instead of retrying, until a new invitation code (or, for the workshop key, a new key) is entered in the settings.
- Each event has a unique id: a batch sent twice (lost answer) is ignored by the server, never applied twice.
- A dev token refused by the server triggers one new registration with the workshop key.
- While a help request is active, its status is read back every 10 s: *waiting for the trainer* → *taken into account* → *resolved* (with the trainer's note), or *cancelled* by the developer ("I'm unblocked").

### Reset the identity

Delete `Dashboard/.data/identity.json` (and `outbox.json`, and `standalone` if present): the next launch shows the welcome dialog again and a developer who joins appears as a **new** developer on the admin (the trainer can archive the old one). To change only the username, use the settings.

## Add a lab

No code change is needed: add an entry to `LabDashboard/labs.json` (see `azureopenai-lab02`, `azureopenai-lab03`, `azureopenai-lab08` or, for an interactive lab, `azureopenai-lab09` for complete examples), then add its id to the catalog test (`LabCatalogTests`):

```jsonc
{
  "id": "multiagentsystem-lab01",
  "number": "01", "track": "Multi-Agent System", "level": "Intermediate",
  "title": "...", "summary": "...", "objectives": ["..."],
  "path": "LearningLabs/MultiAgentSystem/Lab01-UseAgentAsTool",
  "startProject": "Start/<Project>.csproj",
  "solutionProject": "Solution/<Project>.csproj",
  "timeoutSeconds": 180,
  "interactive": false,              // true when the program reads Console.ReadLine
  "expectations": [
    { "id": "scenario1", "description": "Scenario 1 answered", "pattern": "^=== Scenario 1:[^\\n]*===\\n\\S" }
  ],
  "translations": {
    "fr": { "title": "...", "summary": "...", "level": "Débutant", "objectives": ["..."], "checks": { "scenario1": "Scénario 1 : réponse reçue" } }
  }
}
```

`pattern` is a .NET regular expression evaluated (multiline) on the standard output of the run.
Write each check so that it fails on the delivered exercise (`Start`) and passes on the `Solution`. To check the token usage of one scenario only,
stop at the next header: `(?is)^=== Scenario 1:(?:(?!^=== Scenario).)*?input tokens:?\s*\d+`.
For labs with tools, check data that only a tool can produce (a value of the tool's data set, an id generated by the tool, a line written by a middleware), not only the presence of an answer.
For labs that call a remote agent, check what only the remote tools can give (a signed key accepted, a tampered one rejected, see `azureopenai-lab06-client`).
For labs with memory or sessions, check what only the history can give (a name given in an earlier turn), and use a backreference to check that a value printed twice is the same (e.g. the key of a restored session, see `azureopenai-lab05`).
For labs with RAG, check the ranking of the search (the expected entry first), the lines written by the search (tool call, provider search input and results) and a fact that only the knowledge base contains (a deadline, a delay), not the presence of an answer (see `azureopenai-lab07`).
For labs that compare formats or costs, check the measured result itself: the expected record first in the answer, the answer in the requested format (header line), and the sign of the difference in the comparison table (see `azureopenai-lab08`). A comparison row such as `Input tokens  3180  1421  -55%` has no colon, so the token usage parser ignores it.
For labs with tool approvals, check the request itself (function and arguments), the decision, the tool result that only an executed tool can print, and that a rejected call was **not** executed (a negative lookahead on the scenario block, see `azureopenai-lab09`); use lookaheads when the model may order the requests differently from one run to another.
For labs that expose an agent as an MCP tool, check what the MCP client sees (the tool name and its `query` parameter), data that only the tools of the exposed agent know in the tool result (an employee name for an id), the log line a stdio server writes on stderr (relayed by the lab as `[server]`), and the tool call of an agent that uses the MCP tool (see `azureopenai-lab10`).
For labs with an `AIContextProvider`, make the provider write what it does while the agent runs (memories added before the model call, memories extracted after it) and check those lines in order, the data stored for the scope (the user) and what only the provider can give in a **new** session: the name and a preference of the user, with lookaheads over a multi-line answer, bounded by the next block (see `azureopenai-lab12`). Accept any of several memory-only details (`vegetarian`, `tofu`, `Alps`...): the model words its answer differently from one run to another.
For labs that log HTTP traffic, check the logged request itself (the URL, a header added by the handler, the status and duration of the response), that no credential header is printed in clear (a lookahead anchored at `\A` on the whole output, so the pattern is tried once), and what only the second request of a function call contains (`"role": "tool"` with the tool result), not only the agent answer (see `azureopenai-lab11`).

## Tests

```bash
cd Dashboard
dotnet test
```

Unit tests cover the console decoder (spinner, streaming, CRLF split across reads), the token usage parser,
the checks (including the real Lab02, Lab03, Lab04, Lab05, Lab06, Lab07, Lab08, Lab09, Lab10, Lab11 and Lab12 checks against a solution and a delivered-exercise output, Lab03 / Lab04 answers given without calling the tools, a Lab05 conversation that lost its history, Lab06 answers that do not come from the remote tools, Lab07 answers that do not come from the FAQ, Lab08 answers in the wrong format or without token savings, Lab09 approvals that were not honored, Lab10 answers that do not come from the exposed agent, a Lab11 handler that only forwards the requests, prints a credential, or a model that answers without calling the tool, and a Lab12 context provider that is not written or never gives the stored memories to the model, and recall answers with the name only), the build diagnostics,
the catalog path and companion validation, the French check descriptions, the localized README lookup, the Azure OpenAI settings store
(format, preserved entries, precedence, validation, permissions — always in a temporary folder, never the real user-secrets file)
and the masking of the API key, including an end-to-end run of a throwaway project that prints it;
the interactive input: signal parsing (split signals, mixed stderr), numbering, type-ahead, validation, end of input, the paused lab timeout,
the reporting: identity file (creation, permissions, validation), outbox (order, cap, persistence), progress snapshot, and the client against an in-process fake of the admin API (registration with the workshop key, `X-API-Key` on every `/api/v1` route but health (header only, from the configuration or `LABS_ADMIN_API_KEY`), missing or refused API key stopping without retry, refused workshop key, the API key never in logs, status or local files, batches of 100 in order, wrong key, `Retry-After`, lost answer, re-registration on a rejected token, server down then back, help request sent / acknowledged / resolved / cancelled / adopted, settings changes, and a real run against a server that never answers — same verdict, no delay),
the companions: end-to-end runs of throwaway server and client projects (a server ready before the lab and stopped after it, a lab that is a server checked with its client, a lab or a companion server that exits before it is ready);
the reset of exercises, in a temporary repository (preview counts and source files, the name for one lab and the code only for several, single use, expiry, three wrong codes, a running lab, a symbolic link never followed, a catalog project inside `_StartCompleted`, a folder that cannot be deleted keeping its runs, `Start/`, `Solution/` and the other labs byte-for-byte unchanged, the snapshot sent only when joined, and a reset lab that builds and runs again);
the journey (states with Solution-only and cancelled runs, *to resume*, partial XP, both bonuses, persistence, rank thresholds, milestones and distinctions, next mission order, an empty catalog);
the AI models (an existing configuration taken over without a write, both profiles kept across mode switches, the embedding on the LLM's resource or its own, refused incomplete profiles and providers, the trainer configuration identity, keys never in a status) and the trainer flow, against an in-process fake of the admin dashboard and the official test vector: the envelope protocol (the vector opened with the code however it is typed, every tampering failing on the tag, unsupported parameters refused before any derivation, the exact unlock body), the retry and refusal rules, one envelope held per unlock, the keys saved for the ticked models only, the custom profiles untouched, the code and the keys never in an answer or a log, and the mock;
the invitation code (format, every refusal, the trainer's command called directly and as `dotnet LabDashboard.dll invitation`) and the admin API key it brings, used by the reporting and the trainer flow without a restart;
and end-to-end runs of a throwaway project answering three `ReadLine` calls (non-ASCII text, masked secret), closing the input, timing out without an answer, and running unchanged when the lab is not interactive.

## Limitations

- Token usage is only what the exercise prints; model calls whose usage is not printed are not counted. No cost estimate.
- AI models: Azure OpenAI only (the labs only call Azure OpenAI); the trainer's configuration needs the model configuration routes of the admin dashboard (contract v1) and an access code created by the trainer.
- Reporting: a help request clicked while the admin server is down is kept in memory only (it is sent when the server answers, but lost if the dashboard is restarted before); copying `identity.json` to another machine makes both report as the same developer.
- One run at a time (the labs share `CommonUtilities`, parallel builds would conflict). A companion is part of the run, not a second run.
- Interactive labs must be declared `"interactive": true`; `Console.ReadKey` is not supported (see [Interactive labs](#interactive-labs)).
- Colors of `ColoredConsole` are lost: .NET does not emit them when the output is redirected.
