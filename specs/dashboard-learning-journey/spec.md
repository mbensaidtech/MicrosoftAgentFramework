---
title: Dashboard Learning Journey
status: implemented
priority: medium
author: PM Agent
created: 2026-10-25
updated: 2026-10-25
jira_project: none
jira_epic: none
---

# Dashboard Learning Journey

## Summary

The Lab Bench dashboard gets a **Journey** column, to the left of the exercise list, that turns the learner's progress into a motivating, game-like view while keeping the dashboard's sober, technical look. It shows the overall advancement as a segmented "core" ring (one segment per exercise), a rank and experience points (XP) earned by completing exercises, a trajectory map where every exercise is a waypoint grouped in milestones (one per level), the next mission to work on, and a shelf of distinctions earned along the way. Everything is computed from the run history the dashboard already keeps, so it reflects the real state of each exercise (not started, in progress, completed, to resume, running) and updates by itself when a run starts, ends, or an exercise is reset.

## Problem Statement

- Today progress is only visible lab by lab: a small *Not started / In progress / Completed* pill in each item of the rail, and the *Exercise* metric of the open lab. Nothing shows how far the learner is in the whole course, what is left, or what to do next.
- The learner gets no sense of momentum: completing an exercise only produces a toast, and a regression (an exercise that passed but whose last attempt fails) is invisible because the pill keeps saying *Completed*.
- In a workshop the learner works through 13 exercises over hours; without a visible goal and milestones, motivation drops after the first labs, and learners stop before the intermediate ones.

## Goals

- [ ] At a glance, the learner sees how many exercises are completed out of the catalog and the state of every exercise, without opening any lab.
- [ ] Every exercise is shown in exactly one of the states of Business Rule 1, consistent with the rail pill, the History tab and the trainer's admin dashboard.
- [ ] Completing an exercise, reaching a new rank, clearing a milestone or earning a distinction is celebrated at the moment it happens, then stays visible.
- [ ] The view updates by itself after a run starts, after a run ends and after a reset, with no page reload.
- [ ] The existing features (run, input, history, reset, settings, help, language switch) behave exactly as before.

## Non-Goals

- No leaderboard, no comparison with other learners, nothing sent to the trainer's admin dashboard (the reporting payloads are unchanged).
- No new persisted data: XP, ranks and distinctions are recomputed from the local run history; there is no separate "achievements" file.
- No sound, no confetti storm, no mascot: celebrations stay short and quiet.
- No change to the completion rule of an exercise (a passing Start run), nor to the rail pills.
- No configuration screen for XP values, ranks or distinctions.

## Changelog

- **v1.0** (2026-10-25) — Initial release. Tasks T1..T9, no Jira (tracked in this file only).

---

## Reference

### Data Model

No storage change. The journey is derived from `Dashboard/.data/history.json` (30 runs per lab, newest first), the catalog (`labs.json`: order, `level`, number of checks) and the active run. A new read-only API resource describes it:

```jsonc
// GET /api/journey
{
  "xp": 525, "baseXp": 1800,                        // XP earned; sum of the base XP of the catalog (Business Rule 3)
  "completed": 3, "total": 13, "percent": 23,       // completed = completed + toResume
  "rank": { "index": 2, "id": "tooling", "minXp": 270, "next": { "id": "integration", "minXp": 630 } },
  "labs": [
    { "id": "azureopenai-lab01", "number": "01", "level": "Beginner",
      "state": "completed",            // notStarted | inProgress | completed | toResume
      "running": false,                // an active run (Start or Solution) on this lab
      "attempts": 1,                   // Start runs, cancelled ones excluded
      "bestChecks": 4, "totalChecks": 4,
      "xp": 175, "potentialXp": 175,   // potentialXp: what the next passing attempt would earn (bonuses still within reach)
      "firstTry": true, "solo": true, "persistence": false,
      "completedAt": "2026-10-25T09:12:00Z", "lastStartAt": "2026-10-25T09:12:00Z" }
  ],
  "milestones": [ { "level": "Beginner", "labIds": ["…"], "completed": 3, "total": 3, "cleared": true, "clearedAt": "…" } ],
  "badges": [ { "id": "firstSteps", "unlocked": true, "earnedAt": "…", "labId": "azureopenai-lab01" } ],
  "next": { "labId": "azureopenai-lab04", "reason": "inProgress" }   // null when every exercise is completed
}
```

### Business Rules

