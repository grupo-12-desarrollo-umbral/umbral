# Parallel Work Lanes — Active Todo HU Tickets

This plan contains the remaining **5 active Linear HU tickets** for `umbral-equipo-12` plus the single remaining open GitHub issue (**GH #85**). Every other HU ticket and GitHub issue is **Done**. Reconciled against live Linear and `workflow_refactor.md` on **2026-07-14** after DES-99 (HU-37 + HU-39, score ledger + real-time ranking) merged and unblocked the remaining consumer HUs.

The conflict rule is simple: two tickets that edit the same service or application tree must not run concurrently. Run one ticket at a time within a lane; parallelize only across disjoint trees.

## Active set

| Ticket | HU | Primary tree(s) | UI surface |
|---|---|---|---|
| DES-53 | HU-38 Apply justified penalties | `scoring-monitoring-service` (+ session-authorization boundary) | Operator penalty form |
| DES-101 | HU-35 + HU-36B Trivia results on question close | `scoring-monitoring-service` + `session-operations-service` | Participant reveal + operator review |
| DES-35 | HU-25B Participant board and ranking queries | cross-service reads + participant client | Participant (mobile), read-only |
| DES-33 | HU-24B Real-time operator panel | `scoring-monitoring-service` + `session-operations-service` | Operator dashboard (SignalR) |
| DES-100 | HU-40 Session history | `scoring-monitoring-service` + `session-operations-service` | Operator history view |
| GH #85 | Rename `identity-access-service` to `users-service` | broad backend deployable/bounded-context rename | — |

## Dependency graph

The only remaining edge inside this set is **DES-53 → DES-100**. DES-99 (the ledger + ranking source) is **Done**, so DES-101, DES-35, and DES-33 have no remaining blocker.

```text
DES-53 → DES-100

DES-101   (no remaining blocker — DES-99 Done)
DES-35    (no remaining blocker — DES-99 Done)
DES-33    (no remaining blocker — DES-99 Done)
```

## Lane heads

| Lane | Primary tree | Startable head | Notes |
|---|---|---|---|
| B | `scoring-monitoring-service` | DES-53 | Scoring backend + consumers. Serialize; DES-101, DES-33, DES-100 also touch this tree. |
| A | `session-operations-service` | DES-101 / DES-33 | Cross-lane with Lane B; reserve both trees for their duration. |
| E | `mobile/` | DES-35 | Cross-service participant read slice. |
| D | `identity-access-service` | GH #85 | Independent; schedule in a quiet backend window to reduce rename conflicts. |

## Cross-lane tickets

| Ticket | Trees reserved | Parallelization rule |
|---|---|---|
| DES-53 | scoring/monitoring + session-authorization boundary | Reserve Lanes A and B. |
| DES-101 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-33 | session operations + scoring/monitoring | Reserve Lanes A and B. |
| DES-100 | session operations + scoring/monitoring | Reserve Lanes A and B; starts after DES-53. |
| DES-35 | scoring/monitoring **read side** + participant client (`mobile/`) | Touches the scoring tree only for the ranking **read** endpoint. Merge-safe beside DES-53 (see below). Confirm its generated brief before reserving additional trees. |
| GH #85 | broad backend deployable/bounded-context rename | Formally independent. Reserve Lane D plus all backend service/config trees named by the issue; schedule in a quiet window. |

## DES-53 ‖ DES-35 — parallel-safe pair

DES-53 and DES-35 both touch `scoring-monitoring-service`, but they edit **disjoint files** within it, so they are **merge-safe to run in parallel**:

- **DES-53 (write side):** `Domain` Penalty entity + `IPenaltyPolicy`, `Application/Penalties/Commands/*` (auto-registered via MediatR scan), `Api/Controllers/PenaltyController.cs` (new), Penalty EF config + a new migration + `DbSet<Penalty>` on the DbContext.
- **DES-35 (read side):** reuses the existing `Application/Rankings/Queries/GetRankingSnapshot` (shipped by DES-99) and the existing `ParticipantOrOperator` auth policy — no new registration; adds a participant read action (new `BoardController` or an action on `RankingController`); bulk of the work is in `mobile/`.

The DbContext, migrations, controllers, and mobile client are all non-overlapping. The only theoretically shared file is `Application/DependencyInjection.cs` (DES-53 adds one `AddScoped<IPenaltyPolicy>` line; DES-35 most likely edits nothing there) — at worst a one-line, trivially-resolved conflict.

> **Caveat — not a merge conflict:** the two share the same `.csproj`/build and the same integration-test database. Running both test suites concurrently against one scoring-monitoring instance can cause **test interference** (DES-53's penalty writes shift the ranking DES-35 asserts on). Use separate worktrees/DBs to keep the suites isolated. Git-merge remains clean either way.

## Safe scheduling rules

- The scoring/monitoring tree is the hot spot for the **write-side** tickets: DES-53, DES-101, DES-33, and DES-100 all mutate it, so run those **one at a time** — they cannot parallelize with each other.
- **Exception:** DES-35 is read-only against the scoring tree and edits disjoint files, so it **is** parallel-safe beside DES-53 (see the pair section above), subject to the shared-DB test caveat.
- DES-100 is blocked by DES-53 and cannot start until penalties land.
- GH #85 has no formal blocker, but should use a quiet backend window because its mechanical rename can conflict with sibling realignment or identity PRs.
- When generated context reveals an additional touched tree, the generated context wins: reserve that tree before starting.

## Done

All other HU tickets and GitHub issues have landed. Highlights:

- **DES-99** (HU-37 + HU-39 score ledger + real-time ranking) — merged; the backend source for DES-53, DES-101, DES-35, DES-33, DES-100. Absorbed DES-51/DES-54.
- **Session-operations / QR / context chain:** DES-42, DES-95, DES-94, DES-38, DES-43, DES-13, DES-29, DES-36, DES-39, DES-93 — all Done.
- **Canceled / merged originals:** DES-34 (→ DES-96), DES-40/DES-41 (dup of DES-95), DES-51/DES-54 (→ DES-99), DES-56/DES-57 (→ DES-100), DES-48/DES-50 (→ DES-101).
- **GitHub:** GH #139, GH #143, GH #144, GH #153, GH #156 closed; GH #148 closed 2026-07-13 via PR #207. Only **GH #85** remains open.
