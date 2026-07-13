# Parallel Work Lanes — Active Todo HU Tickets

This plan contains the **14 active, non-archived Linear HU tickets in `Todo`** for `umbral-equipo-12` plus the repository's **6 open GitHub issues**. Reconciled against live Linear and `workflow_refactor.md` on **2026-07-13** (~20:33 UTC): DES-29, DES-36, DES-39, and DES-93 completed; DES-37/HU-27 archived; DES-94 (ENABLER) added; **DES-42 (HU-31) and DES-95 (HU-30) moved to In Progress** and left the Todo set; **DES-40 (HU-30A) and DES-41 (HU-30B) resolved as Duplicate of DES-95**. DES-95 replaced the old DES-40/DES-41 split and is not itself in Todo.

The conflict rule is simple: two tickets that edit the same service or application tree must not run concurrently. Run one ticket at a time within a lane; parallelize only across disjoint trees.

## Lane heads

| Lane | Primary tree | Startable head | Notes |
|---|---|---|---|
| A | `session-operations-service` | DES-94 or DES-38 | Pick one; this lane is serialized. DES-42 and DES-95 are already In Progress in this tree — any Lane A ticket runs against active work there. |
| B | `scoring-monitoring-service` | Wait for DES-42 (In Progress), then DES-51 | The scoring chain begins at the ledger. |
| C | `mission-design-service` | — | No active Todo ticket touches this tree; DES-93 completed 2026-07-13. |
| D | `identity-access-service` | GH #143, GH #144, or GH #85 | All are startable, but serialize them; GH #85 is best scheduled after identity feature work to reduce rename conflicts. DES-13 is *not* a valid head here: Linear labels it `svc:session-operations-service` only, and it is blocked by DES-42 (In Progress) + DES-11/DES-12. |
| E | `mobile/` | GH #156 | GH #143 and GH #144 also use this tree and are cross-lane. |
| F | `frontend/` | GH #144 | GH #148 closed 2026-07-13 (PR #207); GH #144 is the remaining frontend head and must run alone. |
| G | Backend architecture/docs | GH #139 | Documentation/decision work; check touched files before parallelizing. |
| P | Planning/triage | GH #153 | Startable maintainer-review task; parent of GH #156 but not its blocker. |

## Lane A — session operations

The dependency chains are (`*` = In Progress, not Todo; an unmet blocker until it merges):

```text
DES-42* ─┐
DES-95* ─┴→ DES-43 → DES-33   (DES-33 also needs DES-54, Lane B)

DES-42* → DES-13
DES-42* → DES-51              (Lane B ledger root)

DES-53 → DES-56 → DES-57      (DES-56 also needs DES-42*)
```

DES-94 and DES-38 are the only startable Lane A roots. DES-40 and DES-41 no longer exist as work — both were resolved as Duplicate of DES-95, which itself is now In Progress. DES-94 touches session-operations only but enriches the operator-panel DTO contract shared with DES-33, so do not run the two concurrently. DES-13 is also a Lane A ticket (Linear labels it `svc:session-operations-service` only; its identity-access framing comes from its membership blockers HU-07A/07B, not from its own tree), but it is blocked by DES-42 (In Progress) and cannot start yet.

## Lane B — scoring and monitoring

```text
DES-42* → DES-51 → DES-53
                  ├→ DES-50
                  └→ DES-54 → DES-48
                             ├→ DES-34
                             ├→ DES-35
                             └→ DES-33

DES-42* + DES-53 → DES-56
DES-51 + DES-54 + DES-56 → DES-57
```

`*` DES-42 is In Progress, not Todo, but still gates the whole ledger chain until it merges. Run one scoring ticket at a time. Tickets that also touch session operations reserve both Lane A and Lane B for their duration.

## Cross-lane tickets

| Ticket | Trees reserved | Parallelization rule |
|---|---|---|
| DES-13 | session operations (labeled `svc:session-operations-service`; membership blockers HU-07A/07B are the identity touchpoint) | Blocked by DES-42 (In Progress) — not startable yet. When unblocked, run alone with respect to Lane A. |
| DES-34 | cross-service reads | Confirm its generated brief, then reserve every tree it names. |
| DES-35 | cross-service reads + participant client | Confirm its generated brief, then reserve every tree it names. |
| DES-43 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-48 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-50 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-53 | scoring/monitoring + session-authorization boundary | Reserve Lanes A and B. |
| DES-56 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| GH #85 | broad backend deployable/bounded-context rename | Formally independent. Reserve Lane D plus all backend service/config trees named by the issue; schedule after sibling identity work when practical. |
| GH #143 | identity access + mobile | Reserve Lanes D and E. |
| GH #144 | identity access + frontend + mobile | Reserve Lanes D, E, and F; run alone relative to those lanes. |

## Safe scheduling rules

- A ticket confined to Lane A can run beside a ticket confined to Lane B only when neither is listed as cross-lane.
- DES-13 is blocked by DES-42 (In Progress) and cannot start yet; when unblocked it can run beside Lane B work, but not beside any other Lane A work.
- GH #148 is closed (done 2026-07-13 via PR #207) — no longer schedulable.
- GH #156 can run beside backend and frontend work, but not beside GH #143 or GH #144.
- GH #139 and GH #153 may run beside implementation only after confirming that their documentation scope does not overlap files being edited by another ticket.
- GH #85 has no formal blocker, but should use a quiet backend window because its mechanical rename can conflict with sibling realignment or identity PRs.
- When generated context reveals an additional touched tree, the generated context wins: reserve that tree before starting.

## GitHub dependency audit

The six open issue descriptions name these prerequisites (all verified against live GitHub 2026-07-13):

| Open issue | Named prerequisite or relationship | Live result |
|---|---|---|
| GH #85 | Explicitly independent | Startable; coordination hazard only. |
| GH #139 | None | Startable. |
| GH #143 | GH #137 decision and GH #141 blocker | Both closed; startable. |
| GH #144 | GH #141 blocker | Closed; startable. |
| GH #153 | None | Startable planning/review issue. |
| GH #156 | Parent GH #153; blocked by GH #154 and GH #155 | Both blockers closed. Parent is not declared as a blocker; startable. |

GH #148 (admin user management + operator-invite UI) closed 2026-07-13 via PR #207 — dropped from the open set. Its prerequisites GH #137 and GH #142 were both closed.

No open GitHub issue names any of the 14 Todo HUs as a blocker. The GitHub ordering is therefore driven by file conflicts and coordination, not by an unresolved dependency edge.

## Complete Todo inventory (14)

DES-13, DES-33, DES-34, DES-35, DES-38, DES-43, DES-48, DES-50, DES-51, DES-53, DES-54, DES-56, DES-57, DES-94.

Not in Todo: DES-42 and DES-95 are In Progress; DES-40 and DES-41 are Duplicate of DES-95.

## Complete open GitHub inventory (6)

GH #85, GH #139, GH #143, GH #144, GH #153, GH #156. (GH #148 closed 2026-07-13 via PR #207.)
