# Refactor Workflow — Active Todo HU Tickets

This document tracks the remaining **5 active HU tickets** for team `umbral-equipo-12` plus the single remaining open GitHub issue (**GH #85**). Every other HU ticket and GitHub issue is **Done**.

Snapshot verified against live Linear and GitHub: **2026-07-14**. DES-99 (HU-37 + HU-39, score ledger + real-time ranking) merged and is **Done**, unblocking the remaining consumer HUs. All other previously-tracked tickets and GitHub issues have landed; see [Done](#done) below.

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

## Active queue (5 HU + 1 GH)

The only remaining dependency edge inside this set is **DES-53 → DES-100** (penalties feed the history surface). The other three HU tickets depend only on DES-99, which is **Done**, so they are startable now. GH #85 is independent and best scheduled in a quiet backend window.

| Order | Ticket | HU | Work | UI surface | Dependencies |
|---:|---|---|---|---|---|
| 1 | DES-53 | HU-38 | Apply justified penalties | Operator penalty form (reason + moment) | — |
| 2 | DES-101 | HU-35+HU-36B | Trivia results on question close (merged) | Participant reveal + operator review | — |
| 3 | DES-35 | HU-25B | Participant board and ranking queries | Participant (mobile), read-only | — |
| 4 | DES-33 | HU-24B | Real-time operator panel for events, evidence, and ranking | Operator dashboard (SignalR) | — |
| 5 | DES-100 | HU-40 | Session history: events, score, ranking, post-session review (merged) | Operator history view | DES-53 |
| 6 | GH #85 | — | Rename `identity-access-service` to `users-service` | — | — (independent) |

## Startable now

- **DES-53**, **DES-101**, **DES-35**, **DES-33** — all remaining blockers are Done.
- **GH #85** — no formal blocker; schedule in a quiet window because its mechanical rename can conflict with sibling PRs.

DES-100 is the only ticket with a remaining in-set blocker (DES-53) and starts once penalties land.

"Startable" means no blocker remains. Before implementation, the driver must still perform its normal preflight against the ticket, repository, and live Linear relations.

## Cross-service cautions

- DES-35 is a cross-service read slice (participant client + reads).
- DES-33, DES-53, DES-101, and DES-100 span session operations and scoring/monitoring (DES-99, their backend source, shipped as scoring/monitoring backend-only).
- GH #85 is formally independent, but its broad mechanical rename should not overlap sibling backend/identity PRs.

Use `parallel-work-lanes.md` to select combinations that do not edit the same service tree.

## Done

Everything below has landed and is no longer in the active queue.

### Score ledger + ranking (2026-07-14)

| Ticket | HU | Note |
|---|---|---|
| DES-99 | HU-37 + HU-39 Score ledger + real-time ranking | Merged workstream (absorbed DES-51/HU-37 + DES-54/HU-39); scoring/monitoring backend-only. Source of score/ranking events for DES-53, DES-101, DES-35, DES-33, DES-100. |

### Session-operations and QR/context chain

| Ticket | HU | Note |
|---|---|---|
| DES-42 | HU-31 Scan/validate/record QR targets | Done |
| DES-95 | HU-30 Context validation + explained rejection | Done (superseded DES-40/DES-41 split) |
| DES-94 | ENABLER Expose releasable targets of active substage | Done |
| DES-38 | HU-28 Add operational clues during a live session | Done |
| DES-43 | HU-32 Evidence traceability | Done |
| DES-13 | HU-08 Multi-device team synchronization | Done |
| DES-29 | HU-21 Audit session state changes | Done (2026-07-13) |
| DES-36 | HU-26 Manual clue release | Done (2026-07-13) |
| DES-39 | HU-29 Team evidence-submission intake | Done (2026-07-13) |
| DES-93 | TreasureHunt substage timer | Done (2026-07-13) |

### Canceled / merged (originals)

| Ticket | HU | Note |
|---|---|---|
| DES-34 | HU-25A Operational read queries | Canceled; residual moved to DES-96 |
| DES-40 | HU-30A Context validations | Duplicate of DES-95 |
| DES-41 | HU-30B Explained rejection | Duplicate of DES-95 |
| DES-51 | HU-37 Score ledger | Merged into DES-99 |
| DES-54 | HU-39 Real-time ranking | Merged into DES-99 |
| DES-56 | HU-40A Session event history | Merged into DES-100 |
| DES-57 | HU-40B Score/ranking history | Merged into DES-100 |
| DES-48 | HU-35 Trivia result reveal | Merged into DES-101 |
| DES-50 | HU-36B Post-close trivia review | Merged into DES-101 |

### GitHub issues

All open GitHub issues except **GH #85** are closed:

| Issue | Work | Note |
|---|---|---|
| GH #139 | Controller try/catch vs global ProblemDetails | Done |
| GH #143 | Participant self-registration from mobile | Done |
| GH #144 | Forgot-password flow end to end | Done |
| GH #148 | Administrator user management + operator-invite UI | Closed 2026-07-13 via PR #207 |
| GH #153 | Plan target geolocation / treasure-hunt play slices | Done |
| GH #156 | Real map view on treasure-hunt mobile play surface | Done |
