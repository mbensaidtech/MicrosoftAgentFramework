---
title: Dashboard Exercise Reset
status: implemented
priority: medium
author: PM Agent
created: 2026-10-25
updated: 2026-10-25
jira_project: none
jira_epic: none
---

# Dashboard Exercise Reset

## Summary

The Lab Bench dashboard lets the learner put one exercise, several exercises or all of them back to *Not started*, to undo a mistake such as an exercise run by accident or a `_StartCompleted` folder left in a lab folder. The reset removes the state the dashboard keeps for the lab (its runs in the local history and its `_StartCompleted/` folder) and nothing else: the learner's code in `Start/`, the reference `Solution/`, the instructions and every setting stay untouched. Because it deletes data that cannot be recovered, the reset is guarded by confirmations proportional to the risk (the exercise name typed for one exercise; every name plus a random 5-character code for several), shows exactly what will be deleted before anything happens, and ends with a per-exercise summary.

## Problem Statement

- Today an exercise only moves forward: one Start run puts it *In progress*, one passing Start run marks it *Completed*, and the 30 last runs stay in the History tab. The only way back is to edit `Dashboard/.data/history.json` by hand while the dashboard is stopped, which is error-prone (one JSON file holds every lab) and undocumented.
- `_StartCompleted/` folders (git-ignored completed copies of `Start/` used to check the labs) are left in lab folders: `Lab01-FirstBasicAIAgent/_StartCompleted` and `Lab02-AIAgentWithSO/_StartCompleted` exist today and only hold MSBuild `obj/` leftovers. They are deleted by hand, with the risk of deleting the wrong folder (`Start/` sits next to it).
- In a workshop, the trainer's admin dashboard shows the progress computed from that history: an accidental run makes a learner look *In progress* on a lab they have not started.

## Goals

- [x] A learner can reset one exercise, a selection of exercises or all of them from the dashboard, without a terminal and without editing a file.
- [x] After a reset, the exercise shows *Not started*, *No run yet*, an empty history, its `_StartCompleted/` folder is gone, and it runs again normally.
- [x] No reset can delete a file of `Start/`, `Solution/`, the instructions, the catalog or the settings, and no folder is deleted without its role having been checked.
- [x] No reset happens by accident: confirmations scale with the number of exercises, a double click or a replayed request never resets twice, and the learner sees what will be deleted before confirming.

## Non-Goals

- No restore of the learner's code: `Start/` is never reverted to the delivered exercise (no `git checkout`, no file copy). Restoring the exercise code is a separate feature.
- No undo and no backup of what a reset deletes (the confirmations are the protection).
- No cleanup of build outputs (`Start/bin`, `Start/obj`), of other local folders (`.tmp-*`), of lab runtime data (MongoDB containers of Lab05 / Lab12) or of the reporting identity (it has its own procedure in the dashboard README).
- No partial reset (only the Solution runs, only the failed runs…).
- No remote reset: the trainer's admin dashboard cannot trigger a reset.

## Changelog

- **v1.0** (2026-10-25) — Initial release. Tasks T1..T8, no Jira (tracked in this file only), implemented directly.
- **v1.1** (2026-10-25) — Copy button on every exercise name; several / all confirmed with the code only (no names); only the exercises with something to reset are listed. Business Rules 5, 7, 8 and 14 revised, Tasks T9..T11 (implemented directly).

---

## Reference

### Data Model

No database and no new file. A reset removes entries from existing local files and folders:

```jsonc
// Dashboard/.data/history.json — the key of each reset lab is removed (all its Start and Solution runs)
{ "azureopenai-lab01": [ /* removed */ ], "azureopenai-lab02": [ /* kept */ ] }
```

```text
LearningLabs/<track>/<lab folder>/_StartCompleted/   ← deleted (git-ignored, see .gitignore "Completed copies of the Start projects")
LearningLabs/<track>/<lab folder>/Start/             ← never touched
LearningLabs/<track>/<lab folder>/Solution/          ← never touched
```

Local API (new routes, same JSON conventions as the existing ones):

