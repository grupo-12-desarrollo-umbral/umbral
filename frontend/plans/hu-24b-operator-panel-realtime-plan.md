# HU-24B — Operator panel real-time: events, evidence/submissions, ranking (DES-33)

## Context

DES-33 (HU-24B) asks to extend the **operator panel** so that during a live session it reflects,
in real time via SignalR/WebSockets and without manual reload: (a) relevant events, (b)
evidence/submissions, and (c) ranking — all restricted to authorized sessions.

An assessment of the current codebase shows this is **not** a pure frontend-wiring task. Two of
the three concerns need backend work as well:

- **Events** — already pushed real-time and operator-scoped over `/hubs/sessions`. The frontend
  already subscribes to all of them but has no *consolidated feed* (only a hardcoded mock). → frontend only.
- **Evidence/submissions** — REST-only today. Evidence domain events are published to RabbitMQ
  integration events but **never broadcast over SignalR**, and there is no frontend surface. → backend push + frontend.
- **Ranking** — `/hubs/scoring` already pushes `RankingChanged`, but **both the hub join and the
  REST endpoint require a `teamId` and validate team membership** (`RankingSessionMembershipGuard`),
  so only participants can access it. There is **no operator path** to session-wide standings, and
  the frontend has no `/hubs/scoring` client at all. → backend operator access + frontend.

Decisions confirmed with the user: evidence delivered via **new SignalR push**; ranking gets a new
**operator-scoped access path**; the events feed is **derived client-side** from events already received.

Note: an earlier handoff doc (`backend/docs/hu33b-generation-handoff-2026-07-08.md`) calls
`scoring-monitoring-service` an empty skeleton — that is **stale**. The service is fully built
(129 `.cs` files, `ScoringHub` at `/hubs/scoring`, ranking aggregate + broadcaster).

Relevant repo skill: `signalr-websockets-aspnetcore` (under `backend/.claude/skills`) documents the
intended hub/broadcaster/group/auth patterns — follow it for all backend real-time work below.

---

## Execution order (STRICT — do in this exact sequence)

Work items are lettered by concern (A/B/C/D), **not** by execution order. Build them in the numbered
order below. Each step is a gate: do **not** start the next step until the current one is implemented,
tested, and verified end-to-end (see Verification). Do not parallelize.

1. **Work item B — Operator ranking.** Highest value and backend auth is already solved
   (`IScoringSessionAccessResolver`). Deliver the operator hub join + REST path, then `RankingPanel`.
2. **Work item A — Evidence/submissions real-time push.** The one true backend gap. Deliver the
   broadcaster + handlers (keeping RabbitMQ publishers), then `EvidenceSubmissionsPanel`.
3. **Work item C — Live events feed.** Cheap once A's evidence events exist; frontend-only. Replaces
   the static mock.
4. **Work item D — Operator panel freshness (OPTIONAL).** Only after 1–3 are done and green. Skip
   entirely if descoping polish.

Optional feed enrichment (discrete clue/penalty lines) and the RF-15 audit read-path are **out of scope**
for this ticket regardless of the above (separate tickets).

---

## Work item A — Evidence/submissions real-time push  ·  STEP 2

### Backend (`session-operations-service`)
Mirror the existing operator-only "team answered" path (`SignalRTeamAnsweredBroadcaster` +
`TeamAnsweredNotificationHandler`), which already broadcasts to the operator-only group
`live-session-operators:{id}`.

- Add a broadcaster interface + impl in `src/Api/Hubs/` (e.g. `SignalREvidenceSubmissionBroadcaster.cs`)
  using `IHubContext<SessionsHub>`, emitting events (e.g. `EvidenceSubmissionRegistered`,
  `EvidenceSubmissionResolved`) to group `live-session-operators:{id}`. Register in
  `src/Api/DependencyInjection.cs` alongside the other broadcasters (lines ~30–36).
- Add MediatR notification handlers in `src/Application/Sessions/EventHandlers/` for the existing
  domain events `EvidenceSubmissionRegisteredEvent`, `EvidenceSubmissionAcceptedEvent`,
  `EvidenceSubmissionRejectedEvent` that call the new broadcaster. (These events currently have only
  `PublishEvidenceSubmission*IntegrationEventHandler` handlers — add the SignalR handler beside them;
  do not remove the RabbitMQ ones.)
- Payload DTO: reuse the shape returned by the existing operator read surface
  `GET {liveSessionId}/evidence-submissions` (`SessionsController.cs:417` →
  `GetOperatorEvidenceTraceQuery`) so snapshot and push agree.

