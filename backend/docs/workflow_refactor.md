# Refactor Workflow — Active Todo HU Tickets

This document tracks the **14 active, non-archived HU tickets in Linear's `Todo` state** for team `umbral-equipo-12` plus the repository's **6 open GitHub issues**.

Snapshot verified against live Linear and GitHub: **2026-07-13**, re-verified after DES-42 (HU-31) and DES-95 (HU-30) moved to **In Progress** at ~20:33 UTC and DES-40/DES-41 were resolved as **Duplicate** of DES-95. Completed, canceled, archived, in-progress, and Linear Backlog work does not belong in the active Todo queue below.

## Per-ticket loop

Run the following loop once per ticket, respecting the dependency order below.

1. Generate the ticket context with `backend/.agents/generator-agent.md`.
2. Review the generated context, prompt, and compact brief.
3. Drive the brief with `backend/.agents/driver-agent.md`.
4. Complete Domain, Application, Infrastructure, and Api phases in order, passing each gate before committing.
5. Verify the acceptance criteria end to end.
6. Open a draft PR to `develop`; after merge, move the ticket to `Done` and remove its worktree.

The authority order remains:

```text
canon docs  >  rewritten ticket acceptance criteria  >  existing code
```

## Active queue (14)

Dependencies shown here are other Todo tickets in this 14-ticket set, plus DES-42 and DES-95, which left Todo for **In Progress** but remain unmet blockers until they merge. A blank dependency cell means the ticket has no remaining blocker.

| Order | Ticket | HU | Work | Dependencies |
|---:|---|---|---|---|
| 1 | DES-94 | ENABLER | Operator panel: expose releasable targets of active substage | — |
| 2 | DES-38 | HU-28 | Add operational clues during a live session | — |
| 3 | DES-13 | HU-08 | Multi-device team synchronization | DES-42 (In Progress) |
| 4 | DES-43 | HU-32 | Evidence traceability | DES-42 (In Progress), DES-95 (In Progress) |
| 5 | DES-51 | HU-37 | Score ledger for validations, answers, and penalties | DES-42 (In Progress) |
| 6 | DES-53 | HU-38 | Apply justified penalties | DES-51 |
| 7 | DES-54 | HU-39 | Real-time session ranking | DES-51 |
| 8 | DES-50 | HU-36B | Post-close review of trivia answers and points | DES-51 |
| 9 | DES-48 | HU-35 | Reveal trivia result and explanation | DES-54 |
| 10 | DES-34 | HU-25A | Operational read queries for administrators and operators | DES-54 |
| 11 | DES-35 | HU-25B | Participant board and ranking queries | DES-54 |
| 12 | DES-33 | HU-24B | Real-time operator panel for events, evidence, and ranking | DES-43, DES-54 |
| 13 | DES-56 | HU-40A | Session event history | DES-42 (In Progress), DES-53 |
| 14 | DES-57 | HU-40B | Historical score, ranking, and post-session review | DES-51, DES-54, DES-56 |

## Startable now

Within this Todo-only graph, the current roots are:

- DES-94
- DES-38

Both live in `session-operations-service` and edit the same tree, so serialize them — and note DES-42 and DES-95 are also **In Progress** in that same tree, so any Lane A backend ticket runs against active work there. The former roots DES-95 and DES-42 left Todo for In Progress; every other Todo ticket is transitively blocked by the DES-42 → DES-51 → DES-54 chain.

"Startable" means no blocker remains. Before implementation, the driver must still perform its normal preflight against the ticket, repository, and live Linear relations.

## Recently completed (since 2026-07-12 snapshot)

| Ticket | HU | Completed |
|---|---|---|
| DES-29 | HU-21 Audit session state changes | 2026-07-13 |
| DES-36 | HU-26 Manual clue release | 2026-07-13 |
| DES-39 | HU-29 Team evidence-submission intake | 2026-07-13 |
| DES-93 | TreasureHunt substage timer | 2026-07-13 |

These four were all in the previous snapshot's "startable now" list and landed in a single day. DES-39 was the compile-root that unblocked DES-95 and DES-42. DES-36 unblocked DES-38. DES-93 was a leaf.

## Moved out of Todo (2026-07-13, ~20:33 UTC)

| Ticket | HU | New state | Note |
|---|---|---|---|
| DES-42 | HU-31 Scan/validate/record QR targets | In Progress | Still an unmet blocker for DES-13, DES-43, DES-51, DES-56 until merged. |
| DES-95 | HU-30 Context validation + explained rejection | In Progress | New ticket that superseded the DES-40/DES-41 split; blocks DES-43. |
| DES-40 | HU-30A Context validations | Duplicate | Resolved as duplicate of DES-95. |
| DES-41 | HU-30B Explained rejection | Duplicate | Resolved as duplicate of DES-95. |

## Open GitHub queue (6)

These issues have no Linear DES identifier and do not change the 14-ticket HU count.

| Suggested order | Issue | Work | Named prerequisites | Current status |
|---:|---|---|---|---|
| 1 | GH #139 | Decide controller try/catch versus global ProblemDetails handling | None | Startable documentation decision. Prefer resolving before new endpoints copy an unsettled convention. |
| 2 | GH #153 | Plan target geolocation and treasure-hunt participant play slices | None | Startable maintainer-review task. It is GH #156's parent, but GH #156 does not list it as a blocker. |
| 3 | GH #143 | Participant self-registration from the mobile app | GH #137 decision; blocked by GH #141 | Both named prerequisites are closed; startable. |
| 4 | GH #144 | Forgot-password flow end to end | Blocked by GH #141 | Prerequisite is closed; startable. Serialize with GH #143 because both touch identity and mobile. |
| 5 | GH #156 | Real map view on the treasure-hunt mobile play surface | Blocked by GH #154 and GH #155 | Both prerequisites are closed; startable. Serialize with GH #143/GH #144 because they touch mobile. |
| 6 | GH #85 | Rename `identity-access-service` to `users-service` | Explicitly independent | No formal blocker. Schedule in a quiet window after identity work when practical because it mechanically renames a deployable and can conflict with sibling PRs. |

GH #148 (administrator user management + operator-invite UI) closed 2026-07-13 via PR #207 and is no longer in the open queue.

This is a **conflict-minimizing suggested order**, not a dependency chain: all six issues are currently startable, and none names an active HU as a blocker.

## Cross-service cautions

- DES-13 spans session operations and identity access.
- DES-34 and DES-35 are cross-service read slices.
- DES-43, DES-48, DES-50, DES-53, and DES-56 span session operations and scoring/monitoring.
- DES-94 spans session-operations only but enriches the operator-panel DTO contract shared with DES-33.
- GH #143 spans identity access and mobile.
- GH #144 spans identity access, frontend, and mobile.
- GH #85 is formally independent, but its broad mechanical rename should not overlap sibling backend/identity PRs.

Use `parallel-work-lanes.md` to select combinations that do not edit the same service tree.
