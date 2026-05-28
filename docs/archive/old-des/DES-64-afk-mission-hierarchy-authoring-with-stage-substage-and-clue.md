# DES-64 — AFK - Mission hierarchy authoring with Stage, Substage, and Clue

> Archived from Linear. This issue was created in the wrong tracker (Linear instead of GitHub).
> Recreate as a GitHub issue in umbral-backend using /to-issues DES-62.
>
> Original URL: https://linear.app/desarrollo-equipo-12/issue/DES-64/afk-mission-hierarchy-authoring-with-stage-substage-and-clue
> Created: 2026-05-24 | Priority: High | Parent: DES-62

## Parent

DES-62 — PRD - Primera implementación de MissionDesign service (HU-09 a HU-14)

## What to build

Implement the second `MissionDesign` vertical slice for hierarchical `MissionNode` authoring.

This slice must let an administrator build the bounded mission hierarchy using `Stage`, `Substage`, and `Clue`, attached to an existing `Mission`. The slice must add and update hierarchy content end-to-end and preserve the project's ubiquitous language rather than introducing generic workflow terminology.

This slice should establish `MissionNode` as a child entity owned by `Mission` and make the hierarchy queryable through the same service so authoring can be reviewed after write operations.

## Acceptance criteria

- [ ] An administrator can add `Stage`, `Substage`, and `Clue` nodes to a `Mission` using the allowed hierarchy entrypoints.
- [ ] A `Mission` detail read exposes the authored hierarchy in a way that is sufficient to review structure after changes.
- [ ] A `Clue` cannot behave as a parent node in the authoring flow.
- [ ] The slice is implemented end-to-end through domain, application, persistence, API, and automated tests.

## Blocked by

DES-63
