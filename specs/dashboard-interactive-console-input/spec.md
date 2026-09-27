---
title: Dashboard Interactive Console Input
status: implemented
priority: medium
author: PM Agent
created: 2026-09-27
updated: 2026-09-27
jira_project: none
jira_epic: none
---

# Dashboard Interactive Console Input

## Summary

The Lab Bench dashboard learns to run **interactive labs**: when the program of a lab calls `Console.ReadLine()`, the dashboard shows that the program is waiting, lets the learner type the value in an input box under the output, and sends it to the running process. Several successive prompts work, the learner can end the input (the equivalent of `Ctrl+D`), and a lab waiting for the learner does not time out while they read the question. It is a dashboard-only feature, enabled per lab in `labs.json`: no lab source file changes, and `dotnet run` in a terminal behaves exactly as today.

## Problem Statement

- Today the dashboard closes the standard input of every run (`ProcessRunner`), so `Console.ReadLine()` returns `null` at once. Lab09 (human approval, `Y` to approve) and Lab12 (chat loop) cannot be run from the dashboard; the root README lists "interactive labs are not supported" as a limitation.
- These labs are next in the migration plan: without this feature they will be the first migrated labs the dashboard cannot run.
- From the outside, a process waiting on `ReadLine` cannot be told apart from a process waiting for the model (both are silent), so a heuristic "the program seems to wait" is not reliable enough to drive the UI.

## Goals

- [ ] A lab marked interactive can be run from the dashboard end to end, answering each `Console.ReadLine()` from the browser, including accents and non-ASCII characters.
- [ ] The dashboard shows a prompt **exactly** when the program is blocked on a read — never on a mere silence — and numbers successive prompts.
- [ ] Waiting for the learner never counts against the lab timeout; an abandoned run still ends by itself.
- [ ] Labs not marked interactive, and every lab run with the `dotnet` CLI, behave exactly as today.

## Non-Goals

- No `Console.ReadKey()` / `Console.Read()`-driven key-by-key interaction: `ReadKey` throws when stdin is redirected, and no lab uses it.
- No pseudo-terminal (ConPTY / forkpty) and no colors: the output pipeline stays the same.
- No scripted/automatic answers in `labs.json` for unattended checks (future consideration).
- No change to any lab source file, and no migration of Lab09 / Lab12 in this spec (they get `"interactive": true` when they are migrated and registered).
- No input during the build phase.

## Changelog

- **v1.0** (2026-09-27) — Initial release. Tasks T1..T5, no Jira (tracked in this file only).

---

## Reference

### Data Model

No database. Two additive changes to existing JSON contracts:

```jsonc
// Dashboard/LabDashboard/labs.json — per lab, optional, default false
{ "id": "azureopenai-lab09", "interactive": true, "inputIdleTimeoutSeconds": 600 /* optional, default 600 */ }
```

```jsonc
// New Server-Sent Events of /api/runs/{runId}/events (replayed on reconnection like the others)
{ "type": "input-request", "elapsed": 12.3, "inputId": 1 }                 // the program is blocked on ReadLine #1
{ "type": "input-sent",    "elapsed": 20.1, "inputId": 1 }                 // ReadLine #1 received a value
{ "type": "output", "stream": "stdin", "text": "y", "endOfLine": true }   // echo of the value, as a terminal would show it
{ "type": "input-closed",  "elapsed": 25.0 }                               // stdin closed by the learner
```

`LogLine.Stream` accepts the new value `stdin` in the run history.

### Business Rules