```jsonc
// POST /api/labs/reset/preview  { "labIds": ["azureopenai-lab01", "azureopenai-lab02"] }
// 200 — nothing is deleted at this stage
{
  "confirmationId": "…",                 // single use, expires after 10 minutes
  "expiresAt": "2026-10-25T16:10:00+01:00",
  "code": "K7QX3",                       // only when several labs are selected
  "labs": [
    {
      "id": "azureopenai-lab01", "folderName": "Lab01-FirstBasicAIAgent",
      "progress": "completed", "startRuns": 4, "solutionRuns": 2,
      "completedCopy": { "path": "LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/_StartCompleted", "files": 9, "bytes": 18432, "sourceFiles": 0 },
      "blocked": "link",                 // only when blocked: "running" | "link" | "notDirectory" | "containsProject" | "unreadable"
                                         // (then no confirmationId and no code are issued)
      "nothingToReset": false
    }
  ]
}

// POST /api/labs/reset  { "confirmationId": "…", "names": ["Lab01-FirstBasicAIAgent"] }   — one lab: its folder name
// POST /api/labs/reset  { "confirmationId": "…", "code": "K7QX3" }                       — several labs: the code only (v1.1)
// 200
{ "labs": [ { "id": "azureopenai-lab01", "status": "reset", "runsRemoved": 6, "filesRemoved": 9, "verified": true },
            { "id": "azureopenai-lab02", "status": "failed", "error": "…", "runsRemoved": 0, "filesRemoved": 0 } ] }
// 400 { "errors": { "names": "mismatch" } } | { "errors": { "code": "mismatch" } }
// 409 { "error": "running", "labIds": [...] } | { "error": "busy" }
// 410 { "error": "expired" }   — unknown, expired or already used confirmation
// 410 { "error": "tooManyAttempts" } — the 3rd wrong code cancelled the confirmation

// GET /api/labs — each lab also carries "hasCompletedCopy" (badge in the selection list) and "folderName" (the name typed / copied, v1.1)
```

### Business Rules

1. **What the state of an exercise is.** For a lab, the dashboard keeps (a) its runs in the local history (`Dashboard/.data/history.json`, keyed by lab id), from which the progress (*Not started* / *In progress* / *Completed*), the last result, the metrics, the History tab and the progress sent to the trainer are computed, and (b) the `_StartCompleted/` folder directly inside the lab folder. Resetting a lab removes **all** its runs (Start and Solution) and that folder. Nothing else is part of the state.
2. **Never touched.** `Start/` and `Solution/` (sources, `bin/`, `obj/`), README files, `labs.json`, `appsettings*.json`, user secrets, `identity.json`, `outbox.json`, `standalone`, the history of the labs not selected, every other file or folder of the lab folder and of the repository.
3. **Folder verification.** `_StartCompleted/` is deleted only when all of these hold: its path is `<lab folder>/_StartCompleted` (fixed name, never taken from the request), it resolves inside the repository, it is a real directory and not a symbolic link or junction, and no project declared in the catalog (Start, Solution or companion of any lab) lives inside it. Otherwise the lab is *blocked*: it is not reset, and the reason is shown.
4. **Preview first.** Before any confirmation, the dashboard shows for each selected lab: its progress, the number of Start and Solution runs that will be removed, and whether `_StartCompleted/` exists with its number of files, its size and its number of **source files** (files outside `bin/` and `obj/`). When source files are present, a warning says that this completed copy of the code will be deleted.
5. **Exercise name.** The name of an exercise is its lab folder name (e.g. `Lab01-FirstBasicAIAgent`), shown next to the lab title in the current language with a **copy** button wherever the dialog lists the exercise (v1.1). When it is typed (one exercise), it is compared exactly (case-sensitive), ignoring leading and trailing spaces; pasting the copied name is accepted.
6. **One exercise.** The learner types the exercise name, then confirms a second, explicit screen stating what will be deleted and kept. Only then is the reset executed.
7. **Several exercises or all.** After checking what will be deleted, the learner retypes a random 5-character code shown on screen (uppercase letters and digits without ambiguous characters `0 O 1 I L`, case-insensitive) — **no exercise name is asked** (v1.1); then a recap screen lists the consequences and the exercises. Only then is the reset executed. Selecting two exercises or all of them follows this path; selecting only one in this mode follows Rule 6.
8. **Enforced by the local API, not only by the page.** A reset requires a confirmation issued by a preview for exactly that set of labs, valid 10 minutes and usable once; the request carries the typed name for one lab, or the code for several labs (names are not checked then). Three wrong codes cancel the confirmation. A replayed, double-submitted or expired request deletes nothing and answers that the confirmation is no longer valid.
9. **No overlap.** A lab with a run in progress cannot be selected, previewed or reset. Only one reset executes at a time; a second one is refused while the first runs.
10. **Who may reset.** The same protections as every state-changing request of the dashboard: loopback only, local `Host`, `X-Lab-Dashboard: 1` header, same-origin `Origin`. Deletions run with the permissions of the dashboard process: a permission or I/O error is reported for that lab with the path, never ignored.
11. **Order, atomicity, verification.** For each lab, the folder is deleted first; the runs are removed only if the folder is gone (a lab never looks reset while its folder remains). The history changes of every lab are written in one atomic write. A lab is reported *reset* only when a re-check confirms it: no run left, no `_StartCompleted/`, Start and Solution projects still present. A lab that already had nothing to reset is reported as such. A failure on one lab does not stop the others.
12. **Trainer's dashboard.** When the learner joined a workshop, a fresh progress snapshot (the existing `progress.snapshot` event, authoritative on the server) is sent after a reset that changed the history. Nothing is sent when working alone. No new event type.
13. **Summary.** After execution, the learner sees per lab what was removed (runs, files) or the error and its reason; the lab list, the metrics and the open lab refresh to the new state.
14. **Only exercises with something to reset are listed (v1.1).** The selection lists (one exercise and several / all) show only the labs whose progress is *In progress* or *Completed* (a run of the Start project in the history) or that hold a `_StartCompleted/` folder. A never-started exercise is excluded — including a lab with only Solution runs, which is *Not started* — and its History tab has no reset button. When no lab qualifies, the dialog says so and offers nothing to select.

