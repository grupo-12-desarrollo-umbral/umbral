# DES-63 — AFK - Mission authoring baseline and catalog/detail flow

> Archived from Linear. This issue was created in the wrong tracker (Linear instead of GitHub).
> Recreate as a GitHub issue in umbral-backend using /to-issues DES-62.
>
> Original URL: https://linear.app/desarrollo-equipo-12/issue/DES-63/afk-mission-authoring-baseline-and-catalogdetail-flow
> Created: 2026-05-24 | Priority: High | Parent: DES-62

## Parent

DES-62 — PRD - Primera implementación de MissionDesign service (HU-09 a HU-14)

## What to build

Implement the first complete vertical slice for `MissionDesign` authoring around the `Mission` aggregate.

This slice must let an administrator create, consult, edit, and deactivate a `Mission` used as base content for mission sessions. It must establish the service pattern that the rest of `MissionDesign` will follow: aggregate-centered domain logic, application commands/queries, persistence through service-owned repositories, API exposure, and automated tests.

The slice must include the minimum domain model required for `Mission` basics and source-readiness state, including `Difficulty`, `MaximumTime`, and `MissionActivation`, while keeping runtime authority out of this service.

## Acceptance criteria

- [ ] An administrator can create a `Mission` with its basic data and later retrieve it through catalog and detail reads.
- [ ] An administrator can update the basic authoring data of an existing `Mission` without recreating it.
- [ ] An administrator can deactivate a `Mission`, and a deactivated `Mission` is no longer considered available for new session-source use.
- [ ] The slice is implemented end-to-end through domain, application, persistence, API, and automated tests.

## Blocked by

None - can start immediately
