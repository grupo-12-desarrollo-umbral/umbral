---
name: validate-requirement
description: Validate a functional requirement or user story against the current codebase end to end and save the finding as a Git-ignored Markdown report. Use when the user asks to validate, verify, audit, assess, or check implementation of an RF, HU, acceptance criterion, feature requirement, or story, especially when both backend and frontend behavior, integration boundaries, and automated tests must be examined.
---

# Validate Requirement

Validate the requested RF or HU using source evidence and executed tests. Treat validation as a read-only review unless the user separately asks for fixes.

## Workflow

1. Locate the authoritative requirement or story. Quote its identifier and exact meaning. Convert its acceptance criteria into observable capabilities.
2. Read every applicable `AGENTS.md`, context map, architecture document, and workload-specific instruction before inspecting implementation.
3. Trace each capability through every relevant layer:
   - user-facing UI and state
   - frontend action or data-access function
   - API gateway or integration boundary
   - endpoint and transport contract
   - application handler and validation
   - domain behavior
   - persistence, messaging, or real-time delivery
4. Inspect happy paths and relevant negative states: authorization, validation, not-found behavior, conflicts, inactive states, error presentation, and deployment configuration.
5. Find tests for each capability. Distinguish tests that exist from tests actually executed.
6. Establish executed-test results according to **Test Execution Modes**. Never claim an unexecuted test passed. State environmental blockers precisely.
7. Compare observed behavior with both the requirement and documented architecture. Report functional gaps separately from integration or architecture violations.
8. Produce the mandatory report below. Do not replace it with a shorter summary.
9. Save the complete report according to **Report Persistence**, then return the same report in the final response with a link to the saved file.

## Evidence Rules

- Open a source file before citing its symbol, behavior, or line.
- Use clickable absolute file links with a single starting line when the client supports them.
- Base `Pass` on implemented behavior with adequate evidence. A route or test name alone is insufficient.
- Use `Pass, with limitation` when the capability works but has a meaningful scope restriction.
- Use `Partial` when only part of a capability is implemented.
- Use `Fail` when observed behavior contradicts the requirement.
- Use `Not verifiable` when evidence or the required runtime is unavailable.
- Do not count unrelated passing tests as coverage for the requirement.
- Preserve user changes and do not implement fixes during validation.

## Test Execution Modes

Exactly one agent executes tests for a validation. Concurrent runs of the same suites collide on ports, containers, and build output, and produce unreliable results.

**Execute mode** (default, and the only mode for a single-agent validation): run the smallest relevant backend, frontend, integration, and end-to-end suites allowed by repository instructions. When invoked as the owner of a fan-out, write the executed commands and their passed/failed/skipped totals to a results file and pass its path to every tracing agent.

**Trace mode** (`--no-exec`, or whenever a results-file path is supplied): do not invoke any test runner — no `dotnet test`, `npm test`, `npx playwright`, or equivalent. Report which tests exist for the assigned scope with file paths, and read executed results from the supplied file. If no results file is supplied, record `not executed` rather than running the suite yourself.

Trace mode covers analysis only: return findings to the caller and do not write a report file. **Report Persistence** applies to the agent that synthesizes the final report.

## Report Persistence

- Save every completed validation under `backend/docs/validation-findings/`.
- Use one Markdown file per requirement or story: `backend/docs/validation-findings/<ID>.md`, preserving the canonical identifier such as `RF-01.md` or `HU-24B.md`.
- If no canonical identifier exists, create a short lowercase hyphenated slug from the requirement title.
- Create the directory when absent.
- Overwrite an existing report for the same identifier so the file represents the current codebase assessment and contains no stale findings.
- Write the exact mandatory report delivered to the user; do not save a shortened variant.
- Confirm the output path is ignored by Git before finishing. If it is not ignored, stop and report the configuration problem rather than leaving a trackable finding.
- Include a clickable link to the saved report in the final response.

## Mandatory Report Template

Follow this section order and retain every heading. Replace placeholders with findings. Add or remove capability rows as needed.

Start with one bullet containing the requirement identifier, functional conclusion, verdict, and primary reason:

```markdown
- **<ID> is <implementation conclusion>, but I would mark it <verdict> because <primary reason>.**
```

Then provide the capability matrix:

```markdown
| Capability | Backend | Frontend | Result |
|---|---|---|---|
| <Capability 1> | <endpoint, validation, domain, persistence evidence> | <UI, action, and client evidence> | <Pass/Pass, with limitation/Partial/Fail/Not verifiable> |
| <Capability 2> | <evidence> | <evidence> | <result> |
```

Use `N/A` when a capability legitimately has no backend or frontend concern; do not invent evidence to fill a cell.

Continue with:

```markdown
Key evidence:

- <Most direct backend or contract evidence with file and line.>
- <Persistence/domain/integration evidence with file and line.>
- <Most direct frontend evidence with file and line.>

Issues found:

1. <Concrete issue, consequence, requirement or architecture conflict, and file/line evidence.>
2. <Next issue.>

Verification:

- Backend: <command scope and passed/failed/skipped totals, or why it was not run.>
- Frontend: <command scope and passed/failed/skipped totals, or why it was not run.>
- End to end: <executed result; if only scenarios exist, say they exist but were not executed and why.>

Fill these from the run this agent performed, or from the supplied results file when validating in trace mode. Cite the results file path so the reader can see which run the totals came from.

Overall verdict: **<ID> <final compliance statement and what prevents a stronger verdict, if anything>.**
```

If there are no issues, write `No issues found.` under `Issues found`. Do not omit the section.

## Verdict Selection

- **Pass**: every capability is present across applicable layers, no material contradiction exists, and verification is proportionate to risk.
- **Conditional pass**: functionality is substantially complete, but a material integration, architecture, deployment, or unexecuted critical-path condition prevents full compliance.
- **Partial**: one or more required capabilities are missing or incomplete while others work.
- **Fail**: the main required outcome is absent or contradicted.
- **Not verifiable**: available code or runtime evidence cannot support a responsible conclusion.

Let the weakest material capability or integration boundary determine the overall verdict. Do not soften a verdict merely because many tests pass.