1. **State of an exercise** (Start runs only; Solution runs never change it):
   - `notStarted` — no Start run in the history.
   - `inProgress` — at least one Start run, none passed.
   - `completed` — a Start run passed, and the latest non-cancelled Start run passed.
   - `toResume` — a Start run passed, but the latest non-cancelled Start run failed or timed out. It still counts as completed (same rule as the rail pill and the admin dashboard), and is flagged so the learner fixes it.
   - `running` is not a state but an overlay: the lab has an active run, shown on top of its state.
2. **Attempt**: a Start run that was not cancelled. **Best checks**: the highest number of checks passed by one Start run, out of the lab's checks.
3. **XP** (never from Solution runs):
   - Base XP by level: Beginner 100, Intermediate 150, Advanced 200, any other level 100.
   - Completed (or toResume): base XP + 50 if passed on the first attempt (*first try*) + 25 if no Solution run happened before the first passing Start run (*solo*).
   - In progress: `round(base × 0.5 × bestChecks / totalChecks)` (0 when the lab has no checks or no check ever passed).
   - Total XP is the sum over the catalog; it only decreases after a reset.
4. **Ranks** — thresholds are shares of the catalog base XP (bonuses excluded), so they adapt when the catalog grows: 1 Foundations 0 %, 2 Tooling 15 %, 3 Integration 35 %, 4 Orchestration 60 %, 5 Mastery 85 % (thresholds rounded to the unit). The rank shown is the highest whose threshold is ≤ total XP.
5. **Milestones**: one per group of consecutive catalog labs sharing the same `level`, in catalog order. A milestone is *cleared* when all its exercises are completed (or toResume); `clearedAt` is the latest of their first passing dates.
6. **Distinctions** (unlocked once, recomputed from history, so a reset can lock them again):
   - `firstSteps` — at least one exercise completed.
   - `firstTry` — an exercise passed on its first attempt.
   - `solo` — an exercise completed without running its Solution before.
   - `persistence` — an exercise completed after at least 3 unsuccessful attempts.
   - `tierCleared` — at least one milestone cleared.
   - `summit` — every exercise of the catalog completed.
7. **Next mission**: the `inProgress` exercise with the most recent Start run; otherwise the first `toResume` exercise in catalog order; otherwise the first `notStarted` exercise in catalog order; otherwise none (journey finished).
8. **Celebrations** happen only for changes observed while the page is open (a lab becoming completed, a rank up, a milestone cleared, a distinction unlocked) — never on page load, language switch or after a reset (decreases are applied silently).

### Edge Cases

| Case | Expected behavior |
|---|---|
| Fresh install, empty history | Ring with every segment unlit, rank 1, 0 XP, next mission = first lab, all distinctions locked with their unlock hint |
| Only Solution runs on a lab | `notStarted`, 0 XP; the Solution runs don't count as attempts |
| Lab without checks | Completed by a passing Start run like any lab; partial XP stays 0 while in progress |
| Last Start run cancelled | Ignored: the state follows the latest non-cancelled Start run |
| More than 30 runs on a lab | Computed on the kept history; the oldest kept non-cancelled Start run is treated as the first attempt |
| A run is in progress | Its lab shows the running overlay (pulse) on top of its state, as soon as the run starts |
| Exercise reset (one or all) | The journey drops back silently (state, XP, rank, milestones, distinctions) — no celebration, no error |
| Journey API unreachable | The column keeps the last known view; on the very first load it shows a muted "progress unavailable" line; the rest of the dashboard keeps working |
| Every exercise completed | Ring fully lit, *Mastery* rank, `summit` distinction, next-mission card replaced by a "journey completed" message |
| Language switch (EN/FR) | Texts change immediately; numbers, states and positions unchanged; no animation replayed |
| `prefers-reduced-motion` | No animation (pulse, glow, count-up): the final state is shown directly |
| Narrow screens | ≤ 1280 px: the column becomes a horizontal band above the lab list and the lab view; phone width: compact stacked band, no horizontal page scroll |

### UI / UX

| Page / View | Route | Purpose |
|---|---|---|
| Lab Bench (single page) | `http://127.0.0.1:5057/` | New Journey column left of the *Labs* rail |
| Journey API | `GET /api/journey` | Read-only journey model (Data Model) |

The column follows the existing visual system (dark only, neutral cool greys, one blue accent, semantic green/amber/red with tinted backgrounds, 1px borders, mono numerals). It is sticky like the rail. From top to bottom:

- **Core**: a ring split into one arc per exercise (catalog order, a wider gap between milestones), lit by state, around the rank number and the completion percentage; the rank name and an XP track toward the next rank ("180 XP to Orchestration") below it.
- **Trajectory**: the exercises as numbered waypoints on a path, one row per milestone with a gate label (the level, localized) that lights up when the milestone is cleared. Each state has its own glyph, not only a colour (hollow = not started, partial arc = in progress with best checks, filled with check = completed, alert ring = to resume, halo pulse = running); the lab open in the main view is outlined. Selecting a waypoint opens that lab.
- **Next mission**: the lab title, why it is proposed (in progress with *n/m checks*, to resume, or next to start), and a *Continue* button that opens it.
- **Distinctions**: six insignia, locked ones dimmed with their unlock hint, unlocked ones lit with the date earned.

Celebration: the waypoint ignites, the XP counts up, and a short achievement card ("+175 XP · Lab 03 completed", "New rank: Integration", "Distinction unlocked: Solo") appears in the column for a few seconds, also announced to screen readers. All texts in English and French.

### User Stories

#### Story 1: Learner sees where they stand
**As a** workshop learner, **I want to** see my overall progress and the state of every exercise next to the lab list, **So that** I know at a glance what is done, what is left and what needs fixing.

#### Story 2: Learner knows what to do next
**As a** learner coming back to the dashboard, **I want to** be pointed to my next mission and open it in one click, **So that** I resume without searching.

#### Story 3: Learner feels progress
**As a** learner, **I want to** earn XP, ranks, milestones and distinctions as I complete exercises, **So that** I stay motivated to finish the course.

#### Story 4: Learner trusts the view
**As a** learner, **I want** the journey to change as soon as a run starts, ends or an exercise is reset, **So that** it always matches the real state of my exercises.

---

## Tasks

### T1 — [backend] State and statistics of each exercise

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: The local API describes, for every exercise of the catalog, its journey state (not started, in progress, completed, to resume), whether it is running, its attempts, its best checks out of its total checks and when it was first completed. A learner whose last attempt on a completed exercise fails sees it reported as *to resume* instead of silently *completed*.

**Business constraints**:
- Business Rules 1 and 2; same completion rule as the rail pill and the admin progress snapshot (a passing Start run).
- Solution runs never change the state or the attempts.
- Read-only: no write to the history, no reporting event.

**Success criteria**:
- [ ] `GET /api/journey` lists every catalog lab in catalog order with the fields of the Data Model.
- [ ] A lab with only Solution runs is `notStarted` with 0 attempts.
- [ ] A lab whose Start run passed then failed is `toResume`; a cancelled run after a pass leaves it `completed`.
- [ ] A lab with an active run reports `running: true` while the run lasts.

**References**: Data Model, Business Rules 1–2, Edge Cases ("only Solution runs", "last Start run cancelled", "more than 30 runs"), Story 4

---

### T2 — [backend] XP, ranks, milestones, distinctions and next mission

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: T1

**User outcome**: The same API returns the learner's XP, rank and next rank, milestones per level, the six distinctions with their unlock date, and the next mission, so that the dashboard can show a coherent journey from one source.

**Business constraints**:
- Business Rules 3–7 exactly (values, thresholds, ordering).
- Thresholds derive from the catalog, never hard-coded per lab.

**Success criteria**:
- [ ] Empty history: 0 XP, rank 1 *Foundations*, every distinction locked, next mission = first catalog lab.
- [ ] A Beginner lab passed on its first attempt without a prior Solution run gives 175 XP and unlocks `firstSteps`, `firstTry` and `solo`.
- [ ] Completing all Beginner labs clears their milestone and unlocks `tierCleared`.
- [ ] Every lab completed: `next` is null, rank *Mastery*, `summit` unlocked.

**References**: Data Model, Business Rules 3–7, Edge Cases ("fresh install", "every exercise completed", "lab without checks"), Story 2, Story 3

---

### T3 — [frontend] Journey column and global progress core

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T2

**User outcome**: Left of the *Labs* rail, the learner sees a Journey column with the segmented core ring (one lit arc per completed exercise, a distinct look for in progress / to resume), the completion percentage, the rank and the XP track toward the next rank. On narrower screens the same information becomes a band above the lab list.

**Business constraints**:
- Visual system of the dashboard (UI / UX); professional, not childish; dark theme only.
- Layout of the rail and the lab view unchanged apart from the space the column takes; no horizontal page scroll down to phone width.

**Success criteria**:
- [ ] With 3 of 13 exercises completed, the ring shows 3 lit arcs out of 13 and 23 %.
- [ ] The rank name and "N XP to <next rank>" match the API.
- [ ] Between 1080 and 1280 px and at phone width the journey stays readable and the lab view keeps its current behavior.

**References**: UI / UX, Business Rules 3–4, Edge Cases ("narrow screens", "fresh install"), Story 1

