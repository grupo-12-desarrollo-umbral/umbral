# HU-18 Context - Asociacion de equipos a sesiones

> Paste this section into any agent session that needs context for HU-18.
> Last updated: 2026-06-03 | Branch: `feature/hu-18-asociacion-de-equipos-a-sesiones`

## State

- DES-25 (HU-18): **Todo**, labels: `Feature`, `ready-for-agent`, `svc:session-operations-service`
- Same-service predecessors already resolved: DES-11 (HU-07A) **Done**, DES-12 (HU-07B) **Done**, DES-23 (HU-16) **Done**, DES-26 (HU-19) **In Progress**
- DES-70 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:session-operations-service`
- Branch: `feature/hu-18-asociacion-de-equipos-a-sesiones` (branch from `feature/hu-19-asignacion-de-operador-a-sesion` while DES-26 remains in progress; otherwise from `develop`)

## Required design patterns

- `Facade`
  - Why: team assignment coordinates runtime session state changes and persistence.
  - Phase owner: X.2 Application
  - Concrete obligation: expose team association through one orchestration entry point that coordinates session lookup, team validation against Identity-owned reference data, assignment persistence, and pre-start readiness rules. Do not spread that coordination across endpoints or ad-hoc handlers.

## What predecessors have already landed

The predecessor fast path is partial for `session-operations-service`: there is
no dedicated `backend/docs/hu16-context.md` or `backend/docs/hu19-context.md`,
and `backend/services/session-operations-service/README.md` is still sparse. The
strongest same-service documentation comes from `hu07a-context.md` and
`hu07b-context.md`, plus live Linear state for HU-16 and HU-19.

**Domain layer**
- `SessionOperations` already owns `LiveSession`, runtime `Team`,
  `SessionParticipant`, `JoinContext`, and the final admission decision for a
  live session.
- HU-07B established that reconnect, late join, capacity, assignment, and
  runtime restoration are session-owned rules, not Identity-owned rules.
- HU-16 is done and establishes the session-creation baseline from exactly one
  trivia source with a frozen source copy and one authoritative `LiveSession`
  backbone.
- Identity-side team reference data and participant/team membership facts already
  exist, but they remain foreign inputs. `SessionOperations` must not reuse the
  Identity `Team` aggregate as its own runtime entity.

**Application layer**
- The service boundary already expects session-owned orchestration for runtime
  decisions and admission, with Identity consumed through access-fact or
  reference-data ports rather than as a co-owner of the session.
- HU-07B documented `JoinPolicy` and session-owned runtime checks as the
  application/domain seam for participation rules.
- HU-16 implies session creation orchestration already exists or is the current
  service baseline to extend rather than recreate.

**Infrastructure / API**
- `session-operations-service` is already the owner of the runtime API/hub
  surface for live admission and session coordination.
- HU-07B set the expectation that session runtime concerns can require durable
  persistence and thin transport surfaces in this service.
- No documented predecessor context file captures the current persistence shape
  for session-owned teams yet; verify the existing repository/schema before
  adding new tables or migrations.

**Frontend**
- The current participant-side session entry flow from HU-07A/HU-07B is already
  separated from Identity validation and session runtime admission.
- No dedicated frontend context file exists yet for the operator-side session
  setup flow; HU-18 is the slice that should make operator team assignment to a
  scheduled session concretely plannable.

**Coverage:** no predecessor context file records a verified aggregate coverage
percentage for `session-operations-service`; verify the real percentage during
phase X.4 rather than assuming it from earlier services.

## What HU-18 adds on top (per PRD DES-70 and DES-25)

| Concern | New work |
|---|---|
| Team association use case | Allow an operator to add registered, active teams to a scheduled session before start, extending the existing `LiveSession` setup flow rather than recreating session creation. |
| Session-owned runtime team list | Persist which teams are associated with a session and expose a read surface to query them by session. |
| Pre-start readiness invariant | Ensure a session cannot start with zero associated teams; establish the invariant in a reusable session-owned seam rather than leaving it as endpoint-only validation. |
| Cross-context team validation | Validate that the referenced teams come from the Identity-owned team registry and are active, while keeping ownership of the runtime session team association inside `SessionOperations`. |
| Backend contract | Add the operator-facing backend contract needed to associate teams to a session and list the associated teams without leaking cross-context persistence details. |
| Frontend flow | Add or plan the operator session-setup UI for assigning teams and reviewing the current team list before session start. |

## Touched surfaces

- `backend/services/session-operations-service`
- `frontend/` operator session setup / assignment UI
- `backend/frontend` API contract boundary: session team-association command/query payloads and error shape

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| - | - | No commits yet |

## Known quirks / gotchas

- `HU-18` is mapped to `Facade`, not `Proxy`. Standard authorization still
  applies, but this slice must not invent a new pattern gate beyond the
  mandated orchestration seam.
- The runtime `Team` in `SessionOperations` is not the Identity reference-data
  `Team` aggregate. Correlate on the foreign team identifier or source identity,
  but do not import Identity domain ownership into this service.
- `DES-26` (HU-19) is currently **In Progress**, so the generator rule sets the
  branch base to `feature/hu-19-asignacion-de-operador-a-sesion` while that work
  remains unmerged. Re-resolve before implementation if DES-26 merges first.
- `HU-16` is the landed baseline for session creation. HU-18 should extend that
  backbone with team association, not reopen source-selection or snapshot scope.
- The acceptance criterion "a session cannot start if it has no associated
  teams" lands before the dedicated lifecycle slice (`HU-21A`). Implementers
  should realize it as a session-owned invariant or readiness guard that the
  later lifecycle work can reuse, not by inventing the whole transition model
  early.