### Edge Cases

| Case | Expected behavior |
|---|---|
| Lab already at zero (no run, no folder) | Not listed (Rule 14). If its state changes between the list and the reset, the server still answers *nothing to reset*, no error. |
| Lab with only *Reference solution* runs | *Not started*: not listed, no reset button in its History tab (Rule 14). |
| Lab never run but holding `_StartCompleted/` | Listed, with the `_StartCompleted` badge (the folder is something to reset). |
| No lab to reset | The dialog shows "No exercise to reset", nothing to select, *Continue* disabled. |
| `_StartCompleted/` holds only `obj/` | Preview: N files, 0 source files; deleted. |
| `_StartCompleted/` holds source files (`.cs`, `.csproj`…) | Preview warns with the count; deleted only after the confirmations. |
| `_StartCompleted` is a symbolic link / junction | Lab *blocked* (`link`): nothing deleted, reason shown, the link must be removed by hand. |
| A catalog project lives inside `_StartCompleted/` | Lab *blocked* (`containsProject`). |
| Lab running when selected, or starts running before execution | Not selectable / refused (409 `running`), nothing deleted for any lab of the request. |
| Typed name wrong, wrong case, other lab's name (one exercise) | Cannot continue (page); 400 `names` (API). |
| Names sent for several exercises | Ignored: only the code is checked (v1.1). |
| Clipboard not available | The copy button shows a failure state and says to copy the name by hand; nothing else changes. |
| Code wrong | Cannot continue (page); 400 `code` (API); the 3rd wrong code cancels the confirmation (410 `tooManyAttempts`, then 410 `expired`). |
| Double click on the final button / request replayed | One request (the button is disabled while it runs); a replayed request gets 409 `busy` while the first executes, 410 `expired` after. Nothing deleted twice. |
| Confirmation left open more than 10 minutes | 410 `expired`; the page offers to start again with a fresh preview. |
| Second reset while one executes (two tabs) | 409 `busy`. |
| Permission denied on the folder | Lab *failed* with the path; its runs are kept; other labs reset. |
| History file cannot be written | Labs *failed* ("history not written"); their folders may already be deleted — said in the summary. |
| Reset of the lab currently open | After the summary, the open lab shows *Not started*, *No run yet*, an empty terminal and history. |
| Working alone / reporting off | Nothing is sent to any server. |
| Admin server down | Snapshot queued in the outbox like any event; the reset itself is not delayed. |

### UI / UX

| Page / View | Route | Purpose |
|---|---|---|
| Lab list (left rail) | `http://127.0.0.1:5057` (single page) | Discreet "Reset exercises…" action under the list: opens the reset dialog with nothing selected. |
| Lab page → **History** tab | same | "Reset this exercise…" action: opens the reset dialog on *One exercise* with the open lab selected. |
| Reset dialog (modal) | same | Steps: choose → preview + typed names → code (several only) → final confirmation → summary. |