---

### T4 — [frontend] Trajectory map with milestones

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T3

**User outcome**: Under the core, every exercise is a numbered waypoint on a path, grouped by milestone (level), with a glyph per state and the lab currently open outlined. Hovering or focusing a waypoint names the lab and its state; selecting it opens the lab in the main view.

**Business constraints**:
- States distinguishable without colour (glyph/shape), Business Rules 1 and 5.
- Opening a lab from a waypoint behaves exactly like selecting it in the rail.

**Success criteria**:
- [ ] Each waypoint's state matches the rail pill (with *to resume* and *running* as extra nuances).
- [ ] An in-progress waypoint shows its best checks (e.g. 4/6).
- [ ] A cleared milestone gate is lit; selecting a waypoint opens its lab and moves the outline.

**References**: UI / UX, Business Rules 1, 5, Story 1

---

### T5 — [frontend] Next mission and distinctions

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T3

**User outcome**: The column shows the next mission with the reason it is proposed and a *Continue* button that opens it, followed by the six distinctions: earned ones lit with their date, locked ones dimmed with how to unlock them. When everything is done, the mission card congratulates the learner instead.

**Business constraints**:
- Business Rules 6–7.
- A locked distinction always explains its condition; no hidden achievements.

**Success criteria**:
- [ ] *Continue* opens the lab named by the API's next mission.
- [ ] After all exercises are completed the mission card shows the "journey completed" message.
- [ ] Each distinction shows its name, its state and its condition (on hover/focus).

**References**: UI / UX, Business Rules 6–7, Edge Cases ("every exercise completed"), Story 2, Story 3

---

### T6 — [frontend] Live updates and celebrations

**Jira**: none
**Agent hint**: frontend-mobile-development:frontend-developer
**Depends on**: T4, T5

**User outcome**: The journey changes by itself: the waypoint pulses as soon as a run starts, and when a run completes an exercise the waypoint ignites, the XP counts up and a short achievement card announces the XP, a new rank, a cleared milestone or a new distinction. After a reset the journey drops back without any celebration.

**Business constraints**:
- Business Rule 8 (no celebration on load, language switch or reset).
- Refreshes must not steal focus or disturb a running lab (terminal, input box).
- Changes made from another browser tab show up within half a minute while the page is visible.

**Success criteria**:
- [ ] Starting a run makes its waypoint pulse without reloading the page; the pulse stops when the run ends.
- [ ] A first passing Start run triggers the celebration once, with the right XP gained.
- [ ] Resetting an exercise updates the ring, XP and waypoint silently.

**References**: Business Rule 8, Edge Cases ("a run is in progress", "exercise reset", "journey API unreachable"), Story 3, Story 4

---

### T7 — [a11y] Keyboard, screen reader and reduced motion

**Jira**: none
**Agent hint**: ui-design:accessibility-expert
**Depends on**: T6

**User outcome**: A keyboard or screen reader user can reach every waypoint, the *Continue* button and the distinctions, hears the overall progress and each lab's state, and is told about achievements; users who prefer reduced motion see the final state without animation.

**Business constraints**:
- Contrast of texts and state glyphs at least WCAG AA on the dark theme.
- Achievement announcements are polite (they never interrupt the run output).

**Success criteria**:
- [ ] Reading order: core summary (an image with its text alternative, not a tab stop), then the tab stops: waypoints in catalog order, Continue, distinctions; each has an accessible name with lab and state.
- [ ] A screen reader announces "3 of 13 exercises completed" (or the French equivalent) for the core.
- [ ] With `prefers-reduced-motion`, no pulse, glow or count-up runs.

**References**: UI / UX, Edge Cases ("prefers-reduced-motion"), Story 1

---

### T8 — [glue] English / French texts and documentation

**Jira**: none
**Agent hint**: code-improver
**Depends on**: T3, T4, T5, T6

**User outcome**: Every journey text (ranks, states, milestones, distinctions and their conditions, mission reasons, achievements) is available in English and French and follows the language switch; the dashboard README explains the journey, its XP rules and its new API.

**Business constraints**:
- Level names come from the lab translations of the catalog, not from new strings.
- French texts gender-neutral (ranks are skill domains, not titles of people).

**Success criteria**:
- [ ] Switching EN ↔ FR changes every journey text with no untranslated key visible.
- [ ] The README lists `GET /api/journey` and the XP / rank / distinction rules.

**References**: Business Rules 3–7, UI / UX

---

### T9 — [test] Automated tests of the journey computation and API

**Jira**: none
**Agent hint**: unit-testing:test-automator
**Depends on**: T1, T2