### Frontend
- Extend the session hub client `frontend/app/lib/realtime/session-state-client.ts` to subscribe to
  the two new events (it already subscribes to the 7 existing session events).
- Build an evidence monitor panel `frontend/app/dashboard/EvidenceSubmissionsPanel.tsx`, wired into
  `DashboardClient.tsx` reducer state (same pattern as `AnsweredMonitorPanel`). On connect/reconnect,
  fetch a REST snapshot via a new server action calling `GET .../evidence-submissions` (HU-32
  endpoint already exists), then apply live pushes on top. Reuse the unused `.evidenceThumb` style in
  `dashboard.module.css`.

## Work item B — Operator ranking  ·  STEP 1

### Backend (`scoring-monitoring-service`)
Add an operator-scoped path that authorizes via operator access instead of team membership.
**The authorization is already solved** — reuse the existing `IScoringSessionAccessResolver`
(`src/Application/Scores/Common/Authorization/ScoringSessionAuthorizationProxy.cs`), which the penalty
flow (`ApplyPenaltyCommandHandler`) already uses. `EnsureAccessAsync(liveSessionId)` allows
Administrators, allows an Operator **assigned to that session** (checked against the local
`SessionOperatorAssignmentProjection`, kept in sync by `LiveSessionOperatorAssignedConsumer` from
session-ops integration events), and otherwise throws `ForbiddenAccessException`. No new resolver and
no `IParticipantSessionMembershipClient` change are needed.

- `ScoringHub` (`src/Api/Hubs/ScoringHub.cs`): add `JoinSessionGroupAsOperatorAsync(Guid liveSessionId)`
  that seeds `_currentUserContext.Principal = Context.User` (same fallback the existing
  `JoinSessionGroup` uses), calls `IScoringSessionAccessResolver.EnsureAccessAsync(liveSessionId)`,
  then adds the connection to the existing `live-session:{id}` group (which already receives
  `RankingChanged`). Keep the participant `JoinSessionGroup(liveSessionId, teamId)` untouched.
- REST: add an operator query path — an operator endpoint on `RankingController.cs` (or a `teamId`-less
  `GetRankingSnapshotQuery` overload) guarded by `IScoringSessionAccessResolver` instead of the
  team-membership guard. Reuse `RankingSnapshotDtoFactory` / `IRankingRepository.GetByLiveSessionIdAsync`
  (the repo fetch is already session-scoped, not team-scoped — only the guard was team-scoped).

### Frontend
- New SignalR client `frontend/app/lib/realtime/ranking-client.ts` for `/hubs/scoring` (build URL from
  `NEXT_PUBLIC_API_GATEWAY_URL + '/hubs/scoring'`, reuse `getHubAccessToken`/`/api/realtime/hub-token`
  and the `withAutomaticReconnect` pattern from `session-state-client.ts`). On start call the new
  operator join method; subscribe `RankingChanged`; re-join on `onreconnected`.
- Build `frontend/app/dashboard/RankingPanel.tsx` — a ranked leaderboard. **RB-08 compliance:** render
  strictly by the backend `RankingRowDto.Position` (desc score, resolution-time tiebreak already applied
  by `ResolutionTimeRankingPolicy`); do NOT re-sort client-side by teamCode the way
  `OperatorTeamProgressPanel` does — that would break the required ordering. `RankingRowDto` already
  carries `Position`, `TotalScore`, `ResolutionTime`, `TeamDisplayName`. Fetch a REST snapshot on
  connect/reconnect as fallback. Reuse the unused `.leaderboard` style in `dashboard.module.css`.

## Work item C — Consolidated live-events feed (frontend only)  ·  STEP 3

- Build `frontend/app/dashboard/SessionEventsFeedPanel.tsx` that renders a session-scoped, reverse-chron
  feed derived from the events the frontend already receives on `/hubs/sessions`
  (`SessionStateChanged`, `QuestionActivated`, `QuestionClosed`, `SubstageAdvanced`, `TeamAnswered`)
  plus the new evidence events from Work item A. Maintain a bounded list in `DashboardClient.tsx`
  reducer state. Replace the hardcoded static "Recent global activity" mock (`DashboardClient.tsx`
  ~lines 131–137 / 1604–1624) for the operator/session context.
- This feed is **ephemeral** (client-side, resets on reload). That satisfies AC#1/#5 ("refleja eventos
  … sin recarga manual"), which require live updates, not persisted history.
