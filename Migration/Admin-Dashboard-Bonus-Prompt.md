# Prompt — admin dashboard (labs-admin-dash): bonus scenarios and "Completed+"

To run in a Claude Code session opened in the `labs-admin-dash` repository.

---

Feature: **bonus scenarios and the "Completed+" state** (French: « Terminé+ »). Follow the conventions of this repository: write the spec under `specs/<feature>/spec.md` first, like the other features (`specs/lab-tracks`, `specs/admin-ui-v5`...), then implement it, with tests (vitest, `mongodb-memory-server`). Read `AGENTS.md`: this Next.js version differs from what you know.

## Context

The Lab Bench (the developer dashboard of the MicrosoftAgentFramework labs repository) now has **bonus scenarios**: optional scenarios of a lab (today the Responses API scenarios of Lab04 (4, 5), Lab05 (4, 5) and Lab07 (5, 6)).

- A lab is **completed** once every **non-bonus** scenario passed. This is unchanged for the admin: `progress.snapshot` still sends `state: "completed"` and `completedAt`, with the same meaning as today.
- A lab is **completed with its bonus** ("Completed+", FR "Terminé+") once **every** scenario, bonus included, passed. This is new.
- A failed bonus scenario never undoes a completed lab.

The Lab Bench already sends the new fields. Today they are silently dropped by the zod schemas (they are not strict), so nothing breaks while this feature is not deployed. Every field is additive and optional: an old Lab Bench sends none of them, and must keep working.

## Contract sent by the Lab Bench (already in production on its side)

1. **`catalog.synced`** (per developer), each lab: optional `bonusScenarios: number[]`, distinct integers ≥ 1. Absent: the lab has no bonus.
2. **`PUT /api/catalog`** (reference catalog published from labs.json by `.github/scripts/publish-lab-catalog.sh`), each lab: the same optional `bonusScenarios: number[]` (at most 50 entries).
3. **`progress.snapshot`**, each lab: optional `bonusCompletedAt: ISO date`, sent when the bonus scenarios passed too.
   - `state` stays `"completed"` (never a new state value).
   - `bonusCompletedAt` ≥ `completedAt`, and it is only sent with `state: "completed"`.
   - Absent or null: the bonus is not completed.
4. **`run.finished`**, each item of `scenarios`: optional `bonus: true` on a bonus scenario (absent otherwise).
   - A run of every scenario where only bonus scenarios failed has `status: "failed"`, but the exercise is still passed.
   - The completion always comes from the `progress.snapshot` that follows (authoritative, as today).

## What to do

1. **Schemas** (`src/lib/api/schemas.ts`): accept the four fields above, with validation and clear error messages, in the style of the existing ones (`catalogLabSchema`, `catalogSyncedPayload`, `progressSnapshotPayload`, `runFinishedPayload.scenarios`).
2. **Storage** (MongoDB, `src/lib/db/types.ts`):
   - `LabProgress.bonusCompletedAt?: Date`.
   - The bonus scenarios on the lab documents of both catalogs (the per-developer one and the published reference one).
   - `bonus?: boolean` on stored run scenarios.
   - No migration of the existing documents: a missing field means "no bonus" / "not completed".
3. **Projection** (`src/lib/domain/projection.ts`):
   - `applyProgressSnapshot`: merge `bonusCompletedAt` with **exactly** the semantics of `completedAt` today (kept once set, earliest date wins).
   - `applyRunFinished`: the current rule "a passed Start run of every scenario completes the lab" stays. Also set `bonusCompletedAt` when that run passed **every** scenario, bonus included, none skipped, and the lab has bonus scenarios. Never set it from a run whose scenarios are absent.
   - `catalog.synced` / `PUT /api/catalog`: store `bonusScenarios`.
4. **Display state** (`src/lib/domain/states.ts`):
   - `labDisplayState` returns a new `"completedPlus"` when `state === "completed"` and `bonusCompletedAt` is set.
   - `completedCount` and every count of completed labs **keep counting `completedPlus` as completed**. Add a separate count of labs completed with their bonus where useful (developer header, overview KPIs, TV KPIs).
   - Check every `=== "completed"` of `src/` (`StatePill`, `CellGlyph`, matrix `model.ts`, overview `model.ts`, `DevSections`, `DevCompletionChart`, `TvProgressBoard`, `tvBoard.ts`, `tv.ts`, `trackProgress.ts`, `analytics.ts`, `queries.ts`, `completions.ts`). A Completed+ lab must stay counted as completed everywhere; only its look changes.
5. **UI**:
   - `StatePill`: "Completed+" / "Terminé+", same colour family as Completed, more marked (border or "+" mark).
   - Matrix glyph `CellGlyph`: the completed glyph with a "+".
   - Developer page: Completed+ on the lab rows, and in the run detail a "Bonus" tag on the bonus scenarios of a run (from `run.finished.scenarios[].bonus`).
   - Catalog view: "Bonus: scenarios 4, 5" on the labs that have some.
   - TV board: the Completed+ glyph.
   - Labels in `src/lib/i18n/messages.ts`, in English and French. Keep accessibility: text alternatives, not colour only.
6. **Celebration** (`src/lib/domain/completions.ts`, `src/lib/celebration/detector.ts`, admin-ui-v5 feature 9):
   - Keep the lab completion celebration on `completedAt`.
   - Add a lighter "bonus completed" celebration on `bonusCompletedAt`: same privacy allow-list, same anonymity rule, same "Célébrer les labs terminés" toggle.
   - It never replaces nor duplicates the completion card when both dates arrive together. In that case, show one card: "Lab terminé+".
7. **Analytics**: completion rates unchanged (Completed+ is completed). Optionally a "bonus completed" rate per lab, among the labs that have a bonus.
8. **Tests**:
   - Schemas: the fields are accepted, invalid values are refused, and an old payload without the fields still passes.
   - Projection: sticky `bonusCompletedAt`; earliest date wins; `run.finished` with every scenario passed sets it; with a skipped or failed bonus it does not; a snapshot without the field does not erase it.
   - Display state and counts: Completed+ is counted as completed.
   - Celebration: one card when both dates arrive together.
   - UI labels in EN and FR.
9. **Docs**: the spec, plus the README sections that describe the event contract and the lab states.

Do not change the meaning of `state`, `completedAt` or the completion count. A dashboard or a TV that does not know the bonus must show the same numbers as before.
