---
title: Labs - Agent Evaluation and Agent Skills
status: implemented
priority: high
author: PM Agent
created: 2026-10-25
updated: 2026-10-25
jira_project: none
jira_epic: none
---

# Labs - Agent Evaluation and Agent Skills

## Summary

Two new Azure OpenAI labs complete the learning path before the future workflow and orchestration labs. **Lab14 - Evaluate an Agent** teaches how to measure the quality of an agent the way a test suite measures code: a dataset of questions with expected answers and expected tool calls, deterministic local checks, LLM-as-judge quality scores, and a non-regression run that catches a degraded prompt before it ships. **Lab15 - Give an Agent Skills** teaches Agent Skills: portable packages of instructions and resources (`SKILL.md` files, or skills defined in code) that the agent discovers and loads on demand (progressive disclosure), including an in-process script and the approval of skill tools. Both labs follow the conventions of the 14 migrated Azure OpenAI labs (Start/Solution, README with TODO tables, Lab Bench dashboard checks in English and French) and use **only stable APIs of Microsoft Agent Framework 1.26.0** — verified by compilation on 2026-10-25.

## Problem Statement

- The labs teach how to build, ground, connect, observe and host an agent, but never how to **check that it still behaves correctly** after a change of prompt, tool or model. The only former attempt (judge agents in the old MultiAgentSystem Lab01) was ad hoc and was removed at step 4 of the action plan. The audit ranked evaluation P1 (gap G7).
- **Agent Skills** are step 4 of the official learning journey and the way the framework packages domain expertise; no lab mentions them (gap G12). The only "skill" in the labs is the `AgentSkill` of an A2A agent card (Lab06), an unrelated concept that learners may confuse with Agent Skills.
- The action plan v2 deferred both topics as "first candidates of a next phase" (§6). Without them, a learner reaches workflows and orchestration without the tools to test an agent or to give it reusable expertise.

## Goals

- [x] A learner can evaluate an agent on a small dataset and read, per question, which checks passed and why, with deterministic checks and LLM-as-judge scores.
- [x] A learner can see a regression detected automatically when the prompt of an agent is degraded, on the same dataset.
- [x] A learner can give an agent skills from `SKILL.md` files and from code, and see the progressive disclosure at work (advertise → load → read resource → run script).
- [x] A learner understands and controls the approval of skill tools (read-only tools auto-approved, script execution approved explicitly).
- [x] Both labs run end to end with an API key and with Microsoft Entra ID, pass all their dashboard checks on the Solution, and only the startup checks on the delivered Start.
- [x] Both labs appear in the essential learning path (root README and dashboard order), before the future workflow labs.

## Non-Goals