The dialog starts with a two-way choice, **One exercise** (a list to pick one lab, then its name with a copy button under the list) or **Several or all** (a checklist with *Select all* / *Clear*), each lab showing its number, title, progress, a `_StartCompleted` badge and its name with a copy button; only the labs with something to reset are listed (Rule 14); running labs are disabled. Step 2 shows what will be deleted, then the name field (one exercise) or the code (several / all); step 3 is the explicit confirmation — three steps in both cases (v1.1). The steps that follow keep a visible "Back" and "Cancel", the destructive button is red and states the action ("Reset Lab01-FirstBasicAIAgent", "Reset 3 exercises") and is disabled while the request is in flight. The warning "This cannot be undone" and the list of what is kept (code in `Start/`, `Solution/`, instructions, settings) are always visible on the confirmation screens. The code is shown large, in monospace. All texts in English and French; keyboard-only usable, labelled for screen readers, errors announced; responsive down to phone width without horizontal scroll; `prefers-reduced-motion` respected; dark theme only; no framework, no CDN.

### User Stories

#### Story 1: Learner undoes an accidental run
**As a** learner who ran an exercise by mistake, **I want to** reset that one exercise, **So that** it shows *Not started* again, for me and for the trainer.

#### Story 2: Learner removes a leftover `_StartCompleted` folder safely
**As a** learner, **I want** the dashboard to delete the `_StartCompleted/` folder of a lab after showing me what it contains, **So that** I never delete `Start/` by mistake.

#### Story 3: Learner starts the labs over
**As a** learner who wants to redo the course, **I want to** reset several or all exercises at once with a strong confirmation, **So that** I start from a clean state without editing files.

#### Story 4: Learner keeps their code
**As a** learner, **I want** a reset never to touch my code, the solution or my settings, **So that** resetting the progress is safe.

---

## Tasks

### T1 — [backend] Preview of what a reset would delete

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: For any selection of labs, the dashboard can tell exactly what a reset would remove — runs per target, the `_StartCompleted/` folder with its files, size and source files — and flags the labs that cannot be reset (running, link, folder containing a project), without deleting anything.

**Business constraints**:
- Business Rules 1, 3, 4, 9.
- Unknown lab ids are rejected; an empty selection is rejected.
- Reading the preview has no side effect on the history, the folders or the reporting.

**Success criteria**:
- [x] The preview of Lab01 lists its Start and Solution run counts as shown in its History tab and its `_StartCompleted/` folder with 0 source files.
- [x] A `_StartCompleted/` holding a `.cs` file is reported with 1 source file.
- [x] A `_StartCompleted` symbolic link, or a lab running at that moment, is reported as blocked with the reason.
- [x] After any number of previews, history, folders and outbox are unchanged.

**References**: Data Model (preview), Business Rules 1, 3, 4, 9, Edge Cases (lab at zero, `obj/` only, source files, link, project inside, running), Stories 2, 4

---

### T2 — [security] Confirmation protocol enforced by the local API

**Jira**: none
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T1

**User outcome**: A reset can only happen through a confirmation issued for exactly the labs previewed, with every exercise name typed and, for several labs, the code retyped. Double clicks, replayed or late requests never reset anything twice, and a reset never overlaps a run or another reset.

**Business constraints**:
- Business Rules 5, 7 (code format), 8, 9, 10.
- The confirmation id and the code are unpredictable (cryptographic randomness).
- Errors use the existing `{ errors: { field: code } }` / `{ error: code }` shapes and status codes of the Data Model.

**Success criteria**:
- [x] Without a preview, with a wrong or missing name, or with a wrong code, the reset is refused and nothing changes on disk.
- [x] The same confirmation sent twice resets once; the second answer says the confirmation is no longer valid.
- [x] A confirmation older than 10 minutes, or after three wrong codes, is refused.
- [x] A request without `X-Lab-Dashboard: 1` or with a foreign `Origin` is rejected as for every other state-changing route.
- [x] A reset of a running lab, or during another reset, is refused.

**References**: Data Model (reset request, errors), Business Rules 5, 7, 8, 9, 10, Edge Cases (wrong name, wrong code, double click, expired, two tabs, running), Story 3

---

### T3 — [backend] Reset execution, verification and progress sent to the trainer

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: T2