- **RF-13 / §6.6 — clue release is ALREADY surfaced (optional feed enrichment only).** Clue release is
  present in the operator dashboard today: `OperatorClueReleasePanel` (the release action), a per-team
  `releasedClueCount` column in `OperatorTeamProgressPanel` (carried in the real-time
  `OperatorSessionPanelUpdated` payload, `session-state-client.ts:207`), and a local "Clue released"
  toast for the acting operator. Since a session has a single assigned operator (RB-10), that operator
  is the one releasing clues; the only nuance is refreshing their own count promptly (Work item D,
  client-side refetch) — not a missing surface. **Optional:** the consolidated feed could additionally show discrete
  "clue released" / "penalty applied" lines for a richer event log — but this is enrichment, not a
  requirement gap. Penalties similarly already have `PenaltyPanel`; a discrete feed line would need a
  `PenaltyApplied` broadcast on `ScoringHub`'s `live-session:{id}` group. Treat both as nice-to-have.

### Note: a persistence substrate already exists (future upgrade path, out of scope here)
`scoring-monitoring-service` already persists a `SessionEventHistory` (append-only, deduped, indexed by
`live_session_id, occurred_at`, written by `SessionEventHistoryConsumer` from RabbitMQ integration
events). This is the **RF-15 audit substrate** and its *existence* satisfies RF-15. It is currently
**write-only** (`ISessionEventHistoryRepository` exposes only `AppendAsync`, no read/query, no endpoint)
and captures a narrow set (`SessionStateChanged`, `QuestionClosed`, `SessionResultsFinalized` — not
substage/team-answered/evidence). RF-15 (persisted audit log, scoring/monitoring context) and RF-13/§6.6
(live operator feed, this ticket) are **distinct requirements** — HU-24B does not need to touch this
store. If an auditable, reload-surviving, operator-readable log is later wanted, the incremental work is:
add a read method + query + operator endpoint (guarded by `IScoringSessionAccessResolver`), broaden the
consumed event types, and seed the feed from it on connect. Deferring this keeps HU-24B scoped to the
live-feed AC.

## Work item D — Operator panel freshness (OPTIONAL polish; client-side refetch, single-operator model)  ·  STEP 4

**Domain fact:** a live session has **exactly one (or zero) assigned operator** — `LiveSession.AssignedOperatorUserId`
is a single nullable scalar and `AssignOperator` replaces it (RB-10). So the `live-session-operators:{id}`
group only ever holds that one operator's connection(s), and there is no "other operator sees stale data"
scenario.

`BroadcastOperatorSessionPanelNotificationHandler` (`session-operations-service/src/Application/Sessions/EventHandlers/`)
only re-pushes `OperatorSessionPanelUpdated` on `SessionStateChangedEvent` and `SubstageAdvancedEvent`, so
two per-team fields in that payload (`score`, `releasedClueCount`) can lag between those events. Because
the sole operator is also the one taking the action (releasing a clue, etc.), the simplest fix is
**client-side**: after a successful `releaseClueAction` (and other operator actions), refetch the operator
panel snapshot (`loadOperatorPanel`) so the acting operator's own view reflects the change immediately —
no backend rebroadcast needed. A backend trigger (adding `ClueReleasedEvent` / `AnswerRegisteredEvent` to
the handler) is an alternative but is optional given the single-operator model. Either way this is polish,
not an AC requirement.

---

## Critical files

Backend — session-operations-service:
- `src/Api/Hubs/SessionsHub.cs`, `src/Api/Hubs/SignalRTeamAnsweredBroadcaster.cs` (pattern to copy)
- `src/Api/DependencyInjection.cs` (broadcaster registration)
- `src/Application/Sessions/EventHandlers/` (add evidence SignalR handlers; extend panel-rebroadcast triggers)
- `src/Api/Controllers/SessionsController.cs:417` (`GetOperatorEvidenceTraceQuery` — snapshot shape)

Backend — scoring-monitoring-service:
- `src/Api/Hubs/ScoringHub.cs`, `src/Api/Hubs/RankingBroadcaster.cs`
- `src/Api/Controllers/RankingController.cs`
- `src/Application/Scores/Common/Authorization/IScoringSessionAccessResolver.cs` + `ScoringSessionAuthorizationProxy.cs` (**reuse** for operator auth)
- `src/Application/Rankings/Common/RankingSnapshotDtoFactory.cs`, `src/Application/Rankings/Queries/GetRankingSnapshot/*`
- (event-log upgrade only) `src/Application/Common/Interfaces/ISessionEventHistoryRepository.cs`, `src/Application/SessionEvents/Consumers/SessionEventHistoryConsumer.cs`

