# Lab Bench — local dashboard for the labs

An optional, local web UI to follow the labs and run them without typing `dotnet` commands.
It is a convenience layer: **every lab keeps working exactly the same with the CLI**, and no lab depends on the dashboard.

> Prototype: registered labs are `Lab01-FirstBasicAIAgent`, `Lab02-AIAgentWithSO`, `Lab03-AIAgentWithFunctionTools`, `Lab04-AIAgentWithMCPClient`, `Lab05-AIAgentWithThreads`, `Lab06_A2AServer` and `Lab06_A2AClient` (the migrated labs).
> The two Lab06 labs are run in pairs: each run of one starts the reference solution of the other as its **companion** (see [Labs run in pairs](#labs-run-in-pairs-companion)), on port 5071.
> Scenario 3 of Lab05 needs its MongoDB container (`docker compose up -d` in the lab's `MongoDB/` folder): without it, the Solution run fails with *"MongoDB is not reachable"*.

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
| What wins? | `appsettings.json` of the lab < these settings < `AzureOpenAI__*` environment variables of the dashboard process. The form shows the effective source of each value and warns when an environment variable overrides it. |
| A value is missing? | No endpoint or deployment anywhere: the lab stops with *"'AzureOpenAI:Endpoint' is not configured"* and the run result links to the settings. No API key: the labs use **Microsoft Entra ID** (`az login` + *Cognitive Services OpenAI User* role). |
| API version? | Not needed: the labs use the Azure OpenAI **v1** API (`…/openai/v1/`), which has no `api-version`. |

The CLI remains fully equivalent: `dotnet user-secrets set "AzureOpenAI:APIKey" "<key>"` from any migrated lab folder writes the same file.
Other lab secrets are not managed by the form: for example the optional Hugging Face token of Lab04 is set with `dotnet user-secrets set "MCPServers:HuggingFace:BearerToken" "<token>"` (same file). The form keeps these entries, and only the Azure OpenAI API key is masked in the run output — Lab04 never prints its token.

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

## Add a lab

No code change is needed: add an entry to `LabDashboard/labs.json` (see `azureopenai-lab02`, `azureopenai-lab03` or `azureopenai-lab04` for complete examples), then add its id to the catalog test (`LabCatalogTests`):

```jsonc
{
  "id": "azureopenai-lab08",
  "number": "04", "track": "Azure OpenAI", "level": "Beginner",
  "title": "...", "summary": "...", "objectives": ["..."],
  "path": "LearningLabs/AzureOpenAI/Lab08-DataFormatComparison",
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

## Tests

```bash
cd Dashboard
dotnet test
```

Unit tests cover the console decoder (spinner, streaming, CRLF split across reads), the token usage parser,
the checks (including the real Lab02, Lab03, Lab04, Lab05 and Lab06 checks against a solution and a delivered-exercise output, Lab03 / Lab04 answers given without calling the tools, a Lab05 conversation that lost its history, and Lab06 answers that do not come from the remote tools), the build diagnostics,
the catalog path and companion validation, the French check descriptions, the localized README lookup, the Azure OpenAI settings store
(format, preserved entries, precedence, validation, permissions — always in a temporary folder, never the real user-secrets file)
and the masking of the API key, including an end-to-end run of a throwaway project that prints it;
the interactive input: signal parsing (split signals, mixed stderr), numbering, type-ahead, validation, end of input, the paused lab timeout,
the companions: end-to-end runs of throwaway server and client projects (a server ready before the lab and stopped after it, a lab that is a server checked with its client, a lab or a companion server that exits before it is ready);
and end-to-end runs of a throwaway project answering three `ReadLine` calls (non-ASCII text, masked secret), closing the input, timing out without an answer, and running unchanged when the lab is not interactive.

## Limitations

- Token usage is only what the exercise prints; model calls whose usage is not printed are not counted. No cost estimate.
- One run at a time (the labs share `CommonUtilities`, parallel builds would conflict). A companion is part of the run, not a second run.
- Interactive labs must be declared `"interactive": true`; `Console.ReadKey` is not supported (see [Interactive labs](#interactive-labs)).
- Colors of `ColoredConsole` are lost: .NET does not emit them when the output is redirected.
