---
title: Labs - Provided Agent Prompts
status: implemented
priority: medium
author: PM Agent
created: 2026-11-05
updated: 2026-11-05
jira_project: none
jira_epic: none
---

# Labs - Provided Agent Prompts

## Summary

In every lab, the name of each agent, its instructions and the questions it is asked are **provided** to the learner in a dedicated file, `LabPrompts.cs`, identical in `Start/` and `Solution/`. The learner reuses these constants instead of writing texts, so the whole room works with the same agents, the same instructions and the same questions, and every minute of the lab goes to the Microsoft Agent Framework code. The convention was piloted on **AzureOpenAI Lab01** (reference implementation, see Business Rules); this mini-spec applies it to the other labs of the catalog.

## Problem Statement

- Today the texts are delivered inconsistently: sometimes in a `// Hint:` comment, sometimes in the README ("see the README for the prompt"), sometimes as an example (`e.g., "GeographyAgent"`), sometimes not at all. The Solution holds about 50 agent definitions, the Start only part of them.
- Learners spend time inventing an agent name, writing instructions or phrasing a question, which teaches nothing about Agent Framework, and get answers that differ from the README's Expected Output and from their neighbors', which makes help and comparison harder for the trainer.
- A lab whose Start was committed partly solved (Lab01 in commit `b3c4f9c`) hides the exercise; the same accident can exist elsewhere.

## Goals

- [x] In every lab in scope, no agent name, agent instructions, system message or question has to be written by the learner: each one is a named constant of `LabPrompts.cs`.
- [x] Each Start TODO that creates or runs an agent names the constant to use; each README hint points to the constants.
- [x] The Solution of every lab in scope uses the constants, with no such text left hard-coded in its code.
- [x] Every lab in scope still builds, passes the dashboard tests, and reports its delivered Start as "not implemented" and its Solution as "implemented".

## Non-Goals

- **MultiAgentSystem Lab02_OrchestrationSequential and Lab03_OrchestrationConcurrent**: not migrated yet (old `Azure.AI.OpenAI` SDK, no TODOs, not in the dashboard catalog). They adopt the convention when they are migrated.
- Rewriting or improving the texts themselves: they move verbatim (see Business Rule 4).
- A shared `LabPrompts` in `CommonUtilities`: each lab keeps its own file, so a lab stays self-contained.
- Data files (`Data/*.json`, `Data/*.txt`, `skills/**/SKILL.md`): they already are provided files; they stay where they are.
- Any change to the dashboard code.

## Changelog

- **v1.0** (2026-11-05) — Initial release. Tasks T1..T5, no Jira Epic (mini-spec). Lab01 already converted (reference implementation).
- **v1.0 delivered** (2026-11-05) — Lab01 pilot `019253a`, T1 `7c8c88f`, T2 `4229fee`, T3 `54edd3a`, T4 `467c335`, T5 (this commit). No other Start was found committed solved.

---

## Reference

### Data Model

No schema changes.

### Business Rules

1. **One provided file per lab project**: `LabPrompts.cs`, byte-identical in `Start/` and `Solution/`, in the namespace of the project, headed by the "PROVIDED - do not edit" banner and the one-paragraph explanation used in Lab01. For Lab06 (two projects, `A2AClient` and `A2AServer`), each project has its own file holding only the texts it uses.
2. **Shape of the file** (as in Lab01):
   - `Agents.<Agent>.Name` and `Agents.<Agent>.Instructions` for each agent (`const string`), one nested static class per agent;
   - `SystemMessages.<Name>` for the system messages written by the developer for one run, only when the lab has some;
   - `Questions.<Name>` for every question or user prompt sent to an agent (including follow-up questions of a conversation);
   - an XML `<summary>` on each class or constant naming the scenario(s) that use it.
   Other kinds of text a lab sends to the model (an evaluation criterion, the description given to an agent used as a tool, an injected context) go in an additional static class with an explicit name (e.g. `JudgeCriteria`, `ToolDescriptions`), following the same rules.
