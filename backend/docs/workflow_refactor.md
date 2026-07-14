# Refactor Workflow — Active Todo HU Tickets

This document tracks the **12 active, non-archived HU tickets in Linear's `Todo` state** for team `umbral-equipo-12` plus the repository's **4 open GitHub issues**.

Snapshot verified against live Linear and GitHub: **2026-07-14**. Since the previous snapshot, DES-94 (ENABLER), DES-95 (HU-30) and DES-42 (HU-31) all merged to **Done**, and DES-38 (HU-28) left Todo for **In Progress**. Completed, canceled, archived, in-progress, and Linear Backlog work does not belong in the active Todo queue below.

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

## Active queue (12)

Every blocker in this graph is now another Todo ticket in this set — DES-42 and DES-95 merged, so no In-Progress or external blockers remain in the cells below. A blank dependency cell means the ticket has no remaining blocker.

| Order | Ticket | HU | Work | Dependencies |
|---:|---|---|---|---|
| 1 | DES-13 | HU-08 | Multi-device team synchronization | — |
| 2 | DES-43 | HU-32 | Evidence traceability | — |
| 3 | DES-51 | HU-37 | Score ledger for validations, answers, and penalties | — |
| 4 | DES-53 | HU-38 | Apply justified penalties | DES-51 |
| 5 | DES-54 | HU-39 | Real-time session ranking | DES-51 |
| 6 | DES-50 | HU-36B | Post-close review of trivia answers and points | DES-51 |
| 7 | DES-48 | HU-35 | Reveal trivia result and explanation | DES-54 |
| 8 | DES-34 | HU-25A | Operational read queries for administrators and operators | DES-54 |
| 9 | DES-35 | HU-25B | Participant board and ranking queries | DES-54 |
| 10 | DES-33 | HU-24B | Real-time operator panel for events, evidence, and ranking | DES-43, DES-54 |
| 11 | DES-56 | HU-40A | Session event history | DES-53, DES-92 (Backlog enabler — see caveat) |
| 12 | DES-57 | HU-40B | Historical score, ranking, and post-session review | DES-51, DES-54, DES-56 |

## Startable now

Within this Todo-only graph, the current roots are:

- DES-13
- DES-43
- DES-51

All three had DES-42 (and, for DES-43, DES-95) as their last blocker; both merged on 2026-07-14, so all three are unblocked. Note **DES-38 (HU-28) is In Progress** in `session-operations-service`, so any Lane A backend ticket here runs against active work in that same tree — serialize with it. Every other Todo ticket is transitively blocked by the DES-51 → DES-54 chain.

"Startable" means no blocker remains. Before implementation, the driver must still perform its normal preflight against the ticket, repository, and live Linear relations.

## In progress

| Ticket | HU | State | Note |
|---|---|---|---|
| DES-38 | HU-28 Add operational clues during a live session | In Progress | Started 2026-07-14; domain layer committed (`a02ee42`). Was the previous snapshot's second startable root. Lives in `session-operations-service`. |

## Follow-up / check tickets

| Ticket | Kind | Note |
|---|---|---|
| DES-97 | Validate-criteria check | Verify whether HU-31's `TargetResolutionPolicy` naming divergence in PR #215 (ships the Chain-of-Responsibility under different class names than canon) is acceptable or should be reconciled with canon. Not an HU feature; does not count toward the 12. |

## Recently completed (since 2026-07-13 snapshot)

| Ticket | HU | Completed | PR |
|---|---|---|---|
| DES-94 | ENABLER Operator-panel releasable targets | 2026-07-14 | #213 |
| DES-95 | HU-30 Context validation + explained rejection | 2026-07-14 | #214 |
| DES-42 | HU-31 Scan/validate/record QR targets | 2026-07-14 | #215 |
| DES-29 | HU-21 Audit session state changes | 2026-07-13 | #204 |
| DES-36 | HU-26 Manual clue release | 2026-07-13 | — |
| DES-39 | HU-29 Team evidence-submission intake | 2026-07-13 | #206 |
| DES-93 | TreasureHunt substage timer | 2026-07-13 | #205 |

DES-39 was the compile-root that unblocked DES-95 and DES-42. DES-36 unblocked DES-38. DES-42 was the major downstream linchpin — its merge unblocked DES-13, DES-43, and DES-51, the three current roots. DES-40 (HU-30A) and DES-41 (HU-30B) were resolved as **Duplicate** of DES-95.

## Open GitHub queue (4)

These issues have no Linear DES identifier and do not change the 12-ticket HU count.

| Suggested order | Issue | Work | Named prerequisites | Current status |
|---:|---|---|---|---|
| 1 | GH #153 | Plan target geolocation and treasure-hunt participant play slices | None | Startable maintainer-review task. It is GH #156's parent, but GH #156 does not list it as a blocker. |
| 2 | GH #143 | Participant self-registration from the mobile app | GH #137 decision; blocked by GH #141 | Both named prerequisites are closed; startable. |
| 3 | GH #156 | Real map view on the treasure-hunt mobile play surface | Blocked by GH #154 and GH #155 | Both prerequisites are closed; startable. Serialize with GH #143 because both touch mobile. |
| 4 | GH #85 | Rename `identity-access-service` to `users-service` | Explicitly independent | No formal blocker. Schedule in a quiet window after identity work when practical because it mechanically renames a deployable and can conflict with sibling PRs. |

Closed since the previous snapshot: **GH #139** (controller try/catch vs. ProblemDetails) resolved by ADR-0018 (PR #211); **GH #144** (forgot-password flow) closed via PR #212. GH #148 closed 2026-07-13 via PR #207.

This is a **conflict-minimizing suggested order**, not a dependency chain: all four issues are currently startable, and none names an active HU as a blocker.

## Cross-service cautions

- DES-13 spans session operations and identity access.
- DES-34 and DES-35 are cross-service read slices.
- DES-43, DES-48, DES-50, DES-53, and DES-56 span session operations and scoring/monitoring.
- DES-38 (In Progress) lives in session-operations only but enriches the operator-panel surface; serialize other Lane A work with it.
- GH #143 spans identity access and mobile.
- GH #85 is formally independent, but its broad mechanical rename should not overlap sibling backend/identity PRs.

Use `parallel-work-lanes.md` to select combinations that do not edit the same service tree.