**User outcome**: Once confirmed, each selected lab loses its runs and its `_StartCompleted/` folder, everything else stays, and the dashboard answers with a per-lab result it has verified. The lab then shows *Not started* everywhere, including on the trainer's dashboard when the learner joined a workshop, and runs again normally.

**Business constraints**:
- Business Rules 1, 2, 3, 11, 12, 13.
- The history is still written atomically (no partial or truncated file after a crash).
- A run started right after the reset is recorded normally.

**Success criteria**:
- [x] After resetting Lab01, its History tab is empty, its progress is *Not started*, its `_StartCompleted/` folder is gone, and `Start/`, `Solution/` and the other labs' history are byte-for-byte unchanged.
- [x] A folder that cannot be deleted leaves that lab's runs in place and reports the path; the other selected labs are reset.
- [x] When joined to a workshop, a progress snapshot with the lab *notStarted* is queued after the reset; when working alone, nothing is queued.
- [x] Running the reset lab's Start project right after the reset builds, runs and records a new run.

**References**: Data Model, Business Rules 1–3, 11–13, Edge Cases (permission denied, history not written, admin server down, working alone), Stories 1, 2, 4

---

### T4 — [frontend] Choosing one exercise, several or all

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T1

**User outcome**: The learner opens the reset dialog, chooses *One exercise* or *Several or all*, and picks the labs, seeing for each its progress and whether it has a `_StartCompleted/` folder. Running labs cannot be picked. Continuing shows the preview of what will be deleted.

**Business constraints**:
- UI / UX section; Business Rules 4, 9.
- Opened from the History tab, the dialog starts on *One exercise* with the open lab selected.

**Success criteria**:
- [x] From the rail action, the dialog opens with nothing selected; *Select all* checks every lab that is not running.
- [x] From Lab03's History tab, the dialog opens on *One exercise* with Lab03 selected.
- [x] *Continue* is disabled until a valid selection is made; the preview then lists runs and folder contents per lab, with the source-file warning when relevant.

**References**: UI / UX, Business Rules 4, 9, Edge Cases (lab at zero, running), Stories 1, 3

---

### T5 — [frontend] Confirmation steps proportional to the risk

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T2, T4

**User outcome**: For one exercise, the learner types its name, then confirms an explicit second screen. For several, they type each name, retype the 5-character code shown on screen, then confirm a recap of the consequences. The final button names the action and cannot be pressed twice.

**Business constraints**:
- Business Rules 5, 6, 7, 8; UI / UX section (warning and "kept" list always visible, red destructive button).
- Every server refusal (names, code, expired, running, busy) is shown with a clear next step, never as a silent failure; an expired confirmation offers to start again.

**Success criteria**:
- [x] One exercise: *Continue* stays disabled until the exact folder name is typed; the second screen states the runs and files that will be deleted and what is kept.
- [x] Several: each name must match, then the code; a wrong code shows an error; the recap lists every exercise.
- [x] Double-clicking the final button sends one request.
- [x] Waiting more than 10 minutes then confirming shows "confirmation expired" with a way to start again.

**References**: UI / UX, Business Rules 5–8, Edge Cases (wrong name, wrong code, double click, expired), Stories 1, 3

---

### T6 — [frontend] Summary of the reset and refreshed views

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T3, T5

**User outcome**: After the reset, the learner sees per exercise what was removed or why it failed. Closing the summary leaves the lab list, the metrics and the open lab showing the new state, without reloading the page.

**Business constraints**:
- Business Rule 13; Edge Cases (permission denied, history not written, reset of the open lab).
- Errors are visibly distinct from successes (not by colour alone).

**Success criteria**:
- [x] A reset of two labs where one folder is locked shows one *reset* line (runs and files removed) and one *failed* line with the path.
- [x] Resetting the open lab shows *Not started*, *No run yet*, an empty terminal and an empty history right after closing the summary.

**References**: Business Rule 13, Edge Cases, Stories 1, 3

---

### T7 — [glue] Entry points and English / French texts

**Jira**: none
**Agent hint**: code-improver
**Depends on**: T4, T5, T6

**User outcome**: The reset is reachable from the rail and from the History tab, discreet next to the main actions, and every text of the feature reads correctly in English and French.

**Business constraints**:
- UI / UX section (entry points, accessibility, responsive, reduced motion, dark theme, no CDN).

