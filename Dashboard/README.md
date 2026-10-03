# Lab Bench — local dashboard for the labs

An optional, local web UI to follow the labs and run them without typing `dotnet` commands.
It is a convenience layer: **every lab keeps working exactly the same with the CLI**, and no lab depends on the dashboard.

> Prototype: registered labs are `Lab01-FirstBasicAIAgent`, `Lab02-AIAgentWithSO`, `Lab03-AIAgentWithFunctionTools`, `Lab04-AIAgentWithMCPClient`, `Lab05-AIAgentWithThreads`, `Lab06_A2AServer`, `Lab06_A2AClient`, `Lab07-AgenticRAG-VectorStore`, `Lab08-DataFormatComparison` and `Lab09-AIAgentWithFunctionToolsHumanApproval` (the migrated labs).
> The two Lab06 labs are run in pairs: each run of one starts the reference solution of the other as its **companion** (see [Labs run in pairs](#labs-run-in-pairs-companion)), on port 5071.
> Scenario 3 of Lab05 needs its MongoDB container (`docker compose up -d` in the lab's `MongoDB/` folder): without it, the Solution run fails with *"MongoDB is not reachable"*.
> Lab07 needs an **embedding** deployment (`AzureOpenAI:EmbeddingDeploymentName`, not part of the settings form): without it, the run stops with *"'AzureOpenAI:EmbeddingDeploymentName' is not configured"*.
> Lab09 is the first **interactive** lab of the catalog: scenario 1 waits for your decision in the input box (`Y` approves; the checks expect the approval), scenario 2 needs no input.

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
| Azure OpenAI settings | top-bar button (status: *API key* / *Microsoft Entra ID* / *Not configured*) opening a form for the endpoint, the chat deployment and the API key shared by every migrated lab — see [Azure OpenAI settings](#azure-openai-settings) |

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
   └─ RunHistoryStore JSON file, atomic writes
   ▼
dotnet build / dotnet run  →  LearningLabs/.../Start|Solution
                              └─ LabInputHook (startup hook, interactive labs only)
```

| API | Purpose |
|---|---|
| `GET /api/labs` | catalog with progress and last runs |
| `GET /api/labs/{id}?lang=fr` | details: README (HTML, `README.fr.md` when present), objectives, packages, commands, checks, history |
| `GET /api/labs/{id}/solution` | solution files that differ from the exercise |
| `POST /api/labs/{id}/runs` `{ "target": "start" \| "solution" }` | start a run (409 if one is in progress) |
| `GET /api/runs/{runId}/events` | Server-Sent Events: `phase`, `output` (streams `stdout`, `stderr`, `system`, `stdin`), `progress`, `input-request`, `input-sent`, `input-closed`, `result` (replayed from the start on reconnection) |
| `POST /api/runs/{runId}/input` `{ "text": "…" }` | send one line to an interactive run (400 `newline` / `tooLong`; 409 `notInteractive`, `notAccepting` (build or exited), `closed`, `tooManyPending`) |
| `POST /api/runs/{runId}/input/close` | close the standard input of an interactive run |
| `POST /api/runs/{runId}/cancel` | cancel a run |
| `GET /api/settings/azure-openai` | Azure OpenAI settings: values, effective source of each one, authentication mode — **never the API key** |
| `PUT /api/settings/azure-openai` `{ "endpoint", "chatDeploymentName", "apiKey" }` | save (400 with field errors; empty endpoint/deployment removes the value, empty key keeps the stored key) |
| `DELETE /api/settings/azure-openai/api-key` | remove the stored API key |
| `GET /api/reporting/status` | reporting to the admin dashboard: enabled, state (`disabled` / `registering` / `connected` / `offline` / `rejected`), server host, username, `userId`, outbox size, help request — **never the workshop key nor the dev token** |
| `POST /api/identity` `{ "username", "serverUrl"?, "workshopKey"? }` | first launch: creates the identity (`userId`) and registers it (400 with field errors) |
| `PUT /api/reporting/settings` `{ "username"?, "serverUrl"?, "workshopKey"? }` | change the username, the server URL or the workshop key (write-only); an empty field keeps the value |
| `GET /api/help` · `POST /api/help` `{ "labId"?, "message"? }` · `POST /api/help/cancel` · `POST /api/help/dismiss` | the help request of the developer: read, send ("Help / Je suis bloqué", 409 `active` while one is active), cancel ("I'm unblocked"), hide a closed one |

## Security

It runs processes, so it is locked down to a local, single-user tool:

- listens on **127.0.0.1 only** (not reachable from the network);
- only the projects declared in `labs.json` can be run; paths are resolved and must stay inside the repository;
- no shell: `ProcessStartInfo.ArgumentList`, never a command string;
- requests with a non-local `Host` header are rejected (DNS rebinding);
- `POST` requests need the `X-Lab-Dashboard: 1` header and a same-origin `Origin` (CSRF: a web page cannot trigger a run);
- no CORS, no authentication secrets, no cloud dependency. The README is rendered with raw HTML disabled;
- the Azure OpenAI API key is **write-only**: the API never returns it (only "stored: yes/no"), the form clears it after saving,
  it is never logged, and any occurrence of it in a run output (live stream, result, history, copied output) is replaced by `••••`.

## Azure OpenAI settings

One configuration for every migrated lab, entered once in the dashboard.

| Question | Answer |
|---|---|
| Where is it stored? | In the **shared user secrets** of the labs (`UserSecretsId` = `microsoft-agent-framework-learninglabs`, read from the lab projects): `~/.microsoft/usersecrets/microsoft-agent-framework-learninglabs/secrets.json` (macOS/Linux) or `%APPDATA%\Microsoft\UserSecrets\microsoft-agent-framework-learninglabs\secrets.json` (Windows). **Outside the repository**; created private to the user (`600`/`700`) on macOS/Linux. |
| How do the labs get it? | Unchanged: their `ConfigurationHelper` reads `appsettings.json` → user secrets → environment variables. So the values apply to runs from the dashboard **and** to `dotnet run` in a terminal, without restarting anything. |
| What is written? | Only `AzureOpenAI:Endpoint`, `AzureOpenAI:ChatDeploymentName` and `AzureOpenAI:APIKey`, in the flat format of `dotnet user-secrets set` (`dotnet user-secrets list` shows them). Other entries of the file are kept; a file that is not valid JSON is never overwritten. |
| What wins? | `appsettings.json` of the lab < these settings < `AzureOpenAI__*` environment variables of the dashboard process. A value already set on the computer (`AzureOpenAI__*` variable) is read and shown in the form, read-only, with a *Set on this computer* badge (the API key only as its last 4 characters, the rest is never sent to the browser); otherwise the developer edits it in the form. |
| A value is missing? | No endpoint or deployment anywhere: the lab stops with *"'AzureOpenAI:Endpoint' is not configured"* and the run result links to the settings. No API key: the labs use **Microsoft Entra ID** (`az login` + *Cognitive Services OpenAI User* role). |
| API version? | Not needed: the labs use the Azure OpenAI **v1** API (`…/openai/v1/`), which has no `api-version`. |

The CLI remains fully equivalent: `dotnet user-secrets set "AzureOpenAI:APIKey" "<key>"` from any migrated lab folder writes the same file.
Other lab settings are not managed by the form: for example the optional Hugging Face token of Lab04 is set with `dotnet user-secrets set "MCPServers:HuggingFace:BearerToken" "<token>"` (same file), and the embedding deployment of Lab07 with `dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "<deployment>"` (or in its `appsettings.json`). The form keeps these entries, and only the Azure OpenAI API key is masked in the run output — Lab04 never prints its token.

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

## Reporting to the admin dashboard

Optional. During a workshop, the trainer runs the **Admin Dashboard** (a separate application in its own repository, hosted on the web) and gives every developer its URL and a **workshop key**.
Once the developer joined, the local dashboard reports their progress to it, and the **Help / Je suis bloqué** button lets them raise a hand that the trainer sees at once.

### First launch: join the workshop, or work on your own

The first time the dashboard opens, a welcome dialog offers two choices:

| Choice | What the developer enters | Result |
|---|---|---|
| **Join the workshop** | a **username** (2–32 characters, letters, digits, `.`, `_`, `-`; not necessarily a real name) and the **workshop key** given by the trainer (hidden when already configured). The admin dashboard URL is shown, not asked: it is fixed in the project configuration | the dashboard generates a stable `userId`, registers on the server and starts reporting |
| **Work on my own** | nothing | nothing is ever sent: no status pill, no Help button, no network call. The choice is remembered (`Dashboard/.data/standalone`) and the dialog does not come back |

Both can be changed later in the *Trainer dashboard* section of the settings: a developer working alone joins the workshop by entering a username and the key; a username can be changed at any time.
Nothing has to be typed on the command line by the developer.

### Configure it (trainer)

The admin dashboard URL is **fixed in the project**: `Dashboard:Reporting:ServerUrl` in `LabDashboard/appsettings.json` (committed, so every developer gets it; `Dashboard__Reporting__ServerUrl` overrides it for a local test). It is not shown to developers and can never be changed in the UI; when it is empty, only *Work on my own* is offered.
The workshop key is entered by each developer in the UI; a trainer who prefers to distribute it as a command can use:

```bash
cd Dashboard/LabDashboard
dotnet user-secrets set "Dashboard:Reporting:WorkshopKey" "<key given by the trainer>" --id microsoft-agent-framework-learninglabs
```

The admin API also requires a shared **API key**: the dashboard sends it as the `X-API-Key` header on every `/api/v1/*` request (all but `/api/v1/health`), never in the URL.
It comes **on top of** the workshop key (sent as `X-Workshop-Key` at registration) and of the dev token (`Authorization: Bearer`), never instead of them: the API key alone is rejected everywhere.
It is project configuration, not something developers type, and it is never shown in the UI nor written to the logs:

```json
// Dashboard/LabDashboard/appsettings.Local.json — git-ignored, next to appsettings.json
{ "Dashboard": { "Reporting": { "ApiKey": "<API key of the admin dashboard>" } } }
```

`LABS_ADMIN_API_KEY` (environment variable) overrides it for a local test. Without a key, reporting stays off: one warning is logged and the status pill says *API key missing*.
A key refused by the server (`401 invalidApiKey`) stops reporting at once, without retrying (30 failures per minute block the machine on the server side), until the dashboard is restarted with a valid key.

| Setting (`Dashboard:Reporting`) | Where | Default |
|---|---|---|
| `ServerUrl` | `appsettings.json` (fixed by the project; `Dashboard__Reporting__ServerUrl` overrides it for a local test). Never editable in the UI | `https://labs-admin-dash.vercel.app` (the hosted admin dashboard) |
| `ApiKey` | `LABS_ADMIN_API_KEY` > `appsettings.Local.json` (git-ignored). Sent as `X-API-Key`; never editable nor displayed in the UI | none (reporting off) |
| `WorkshopKey` | `Dashboard__Reporting__WorkshopKey` > the shared user-secrets file (`Dashboard:Reporting:WorkshopKey`, written by the UI, same file as the Azure OpenAI settings) > `appsettings.json` | none |
| `HeartbeatSeconds`, `FlushIntervalSeconds`, `RequestTimeoutSeconds` | `appsettings.json` | 60, 5, 10 |

When the developer joins, the dashboard generates a stable **`userId`** (UUID), registers the pair on the server and stores the identity and the **dev token** returned by the server in `Dashboard/.data/identity.json` (git-ignored, private to the user).
The top bar shows a status pill: green *Reporting* (connected), amber *offline, retrying* (events are kept and sent later), red *rejected* (wrong workshop key or identity: check the settings; API key missing or invalid: fix the project configuration and restart).

### What is sent, what is not

| Sent (`POST /api/v1/events`, `userId` + `username` in every request) | Never sent |
|---|---|
| `catalog.synced` (the labs of `labs.json`), `progress.snapshot` (state of each lab computed from the local history: not started / in progress / completed, run counts, last result, tokens reported) | source files, the solution, the run output |
| `lab.opened` (once per lab per 5 min), `run.started`, `run.finished` (status, failure stage, summary, durations, exit code, checks passed/failed, tokens reported), `solution.viewed` | the Azure OpenAI endpoint, deployment or **API key**; the workshop key (only as a header at registration); the admin API key (only as the `X-API-Key` header) |
| `settings.changed` (`authMode` only: `apiKey` / `entraId` / `notConfigured`), `heartbeat` every 60 s (`browserConnected`, `activeRunId`) | real name, e-mail, machine name, paths (only the platform, e.g. `macOS`, and the dashboard version) |
| help requests (`POST /api/v1/help-requests`: lab, optional message) and their cancellation | typed input of interactive labs |

### Reliability

- Reporting **never delays or fails a run**: the runner only enqueues; a background loop sends batches (100 events at most) every `FlushIntervalSeconds`.
- Server down: events wait in `Dashboard/.data/outbox.json` (500 at most, the oldest are dropped; the next `progress.snapshot` restores a consistent state) and are sent, in order, when it is back (exponential backoff 5 s → 60 s; `Retry-After` honoured on every route; `503 unavailable` retried with backoff).
- A refused API key (`401 invalidApiKey`) or workshop key (`401 invalidWorkshopKey`) stops all requests instead of retrying: until a restart for the API key, until a new key is entered in the settings for the workshop key.
- Each event has a unique id: a batch sent twice (lost answer) is ignored by the server, never applied twice.
- A dev token refused by the server triggers one new registration with the workshop key.
- While a help request is active, its status is read back every 10 s: *waiting for the trainer* → *taken into account* → *resolved* (with the trainer's note), or *cancelled* by the developer ("I'm unblocked").

### Reset the identity

Delete `Dashboard/.data/identity.json` (and `outbox.json`, and `standalone` if present): the next launch shows the welcome dialog again and a developer who joins appears as a **new** developer on the admin (the trainer can archive the old one). To change only the username, use the settings.

## Add a lab

No code change is needed: add an entry to `LabDashboard/labs.json` (see `azureopenai-lab02`, `azureopenai-lab03`, `azureopenai-lab08` or, for an interactive lab, `azureopenai-lab09` for complete examples), then add its id to the catalog test (`LabCatalogTests`):

```jsonc
{
  "id": "azureopenai-lab10",
  "number": "10", "track": "Azure OpenAI", "level": "Beginner",
  "title": "...", "summary": "...", "objectives": ["..."],
  "path": "LearningLabs/AzureOpenAI/Lab10_ExposeAIAgentAsMCPTool",
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

## Tests

```bash
cd Dashboard
dotnet test
```

Unit tests cover the console decoder (spinner, streaming, CRLF split across reads), the token usage parser,
the checks (including the real Lab02, Lab03, Lab04, Lab05, Lab06, Lab07, Lab08 and Lab09 checks against a solution and a delivered-exercise output, Lab03 / Lab04 answers given without calling the tools, a Lab05 conversation that lost its history, Lab06 answers that do not come from the remote tools, Lab07 answers that do not come from the FAQ, Lab08 answers in the wrong format or without token savings, and Lab09 approvals that were not honored), the build diagnostics,
the catalog path and companion validation, the French check descriptions, the localized README lookup, the Azure OpenAI settings store
(format, preserved entries, precedence, validation, permissions — always in a temporary folder, never the real user-secrets file)
and the masking of the API key, including an end-to-end run of a throwaway project that prints it;
the interactive input: signal parsing (split signals, mixed stderr), numbering, type-ahead, validation, end of input, the paused lab timeout,
the reporting: identity file (creation, permissions, validation), outbox (order, cap, persistence), progress snapshot, and the client against an in-process fake of the admin API (registration with the workshop key, `X-API-Key` on every `/api/v1` route but health (header only, from the configuration or `LABS_ADMIN_API_KEY`), missing or refused API key stopping without retry, refused workshop key, the API key never in logs, status or local files, batches of 100 in order, wrong key, `Retry-After`, lost answer, re-registration on a rejected token, server down then back, help request sent / acknowledged / resolved / cancelled / adopted, settings changes, and a real run against a server that never answers — same verdict, no delay),
the companions: end-to-end runs of throwaway server and client projects (a server ready before the lab and stopped after it, a lab that is a server checked with its client, a lab or a companion server that exits before it is ready);
and end-to-end runs of a throwaway project answering three `ReadLine` calls (non-ASCII text, masked secret), closing the input, timing out without an answer, and running unchanged when the lab is not interactive.

## Limitations

- Token usage is only what the exercise prints; model calls whose usage is not printed are not counted. No cost estimate.
- Reporting: a help request clicked while the admin server is down is kept in memory only (it is sent when the server answers, but lost if the dashboard is restarted before); copying `identity.json` to another machine makes both report as the same developer.
- One run at a time (the labs share `CommonUtilities`, parallel builds would conflict). A companion is part of the run, not a second run.
- Interactive labs must be declared `"interactive": true`; `Console.ReadKey` is not supported (see [Interactive labs](#interactive-labs)).
- Colors of `ColoredConsole` are lost: .NET does not emit them when the output is redirected.
