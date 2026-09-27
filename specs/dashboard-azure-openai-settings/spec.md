---
title: Dashboard Centralized Azure OpenAI Settings and Dark-only Theme
status: implemented
priority: high
author: PM Agent
created: 2026-09-27
updated: 2026-09-27
jira_project: _pending_
jira_epic: _pending_
---

# Dashboard Centralized Azure OpenAI Settings and Dark-only Theme

## Summary

The Lab Bench dashboard gets a **Settings** form where the learner enters, once, the Azure OpenAI connection used by every migrated lab: endpoint, chat deployment and (optionally) API key. The dashboard saves these values in the **shared user-secrets store** that the migrated labs already read (`UserSecretsId` = `microsoft-agent-framework-learninglabs`), so the same configuration applies to runs started from the dashboard **and** to `dotnet run` in a terminal, without touching any lab file. The API key is write-only: it is never sent back to the browser, never logged and never written inside the repository. In the same release, the dashboard drops its light theme and keeps a single dark theme.

## Problem Statement

- Today the API key is not in any lab file on purpose (`appsettings.json` is versioned). The learner must discover in each README that it goes into user secrets (`dotnet user-secrets set "AzureOpenAI:APIKey" …`) or into `AzureOpenAI__*` environment variables, and must replace the `YOUR-…` placeholders of `appsettings.json` in every lab folder.
- When the key is missing, the lab silently falls back to Microsoft Entra ID and fails with a 401/403 that the learner does not relate to the missing key.
- The dashboard, which is meant to remove the need for terminal commands, cannot configure anything: a learner who only uses the dashboard is stuck at the first run.
- The theme toggle and the light theme add code and a second palette to maintain, for a tool the owner wants dark only.

## Goals

- [ ] A learner can go from a fresh clone to a passing Lab01 and Lab02 run **without editing any file or typing any configuration command**, only through the dashboard form.
- [ ] One saved configuration is used by every migrated lab, from the dashboard and from the CLI.
- [ ] The API key never appears in the browser after saving, in the dashboard logs, in the run history, or in any file inside the repository.
- [ ] The learner can always see which value is effective for each setting and where it comes from (dashboard / environment variable / lab `appsettings.json` / missing).
- [ ] The dashboard renders in dark theme only, with no theme selector.

## Non-Goals

- No "test connection" call to Azure OpenAI from the dashboard (running Lab01 is the connection test).
- No per-lab configuration or per-lab override: the configuration is global.
- No encryption of the stored key beyond what the user-secrets store offers (plain JSON in the user profile, as documented by Microsoft for development secrets).
- No configuration for labs that are not migrated yet (they do not read the shared user secrets).
- No "API version" setting: the migrated labs use the Azure OpenAI **v1** API, which has no `api-version` parameter (Migration-Manual §4.1). The form explains this instead of offering a field.
- No new settings beyond those read by the migrated labs today (`Endpoint`, `ChatDeploymentName`, `APIKey`). Fields such as `EmbeddingDeploymentName` are added when a lab that needs them is migrated.
- No change to the lab projects themselves (`ConfigurationHelper`, `.csproj`, `appsettings.json`).

## Changelog

- **v1.0** (2026-09-27) — Initial release. Tasks T1..T7 implemented directly (no Jira, "no ticket" override).
- **v1.0.1** (2026-09-27) — Found during implementation: static files are now served with `Cache-Control: no-cache` (a browser kept the pre-update page and scripts); lab list overflow at phone width fixed (appeared with the second lab).

---

## Reference

### Data Model

No database. The single store is the .NET **user-secrets** file of the shared id `microsoft-agent-framework-learninglabs`, at the location .NET uses:

| OS | Path |
|---|---|
| macOS / Linux | `~/.microsoft/usersecrets/microsoft-agent-framework-learninglabs/secrets.json` |
| Windows | `%APPDATA%\Microsoft\UserSecrets\microsoft-agent-framework-learninglabs\secrets.json` |

Format: the flat format written by `dotnet user-secrets set` (so that `dotnet user-secrets list` keeps working):

```json
{
  "AzureOpenAI:Endpoint": "https://my-resource.openai.azure.com/",
  "AzureOpenAI:ChatDeploymentName": "gpt-4o-mini",
  "AzureOpenAI:APIKey": "…"
}
```

Managed keys: `AzureOpenAI:Endpoint`, `AzureOpenAI:ChatDeploymentName`, `AzureOpenAI:APIKey`. Any other key already in the file is preserved as is.