- **Agent Harness**: deferred until its compaction, file memory and loop features leave `[Experimental]` (MAAI001 in 1.26.0).
- **Microsoft Foundry evaluators** (`FoundryEvals`, package `Microsoft.Agents.AI.Foundry` in preview) and **safety evaluators that need an Azure AI Foundry project**: the labs use an Azure OpenAI deployment only.
- **Workflow evaluation** (`Run.EvaluateAsync`): comes with the workflow labs.
- **MCP-based skills** (`UseMcpSkills`, experimental) and **file-based scripts run as subprocesses** (`SubprocessScriptRunner` is "for demonstration purposes only" and needs Python on the learner's machine): scripts are shown with code-defined skills only, which run in-process.
- **Class-based skills packaged as NuGet** (`AgentClassSkill<T>`): mentioned in "Going further", not a scenario.
- No change to the dashboard code (only its catalog `labs.json` and its tests).

## Changelog

- **v1.0** (2026-10-25) — Initial release. Tasks T1..T11, no Jira (tracked in this file only), implemented directly. Open questions resolved: placement of Business Rule 8 and judge of Business Rule 6 confirmed.

---

## Reference

### Data Model

No database and no schema change. The deliverables are two new lab folders under `LearningLabs/AzureOpenAI/` (Start and Solution projects, README), two new entries in the dashboard catalog `Dashboard/LabDashboard/labs.json` (with English and French texts), new output fixtures and tests in the dashboard test project, and documentation updates (root README, migration plan, a step report under `Migration/`).

Packages (all stable, versions verified on NuGet on 2026-10-25):

```xml
<!-- both labs, like the other Azure OpenAI labs -->
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.26.0" />
<PackageReference Include="Azure.Identity" Version="1.21.0" />
<!-- Lab14 only: LLM-as-judge evaluators (RelevanceEvaluator, CoherenceEvaluator...) -->
<PackageReference Include="Microsoft.Extensions.AI.Evaluation.Quality" Version="10.11.0" />
```

### Business Rules

1. **Stable APIs only.** No type or member flagged `[Experimental]` (no `#pragma` other than the `OPENAI001` of the Entra ID client shared by every lab). Stable as of 1.26.0: `AIAgent.EvaluateAsync`, `LocalEvaluator`, `EvalChecks`, `FunctionEvaluator`, `ExpectedToolCall`, `AgentEvaluationResults` (core `Microsoft.Agents.AI`); MEAI quality evaluators (`Microsoft.Extensions.AI.Evaluation.Quality`); `AgentSkillsProvider`, `AgentInlineSkill` (`AddResource`, `AddScript`), file-based skills, `UseToolApproval`, `AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule` / `AllToolsAutoApprovalRule`.
2. **Lab conventions** (`Migration/Migration-Manual.md`): Start and Solution differ only in `Program.cs`; Start and Solution build with **0 warnings**; the delivered Start runs to the end (exit 0) and prints every scenario header; README with TODO tables matching the Start, "Coming from..." not needed (new labs), Expected Output from a real run; no home-made abstraction that hides the taught API; display helpers in a provided console file.
3. **Configuration and secrets**: same configuration as the other labs (appsettings.json → shared user secrets → environment variables); API key or Entra ID; no credential, endpoint or deployment name ever printed beyond the `Endpoint:` / `Deployment:` lines every lab prints.
4. **Checks are behavioral**: every dashboard check verifies a behavior the code guarantees or a fact the model must use (a tool result, a skill resource), never the wording of an answer. LLM-judge scores are displayed but checked only for presence and range, not for a value.
5. **Deterministic demonstrations**: when a lesson depends on a pass/fail outcome (a regression detected, a script approved), the dataset, the degraded prompt and the approval decision are designed so that the expected outcome holds on repeated runs (at least 3 runs with an API key and 1 with Entra ID).
6. **The judge is explicit**: the LLM-as-judge evaluators use a chat client the lab names (by default the same deployment as the agent); the README says that judging with the same model is a simplification and that scores vary between runs.
7. **Skills are untrusted content until reviewed**: the skills lab states the official guidance (review before use, source trust, sandboxing, audit) and keeps script execution behind an explicit approval.
8. **Learning path placement**: Lab15 (Skills) comes after Lab12 in the "Knowledge and memory" part (a skills provider is a context provider); Lab14 (Evaluation) comes after Lab11 in the "In production" part (observe → evaluate → host). Lab numbers are not reused or renumbered.
9. **Real runs before done**: each lab's Solution is run with an API key (≥ 3 runs) and with Entra ID (≥ 1 run) against its dashboard checks; results go into the step report.

### Edge Cases

| Case | Expected behavior |
|---|---|
| The agent does not call the expected tool for a question | The tool-call check of that question fails and the lab shows which check failed; the other questions are unaffected |
| An LLM-judge evaluator returns no score or an error | The result shows the evaluator as failed / without score instead of crashing the run |
| The judge scores vary between runs | Scores are shown with their reason; checks only verify a score is present and in range |
| The degraded prompt passes every check on a run | Must not happen by design (Business Rule 5); the regression relies on deterministic checks (a missing fact or a missing tool call), not on judge scores |
| A question is asked that no skill covers | The agent answers without loading a skill; no skill tool call is shown |
| A skill tool call needs approval and nobody approves | The run ends with a visible pending approval request (tool name, arguments) — never a silent execution |
| A script call is rejected | The agent receives the rejection and answers without the script result; the lab shows the rejection |
| A `SKILL.md` is invalid (bad frontmatter) | Covered in Troubleshooting: the skill is not advertised |
| The Start project before any TODO | Builds with 0 warnings, runs to the end, prints every scenario header, and only the startup checks pass |

### UI / UX

The labs are console programs, also run from the Lab Bench dashboard.

| View | Where | Purpose |
|---|---|---|
| Console output of Lab14 | `dotnet run` / dashboard run | Per scenario: the dataset, a per-question table of checks (pass/fail, reason), judge scores, the summary passed/total, and the regression verdict |
| Console output of Lab15 | `dotnet run` / dashboard run | Per scenario: the skills advertised, each skill tool call (`load_skill`, `read_skill_resource`, `run_skill_script`) with its arguments, approvals, and the answer |
| README of each lab | lab folder, dashboard *Instructions* tab | Objective, TODO tables, concepts, expected output, troubleshooting, going further |
| Dashboard catalog | dashboard lab list and journey | The two labs in the path order, with EN/FR title, summary, objectives and checks |

Every text shown by the dashboard has an English and a French version. Console output and READMEs are in English, like the other labs.

### User Stories

#### Story 1: Learner evaluates an agent
**As a** developer following the labs, **I want to** run my agent on a dataset and get a pass/fail report per question, **So that** I know whether it answers correctly and calls the right tools.

#### Story 2: Learner catches a regression
**As a** developer, **I want to** run the same evaluation on a modified prompt and see the regression flagged, **So that** I can treat prompt changes like code changes.

#### Story 3: Learner packages expertise as a skill
**As a** developer, **I want to** give my agent a skill from a `SKILL.md` folder and another one from code, **So that** it loads detailed instructions and reference data only when a task needs them.

#### Story 4: Learner controls what skills may run
**As a** developer, **I want to** let the agent read skills freely but approve script execution explicitly, **So that** a skill cannot run code without my consent.

#### Story 5: Trainer checks progress
**As a** trainer, **I want** both labs in the dashboard with their checks, **So that** I can see who completed them, in the order of the learning path.

---

## Tasks

> No Jira for this spec: the tasks are tracked in this file.

### T1 — [backend] Lab14: evaluate an agent with deterministic checks

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: The learner runs the evaluation lab and sees an agent with a function tool evaluated on a small dataset: each question has an expected answer and, where relevant, an expected tool call. For every question, the console shows each local check (non-empty answer, expected answer contained, expected tool called, plus one custom check written by the learner) with pass/fail, then the summary passed/total.

**Business constraints**:
- Business Rules 1, 3, 4, 5.
- The dataset makes at least one check depend on data only the tool can return (the model cannot guess it).
- Edge Case "the agent does not call the expected tool".

**Success criteria**:
- [x] The scenario shows one line per question and per check, with pass/fail and a reason for failures.
- [x] On the reference agent, every check passes on 3 consecutive runs with an API key and 1 with Entra ID.
- [x] Removing the tool from the agent makes the tool-call and expected-answer checks fail for the questions that need it, and only those.
- [x] A summary line gives passed/total for the run.

**References**: Business Rules 1, 3, 4, 5; Edge Cases (tool not called); Story 1

---

### T2 — [backend] Lab14: LLM-as-judge quality scores

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: T1

**User outcome**: In a second scenario, the learner adds LLM-as-judge evaluators (relevance and coherence) to the evaluation. For each question, the console shows each score with the judge's reason, next to the deterministic checks of T1, and says which chat client served as judge.

**Business constraints**:
- Business Rules 1, 4, 6.
- Only stable MEAI evaluators that work with an Azure OpenAI deployment (no Foundry project).
- Edge Cases "judge returns no score" and "scores vary".

**Success criteria**:
- [x] Each question shows a relevance score and a coherence score in the evaluator's range, with a reason.
- [x] The judge client is named in the output, and the README explains the "same model as judge" simplification.
- [x] A judge failure on one question is reported for that question without stopping the run.

**References**: Business Rules 1, 4, 6; Edge Cases (judge without score, score variance); Story 1

---

### T3 — [backend] Lab14: catch a regression of the prompt

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: T1

**User outcome**: In a third scenario, the learner evaluates two versions of the same agent on the same dataset — the reference instructions and a degraded version (for example instructions that forbid using the tool or drop a required piece of information). The console compares both runs side by side and states "regression detected" with the questions and checks that went from pass to fail. The learner also sees a question run several times to measure consistency.

**Business constraints**:
- Business Rule 5: the regression is detected by deterministic checks on every run, never by judge scores alone.
- Edge Case "the degraded prompt passes every check" must not happen.

**Success criteria**:
- [x] The comparison lists, per question, the result of the reference and of the degraded version.
- [x] "Regression detected" appears with the failing questions on 3 consecutive runs with an API key and 1 with Entra ID.
- [x] A repeated question shows its result for each repetition and a consistency summary.

**References**: Business Rule 5; Edge Cases (degraded prompt); Story 2

---

### T4 — [glue] Lab14: exercise, README and instructions

**Jira**: none
**Agent hint**: code-documentation:tutorial-engineer
**Depends on**: T1, T2, T3

**User outcome**: The learner opens the evaluation lab, follows the README (objective, configuration, provided files, TODO tables with hints for every step, concepts, expected output from a real run, troubleshooting, going further) and completes the Start project. Before any TODO, the Start project builds without warnings and runs to the end showing every scenario header.

**Business constraints**:
- Business Rule 2 (Start/Solution, 0 warnings, TODO tables match the Start).
- The README explains the difference between evaluation and unit tests, local checks vs LLM judges, the judge simplification (Business Rule 6) and links the official evaluation page and samples.
- "Going further" mentions Foundry evaluators, safety evaluators, workflow evaluation and conversation split strategies as next steps (Non-Goals).

**Success criteria**:
- [x] The delivered Start builds with 0 warnings and runs with exit code 0, printing every scenario header.
- [x] Every TODO of the Start has a row with hints in the README, in the same order.
- [x] The Expected Output section comes from a real run (endpoint and deployment replaced by placeholders).

**References**: Business Rules 2, 3, 6; Edge Cases (Start before any TODO); Stories 1, 2

---

### T5 — [test] Lab14 in the dashboard, verified by real runs

**Jira**: none
**Agent hint**: backend-development:test-automator
**Depends on**: T4

**User outcome**: The learner and the trainer see the evaluation lab in the Lab Bench dashboard with its title, summary, objectives and checks in English and French; running the Solution passes every check, running the delivered Start passes only the startup checks.

**Business constraints**:
- Business Rules 4, 9.
- Dashboard test suite stays green; fixtures come from real runs (spinner frames removed, endpoint and deployment replaced).

**Success criteria**:
- [x] The dashboard lists the lab and shows its checks in both languages.
- [x] Solution: all checks pass on 3 runs with an API key and 1 with Entra ID (results in the step report).
- [x] Delivered Start: only the startup checks pass.
- [x] A negative test shows the checks failing when the tool is missing or the regression is not reported.

**References**: Business Rules 4, 9; Story 5

---

### T6 — [backend] Lab15: skills from SKILL.md files and progressive disclosure

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: —

**User outcome**: The learner runs the skills lab and sees an agent given one or two file-based skills (a folder with a `SKILL.md` and a reference document). The console shows the skills advertised to the agent, then, for a matching question, the agent loading the skill and reading the reference resource before answering with facts that only the resource contains. A question that no skill covers is answered without any skill tool call.

**Business constraints**:
- Business Rules 1, 4, 7.
- The read-only skill tools are auto-approved in this scenario (approval is the topic of T8), and the README says so.
- Edge Case "question no skill covers".

**Success criteria**:
- [x] The console lists each advertised skill (name, description).
- [x] For the matching question, `load_skill` then `read_skill_resource` calls appear with their arguments, and the answer contains a fact present only in the resource.
- [x] For the unrelated question, no skill tool call appears.
- [x] The same behavior holds on 3 runs with an API key and 1 with Entra ID.

**References**: Business Rules 1, 4, 7; Edge Cases (no matching skill, invalid SKILL.md); Story 3

---

### T7 — [backend] Lab15: a skill defined in code, with a dynamic resource and an in-process script

**Jira**: none
**Agent hint**: backend-development:backend-architect
**Depends on**: T6

**User outcome**: In a second scenario, the learner defines a skill in code with a static resource, a resource computed at read time and a script that runs in-process (no subprocess). Asked a matching question, the agent loads the skill, reads the resources and runs the script; the console shows each call with its arguments and the script result used in the answer.

**Business constraints**:
- Business Rules 1, 4.
- Non-Goal: no file-based script and no subprocess.
- The script's result is a value the model cannot compute reliably by itself, so the check can verify it was used.

**Success criteria**:
- [x] `run_skill_script` appears with the arguments chosen by the model and the script result.
- [x] The dynamic resource shows a value computed at run time (for example the current date).
- [x] The answer contains the exact script result on 3 runs with an API key and 1 with Entra ID.

**References**: Business Rules 1, 4; Story 3

---

### T8 — [security] Lab15: approval of skill tools

**Jira**: none
**Agent hint**: backend-api-security:backend-security-coder
**Depends on**: T7

**User outcome**: In a third scenario, the learner sees that skill tools require approval by default, then configures the agent so that reading skills is auto-approved while running a script still needs an explicit decision. The console shows the pending approval request for the script (tool, arguments), the decision taken by the program, and the outcome: with approval the script result is used; with rejection the agent answers without it.

**Business constraints**:
- Business Rule 7 and the official security guidance (review before use, source trust, sandboxing, audit) in the README.
- No interactive input needed: the run must work unattended in the dashboard; the decision rule is explicit in the code and in the output.
- Edge Cases "approval pending" and "script rejected".

**Success criteria**:
- [x] Without auto-approval, the first skill tool call stops with a visible approval request.
- [x] With read-only auto-approval, `load_skill` and `read_skill_resource` run without a request, while `run_skill_script` produces a request.
- [x] Approved: the answer uses the script result; rejected: the output shows the rejection and the answer does not contain the script result.

**References**: Business Rule 7; Edge Cases (approval pending, script rejected); Story 4

---

### T9 — [glue] Lab15: exercise, README and instructions

**Jira**: none
**Agent hint**: code-documentation:tutorial-engineer
**Depends on**: T6, T7, T8

**User outcome**: The learner opens the skills lab, follows the README (objective, the `SKILL.md` format, progressive disclosure, provided skill folders, TODO tables, concepts, "skills vs tools vs workflows", security, expected output from a real run, troubleshooting, going further) and completes the Start project, which builds without warnings and runs to the end before any TODO.

**Business constraints**:
- Business Rule 2.
- The README states the official rule of thumb: "if you want the AI to figure out *how* to accomplish a task, use a skill. If you need to guarantee *what* steps execute and in what order, use a workflow", and distinguishes Agent Skills from the `AgentSkill` of an A2A card (Lab06).
- "Going further" covers class-based skills, the skills provider builder, file-based scripts with a sandboxed runner, and MCP-based skills (experimental).

**Success criteria**:
- [x] The delivered Start builds with 0 warnings and runs with exit code 0, printing every scenario header.
- [x] Every TODO of the Start has a row with hints in the README, in the same order.
- [x] The provided skill folders are identical in Start and Solution.

**References**: Business Rules 2, 7; Edge Cases (invalid SKILL.md, Start before any TODO); Stories 3, 4

---

### T10 — [test] Lab15 in the dashboard, verified by real runs

**Jira**: none
**Agent hint**: backend-development:test-automator
**Depends on**: T9

**User outcome**: The skills lab appears in the dashboard with English and French texts and checks; the Solution passes every check, the delivered Start only the startup checks.

**Business constraints**:
- Business Rules 4, 9.
- Fixtures from real runs; dashboard test suite stays green.

**Success criteria**:
- [x] The dashboard lists the lab and shows its checks in both languages.
- [x] Solution: all checks pass on 3 runs with an API key and 1 with Entra ID.
- [x] Delivered Start: only the startup checks pass.
- [x] A negative test shows the checks failing when a skill is answered without its resource or a script runs without approval.

**References**: Business Rules 4, 9; Story 5

---

### T11 — [glue] Learning path and documentation

**Jira**: none
**Agent hint**: code-documentation:docs-architect
**Depends on**: T5, T10

**User outcome**: A learner reading the root README sees both labs in the lab overview and in the essential learning path (Skills after Lab12, Evaluation after Lab11), and the dashboard lists them in the same order. The migration plan and a step report record what was built, the verified stable status of the APIs, the deviations and the test results.

**Business constraints**:
- Business Rule 8 (placement, no renumbering).
- The Agent Harness stays listed as deferred, with the reason (MAAI001).

**Success criteria**:
- [x] The root README overview has a section for each new lab with its scenarios.
- [x] The learning-path diagram and the dashboard order match.
- [x] The step report lists the runs (key / Entra ID) and their results for both labs.

**References**: Business Rule 8; Story 5

---

## Test Plan

> Status: written after implementation (2026-10-25), no `mbs-spec-dev` run. Results of the automated runs: [Migration/Evaluation-Skills-Report.md](../../Migration/Evaluation-Skills-Report.md).

1. **Lab14, Solution** — `cd LearningLabs/AzureOpenAI/Lab14-AIAgentEvaluation/Solution && dotnet run`: the instructions under test are printed; scenario 1 shows Q1–Q4 with `[pass]` on `non_empty`, `contains_expected`, `tool_call_args_match`, `is_concise` and `Summary: 4/4 questions passed` (Story 1).
2. Scenario 2 prints `Judge: ...` then, per question, `[score n] Relevance` and `[score n] Coherence` (1–5) with a reason (Story 1).
3. Scenario 3 prints the comparison table: Q1–Q3 `pass` / `FAIL (contains_expected, tool_call_args_match)`, Q4 `pass` / `pass`, then `Regression detected: 3 question(s)...` and `Repetitions of Q1: 3/3 passed` (Story 2).
4. Edge case "tool not called": remove the tool from the agent of scenario 1 → `tool_call_args_match` and `contains_expected` fail for Q1–Q3 only.
5. **Lab15, Solution** — `dotnet run` in `Lab15-AIAgentWithSkills/Solution`: scenario 1 lists the 2 advertised skills, then `[Skill] load_skill(skillName: expense-policy)` and `[Skill] read_skill_resource(..., references/limits.md)`, an answer with 145 EUR and `Skill tools run for this question: 2`; the capital question shows `0` (Story 3, edge case "no matching skill").
6. Scenario 2 shows `load_skill`, both `read_skill_resource` (the validity date is today + 7) and `run_skill_script(... arguments: {"weightKg":2.5,"zone":"B"}) -> {"quoteId":"QT-B250-096",...}`, and the answer gives `QT-B250-096` (Story 3).
7. Scenario 3 shows `Without auto-approval, approval requested for load_skill(...)`, then for 2.5 kg `Policy decision: approved` followed by the `run_skill_script` trace, and for 120 kg `Policy decision: rejected` with no `run_skill_script` trace (Story 4, edge cases "approval pending" and "script rejected").
8. Edge case "invalid SKILL.md": change `name:` of `it-support` to `IT Support` → the skill is no longer advertised.
9. **Start projects** of both labs: `dotnet build` with 0 warnings; `dotnet run` exits 0 and prints the three scenario headers.
10. **Dashboard** (Story 5): both labs appear in the order of the learning path (Lab15 after Lab12, Lab14 after Lab11), with EN/FR texts; Run Solution → 10/10 checks each; Run Start → only `config`.

## Dependencies

- **Blocked by**: — (action plan v2 steps 1–5 are done and committed)
- **Blocks**: the workflow and orchestration labs (MultiAgentSystem Lab02 and Lab03 rework), which come after these labs in the path
- **External**: Azure OpenAI deployment that supports function calling (used as agent and as judge); NuGet packages listed in Data Model

## Out of Scope / Future Considerations

- Agent Harness lab, when compaction, file memory and the autonomous loop become stable.
- Foundry evaluators and safety evaluators (need a Foundry project), and evaluation of workflows.
- MCP-based skills and sandboxed file-based scripts.
- An "optional" flag for labs in the dashboard UI.

## Open Questions

- [x] Placement in the path (Business Rule 8): Skills after Lab12 and Evaluation after Lab11 — confirmed on 2026-10-25.
- [x] Judge model (Business Rule 6): the same deployment as the agent, no extra setting — confirmed on 2026-10-25.