1. **Opt-in.** Only labs with `"interactive": true` get an open stdin. Every other lab keeps today's behavior: stdin closed at start, `ReadLine()` returns `null`.
2. **Dashboard-only activation.** The input detection is injected in the lab process only by the dashboard (environment variables set on the child process). A lab run from a terminal loads nothing extra and its behavior is byte-for-byte unchanged.
3. **Exact detection.** A prompt is shown only when the program has actually entered `Console.ReadLine()`. Silence, partial lines or output content never open a prompt by themselves.
4. **Invisible signal.** The signal used by the program to announce a read never appears in the live output, the copied output, the history, the checks, the error list or the token parsing.
5. **Numbering.** Reads are numbered 1, 2, 3… per run. Each value answers the oldest pending read; a value sent while no read is pending is queued and consumed by the next read (type-ahead, as in a terminal).
6. **Input value.** One line, at most 4,096 characters, no `\r` / `\n`, UTF-8 end to end (the program receives exactly the characters typed, on Windows, Linux and macOS). An empty value is allowed (it is a valid `ReadLine` answer).
7. **Who may send.** Input is accepted only for the active run, during the run phase, while stdin is open. Requests keep the existing local-only protections (loopback, `Host` check, `X-Lab-Dashboard: 1`, same-origin `Origin`). Anything else is rejected with a clear status and the run is not affected.
8. **End of input.** The learner can close stdin; the program's next `ReadLine()` returns `null`. After that, no more input is accepted for this run.
9. **Echo and secrets.** Each value is echoed in the output (stream `stdin`) and in the history log, passed through the existing secret redaction first (an API key typed by mistake shows as `••••`). Checks keep evaluating the program's stdout only, so the echo never makes a check pass.
10. **Timeouts.** While a read is pending, the lab timeout (`timeoutSeconds`) is paused. A separate idle input timeout (`inputIdleTimeoutSeconds`, default 600 s) ends a run whose read stays unanswered; its status is *Timed out* with a summary saying no input was received. Cancel works at any moment, including while waiting.

### Edge Cases

| Case | Expected behavior |
|---|---|
| Non-interactive lab calls `ReadLine()` | Unchanged: returns `null` immediately. |
| Interactive lab run from the CLI | Unchanged: reads the keyboard of the terminal. |
| Value sent before the program asks | Queued, consumed by the next read; no prompt flashes. |
| Several reads in a row | Prompt #1 answered → prompt #2 appears; numbering visible. |
| Browser reloaded while a read is pending | After replay, the input box is active again for the pending read. |
| Two browser tabs on the same run | Both show the prompt; the first value sent answers it; the other tab sees it answered. |
| Value with a newline pasted | Rejected (400, field error), nothing sent to the program. |
| Value over 4,096 characters | Rejected (400), nothing sent. |
| Input on a finished / other / non-interactive run, or during build | Rejected (404 / 409), no effect. |
| Program exits while a read is pending | Prompt disappears, the run ends normally with its verdict. |
| Stdin closed, then a value sent | Rejected (409). |
| Nobody answers for `inputIdleTimeoutSeconds` | Run ends as *Timed out* ("no input received"), process tree killed. |
| Lab timeout would expire during a long wait | It does not: only non-waiting time counts. |
| Program writes to stderr at the same time as the signal | Its stderr text is shown intact; only the signal is removed, even if split across reads. |
| Program replaces `Console.In` itself or reads `Console.OpenStandardInput()` directly | Input still reaches it, but no prompt is detected (documented limitation). |

### UI / UX

| Page / View | Route | Purpose |
|---|---|---|
| Lab page → **Run** tab | `http://127.0.0.1:5057` (single page) | Input box under the Output terminal, shown only for interactive labs during the run phase. |
| Lab page → **About** tab | same | States that the lab is interactive and that the dashboard asks for input. |

Interaction: when a read is pending, the input box is enabled and focused, with a badge "Waiting for input #n" and a pulsing cue on the terminal; Enter sends. When no read is pending the box stays usable (type-ahead) but without the badge. An "End input" button closes stdin (with a short explanation of what it does). Sent values appear in the terminal in a distinct `stdin` style, right after the prompt text. All texts exist in English and French (`i18n.js`). Keyboard-only usable, labelled for screen readers, the waiting state is announced (live region), responsive down to phone width, respects `prefers-reduced-motion`. Dark theme only.

### User Stories

#### Story 1: Learner answers the program
**As a** learner running an interactive lab in the dashboard, **I want to** type the answer the program asks for, **So that** I can do the lab without a terminal.