How the labs read it (unchanged): `appsettings.json` → **user secrets** → environment variables (last wins), bound to `AzureOpenAISettings`.

### Business Rules

1. **Single store**: the dashboard reads and writes only the shared user-secrets file above. It never writes configuration inside the repository (no lab `appsettings.json`, nothing under `Dashboard/`).
2. **Preserve foreign content**: saving changes only the three managed keys; every other key of the file is kept unchanged. The file is written atomically (a failed save leaves the previous file intact). If the file exists but is not valid JSON, the dashboard refuses to save and shows an actionable error rather than overwriting it.
3. **File permissions**: on macOS/Linux, a directory or file created by the dashboard is readable and writable by the current user only.
4. **Validation**:
   - `Endpoint`: optional; when provided, an absolute `https://` URL. The resource URL (`https://<resource>.openai.azure.com/`) and the v1 URL (`…/openai/v1/`) are both accepted, as by `AzureOpenAIEndpoint.ToV1Uri`. Values containing the `YOUR-` placeholder are rejected.
   - `ChatDeploymentName`: optional; when provided, non-empty after trimming, no whitespace inside, no `YOUR-` placeholder.
   - `APIKey`: optional, trimmed, no whitespace inside.
   - A field left empty in the form is **not** written (for Endpoint/Deployment: the key is removed from the store, so the lab's `appsettings.json` applies again).
5. **API key is write-only**:
   - The dashboard API never returns the key or any fragment of it; it only returns whether a key is stored.
   - Leaving the key field empty on save **keeps** the stored key. Removing it requires an explicit "Remove API key" action with confirmation.
   - The key is never stored in browser storage, never placed back in the DOM after saving, and the input is a password field with autofill disabled.
6. **Effective value and source** (shown for each field): environment variable `AzureOpenAI__<Name>` of the dashboard process (highest priority, overrides the dashboard value — shown as a warning) → dashboard value (user secrets) → lab `appsettings.json` (shown as "lab default", may be a placeholder) → missing. For the API key, "missing" means **Microsoft Entra ID** mode (`az login` + *Cognitive Services OpenAI User* role).
7. **Secret redaction**: whenever runs are executed, any occurrence of the stored API key (and of an `AzureOpenAI__APIKey` environment value) in the build output, the program output, the live stream and the saved history is replaced by `••••` before it leaves the runner.
8. **Local-only protections apply**: the new endpoints follow the existing rules (127.0.0.1 only, local `Host` header, `X-Lab-Dashboard: 1` header + same-origin `Origin` on every mutating request). No value is ever written to the application logs.
9. **No restart needed**: a saved configuration is used by the next lab run; the dashboard itself does not need to be restarted. A run already in progress is not affected.
10. **Dark theme only**: the dashboard always renders with the dark palette, whatever the operating system preference; there is no theme control, and any previously saved theme preference in the browser is ignored.

### Edge Cases

| Case | Expected behavior |
|---|---|
| Fresh machine, no user-secrets file | Form shows every field as "not configured"; the header status is "Not configured"; saving creates the directory and the file (Business Rule 3) |
| File contains other secrets (e.g. from another tool) | They are preserved after save (Business Rule 2) |
| File is not valid JSON | Save refused with "The user-secrets file is not valid JSON: fix or delete it" and its path; nothing is overwritten |
| `AzureOpenAI__APIKey` (or another `AzureOpenAI__*`) set in the environment of the dashboard | Field shows "Overridden by environment variable — the dashboard value is ignored"; saving still works but the warning stays |
| Endpoint and deployment missing everywhere | Runs still start; the lab fails with its existing "'AzureOpenAI:Endpoint' is not configured" message; the run result shows a link to Settings |
| No API key anywhere | Status shows "Microsoft Entra ID (no API key)" with the `az login` + role hint; runs use Entra ID |
| User leaves key field empty and saves | Stored key unchanged |
| User clicks "Remove API key" | Confirmation, then the key is removed; status switches to Entra ID mode |
| Invalid endpoint (`http://`, relative, placeholder) | Inline validation error, nothing saved |
| A lab prints the key by mistake | Redacted as `••••` in live log and history (Business Rule 7) |
| Save attempted from another origin / without the header | Rejected (403), like existing POST endpoints |
| Run in progress while saving | The running lab keeps its configuration; the next run uses the new one |
| Browser had `labbench.theme = light` saved | Dashboard is dark anyway |

### UI / UX

| Page / View | Route | Purpose |
|---|---|---|
| Settings panel | `/` (dashboard single page, opened from a "Settings" / "Paramètres" entry in the top bar) | Edit the Azure OpenAI configuration shared by the labs |
| Configuration status | top bar of `/` | Always-visible summary: "Azure OpenAI: API key" / "Entra ID" / "Not configured" |
| API | `GET /api/settings/azure-openai`, `PUT /api/settings/azure-openai`, `DELETE /api/settings/azure-openai/api-key` | Read status (never the key), save, remove the key |

Form content: Endpoint, Chat deployment, API key (password, with "stored" indicator and "Remove" action), for each field its effective source (Business Rule 6); a short note "API version: not needed (Azure OpenAI v1 API)"; a note that the configuration applies to the migrated labs, from the dashboard and from `dotnet run`, and where it is stored (path). Save button with success / error feedback; field-level validation messages. All texts in English and French like the rest of the UI; keyboard accessible, labels bound to inputs, works at phone width.

Theme: single dark palette; the theme button disappears from the top bar.

### User Stories

#### Story 1: Learner configures the labs once
**As a** learner using the dashboard, **I want to** enter my endpoint, deployment and API key in one form, **So that** every lab runs without editing files or typing commands.

#### Story 2: Learner uses the terminal too
**As a** learner, **I want** the values saved in the dashboard to be used by `dotnet run` in a terminal, **So that** I don't maintain two configurations.

#### Story 3: Learner understands what is used
**As a** learner, **I want to** see which value is effective and where it comes from, **So that** I understand why a run uses Entra ID or fails with "not configured".

#### Story 4: Learner keeps the key secret
**As a** learner, **I want** my API key never to be displayed, logged or committed, **So that** I can share screenshots, logs or the repository safely.

#### Story 5: Owner wants a single theme
**As the** dashboard owner, **I want** only the dark theme, **So that** there is one palette to maintain and no theme switch.

---

## Tasks

### T1 — [backend] Read and save the shared Azure OpenAI configuration

**Jira**: _pending_
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: The dashboard exposes the current Azure OpenAI configuration used by the labs and lets it be saved. After a save, the next run of Lab01 or Lab02 — from the dashboard or with `dotnet run` in a terminal — uses the new endpoint, deployment and key, with no restart and no file edited in the repository.

**Business constraints**:
- Business Rules 1, 2, 3, 4, 6, 9 (single store, preservation, permissions, validation, effective source, no restart).
- The status returned for the API key is only "stored / not stored" and its source (Business Rule 5).
- The user-secrets id is the one declared by the migrated labs; the dashboard must not hardcode a second id.

**Success criteria**:
- [ ] Saving endpoint + deployment + key, then running `dotnet user-secrets list` in a migrated lab folder shows the three `AzureOpenAI:*` keys.
- [ ] A key added beforehand with `dotnet user-secrets set "Other:Key" "x"` is still listed after a save from the dashboard.
- [ ] After saving, Lab01 Solution passes from the dashboard and from `dotnet run` in a terminal.
- [ ] Reading the configuration never returns the API key value (checked in the network response).
- [ ] Setting `AzureOpenAI__Endpoint` before starting the dashboard makes the endpoint status "environment variable".
- [ ] Invalid values (placeholder, `http://`, whitespace in deployment) are rejected with a field-level error and the file is unchanged.
- [ ] A corrupted `secrets.json` is not overwritten; the error names the file.

**Implementation notes** (optional, non-binding):
- The flat key format must stay compatible with `dotnet user-secrets list/set`; tests must use a temporary directory, never the real user-secrets file of the machine.

**References**: Data Model, Business Rules 1–6 and 9, Edge Cases (fresh machine, other secrets, invalid JSON, environment variable, invalid endpoint, run in progress), Stories 1–3

---

### T2 — [security] Keep the API key out of the UI, logs, history and repository

**Jira**: _pending_
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T1

**User outcome**: The learner can save and remove the API key, but can never read it back from the dashboard. If a lab or a build ever prints the key, the live log and the saved history show `••••` instead. Requests from other websites cannot change the configuration.

**Business constraints**:
- Business Rules 5, 7, 8.
- Removing the key is an explicit action, distinct from saving with an empty key field.
- Redaction covers every channel that leaves the runner: live events, final result, history file, "copy output".

**Success criteria**:
- [ ] Removing the key requires the explicit action; saving with an empty key field keeps it.
- [ ] A test lab output containing the stored key shows `••••` in the live log and in `Dashboard/.data/history.json`.
- [ ] A save or remove request without the dashboard header or from another origin is rejected with 403.
- [ ] The dashboard console/application logs contain no configuration value after a save.
- [ ] `git status` shows no new or modified file after saving the configuration.

**References**: Business Rules 5, 7, 8, Edge Cases (key printed by a lab, other origin, remove key), Story 4

---

### T3 — [frontend] Settings form for the shared Azure OpenAI configuration

**Jira**: _pending_
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T1, T2

**User outcome**: From the top bar, the learner opens "Settings", fills the endpoint, the chat deployment and optionally the API key, and saves. The form shows, for each field, the effective value source, explains that no API version is needed, tells where the configuration is stored and that it applies to the dashboard and to `dotnet run`. The API key field never shows the stored key; a "stored" badge and a "Remove API key" action are shown instead.

**Business constraints**:
- UI / UX section; Business Rules 4, 5, 6.
- English and French texts, like every other UI text.
- Keyboard accessible, labelled inputs, usable at phone width.
- The key input is a password field with autofill disabled and is cleared after a successful save.

**Success criteria**:
- [ ] A learner configures a fresh machine from the form alone, then Lab01 Solution passes.
- [ ] Invalid values show inline errors next to the fields and nothing is saved.
- [ ] An environment-variable override is shown as a warning on the concerned field.
- [ ] After saving, reloading the page shows "API key stored" and an empty key field.
- [ ] Switching EN/FR translates every text of the form.

**References**: UI / UX, Business Rules 4–6, Edge Cases (fresh machine, environment variable, empty key field, remove key, invalid endpoint), Stories 1–3

---

### T4 — [frontend] Configuration status in the top bar and in failed runs

**Jira**: _pending_
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T1

**User outcome**: The top bar always shows whether the labs will use an API key, Microsoft Entra ID, or are not configured, and opens the Settings panel on click. When a run fails because the configuration is missing, the result suggests opening Settings.

**Business constraints**:
- Business Rule 6 (Entra ID when no key; "Not configured" when endpoint or deployment is missing everywhere).
- English and French texts.

**Success criteria**:
- [ ] Fresh machine: status "Not configured"; after saving endpoint + deployment without key: "Microsoft Entra ID"; with key: "API key".
- [ ] A run failing with "'AzureOpenAI:Endpoint' is not configured" shows a link that opens Settings.
- [ ] The status updates after a save without reloading the page.

**References**: UI / UX, Business Rule 6, Edge Cases (endpoint and deployment missing, no API key), Story 3

---

### T5 — [ui] Dark theme only

**Jira**: _pending_
**Agent hint**: ui-design:ui-designer
**Depends on**: —

**User outcome**: The dashboard always opens in the dark theme, whatever the system preference or what was chosen before. The theme button is gone from the top bar; nothing else changes in the layout.

**Business constraints**:
- Business Rule 10; Edge Case "browser had light saved".
- The light palette, the theme selector, its translations and its stored preference handling are removed, not just hidden.
- Contrast of the dark palette stays readable (text, badges, logs, token table).

**Success criteria**:
- [ ] With the operating system in light mode, the dashboard is dark.
- [ ] No theme button in the top bar, in English and French.
- [ ] A browser that had saved the light theme shows the dark theme.
- [ ] Every existing view (lab list, run, instructions, history, about, solution, settings) renders correctly in dark.

**References**: Business Rule 10, Edge Cases (browser had light saved), Story 5

---

### T6 — [test] Automated tests for the configuration store and redaction

**Jira**: _pending_
**Agent hint**: backend-development:test-automator
**Depends on**: T1, T2

**User outcome**: The dashboard test suite (`dotnet test` in `Dashboard/`) guards the new behaviors, so a later change cannot silently overwrite foreign secrets, leak the key or break the precedence rules.

**Business constraints**:
- Tests use a temporary user-secrets location; they never read or write the real user-secrets file of the machine.
- Business Rules 2, 4, 5, 6, 7.

**Success criteria**:
- [ ] Tests fail if a save drops a foreign key, overwrites an invalid JSON file, returns the key, or ignores an environment override.
- [ ] Tests fail if the key appears unredacted in a run result.
- [ ] The whole suite passes, including the existing tests.

**References**: Business Rules 2, 4–7, Edge Cases (other secrets, invalid JSON, environment variable, key printed by a lab)

---

### T7 — [glue] Documentation of the centralized configuration

**Jira**: _pending_
**Agent hint**: code-improver
**Depends on**: T1, T2, T3, T4, T5

**User outcome**: A learner reading the dashboard README, the root README or the Lab01/Lab02 README learns that the configuration can be entered once in the dashboard Settings (or with `dotnet user-secrets`), where it is stored, the precedence order, what happens when a value is missing, and how the key is protected. The theme section no longer mentions light mode.

**Business constraints**:
- Reference Data Model, Business Rules 1, 5–7, 10.
- The CLI path (`dotnet user-secrets set …`) stays documented as fully equivalent.

**Success criteria**:
- [ ] Following only the dashboard README, a learner configures the labs without a terminal command.
- [ ] Following only a lab README, a learner configures it with the CLI, as today.
- [ ] No document mentions a light theme or a theme toggle.

**References**: Data Model, Business Rules 1, 5–7, 10, Stories 1–5

---

## Test Plan

> Status: ready for human QA (implemented 2026-09-27, without Jira — "no ticket" override).
> Tip: to try it without touching your real configuration, start the dashboard with a temporary home:
> `HOME=/tmp/labbench-home NUGET_PACKAGES=~/.nuget/packages dotnet run` in `Dashboard/LabDashboard` (the labs it starts inherit it).

| # | Story / case | Steps | Expected |
|---|---|---|---|
| 1 | Story 5 — dark only | Set the OS to light mode, open http://127.0.0.1:5057 | Dark dashboard; no theme button in the top bar (EN and FR) |
| 2 | Fresh machine | No `AzureOpenAI__*` environment variable, no user-secrets file; open the dashboard | Top bar: *Azure OpenAI · Not configured* (red dot) |
| 3 | Story 1 — configure | Click the chip, enter endpoint + deployment + API key, *Save* | Toast *Settings saved*; chip *API key*; key field empty, badge *Stored*, *Remove API key* visible |
| 4 | Story 2 — CLI | In `LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start`: `dotnet user-secrets list`, then `dotnet run --project ../Solution` | The 3 `AzureOpenAI:*` keys are listed; Lab01 runs |
| 5 | Story 1 — dashboard run | Run Lab01 / Lab02 *Solution* from the dashboard | *Passed* |
| 6 | Story 4 — write-only key | Reload, reopen Settings; DevTools → Network → `GET /api/settings/azure-openai` | Key never in the field nor in the response (only `"stored": true`) |
| 7 | Validation | Enter `http://x`, `gpt 4o`, key `abc`, *Save* | Inline errors under the 3 fields; `secrets.json` unchanged |
| 8 | Keep / remove key | *Save* with the key field empty → then *Remove API key* + confirm | Key kept → then removed; chip *Microsoft Entra ID* (amber) |
| 9 | Story 3 — environment override | Start the dashboard with `AzureOpenAI__Endpoint=https://other.openai.azure.com/` | Endpoint field shows the amber *Overridden by the environment variable…* message |
| 10 | Missing configuration | Empty the endpoint, *Save*, run Lab01 Solution | Run fails with *'AzureOpenAI:Endpoint' is not configured*; errors card shows *Open the Azure OpenAI settings*, which opens the form |
| 11 | Foreign secrets | `dotnet user-secrets set "Other:Key" "x"`, then save from the dashboard | `dotnet user-secrets list` still shows `Other:Key` |
| 12 | Corrupted file | Write `{ not json` into `secrets.json`, open Settings, *Save* | Red message with the file path; file unchanged |
| 13 | Repository | After all the above: `git status` | No file created or modified by the settings |
| 14 | EN/FR, phone width | Switch language, resize to 390 px | Form fully translated; no horizontal scroll |

Automated: `dotnet test` in `Dashboard/` (46 tests, including the store in a temporary folder and an end-to-end redaction run).

## Dependencies

- **Blocked by**: migration of the labs to the shared user-secrets configuration (done for Lab01 and Lab02).
- **Blocks**: registration of the next migrated labs in the dashboard (they inherit the configuration for free).
- **External**: .NET user-secrets conventions (file location and flat JSON format).

## Out of Scope / Future Considerations

- "Test connection" button calling the configured deployment.
- Additional fields (`EmbeddingDeploymentName`, MongoDB connection string…) when Lab05/Lab07 are migrated.
- Settings of non-Azure-OpenAI dependencies (MCP servers, A2A endpoints).
- OS keychain storage (would require a change in every lab's configuration loading).

## Open Questions

- [ ] Security audit at stage 4: this feature handles a secret, so `comprehensive-review:security-auditor` and `pr-review-toolkit:silent-failure-hunter` must run on top of `mbs-code-audit` (global rule "security-critical exception").