**Success criteria**:
- [x] Switching to FR translates every text of the dialog, including errors and the summary.
- [x] At phone width the dialog and its inputs fit without horizontal scroll; the whole flow works with the keyboard only.

**References**: UI / UX, Stories 1–3

---

### T8 — [test] Automated tests and documentation

**Jira**: none
**Agent hint**: unit-testing:test-automator
**Depends on**: T1, T2, T3

**User outcome**: A maintainer running `dotnet test` in `Dashboard/` gets coverage of the preview, the confirmation protocol and the reset; a learner reading `Dashboard/README.md` learns what a reset deletes, what it keeps and how it is confirmed.

**Business constraints**:
- Tests never touch the real labs, the real history or the real `_StartCompleted/` folders (temporary folders only, as the existing tests).
- `Dashboard/README.md`: features table, API table, a "Reset exercises" section.

**Success criteria**:
- [x] `dotnet test` passes and covers: preview counts and source files, link and contained-project blocking, names and code checks, single use, expiry, three wrong codes, running lab, busy, folder deleted before runs, permission failure keeping the runs, other labs and `Start/` untouched, snapshot queued only when joined.
- [x] The README describes the feature and its guarantees.

**References**: all Business Rules, Edge Cases, Stories 1–4

---

### T9 — [frontend] Copy the name of an exercise

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T4, T5

**User outcome**: Wherever the reset dialog lists an exercise (selection list, preview, recap), its name is shown with a copy button. One click puts the name in the clipboard, with a visible and announced confirmation, so the learner can identify the exercise and paste the name into the confirmation field.

**Business constraints**:
- Business Rule 5; UI / UX section.
- Copying never checks or unchecks an exercise in the list; a refused clipboard is reported, not silent.

**Success criteria**:
- [x] Clicking the copy button of Lab04 in the list puts `Lab04-AIAgentWithMCPClient` in the clipboard and leaves its checkbox unchanged.
- [x] Pasting the copied name in the confirmation field of one exercise enables *Continue*.
- [x] The copy is announced to screen readers; a failure shows a distinct state and message.

**References**: Business Rule 5, Edge Cases ("Clipboard not available"), Story 1

---

### T10 — [security] Several or all: the code only

**Jira**: none
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T2, T5

**User outcome**: Resetting several exercises or all of them asks only for the 5-character code shown on screen, then the recap confirmation; no exercise name has to be typed. Resetting one exercise still asks for its exact name, then the explicit confirmation.

**Business constraints**:
- Business Rules 6, 7, 8 (the double confirmation stays: typed proof, then explicit confirmation).
- Enforced by the local API: one lab needs its name, several labs need the code; three wrong codes still cancel the confirmation.

**Success criteria**:
- [x] Several labs with the right code and no name are reset; with a wrong code they are not, whatever names are sent.
- [x] One lab without its name is refused, even with a code.
- [x] The dialog has three steps in both cases (choose, check and type, confirm).

**References**: Business Rules 6–8, Edge Cases ("Names sent for several exercises", "Code wrong"), Story 3

---

### T11 — [frontend] List only the exercises with something to reset

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T4

**User outcome**: The reset dialog only lists the exercises in progress or completed, or holding a `_StartCompleted/` folder; a never-started exercise does not appear and its History tab has no reset button. With nothing to reset, the dialog says so.

**Business constraints**:
- Business Rule 14; the progress is the one already computed by the dashboard (same rule as the lab list and the trainer snapshot).

**Success criteria**:
- [x] With Lab01/02/04 in progress, Lab05 completed, Lab07 holding only `_StartCompleted/` and Lab03 with only a Solution run, the lists show exactly 01, 02, 04, 05, 07.
- [x] After resetting Lab04, it disappears from the lists and its History tab no longer shows the reset button.
- [x] With no lab to reset, the dialog shows the empty message and *Continue* is disabled.

**References**: Business Rule 14, Edge Cases ("Lab already at zero", "Lab with only Reference solution runs", "Lab never run but holding _StartCompleted/", "No lab to reset"), Stories 1, 3

---

## Test Plan

> Status: ready. To try it without touching your real history, start a second dashboard on a copy of the data:
> `cp Dashboard/.data/history.json /tmp/lab-data/ && touch /tmp/lab-data/standalone`, then in `Dashboard/LabDashboard`:
> `Dashboard__DataDirectory=/tmp/lab-data Dashboard__Port=5097 dotnet run` and open <http://127.0.0.1:5097>.
> The `_StartCompleted/` folders are real folders of the repository: create a test one (`mkdir -p <lab>/_StartCompleted/obj && touch <lab>/_StartCompleted/obj/x`) in a lab you do not mind resetting.