Frontend:
- `frontend/app/lib/realtime/session-state-client.ts` (extend), new `ranking-client.ts`
- `frontend/app/dashboard/DashboardClient.tsx` (wire panels + feed into reducer/effects)
- new `EvidenceSubmissionsPanel.tsx`, `RankingPanel.tsx`, `SessionEventsFeedPanel.tsx`
- `frontend/app/dashboard/dashboard.module.css` (`.evidenceThumb`, `.leaderboard`)
- `frontend/app/actions/sessions.ts` (new server actions for evidence + ranking REST snapshots)

---

## Verification

Backend:
- Unit/integration tests for the new broadcasters + handlers (follow `aspnet-backend-testing` skill;
  mirror existing `SignalRTeamAnswered*` / ranking broadcast tests). Assert evidence events reach the
  operator-only group and never the participant group; assert operator ranking join is denied for
  non-operators and allowed for authorized operators.
- Run the relevant service test suites via `backend/Makefile` targets.

End-to-end (real app):
1. Bring up the stack (`backend/docker-compose*`), sign in as an operator, open `/dashboard`, select a live session.
2. Confirm the operator SignalR connections establish: `/hubs/sessions` (existing) and `/hubs/scoring` (new operator join succeeds; a participant token cannot use the operator join).
3. Drive gameplay in another client: submit/accept/reject evidence → operator evidence panel updates with no reload; the events feed shows entries; ranking panel re-orders on `RankingChanged`.
4. Verify authorization: a non-operator or an operator not assigned to the session gets the unauthorized state and receives no pushes.
5. Kill and restore the connection → panels refetch REST snapshots on reconnect and stay consistent.

Use the `verify` and `signalr-websockets-aspnetcore` skills during implementation.

---

## Requirements traceability (`docs/proyecto_requisitos_documento_clave.md`)

| Req | Requirement | Status in this plan |
|---|---|---|
| §6.4 / §6.6 | Panel operador: estado global + ranking; supervisión en tiempo real | Covered — Work items A/B/C |
| RF-09 | Evidencia con fecha, equipo, sesión, estado de validación | Covered — `EvidenceTraceItemDto` already carries all fields; evidence panel surfaces them |
| RF-12 | Ranking mostrado y actualizado en tiempo real | Covered — Work item B |
| RF-13 | Panel operador refleja cambios de estado y eventos relevantes en tiempo real | Covered — state/question/substage/team-answered + evidence. Clue release already surfaced (release panel + `releasedClueCount` column + toast). Single assigned operator per session (RB-10), so no cross-operator staleness; the acting operator's own count freshness is a trivial client-side refetch (Work item D, optional). Discrete clue/penalty feed lines are optional enrichment |
| RF-14 / RNF-05 | Publicar eventos a RabbitMQ al registrar evidencias/cambios | Preserved — Work item A keeps the existing integration-event publishers; SignalR is additive |
| RF-15 | Historial de eventos de la sesión para auditoría | **Existence satisfied outside HU-24B** by `SessionEventHistory` (append-only, deduped via `BuildSourceEventKey`, persisted, indexed by `live_session_id, occurred_at`, fed from RabbitMQ by `SessionEventHistoryConsumer`). RF-15 doesn't mandate a read endpoint, so it's defensibly met. Verified limits: captures only 3 event types (`SessionStateChanged`, `QuestionClosed`, `SessionResultsFinalized`) and has **no read path** (`ISessionEventHistoryRepository` = `AppendAsync` only). Broadening captured types + adding a read/query endpoint would strengthen auditability — separate ticket in the scoring/monitoring context, **not HU-24B** (whose ACs are the live operator panel per RF-13/§6.6) |
| RF-16 / §8 | Diferenciación por rol | Covered — operator-only groups + `IScoringSessionAccessResolver` |
| RB-08 | Ranking desc por puntaje, desempate por tiempo de resolución | Covered — backend `ResolutionTimeRankingPolicy` + `RankingRowDto.Position`; frontend renders by `Position` (see Work item B) |
| RB-10 | Operador solo administra sesiones asignadas/visibles | Covered — `ScoringSessionAuthorizationProxy` checks assigned operator; session-ops operator group already gated |
| RNF-03 | Tiempo real sobre WebSockets | Covered — SignalR over WebSockets |
| RNF-09 | Cobertura backend ≥ 90 % | Ensure new broadcasters/handlers/queries ship with tests (see Verification) |

**Net:** the plan is consistent with the requirements doc. The only requirement-driven scope decisions
to confirm are (1) whether clue-release/penalty events join the live feed within HU-24B (RF-13/§6.6) or
a follow-up, and (2) that RF-15's audit history is treated as satisfied by the existing backend
persistence, with broadening/exposure deferred.