#### Story 2: Learner takes their time
**As a** learner, **I want** the run not to time out while I read the agent's question, **So that** a slow answer does not fail the lab.

#### Story 3: CLI user sees no change
**As a** developer using `dotnet run`, **I want** the labs to behave exactly as before, **So that** the dashboard stays an optional layer.

---

## Tasks

> No Jira: status is tracked with the checkboxes of each block.

### T1 — [backend] Interactive labs receive stdin and announce each read

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: A lab marked `"interactive": true` in `labs.json`, run from the dashboard, keeps its standard input open during the run phase, and every `Console.ReadLine()` it makes is announced to the dashboard by an invisible signal, without any change to the lab's code. Non-interactive labs and CLI runs behave exactly as today.

**Business constraints**:
- Business Rules 1, 2, 3, 6 (UTF-8 part).
- The runner keeps running the same commands (`dotnet build`, then `dotnet run --project … --no-build`); the injection happens only through environment variables on the child process, and must not act inside the `dotnet` CLI host process that also inherits them.
- Stdin stays closed during the build.
- Must work on Windows, Linux and macOS (no shell, no OS-specific API).

**Success criteria**:
- [x] Running an interactive sample from the dashboard: the program receives lines written to its stdin, including `héllo €`, and `null` once stdin is closed.
- [x] Before each `ReadLine()`, the runner observes exactly one numbered read signal (1, 2, 3…).
- [x] The same sample run with `dotnet run` in a terminal reads the keyboard and emits no signal.
- [x] A non-interactive registered lab still gets `null` from `ReadLine()` immediately.

**Implementation notes** (non-binding):
- Validated by prototype: a .NET startup hook (`DOTNET_STARTUP_HOOKS`) gated by `LAB_DASHBOARD_RUN=1`, skipping the entry assembly `dotnet`, calling `Console.SetIn` with a `TextReader` over `Console.OpenStandardInput()` in UTF-8 that flushes stdout, writes `ESC ] lab;input;<n> BEL` to stderr, then reads. `dotnet run` forwards the inherited stdin to the app.
- The hook DLL must be built with the dashboard and its path resolved at runtime (it is not a lab dependency).

**References**: Data Model (`labs.json`), Business Rules 1–3, 6, Edge Cases (non-interactive lab, CLI run), Story 3

---

### T2 — [backend] Prompts, input and end of input through the dashboard API

**Jira**: none
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T1

**User outcome**: While an interactive run is in progress, the dashboard's event stream tells the browser exactly when the program waits for input (and when that input was received or stdin closed), and the browser can send a line or close stdin through the local API. What the learner typed shows up in the run output and in the history, masked like any secret.

**Business constraints**:
- Business Rules 4, 5, 6, 7, 8, 9.
- New endpoints `POST /api/runs/{runId}/input` `{ "text": "…" }` and `POST /api/runs/{runId}/input/close`, covered by the existing CSRF / local-only middleware.
- Events are replayed on reconnection like the existing ones (Data Model).
- The signal is removed from stderr even when it is split across two reads, without altering the surrounding stderr text.

**Success criteria**:
- [x] The event stream of an interactive run contains `input-request` #1 → (value sent) → `input-sent` #1 → `input-request` #2…, and `input-closed` after closing.
- [x] The typed value appears as a `stdin` output line in the live stream and in the history; a stored API key typed as input appears as `••••`.
- [x] Sending to a finished run, another lab's run, a non-interactive run, during build, or after closing is rejected with 404 / 409 and changes nothing; a value with a newline or over 4,096 characters gets a 400 field error.
- [x] A request without `X-Lab-Dashboard: 1` or with a foreign `Origin` is rejected as today.
- [x] The read signal never appears in output, history, errors, checks or copied output.

**References**: Data Model (SSE events), Business Rules 4–9, Edge Cases (type-ahead, several reads, two tabs, invalid values, closed stdin, split signal), Story 1