3. **What is provided**: every text the learner would otherwise have to write that is not Agent Framework knowledge — agent names, instructions, system messages, questions, judge criteria, tool or agent descriptions. A text stays in the exercise **only if writing it is the learning objective of the TODO** (e.g. a TODO whose explicit goal is to degrade a prompt to observe a regression); such a case is listed in the task's notes and in the README.
4. **Verbatim move**: the texts keep exactly their current wording. The dashboard checks read the output of each scenario (facts of the knowledge base, values returned by tools, token usage labels) and the README Expected Output was produced with these texts: rewording them is out of scope.
5. **Solution**: uses the constants everywhere; no agent name, instructions, system message or question remains as a string literal in its code (the strings written to the console — titles, labels — are not concerned).
6. **Start**: each TODO that creates an agent names the `Agents.<Agent>` class to use; each TODO that runs an agent names the `Questions.<Name>` (or other) constant; hints show the constants, never the literal text. The scenario code stays commented out (`// AIAgent x = ...`): the Start must still be reported "not implemented" for every scenario.
7. **README**: under "Complete the Program.cs", the paragraph of Lab01 explaining that the texts are provided (adapted with the lab's own constant names); every hint that gave a literal or an `e.g., "..."` example now gives the constant; `LabPrompts.cs` appears in "Project Structure" and first in "Provided Files".
8. **Delivered Start**: a Start found with solved code (implementation instead of the `TODO` comments, as Lab01 in commit `b3c4f9c`) is restored to its delivered state, keeping any later legitimate change to its comments.
9. **Verification of each lab**: `dotnet build` of Start and Solution without new warning; fingerprints regenerated with `dotnet run --project Dashboard/LabDashboard -- exercise-fingerprints` and committed with the lab; `dotnet run -- --list-scenarios` reports Start all "not implemented" and Solution all "implemented"; `dotnet test` run from `Dashboard/` is green.

### Edge Cases

| Case | Expected behavior |
|---|---|
| The same agent is used in several scenarios | One `Agents.<Agent>` class, cited by every scenario (Lab01: `Agents.Geography` in scenarios 2, 3, 6) |
| A scenario reuses an agent with the same instructions but another name, or vice versa | Two constants, one per distinct value; never a computed string |
| A text is built at run time (interpolated date, value of a previous answer) | Only its fixed part is a constant (e.g. a format string or a prefix); the interpolation stays in `Program.cs` and is given by the hint |
| An agent has no name or no instructions on purpose (Lab01 scenario 1, "basic agent") | No constant for what is absent; the TODO keeps saying "no instructions, no name" |
| A text lives in a file other than `Program.cs` (e.g. `SessionConsole.cs`, `EvaluationDataset.cs`, `ApprovalConsole.cs`) | If the learner completes that file, its texts move to `LabPrompts.cs` too; if it is a provided helper file, it may keep them |
| The lab is a web API (Lab13) | The agent's name and instructions are provided; the questions are those of the README's request examples (`curl`/`.http`), which then cite them in prose |
| Start was committed solved | Business Rule 8 |

### User Stories

#### Story 1: Learner focuses on the framework
**As a** learner, **I want to** get the agent's name, instructions and question ready to use, **So that** I spend the lab on Agent Framework concepts, not on prompt writing.

#### Story 2: Trainer compares answers
**As a** trainer, **I want** every learner to run the same agents with the same questions, **So that** answers are comparable with each other and with the README's Expected Output.

---

## Tasks

> Mini-spec: no Jira Epic. Each task covers a group of labs and is complete only when every lab of the group passes Business Rule 9. Reference implementation: `LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent` (`LabPrompts.cs`, Start TODOs, README hints).

### T1 — [config] Provided prompts in the basic labs (Lab02 to Lab05)

**Jira**: none
**Agent hint**: general-purpose
**Depends on**: —

**User outcome**: A learner opening Lab02 (structured output), Lab03 (function tools), Lab04 (MCP client) or Lab05 (sessions) finds every agent name, instruction and question ready in `LabPrompts.cs`, named by the TODOs and the README hints, and never has to write one.

**Business constraints**:
- Business Rules 1–9.
- Lab03: the prompts currently given as "see the README for the prompt" (scenarios 2, 3, 4) become `Questions` constants; the README keeps explaining what each prompt tests (e.g. one booking in the past, one tomorrow).
- Lab05: the texts of `SessionConsole.cs` follow the Edge Case "text in another file".

**Success criteria**:
- [ ] In each of the 4 labs, the Start's TODOs and the README's hints name a constant for every agent and question; none asks to write a text.
- [ ] Each Solution runs its scenarios with the same answers as before (same texts, same dashboard checks passing on a run with valid credentials).
- [ ] Business Rule 9 holds for each of the 4 labs.

**References**: Business Rules 1–9, Edge Cases, Stories 1–2

---

### T2 — [config] Provided prompts in the tools and connectivity labs (Lab06 to Lab10)

**Jira**: none
**Agent hint**: general-purpose
**Depends on**: —

**User outcome**: A learner opening Lab06 (A2A server and client), Lab07 (agentic RAG), Lab08 (data format comparison), Lab09 (human approval) or Lab10 (agent as MCP tool) uses provided agents, descriptions and questions in both projects of a lab, and the same texts as the trainer.

**Business constraints**:
- Business Rules 1–9; Lab06 has one `LabPrompts.cs` per project (Business Rule 1).
- Lab06 server: the agent card's name and description are provided like an agent's name and instructions. Lab10: the description that exposes the agent as an MCP tool is provided.
- Lab07: the questions must keep matching the facts of `Data/sav-faq.json` checked by the dashboard (Business Rule 4).
- Lab09: the texts of `ApprovalConsole.cs` follow the Edge Case "text in another file".

**Success criteria**:
- [ ] In each of the 6 projects, the Start's TODOs and the README's hints name a constant for every agent, description and question; none asks to write a text.
- [ ] The A2A client still talks to the A2A server, and the MCP tool of Lab10 is still discovered with its description.
- [ ] Business Rule 9 holds for each of the 6 projects.

**References**: Business Rules 1–9, Edge Cases, Stories 1–2

---

### T3 — [config] Provided prompts in the advanced labs (Lab11 to Lab15)

**Jira**: none
**Agent hint**: general-purpose
**Depends on**: —

**User outcome**: A learner opening Lab11 (custom HTTP transport), Lab12 (context provider), Lab13 (agent in an ASP.NET Core API), Lab14 (evaluation) or Lab15 (skills) finds the agents, questions, judge criteria and context texts provided, and writes only Agent Framework code — except where writing the text is the explicit objective of the TODO.

**Business constraints**:
- Business Rules 1–9 and Business Rule 3's exception, decided case by case and written in the task's report and in the README.
- Lab12: the context injected by the provider and the memory-extraction instructions are provided (Business Rule 3), unless a TODO's objective is to write them.
- Lab13: Edge Case "the lab is a web API".
- Lab14: the evaluation dataset (`EvaluationDataset.cs`) stays a provided file; the agent's prompt, its degraded variant and the judge criteria are provided — unless the TODO's objective is to degrade the prompt itself (Business Rule 3).
- Lab15: the `SKILL.md` files stay provided data files (Non-Goals); the code-defined skills' names, descriptions and instructions, the agent's instructions and the questions go in `LabPrompts.cs`.

**Success criteria**:
- [ ] In each of the 5 labs, the Start's TODOs and the README's hints name a constant for every text, except the cases listed as learning objectives, which the README says explicitly.
- [ ] Lab14 still detects the regression of the degraded prompt; Lab15 still shows the progressive disclosure (advertise → load → read resource → run script).
- [ ] Business Rule 9 holds for each of the 5 labs.

**References**: Business Rules 1–9, Edge Cases ("text in another file", "web API", "text built at run time"), Stories 1–2

---

### T4 — [config] Provided prompts in the multi-agent lab (MultiAgentSystem Lab01)

**Jira**: none
**Agent hint**: general-purpose
**Depends on**: —

**User outcome**: A learner opening MultiAgentSystem Lab01 (agent used as a tool) finds the name, instructions and tool description of each specialist agent, the orchestrator's instructions and the questions provided, and spends the lab on composing agents.

**Business constraints**:
- Business Rules 1–9.
- The description under which an agent is exposed as a tool to another agent is provided (Business Rule 2, `ToolDescriptions` or as a constant of the agent's class).

**Success criteria**:
- [ ] The Start's TODOs and the README's hints name a constant for every agent, description and question; none asks to write a text.
- [ ] The orchestrator still calls the specialist agents as tools on the provided questions.
- [ ] Business Rule 9 holds for the lab.

**References**: Business Rules 1–9, Edge Cases, Stories 1–2

---

### T5 — [glue] The convention is documented for learners and lab authors

**Jira**: none
**Agent hint**: general-purpose
**Depends on**: T1, T2, T3, T4

**User outcome**: A learner reading the root README knows, before opening a lab, that agent names, instructions and questions are provided in `LabPrompts.cs`; a lab author knows the convention to follow for the next labs (including MultiAgentSystem Lab02 and Lab03 when they are migrated).

**Business constraints**:
- The root README's lab structure and "how to work" steps mention `LabPrompts.cs` and why it exists, in a few lines.
- The convention (Business Rules 1–7) is written once where lab authors already look for lab conventions, and the remaining unmigrated labs are named as pending.
- Final pass: no lab in scope still has an `e.g., "..."` example of an agent name, instructions or question in its README or its Start.

**Success criteria**:
- [ ] The root README shows `LabPrompts.cs` in the lab structure and explains it in one or two sentences.
- [ ] A search across the labs in scope finds no literal example of an agent name, instructions or question left in a Start TODO or a README hint.
- [ ] `dotnet test` from `Dashboard/` is green and `exercise-fingerprints` reports every lab "unchanged".

**References**: Business Rules 1–9, Non-Goals, Stories 1–2

---

## Test Plan

> Generated: 2026-11-05
> Status: ready-for-qa
> Delivered by: T1, T2, T3, T4, T5 (no Jira)

### Prerequisites

- .NET 10 SDK; the labs built once (`dotnet build` of a Start and a Solution).
- For the runs against the model (Scenario 3): valid Azure OpenAI settings (user secrets `AzureOpenAI:*` or the dashboard's *Azure OpenAI settings*). Builds, `--list-scenarios` and the dashboard tests need none.

### Scenario 1 — A learner finds the texts provided (Story 1)

1. Open `LearningLabs/AzureOpenAI/Lab03-AIAgentWithFunctionTools/Start/LabPrompts.cs`.
   → Expect: the "PROVIDED - do not edit" banner, `Agents.CompanyAssistant`, `Agents.NotificationAssistant`, and `Questions` with a summary naming each scenario.
2. Open `Start/Program.cs` of the same lab, scenario 2.
   → Expect: TODO 13 names `Questions.BookPastAndTomorrowMeetings` and its hint shows the `string.Format(...)` call with the date of tomorrow; no literal question to type.
3. Open the lab's `README.md`, section "Complete the Program.cs".
   → Expect: the paragraph saying the texts are provided; the hints give constants; TODO 11 says the refusal message is yours to write (it is the objective of the TODO).
4. Repeat steps 1–2 on Lab06_A2AServer (agent card name and description come from `Agents.<X>.Description`) and MultiAgentSystem Lab01 (`ToolDescriptions`, `CustomerMessages`).

**Negative case**: in any lab of the catalog, search the Start and the README for `e.g., "` next to an agent name, instructions or question.
   → Expect: no hit, except the documented exceptions (Lab03 TODO 11, Lab07 TODO 15) and console labels.

### Scenario 2 — The delivered exercise is untouched (Story 1)

1. From a lab folder, run `dotnet run --project Start -- --list-scenarios`, then `dotnet run --project Solution -- --list-scenarios`, on Lab01, Lab05, Lab09, Lab12 and MultiAgentSystem Lab01.
   → Expect: Start lists every scenario "not implemented (code unchanged since delivered)" and the setup line "still the delivered one"; Solution lists every scenario "implemented".
2. In Lab01 Start, write the code of TODO 4–6 only, then list the scenarios again.
   → Expect: scenario 1 "implemented", the others still "not implemented".
3. From `Dashboard/`, run `dotnet test`, and from the root `dotnet run --project Dashboard/LabDashboard -- exercise-fingerprints`.
   → Expect: tests green; every lab "unchanged".

### Scenario 3 — Everyone gets comparable answers (Story 2)

1. Run the Solution of Lab01 (`dotnet run`), then of Lab07 and Lab14, with valid credentials.
   → Expect: the same answers as the README's Expected Output (in substance), the dashboard checks of each scenario pass in the Lab Bench dashboard.
2. Lab14: compare the reference and candidate runs.
   → Expect: the candidate (degraded instructions) is flagged as a regression, as before.

### Cross-cutting checks

- `Start/LabPrompts.cs` and `Solution/LabPrompts.cs` are identical in every lab (`cmp`).
- The root README shows `LabPrompts.cs` in the lab structure; `Dashboard/README.md` § "Provided texts (LabPrompts.cs)" documents the convention for authors.

### Rollback / cleanup

- Revert the code written in Lab01 Start at Scenario 2 step 2 (`git restore LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start/Program.cs`).

## Dependencies

- **Blocked by**: the Lab01 pilot (done, uncommitted on branch `lab01-responses-follow-up`): commit it first so the tasks start from the reference implementation.
- **Blocks**: the migration of MultiAgentSystem Lab02 and Lab03 (they follow the convention).
- **External**: none. Running the labs against the model needs valid Azure OpenAI credentials (the environment key returned 401 at the last check); builds, fingerprints, `--list-scenarios` and dashboard tests do not.

## Out of Scope / Future Considerations

- A dashboard check that the learner used the provided constants (e.g. that the agent name in the output is the provided one).
- French versions of the instructions and questions.

## Open Questions

- [x] Lab14: is degrading the prompt a learning objective of a TODO or a provided variant? **Provided** (`Agents.Support.CandidateInstructions`): the objective is to see the regression detected. Lab14 has no judge text to provide (built-in Relevance and Coherence evaluators).
- [x] T1–T4 in parallel or in order? **In order**, one commit per task (user decision, 2026-11-05).
