# DES-65 — AFK - Mission structure invariants and readiness validation

> Archived from Linear. This issue was created in the wrong tracker (Linear instead of GitHub).
> Recreate as a GitHub issue in umbral-backend using /to-issues DES-62.
>
> Original URL: https://linear.app/desarrollo-equipo-12/issue/DES-65/afk-mission-structure-invariants-and-readiness-validation
> Created: 2026-05-24 | Priority: High | Parent: DES-62

## Parent

DES-62 — PRD - Primera implementación de MissionDesign service (HU-09 a HU-14)

## What to build

Implement the third `MissionDesign` vertical slice that hardens mission hierarchy rules and source-readiness validation.

This slice must reject structurally invalid combinations in mission authoring and make activation/readiness depend on the bounded mission rules already defined by the backlog and domain model. The slice should centralize those rules in a domain-level policy or equivalent aggregate-owned logic so they are not scattered across handlers or transport validation.

The scope of this slice is not to add new runtime behavior, but to make the authoring flow safe and explicit for invalid mission structures.

## Acceptance criteria

- [ ] The system rejects a `Clue` directly under a `Stage` and rejects a `Substage` under another `Substage`.
- [ ] The system rejects any attempt to author child nodes under a `Clue`.
- [ ] A `Mission` with structurally incomplete stage composition is not considered ready for live-session use.
- [ ] Rejections expose a consistent business-level reason and the slice is implemented end-to-end through domain, application, persistence, API, and automated tests.

## Blocked by

DES-64
