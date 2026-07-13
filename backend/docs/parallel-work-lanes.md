# Parallel Work Lanes — Active Todo HU Tickets

This plan contains the **21 active, non-archived Linear HU tickets in `Todo`** for `umbral-equipo-12` plus the repository's **7 open GitHub issues**, verified against both live systems on **2026-07-12**.

The conflict rule is simple: two tickets that edit the same service or application tree must not run concurrently. Run one ticket at a time within a lane; parallelize only across disjoint trees.

## Lane heads

| Lane | Primary tree | Startable head | Notes |
|---|---|---|---|
| A | `session-operations-service` | DES-29, DES-36, DES-38, DES-39, or DES-42 | Pick one; this lane is serialized. |
| B | `scoring-monitoring-service` | Wait for DES-42, then DES-51 | The scoring chain begins at the ledger. |
| C | `mission-design-service` | DES-93 phase 1 | DES-93 also needs Lane A for phase 2, so reserve both lanes for the full ticket. |
| D | `identity-access-service` | GH #143, GH #144, GH #85, or DES-13 identity slice | All are startable, but serialize them; GH #85 is best scheduled after identity feature work to reduce rename conflicts. |
| E | `mobile/` | GH #156 | GH #143 and GH #144 also use this tree and are cross-lane. |
| F | `frontend/` | GH #148 | GH #144 also uses this tree and must run alone. |
| G | Backend architecture/docs | GH #139 | Documentation/decision work; check touched files before parallelizing. |
| P | Planning/triage | GH #153 | Startable maintainer-review task; parent of GH #156 but not its blocker. |

## Lane A — session operations

The dependency chains are:

```text
DES-39 → DES-40

DES-42 → DES-41 ─┐
DES-40 ──────────┼→ DES-43 → DES-33
DES-42 ──────────┘

DES-36 → DES-37

DES-42 → DES-51
DES-29 ─┐
DES-36 ─┼→ DES-56 → DES-57
DES-42 ─┤
DES-53 ─┘
```

DES-38 is an independent Lane A root. DES-13 also uses this lane but is cross-lane with identity access. DES-93 uses this lane for its runtime phase and is cross-lane with mission design.

## Lane B — scoring and monitoring

```text
DES-42 → DES-51 → DES-53
                 ├→ DES-50
                 └→ DES-54 → DES-48
                            ├→ DES-34
                            ├→ DES-35
                            └→ DES-33

DES-29 + DES-36 + DES-42 + DES-53 → DES-56
DES-51 + DES-54 + DES-56 → DES-57
```

Run one scoring ticket at a time. Tickets that also touch session operations reserve both Lane A and Lane B for their duration.

## Cross-lane tickets

| Ticket | Trees reserved | Parallelization rule |
|---|---|---|
| DES-13 | session operations + identity access | Run alone with respect to Lanes A and D. |
| DES-34 | cross-service reads | Confirm its generated brief, then reserve every tree it names. |
| DES-35 | cross-service reads + participant client | Confirm its generated brief, then reserve every tree it names. |
| DES-43 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-48 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-50 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-53 | scoring/monitoring + session-authorization boundary | Reserve Lanes A and B. |
| DES-56 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-93 | mission design + session operations | Reserve Lanes A and C for the entire ticket; do not split branches. |
| GH #85 | broad backend deployable/bounded-context rename | Formally independent. Reserve Lane D plus all backend service/config trees named by the issue; schedule after sibling identity work when practical. |
| GH #143 | identity access + mobile | Reserve Lanes D and E. |
| GH #144 | identity access + frontend + mobile | Reserve Lanes D, E, and F; run alone relative to those lanes. |

## Safe scheduling rules

- A ticket confined to Lane A can run beside a ticket confined to Lane B only when neither is listed as cross-lane.
- DES-93 can run beside Lane B work, but not beside any other Lane A or Lane C work.
- DES-13 can run beside Lane B or Lane C work, but not beside any other Lane A or Lane D work.
- GH #148 can run beside backend and mobile work, but not beside GH #144.
- GH #156 can run beside backend and frontend work, but not beside GH #143 or GH #144.
- GH #139 and GH #153 may run beside implementation only after confirming that their documentation scope does not overlap files being edited by another ticket.
- GH #85 has no formal blocker, but should use a quiet backend window because its mechanical rename can conflict with sibling realignment or identity PRs.
- When generated context reveals an additional touched tree, the generated context wins: reserve that tree before starting.

## GitHub dependency audit

The seven open issue descriptions name these prerequisites:

| Open issue | Named prerequisite or relationship | Live result |
|---|---|---|
| GH #85 | Explicitly independent | Startable; coordination hazard only. |
| GH #139 | None | Startable. |
| GH #143 | GH #137 decision and GH #141 blocker | Both closed; startable. |
| GH #144 | GH #141 blocker | Closed; startable. |
| GH #148 | GH #137 decision and GH #142 blocker | Both closed; startable. |
| GH #153 | None | Startable planning/review issue. |
| GH #156 | Parent GH #153; blocked by GH #154 and GH #155 | Both blockers closed. Parent is not declared as a blocker; startable. |

No open GitHub issue names any of the 21 Todo HUs as a blocker. The GitHub ordering is therefore driven by file conflicts and coordination, not by an unresolved dependency edge.

## Complete Todo inventory (21)

DES-13, DES-29, DES-33, DES-34, DES-35, DES-36, DES-37, DES-38, DES-39, DES-40, DES-41, DES-42, DES-43, DES-48, DES-50, DES-51, DES-53, DES-54, DES-56, DES-57, DES-93.

## Complete open GitHub inventory (7)

GH #85, GH #139, GH #143, GH #144, GH #148, GH #153, GH #156.
