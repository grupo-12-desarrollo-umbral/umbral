# HU-31 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

**Nature of this HU:** _feature build on an existing substrate (not greenfield, not a rebuild)._
The `EvidenceSubmission` base, the `IEvidenceIntakeFacade`, the generic validation chain, and the
`EvidenceSubmissionRegistered` outbox are already shipped (HU-29, Done); the concrete-form template is
the trivia form (HU-34, Done). HU-31 **mirrors** the trivia form for the QR/treasure case and adds the
second ordered fact `TargetResolved`. Do **not** authorize rebuilding the base entity, the facade, or
the generic chain. One non-mirror behavior: QR rejection is **retained** (a wrong/duplicate/out-of-context
scan is registered as `Rejected` for audit), unlike trivia's throw.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-31 — Escaneo, validación y registro server-side de objetivo QR | DES-42 | DES-70 | session-operations-service | feature/hu-31-qr-target-scan-resolution | develop |

## Required pattern(s) → owning phase
- `Chain of Responsibility` (phase X.2) — server-side QR target validation is the `TargetResolutionPolicy`
  chain — obligation: ordered links (QR resolves to a Target → Target belongs to the active treasure-hunt
  substage → not already resolved by the team) short-circuit on first failure and supply the rejection
  reason; not one handler with sequential `if` blocks
- `Facade` — **reused, not owned** (existing `IEvidenceIntakeFacade` from HU-29); no new facade
- `Proxy` — **applies-where, no new gate** (participant endpoint inherits `[Authorize(Policy=Participant)]`
  + `AuthorizationBehaviour` + `RuntimeParticipationGuard`)
- Transport: RabbitMQ — `TargetResolved` (+ inherited `EvidenceSubmissionRegistered`) via the existing
  MassTransit bus-outbox after transactional success. No SignalR.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests lock: correct QR → evidence `Accepted` + `TargetResolvedEvent` once (relayed `ScoreValue`); wrong/duplicate/out-of-substage → persisted `Rejected` + reason + **no** `TargetResolved`; `EvidenceSubmissionRegistered` on every registered scan; paused/finished/cancelled rejected via inherited state guard; `resolvedTargets` counter filled; **no `1..100` score check in domain** | — |
| X.2 Application | App build; `RegisterTargetScan` is one slice reusing the existing facade (no second facade); handler + validator tests cover accept + each reject branch with a consistent reason; **Chain of Responsibility verified** — ordered `TargetResolution` links, first-fail reason, reorderable, not sequential `if`s; `TargetResolvedIntegrationEvent` bridges off the resolved fact only; `EvidenceSubmissionRegistered` still fires per scan | `Chain of Responsibility` |
| X.3 Infrastructure | Infra build; `ef migrations add` succeeds for the new `TreasureEvidenceSubmission` owned collection; round-trips through `LiveSessionRepository` with `ValidationState` persisted; integration test proves correct scan → **both** events in the transactional outbox atomically, rejected scan → only `EvidenceSubmissionRegistered`; no second publisher stack | — |
| X.4 Api | Participant `target-scans` endpoint returns acceptance/outcome (no score leak) on a correct scan; wrong/duplicate/out-of-context → consistent RFC 7807; non-admitting session rejected; E2E proves `EvidenceSubmissionRegistered` then `TargetResolved` end-to-end; ADR-0005 aggregate branch coverage ≥95% | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-31)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-31)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-31)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-31)`

Trailer (every phase): `Ref: HU-31` / `Ref: DES-42` / `Ref: DES-70`

## Acceptance criteria
- the system receives the scanned QR/token
- session, team, and mission context are validated before accepting the scan
- the `Target` is validated as belonging to the active treasure-hunt substage (a `MissionNode`), not a `Stage` or a `Clue`
- scans are not accepted when the session is Paused/Finished/Cancelled
- intake is unconditional: every scan registers an `EvidenceSubmission` (`pending`) and publishes `EvidenceSubmissionRegistered` after transactional success
- the scanned QR is validated against the current `Target` of the active substage; a `Clue` is optional guidance, never the validation object
- a correct match resolves the `Target` for the team, moves the evidence to `accepted`, and publishes `TargetResolved` (team, session, valid `MissionNode` of the active substage)
- `TargetResolved` carries the resolved `Target`'s `ScoreValue`
- an incorrect/invalid/duplicate/out-of-context scan auto-rejects (`rejected`) with a consistent reason and publishes no `TargetResolved`
- both events are published to RabbitMQ within the same transaction, without the main flow depending on RabbitMQ

## Endpoints + smoke (driver verifies at Stop 2)
- `POST /api/sessions/{liveSessionId}/participants/target-scans` — participant-auth write, body carries runtime `teamId`, the scanned QR/token value, and optional participation token; correct scan → **200** with acceptance/outcome metadata only (**no score leak** — score travels on the RabbitMQ fact)
- same endpoint with a wrong / duplicate / out-of-context scan → **RFC 7807 rejection**, consistent reason (retained `Rejected`)
- same endpoint while the session is Paused/Finished/Cancelled → **RFC 7807 rejection** (pre-intake block, not registered)
- RabbitMQ: on a correct scan, `EvidenceSubmissionRegistered` **then** `TargetResolved` are observable on the session-operations exchange after transactional success; `TargetResolved` carries `liveSessionId`, `teamId`, `evidenceSubmissionId`, `activeSubstageId`, `targetSnapshotId`, `scoreValue`, `resolvedAt`. On a rejected scan, only `EvidenceSubmissionRegistered` fires.

## Frontend slice
**None** — HU-31 is `backend-only`. There is no client contract in this slice; downstream consumers
(scoring-monitoring/HU-37, audit-history/HU-40A, HU-23 board) consume the async facts + read model.
See Step 9 (downstream contract hand-off) of `prompt_example_feature_hu31.md`. There is no Step 9
frontend plan / Step 9b implementation.