1. **One exercise from the History tab (Stories 1, 2)** — open a lab in progress (e.g. Lab03 after one run of *My exercise*) → *History* → **Reset this exercise…**. The dialog shows *Step 1 of 3* with Lab03 selected and its name with a copy button under the list. *Continue*: the preview lists the runs to delete and `_StartCompleted` (files, size, "build outputs only" or the source-file warning). A lab never started has no **Reset this exercise…** button.
2. Type `lab03-aiagentwithfunctiontools` (wrong case): an error appears, *Continue* stays disabled. Click the copy button next to the name and paste it in the field: a check mark appears, *Continue* is enabled.
3. The confirmation screen states the consequences, the exercise and "valid until HH:MM"; the focus is on *Cancel*, not on the red **Reset Lab03-AIAgentWithFunctionTools** button. Double-click it: one request; the summary says *Reset and verified: N run(s) and N file(s) deleted*.
4. Behind the summary, Lab03 already shows *Not started*, *No run yet*, an empty history; `_StartCompleted/` is gone; `git status -- LearningLabs` shows no change (Story 4).
5. **Only started exercises (Rule 14)** — **Reset exercises…** under the lab list → *Several or all*: only the labs in progress, completed or with a `_StartCompleted` badge are listed, each with its name and a copy button; clicking a copy button does not check the line. A lab with only *Reference solution* runs is not listed.
6. **Several (Story 3)** — check three labs, one of them with a `_StartCompleted` symbolic link (`ln -s /tmp/x <lab>/_StartCompleted`). *Continue*: *Step 2 of 3*, that lab is blocked ("symbolic link… never followed nor deleted"), no code, *Continue* disabled. *Back*, uncheck it, *Continue*.
7. No name to type: the code is shown; type a wrong code: "The code does not match"; type the right one in lower case → recap with the 2 exercises → **Reset 2 exercises** → summary with both *Reset and verified*; both disappear from the lists. The link and its target are untouched.
8. **Select all / Clear** check and uncheck every listed lab that is not running; a lab running in another tab is disabled in both lists.
9. **FR**: switch to FR, open the dialog: every text is translated, including the blocked reasons, the errors and the summary.
10. **Phone width** (390 px): the dialog, the checklist, the code and the buttons fit without horizontal scroll; the whole flow works with the keyboard only (Tab, Space on checkboxes, Enter to continue, Escape to close).
11. **API guard** — `curl -X POST http://127.0.0.1:5097/api/labs/reset/preview -H 'Content-Type: application/json' -d '{"labIds":["azureopenai-lab03"]}'` → 403 (no `X-Lab-Dashboard`); with the header and a wrong name on `/api/labs/reset` → 400 `names`; with an unknown `confirmationId` → 410 `expired`. Nothing is deleted.
12. **Workshop (if joined)**: after a reset, the trainer's dashboard shows the lab *Not started* after the next flush.

## Dependencies

- **Blocked by**: none (builds on the run history, the catalog and the reporting already in place).
- **Blocks**: a future "restore the exercise code" feature would reuse the dialog and the confirmation protocol.
- **External**: none (the trainer's admin dashboard already accepts `progress.snapshot`).

## Out of Scope / Future Considerations

- Restore `Start/` to the delivered exercise (git), with its own confirmation.
- Optional cleanup of `Start/bin` / `Start/obj` and of `.tmp-*` leftovers.
- Export of the history before a reset.

## Open Questions

- [x] Which "name" is typed? — Decided: the lab folder name (`Lab01-FirstBasicAIAgent`), unique and language-independent; the localized title is shown next to it.
- [x] Are the Solution runs removed too? — Decided: yes, the whole history of the lab, so that it looks never run (progress, metrics, History tab and trainer snapshot).
- [x] A `_StartCompleted/` holding source files? — Decided: deleted after the confirmations, with an explicit source-file warning in the preview (it is a git-ignored copy, not the exercise).
- [ ] Should the trainer's admin dashboard show that a reset happened (dedicated event)? — Not in v1: only the authoritative snapshot is sent, which needs no server change.