---

### T3 — [backend] Waiting for the learner does not time the run out

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: T2

**User outcome**: A learner can take as long as they need (up to the idle input timeout) to answer a prompt: the lab timeout only counts the time the program is actually working. A run nobody answers ends by itself with a clear "no input received" timeout, and Cancel stops the run at any moment, even while it waits.

**Business constraints**:
- Business Rule 10; `inputIdleTimeoutSeconds` optional in `labs.json`, default 600.
- Status stays `TimedOut` (no new status), with a distinct summary; the process tree is killed as for the other timeouts.

**Success criteria**:
- [x] With `timeoutSeconds` = 30, a run that waits 60 s on a prompt, then gets an answer, finishes normally.
- [x] With `inputIdleTimeoutSeconds` = 20, an unanswered prompt ends the run after ~20 s as *Timed out* with a "no input received" summary, and the process is gone.
- [x] Cancel during a pending prompt ends the run as *Cancelled* immediately.
- [x] Non-interactive labs time out exactly as before.

**References**: Business Rule 10, Edge Cases (idle timeout, lab timeout during wait, program exits while waiting), Story 2

---

### T4 — [frontend] Input box in the Run tab

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T2

**User outcome**: In the Run tab of an interactive lab, the learner sees an input box under the output. When the program asks, the box is focused with a "Waiting for input #n" badge; they type, press Enter, and see their answer echoed in the terminal before the program continues. They can end the input with a button. After a page reload, a pending prompt is active again. Non-interactive labs show no input box; the About tab says whether the lab is interactive.

**Business constraints**:
- UI / UX section; Business Rules 5, 6 (client-side validation mirrors the server: one line, 4,096 max), 8.
- English and French texts; keyboard-only usable, screen-reader labelled, waiting state announced; responsive; `prefers-reduced-motion`; dark theme only; no framework, no CDN (same as the rest of the dashboard).
- Server errors (400/404/409) are shown next to the box, never as a silent failure.

**Success criteria**:
- [x] On an interactive sample, answering three successive prompts from the keyboard only completes the run.
- [x] The badge appears only while a read is pending; the echo appears in a distinct style.
- [x] Reloading during a pending prompt re-enables the box for that prompt.
- [x] "End input" makes the program's next read return `null` and disables the box.
- [x] Switching to FR translates every new text; at phone width the box and button fit without horizontal scroll.

**References**: UI / UX, Business Rules 5, 6, 8, Edge Cases (reload, two tabs, invalid values), Story 1

---

### T5 — [test] Automated tests and documentation

**Jira**: none
**Agent hint**: unit-testing:test-automator
**Depends on**: T1, T2, T3, T4

**User outcome**: A maintainer running `dotnet test` in `Dashboard/` gets coverage of the whole feature, including a real end-to-end run of a throwaway interactive program; a learner reading the READMEs learns which labs are interactive, how input works in the dashboard, and its limits.

**Business constraints**:
- Tests never touch the real labs or the real user-secrets file (temporary folders only, as the existing tests).
- `Dashboard/README.md` (features table, API table, `labs.json` fields, limitations) and the root `README.md` (comparison table and limitations of Option B) are updated; the "interactive labs are not supported" limitation is replaced by the actual rule.

**Success criteria**:
- [x] `dotnet test` passes and covers: signal parsing (including a split signal and mixed stderr text), numbering and type-ahead, input validation and rejections, close-stdin → `null`, lab timeout paused, idle timeout, redaction of an echoed secret, non-interactive lab unchanged, and an end-to-end run answering 3 prompts with a non-ASCII value.
- [x] Both READMEs describe the feature and no longer claim that interactive labs are unsupported.

**References**: all Business Rules, Edge Cases, Stories 1–3

---

## Test Plan

Status: done (2026-09-27). Verified on macOS; **Windows and Linux still need a manual pass** (steps 1–6).

