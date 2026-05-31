---
name: to-prd
description: Turn the current conversation context into a PRD, publish it to Linear, and save a local copy to docs/prd/. Use when user wants to create a PRD from the current context.
---

This skill takes the current conversation context and codebase understanding and produces a PRD. Do NOT interview the user — just synthesize what you already know.

## Targets

This project uses **two outputs** for every PRD:

1. **Linear** — publish as a `DES-XX` issue in team `umbral-equipo-12` with the `ready-for-agent` label. Use the Linear MCP tools. Note the assigned issue number.
2. **Local file** — save to `docs/prd/DES-XX-<slug>.md` where `DES-XX` is the Linear issue number and `<slug>` is a kebab-case summary of the PRD title (e.g. `docs/prd/DES-62-mission-design-service-baseline.md`).

Both outputs are required. Do not publish to GitHub Issues.

## Read first — canonical documents

Load these before writing the PRD. They are authoritative for this project.

| Document | Owns |
|---|---|
| `docs/ddd_solution_model.md` | Bounded contexts, aggregates, domain events, repository interfaces, domain services, application services, HU backlog alignment |
| `docs/bd_umbral_entity_spec.md` | Field-level entity spec, value objects, enums, invariants, key constraints |

Use the vocabulary from these docs throughout the PRD. Never invent concepts not found in them.

## Process

1. Load the canonical documents above. Then explore any additional repo context relevant to the service (service `CONTEXT.md`, ADRs). Use the project's domain glossary vocabulary throughout the PRD.

2. Sketch out the major modules you will need to build or modify to complete the implementation. Actively look for opportunities to extract deep modules that can be tested in isolation.

A deep module (as opposed to a shallow module) is one which encapsulates a lot of functionality in a simple, testable interface which rarely changes.

Check with the user that these modules match their expectations. Check with the user which modules they want tests written for.

3. Write the PRD using the template below. Then:
   - Publish it to Linear (team `umbral-equipo-12`) with the `ready-for-agent` label — note the assigned `DES-XX` number
   - Save it locally to `docs/prd/DES-XX-<slug>.md`

<prd-template>

## Problem Statement

The problem that the user is facing, from the user's perspective.

## Solution

The solution to the problem, from the user's perspective.

## User Stories

A LONG, numbered list of user stories. Each user story should be in the format of:

1. As an <actor>, I want a <feature>, so that <benefit>

<user-story-example>
1. As a mobile bank customer, I want to see balance on my accounts, so that I can make better informed decisions about my spending
</user-story-example>

This list of user stories should be extremely extensive and cover all aspects of the feature.

## Implementation Decisions

A list of implementation decisions that were made. This can include:

- The modules that will be built/modified
- The interfaces of those modules that will be modified
- Technical clarifications from the developer
- Architectural decisions
- Schema changes
- API contracts
- Specific interactions

Do NOT include specific file paths or code snippets. They may end up being outdated very quickly.

Exception: if a prototype produced a snippet that encodes a decision more precisely than prose can (state machine, reducer, schema, type shape), inline it within the relevant decision and note briefly that it came from a prototype. Trim to the decision-rich parts — not a working demo, just the important bits.

## Testing Decisions

A list of testing decisions that were made. Include:

- A description of what makes a good test (only test external behavior, not implementation details)
- Which modules will be tested
- Prior art for the tests (i.e. similar types of tests in the codebase)

## Out of Scope

A description of the things that are out of scope for this PRD.

## Further Notes

Any further notes about the feature.

</prd-template>