**User outcome**: The journey rules are protected by automated tests that run with the existing dashboard test suite, so a later change cannot silently break the states, XP or distinctions.

**Business constraints**:
- Cover every row of Business Rules 1–7 and the edge cases about history (only Solution runs, cancelled last run, toResume, empty history, all completed).
- Run with the existing test command of the dashboard, without network or Azure credentials.

**Success criteria**:
- [ ] The dashboard test suite passes with the new tests included.
- [ ] Changing an XP value or a rank threshold makes a test fail.

**References**: Business Rules 1–7, Edge Cases

---

## Test Plan

> Status: ready (2026-10-25). Tasks T1–T9 implemented in this session (no Jira). Automated: `cd Dashboard && dotnet test` (207 tests, 22 for the journey in `LabJourneyTests`).

To keep your real history untouched, run a second instance on its own data folder:

```bash
cd Dashboard/LabDashboard
Dashboard__Port=5064 Dashboard__DataDirectory=../.data/journey-qa Dashboard__Reporting__ServerUrl= dotnet run
```

Then open <http://127.0.0.1:5064> (choose **Work on my own** on first launch). Delete `Dashboard/.data/journey-qa` afterwards.

1. **Fresh journey (Story 1, edge case "fresh install")** — On a new data folder: the *Journey* column sits left of *Labs*; the ring has 13 grey arcs, `0 %`, `0 / 13`; *Rank I · Foundations*, `0 XP`, "270 XP to Tooling" (15 % of the 1,800 base XP of the 13 labs); the trajectory shows *Beginner* (01–03) and *Intermediate* (04–12) gates; *Next mission* = Lab 01 with **Continue** and "up to +175 XP"; the 6 distinctions are dimmed and pointing at one shows how to earn it.
2. **Open a lab from the journey (Story 2)** — Select waypoint `05`: Lab 05 opens on the right, waypoint 05 and the rail item are outlined, a blue dot moves on the ring. **Continue** opens the next mission; on the mission's own lab the button is replaced by "Open in the lab view".
3. **A run in progress (Story 4)** — Run *My exercise* on any lab: its waypoint gets a blue pulse and its arc breathes at once (the rail pill says *Running*); both stop when the run ends.
4. **A failed attempt** — When the run fails with some checks passed, the waypoint turns amber with a partial ring (best checks), the mission card says "In progress · best run n/m checks", and the XP rises by the partial XP with a short *Progress* card. A failure with 0 checks passed changes the state without any card.
5. **Completion (Story 3)** — A passing Start run: the waypoint and its arc ignite, the XP counts up, a card shows "Exercise completed · +175 XP · Lab 01 …" (first try, without the solution), followed by *Distinction unlocked* cards (*First steps*, *First try*, *No safety net*); a screen reader hears the same text. Reload the page: no card replays.
6. **To resume** — Run *My exercise* again on a completed lab so that it fails: the waypoint becomes red dashed with `!`, its arc red dashed, the rail still says *Completed*, the XP is unchanged, no card.
7. **Reset (edge case "exercise reset")** — **Reset exercises…** on a completed lab: the ring, XP, rank, waypoint and distinctions drop back silently (no card, no error).
8. **Another tab** — Keep a second tab open on the dashboard and start a run in the first one: within 20 s (or at once when switching back to the second tab) its journey and lab list show the run.
9. **Language** — Switch **FR**: *Parcours*, *Rang I · Fondations*, *Débutant / Intermédiaire* (from the catalog translations), *Prochaine mission*, *Distinctions* in French; no card is replayed, nothing moves.
10. **Narrow screens and motion** — At 1200 px the journey is a band above the lab list (core | trajectory | mission + distinctions); at phone width it stacks, with no horizontal scroll. With *Reduce motion* enabled in the OS, nothing pulses, glows or counts up.
11. **Server stopped** — Stop the instance with the page open: the journey keeps its last view; reloading shows "Progress unavailable for now" in the column only.

## Dependencies

- **Blocked by**: — (works with the current history format; compatible with `dashboard-exercise-reset`, whose reset must refresh the journey)
- **Blocks**: —
- **External**: none (no CDN, no package: the dashboard stays dependency-free in the browser)

## Out of Scope / Future Considerations

- Showing ranks and distinctions on the trainer's admin dashboard.
- Streaks across days, time-based goals, weekly recaps.
- A "journey" export or share card.

## Open Questions

- [ ] Should *to resume* also appear in the rail pill (today it says *Completed*)? Default: no, only in the journey, to keep the rail and the admin dashboard on the same rule.