**Setup (sample interactive lab, not shipped in the lab list).** Create a console project inside the repository (e.g. `Dashboard/.qa/AskLab/Solution/AskLab.csproj`, `net10.0`, with a `<UserSecretsId>` of its own) whose `Program.cs` asks: `Console.Write("Your name> ")` + `ReadLine`, then `WriteLine("Reply Y to approve…")` + `ReadLine` + prints `Approved`/`Rejected`, then a `> ` chat loop that stops on an empty line or `null`. Register it temporarily in `Dashboard/LabDashboard/labs.json` with `"interactive": true`, `"timeoutSeconds": 60`, `"inputIdleTimeoutSeconds": 120`, and a check `^Approved$`. Remove the entry after QA.

1. **Prompt and echo** — `cd Dashboard/LabDashboard && dotnet run`, open http://127.0.0.1:5057, select the sample, click **Run**. After the build, the input box appears under the output with the badge *Waiting for input #1* and the field has the focus. Type `Zoé €`, press Enter: the terminal shows `Your name> Zoé €` on one line (the value in green), then `Hello Zoé €!`, and the badge becomes *#2*. *(Story 1, Business Rules 3, 6)*
2. **Reload while waiting** — reload the page, select the lab: the run replays and the box is active again for *#2*. Type `y`: `Approved` is printed. *(Edge case "browser reloaded")*
3. **Type-ahead and several reads** — at the `> ` prompt, send `bonjour`, then immediately `tapé en avance` before the agent answers: both are answered in order, without a prompt flashing for the second. *(Business Rule 5)*
4. **End input** — click **End input**: the box is disabled with "Input ended…", the program prints `Bye`, the run is **Passed** (1/1 checks). *(Business Rule 8)*
5. **Wait longer than the lab timeout** — run again and wait ~70 s at the first prompt (the lab timeout is 60 s), then answer and end the input: the run still passes. *(Story 2, Business Rule 10)*
6. **Idle timeout and Cancel** — set `inputIdleTimeoutSeconds` to 20, run, don't answer: after ~20 s the run ends *Timed out — no input received for 20 s*. Run again and click **Cancel** while a prompt is pending: *Cancelled* at once, no `AskLab` process left. *(Business Rule 10)*
7. **History** — select the lab again: the last run replays with prompts and answers on the same lines; the **History** tab lists the runs.
8. **French and phone width** — switch to **FR**: badge *Saisie n° 1 attendue*, buttons *Envoyer* / *Terminer la saisie*. At 390 px wide, the field and buttons fit without horizontal scroll. The **About** tab shows the interactive-lab note.
9. **Non-interactive labs and CLI unchanged** — run AzureOpenAI Lab01 from the dashboard: no input box. Run the sample with `dotnet run` in a terminal: it reads the keyboard normally and no escape sequence appears. *(Story 3, Business Rules 1, 2)*
10. **API guards** — `curl -X POST -H 'Content-Type: application/json' -d '{"text":"x"}' http://127.0.0.1:5057/api/runs/nope/input` → 403 (no `X-Lab-Dashboard` header); with `-H 'X-Lab-Dashboard: 1'` → 404. *(Business Rule 7)*

Automated: `cd Dashboard && dotnet test` (77 tests, including `InteractiveRunTests` end-to-end).

## Dependencies

- **Blocked by**: none (builds on the existing runner, SSE stream, CSRF middleware and secret redaction).
- **Blocks**: registering Lab09 and Lab12 in the dashboard after their migration.
- **External**: .NET startup hooks (`DOTNET_STARTUP_HOOKS`, supported since .NET Core 3.0 on all platforms).

## Out of Scope / Future Considerations

- Scripted answers in `labs.json` (`"stdin": ["y"]`) to run and check interactive labs unattended.
- Key-by-key input (`ReadKey`) through a pseudo-terminal.
- Registering Lab09 / Lab12 (done with their migration).

## Open Questions

- [x] No registered lab is interactive yet: manual QA needs a sample. Decision: the end-to-end tests use a throwaway project; the Test Plan registers a sample temporarily in `labs.json` — nothing is shipped in the lab list.
