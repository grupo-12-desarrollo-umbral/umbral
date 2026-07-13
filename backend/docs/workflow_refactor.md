# Refactor Workflow — Active Todo HU Tickets

This document tracks the **21 active, non-archived HU tickets in Linear's `Todo` state** for team `umbral-equipo-12` plus the repository's **7 open GitHub issues**.

Snapshot verified against live Linear and GitHub: **2026-07-12**. Completed, canceled, archived, and Linear Backlog work does not belong in the active queues below.

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

## Active queue (21)

Dependencies shown here include only other tickets in this 21-ticket Todo set. A blank dependency cell means the ticket has no remaining blocker inside this set.

| Order | Ticket | HU | Work | Todo dependencies |
|---:|---|---|---|---|
| 1 | DES-29 | HU-21 | Audit session state changes | — |
| 2 | DES-42 | HU-31 | Scan, validate, and record QR targets server-side | — |
| 3 | DES-39 | HU-29 | Team evidence-submission intake | — |
| 4 | DES-40 | HU-30A | Context validation for evidence acceptance | DES-39 |
| 5 | DES-41 | HU-30B | Explained rejection of invalid evidence | DES-42 |
| 6 | DES-43 | HU-32 | Evidence traceability | DES-40, DES-41, DES-42 |
| 7 | DES-36 | HU-26 | Manual clue release | — |
| 8 | DES-38 | HU-28 | Add operational clues during a live session | — |
| 9 | DES-37 | HU-27 | Rule-driven clue release | DES-36 |
| 10 | DES-51 | HU-37 | Score ledger for validations, answers, and penalties | DES-42 |
| 11 | DES-53 | HU-38 | Apply justified penalties | DES-51 |
| 12 | DES-54 | HU-39 | Real-time session ranking | DES-51 |
| 13 | DES-50 | HU-36B | Post-close review of trivia answers and points | DES-51 |
| 14 | DES-48 | HU-35 | Reveal trivia result and explanation | DES-54 |
| 15 | DES-34 | HU-25A | Operational read queries for administrators and operators | DES-54 |
| 16 | DES-35 | HU-25B | Participant board and ranking queries | DES-54 |
| 17 | DES-33 | HU-24B | Real-time operator panel for events, evidence, and ranking | DES-43, DES-54 |
| 18 | DES-56 | HU-40A | Session event history | DES-29, DES-36, DES-42, DES-53 |
| 19 | DES-57 | HU-40B | Historical score, ranking, and post-session review | DES-51, DES-54, DES-56 |
| 20 | DES-13 | HU-08 | Multi-device team synchronization | — |
| 21 | DES-93 | HU-23 follow-up | Treasure-hunt substage timer | — |

## Startable now

Within this Todo-only graph, the current roots are:

- DES-13
- DES-29
- DES-36
- DES-38
- DES-39
- DES-42
- DES-93

“Startable” means no blocker remains inside the active Todo set. Before implementation, the driver must still perform its normal preflight against the ticket, repository, and live Linear relations.

## Open GitHub queue (7)

These issues have no Linear DES identifier and do not change the 21-ticket HU count.

| Suggested order | Issue | Work | Named prerequisites | Current status |
|---:|---|---|---|---|
| 1 | GH #139 | Decide controller try/catch versus global ProblemDetails handling | None | Startable documentation decision. Prefer resolving before new endpoints copy an unsettled convention. |
| 2 | GH #153 | Plan target geolocation and treasure-hunt participant play slices | None | Startable maintainer-review task. It is GH #156's parent, but GH #156 does not list it as a blocker. |
| 3 | GH #143 | Participant self-registration from the mobile app | GH #137 decision; blocked by GH #141 | Both named prerequisites are closed; startable. |
| 4 | GH #144 | Forgot-password flow end to end | Blocked by GH #141 | Prerequisite is closed; startable. Serialize with GH #143 because both touch identity and mobile. |
| 5 | GH #148 | Administrator user management and operator-invite UI | GH #137 decision; blocked by GH #142 | Both named prerequisites are closed; startable. |
| 6 | GH #156 | Real map view on the treasure-hunt mobile play surface | Blocked by GH #154 and GH #155 | Both prerequisites are closed; startable. Serialize with GH #143/GH #144 because they touch mobile. |
| 7 | GH #85 | Rename `identity-access-service` to `users-service` | Explicitly independent | No formal blocker. Schedule in a quiet window after identity work when practical because it mechanically renames a deployable and can conflict with sibling PRs. |

This is a **conflict-minimizing suggested order**, not a dependency chain: all seven issues are currently startable, and none names an active HU as a blocker.

## Cross-service cautions

- DES-13 spans session operations and identity access.
- DES-34 and DES-35 are cross-service read slices.
- DES-43, DES-48, DES-50, DES-53, and DES-56 span session operations and scoring/monitoring.
- DES-93 spans mission design and session operations and must remain one ticket/branch.
- GH #143 spans identity access and mobile.
- GH #144 spans identity access, frontend, and mobile.
- GH #85 is formally independent, but its broad mechanical rename should not overlap sibling backend/identity PRs.

Use `parallel-work-lanes.md` to select combinations that do not edit the same service tree.
