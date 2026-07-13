# DES-93 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this ticket:** feature build (additive), **no HU** — closes the `TreasureHunt` branch of
> DES-77/HU-22 AC#1 (shipped Trivia-only). Every phase **adds** a treasure-hunt substage timer mirroring the
> existing trivia `_questionTimer*` machinery; nothing is torn out and the trivia path is untouched. X.2 is a
> **verify-only pass-through** (no production change expected). Never authorize a subagent to touch question
> activation/advancement or `TriviaRoundOrchestratorFacade` (DES-78), to build treasure-hunt target
> resolution/advancement (HU-29–32), or to add a field to `mission-design-service` (deferred follow-up).
>
> **⚠️ Single-service scope + duration-source deviation:** this slice is `session-operations-service` **only**.
> With `mission-design-service` out of scope there is no per-substage authored duration, so the timer is seeded
> from the **session-level `MaximumTime`**. AC#2 (aligned with the session clock) is fully met; AC#1's literal
> "del `MaximumTime` de la subetapa" is **partially** met — the observable `00:00 / Expired` bug is fixed, the
> per-substage authored field is a deferred `mission-design-service` ticket.
>
> **⚠️ `ready-for-agent` is not yet on DES-93** — Step 2 of the prompt adds it (pass the full existing label set).

## Slice
| Ticket | DES | PRD | Service | Branch | Base |
|--------|-----|-----|---------|--------|------|
| DES-93 — TreasureHunt substage authoritative timer | DES-93 (origin DES-77) | DES-70 | session-operations-service | des-93 | develop |

## Required pattern(s) → owning phase
- **`State`** (phase X.1) — `required_patterns_matrix.md:41,110`: timer behaviour depends on session state (Active vs Paused) — obligation: the treasure-hunt timer's advancing-vs-frozen owned by the per-`SessionState` type via `LiveSessionStateFactory`, **mirroring** the question-timer state methods, not ad-hoc `if (State == …)`.
- **Transport SignalR** (phase X.4) — verify the treasure-hunt `SessionTimerUpdated` broadcast to `live-session:{id}`. **No RabbitMQ.**
- No new `Proxy` gate — the two timer GETs inherit the standard operator / participant-membership `AuthorizationBehaviour`/gateway guard (ADR-0001/0002); DES-93 is not in the applies-where set — note only.

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test locks: active **TreasureHunt** substage → authoritative snapshot is an **advancing** countdown seeded from the session `MaximumTime` (`Remaining > 0`, not `Expired`); `Paused` freezes, `Active` resumes at the frozen remainder; active **Trivia** substage still returns the question window (unchanged); expiry raises **no** advancement event | `State` |
| X.2 Application | App build; test proves the operator + participant timer DTOs return the treasure-hunt advancing remaining (`ActiveQuestion == null`, not `Expired`) for treasure-hunt and the question window for trivia; authz preserved; **pass-through — no production edit expected** | — |
| X.3 Infrastructure | Infra build; a migration **ADDS** the `substage_timer_*` columns (assert via model snapshot); `ListActiveTimersAsync` also selects advancing **substage** timers; repo integration test round-trips a treasure-hunt timer state; worker **broadcasts** treasure-hunt remaining but **does not** `CloseAndAdvanceAsync` on treasure-hunt expiry (report-only; trivia path unchanged) | — |
| X.4 Api | Endpoint tests (operator + participant timer GET → treasure-hunt advancing remaining, not `Expired`; auth enforced) + hub test (treasure-hunt `SessionTimerUpdated` to `live-session:{id}`) + ADR-0005 coverage | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (DES-93)`
- X.2 `feat(session-operations): phase X.2 - application layer (DES-93)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (DES-93)`
- X.4 `feat(session-operations): phase X.4 - api layer (DES-93)`

Trailer (every phase): `Ref: DES-93` / `Ref: DES-77` / `Ref: DES-70`

## Acceptance criteria
- an active `TreasureHunt` substage yields an advancing authoritative countdown (not `Zero`/`Expired`), seeded from the session-level `MaximumTime`, anchored at substage activation
- the HU-23 board counts down on a `TreasureHunt` substage, aligned with the session clock (no board change — behavioural)
- pause freezes the treasure-hunt timer; resume continues from the frozen remainder
- test coverage for the `TreasureHunt` branch of the authoritative snapshot
- **partially met (deviation):** AC#1's literal "del `MaximumTime` de la **subetapa**" — this slice sources the session-level `MaximumTime`; the per-substage authored field is a deferred `mission-design-service` follow-up
- **out of scope (surface, do not build):** the per-substage authored `MaximumTime` field in mission-design (deferred); treasure-hunt auto-advance on expiry (report-only; HU-29–32 own target-resolution advancement); question activation/advancement + trivia round orchestration (DES-78)

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints — the two timer GETs already exist; for a `TreasureHunt` substage their remaining-time **behaviour changes** (`0`/`Expired` → advancing countdown). Behavioural, not a shape change._
After the X.4 docker rebuild (`docker compose build session-operations-service api-gateway && docker compose up -d session-operations-service api-gateway`), smoke through the gateway against a session whose **active substage is TreasureHunt**:
- `GET /api/sessions/{liveSessionId}/timer` — Operator → expect **200**; `RemainingSeconds` decreases across successive reads, `TotalSeconds` reflects the session `MaximumTime`, `Status` not `"Expired"`, `ActiveQuestion` null; shape `SessionTimerSnapshotDto { …, RemainingSeconds, TotalSeconds, Status, ActiveQuestion? }`
- `GET /api/sessions/{liveSessionId}/participants/timer` — participant (team + token) → expect **200** with the same treasure-hunt remaining
- pause (`PATCH …/state` → `Paused`) then re-read → remaining **frozen**; resume (→ `Active`) → countdown continues from the frozen remainder
- confirm a SignalR client on `live-session:{id}` receives `SessionTimerUpdated` carrying the treasure-hunt remaining

## Frontend slice
**None.** The HU-23 board already renders the timer and starts counting when the snapshot is non-zero — the DTO shape does not change. See Step 9 of `prompt_example_feature_des93.md` (and the optional frontend follow-up to remove HU-23's temporary "hide the chip in TreasureHunt" workaround). No Step 9b.
