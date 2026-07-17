# HU-24B — Evidence/submissions real-time push to the operator panel: implementation plan — 2026-07-16

**Branch**: `umbral-hu-24b` · **Base commit**: `de6ec9b` · **Status**: plan only, nothing implemented

Scopes **Work item A / STEP 2** of `frontend/plans/hu-24b-operator-panel-realtime-plan.md` (DES-33).
That plan's execution order is B → A → C → D. **B (operator ranking) is done and E2E-verified**; this
plan covers A, the only remaining AC gap. C (consolidated feed) becomes cheap once A's events exist.

---

## Why this ticket is not done

A validation pass against the tree at `de6ec9b` scored the five ACs:

| AC | Verdict | Basis |
|---|---|---|
| 1. Events without manual reload | Met | 7 events subscribed (`session-state-client.ts:256-291`), each wired to operator state in `DashboardClient.tsx` |
| 2. Monitor evidence **or** submissions | **Partial** | Trivia submissions live via `TeamAnswered`; treasure-hunt QR evidence has no operator surface |
| 3. Ranking updated | Met | `RankingPanel` at `DashboardClient.tsx:1382`, `/hubs/scoring` + REST fallback |
| 4. Restricted to authorized sessions | Met | Every REST/hub path enforces role **and** assignment to that session |
| 5. Events + evidence/submissions + ranking real-time via SignalR | **Not met** | Evidence has no SignalR path at all |

AC #2 is partial rather than failed because the codebase treats a trivia answer *as* an evidence
submission (`EvidenceSubmissionType.TriviaAnswer`), and that form already reaches the operator live —
but on `AnswerRegisteredEvent`, a **different** event, whose payload deliberately drops
correctness/points. The **`TreasureHuntQrScan` form has no equivalent**, so a treasure-hunt operator
sees nothing.

## The problem in one paragraph

The three evidence domain events are raised correctly and go nowhere the operator can see. Their only
subscribers are `PublishEvidenceSubmission{Registered,Accepted,Rejected}IntegrationEventHandler`
(`src/Application/Sessions/EventHandlers/`), which are **plain classes, not `INotificationHandler`** —
they are invoked by direct switch dispatch in `OutboxDomainEventDispatcher.cs:50-55` and only
`IPublishEndpoint.Publish` to RabbitMQ. SignalR broadcasts ride exclusively on the **post-commit
MediatR fan-out** (`DispatchDomainEventsInterceptor.cs:19-23`), and no handler subscribes there. The
resulting `EvidenceTraceEntry` projection is readable at `GET /api/sessions/{id}/evidence-submissions`
(`SessionsController.cs:417`) — but **no frontend code calls it**: "evidence" appears in `frontend/app/`
only in a comment (`definitions.ts:489`) and an unused `.evidenceThumb` CSS class
(`dashboard.module.css:754`).

## The finding that shrinks the work

`DispatchDomainEventsInterceptor.DispatchPostCommitAsync` (`:96-116`) calls `_mediator.Publish` on
**every** domain event after commit — evidence events included. They are already on the MediatR path;
nothing is listening. So a new `INotificationHandler<EvidenceSubmissionRegisteredEvent>` self-wires: the
handlers live in the Application assembly, which `Application/DependencyInjection.cs:35` already scans
(`cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())`). The six existing
`INotificationHandler`s in `Sessions/EventHandlers/` are registered by that same scan and nothing else.

**This work is purely additive.** No change to `OutboxDomainEventDispatcher`, the interceptor, the
RabbitMQ publishers, the consumers, the REST endpoint, or any migration. Do not touch the existing
`Publish*IntegrationEventHandler` classes — RF-14/RNF-05 require those publishers to stay.

## What is already in place (do not rebuild)

| Asset | Location | State |
|---|---|---|
| The three domain events, fully populated | `src/Domain/Events/EvidenceSubmission{Registered,Accepted,Rejected}Event.cs` | Raised; carry session/team/submission/substage/type/timestamps |
| Post-commit MediatR fan-out over all events | `DispatchDomainEventsInterceptor.cs:112-115` | Working — evidence events already flow |
| Operator-only SignalR group + join guard | `SignalRTeamAnsweredBroadcaster.BuildOperatorGroup` (`:21`); `SessionsHub.cs:78-89` | Working, assignment-checked |
| End-to-end broadcaster template | `AnswerRegisteredEvent` → `TeamAnsweredNotificationHandler` → `SignalRTeamAnsweredBroadcaster` | Mirror this exactly |
| Operator REST read + guard | `SessionsController.cs:417` → `GetOperatorEvidenceTraceQuery`; `EvidenceTraceDto` | Working, `[Authorize(Operator)]` + assignment check |
| Gateway routing | `appsettings.json:88` `/api/sessions/{**catch-all}` → `session-ops` | Already covers it — **no gateway route needed** |
| Out-of-order merge precedent | `EvidenceTraceEntry.MergeFrom` (`:89`) | Server-side; the panel needs the same idea |
| QR evidence producer | `mobile/src/components/target-scanner.tsx` → `RegisterTargetScan` | Live — the panel will have real data |
| Unused styling | `dashboard.module.css:754` `.evidenceThumb` | Reserved for this panel |

Unlike Work item B, **no gateway route is required**: B needed one because scoring's ranking sits
outside the `/api/sessions` catch-all. Evidence lives in session-ops, which already owns that prefix.

## Scope — backend (`session-operations-service`)

Placement follows `backend/AGENTS.md`: SignalR notification payloads are outbound contracts, so they
live in `<Area>/Common/`, **not** in `Application/Dtos/`. Broadcaster interfaces sit in
`Application/Common/Interfaces/` beside their six siblings; implementations in `Api/Hubs/`.

1. **`src/Application/Sessions/Common/Notifications/EvidenceSubmissionNotificationDto.cs`** (new).
   Mirror `EvidenceTraceItemDto` field-for-field so push and snapshot agree — the panel must merge
   them. Two shapes, matching the trace projection's register/resolve split.
2. **`src/Application/Common/Interfaces/IEvidenceSubmissionBroadcaster.cs`** (new). Mirror
   `ITeamAnsweredBroadcaster`, including the doc comment naming the group boundary as the leak surface.
3. **`src/Api/Hubs/SignalREvidenceSubmissionBroadcaster.cs`** (new). `IHubContext<SessionsHub>`, sending
   to `SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(liveSessionId)` — **reuse that method, do not
   re-declare the group string.** Suggested methods: `EvidenceSubmissionRegistered`,
   `EvidenceSubmissionResolved`.
4. **Three handlers in `src/Application/Sessions/EventHandlers/`** (new), named per the existing
   `Broadcast…NotificationHandler` convention. Accepted and Rejected both map to the *resolved* signal.
5. **One DI line** in `src/Api/DependencyInjection.cs` (~line 35, beside the other broadcasters):
   `AddSingleton<IEvidenceSubmissionBroadcaster, SignalREvidenceSubmissionBroadcaster>()`.
   **Requires `make rewire SVC=session-operations-service`** — DI registrations do not hot-reload.
6. **Tests** (RNF-09 ≥90%): handler unit tests mirroring `TeamAnsweredNotificationHandlerTests`, and an
   integration test mirroring `TeamAnsweredHubTests` asserting delivery to `live-session-operators:{id}`
   and **never** to the participant `live-session:{id}` group.

### Payload note — resolution events carry no `OriginReference`

Only `EvidenceSubmissionRegisteredEvent` has `OriginReference` (`DescribeOrigin()` →
`target:{targetSnapshotId}` or `qr:{scannedValue}`). Accepted/Rejected do not. So the resolved push
**cannot** carry it; the panel merges the resolution onto the registered row by `EvidenceSubmissionId`.
Don't invent a field the event can't fill.

## Scope — frontend

7. `app/lib/definitions.ts` — `EvidenceTraceDto` / `EvidenceTraceItemDto` types.
8. `app/lib/sessions.ts` — `getOperatorEvidenceTrace`; `app/actions/sessions.ts` —
   `getOperatorEvidenceTraceAction` in the established three-outcome `data` / `unauthorized` / `error`
   shape (a transient 5xx must not tell an assigned operator they're unauthorized).
9. `app/lib/realtime/session-state-client.ts` — subscribe the two new events beside the existing seven.
10. `app/dashboard/EvidenceSubmissionsPanel.tsx` + module CSS (reuse `.evidenceThumb`).
11. `app/dashboard/DashboardClient.tsx` — reducer + effect, rendered inside the
    `role === 'operator' && selectedOperatorSession && selectedOperatorState` branch (`:1322`); REST
    snapshot on select and on reconnect, pushes applied on top.
12. Unit tests + an e2e mirroring `frontend/tests/e2e/session-operator-ranking.spec.ts` (live push with
    no reload; hub blocked → REST snapshot still renders).

## Decisions to make before building, not during

- **The push can beat the snapshot.** SignalR fires post-commit immediately; the REST trace is fed
  *asynchronously* by RabbitMQ consumers (`EvidenceSubmissionRegisteredConsumer` →
  `RecordEvidenceTraceRegistrationCommand`). A live push can legitimately arrive before the REST row
  exists, and a resolution can arrive before its registration. **Merge by `EvidenceSubmissionId`; never
  assume snapshot-then-deltas.** This is the same out-of-order problem `EvidenceTraceEntry.MergeFrom`
  solves server-side, and it is the most likely source of a flaky panel.
- **Rejection reasons are pre-rendered copy, not enum names.** `EvidenceSubmissionRejectedEvent
  .RejectionReason` is a normalized `string`. The QR path supplies
  `TargetResolutionRejectionReason.ToMessage()` (human text) via `TreasureEvidenceSubmission
  .RejectRegisteredTarget`. The contextual `EvidenceRejectionReason` path (`EvidenceSubmission.Reject`,
  which would pass `.ToString()`) is **dead in production** — its only caller,
  `EvidenceIntakeFacade.RegisterPendingAsync`, has no production callers. So in practice the field is
  always a display message: render it, don't switch on it.
- **Trivia answers will now emit two operator signals.** A trivia submission raises *both*
  `AnswerRegisteredEvent` (→ existing `TeamAnswered`) and the evidence events (→ new pushes). Decide
  whether the evidence panel shows both forms or filters to `TreasureHuntQrScan`. Showing both
  double-reports trivia answers next to `AnsweredMonitorPanel`; filtering leaves AC #2's "envíos"
  reading covered by the existing panel. **Recommendation: show both, typed** — AC #5 names
  "evidencias/envíos" together, and the panel is the honest trace surface.
- **`qr:{scannedValue}` echoes raw scanned input** into `OriginReference` on unmatched scans. It is
  operator-only (assignment-guarded group + endpoint), so this is acceptable — but treat it as
  untrusted text in the UI, not markup.

## Out of scope

- Work item C (consolidated feed) and D (panel freshness) — separate steps of the frontend plan.
- The RF-15 read path is owned by DES-100 / HU-40. Its first delivery makes
  `SessionEventHistory` queryable through scoring-monitoring; a write-only table does not satisfy RF-15.
- The admin/hub policy inconsistency: `RankingController` allows `AdministratorOrOperator` and
  `ScoringSessionAuthorizationProxy` grants Administrator, but `/hubs/scoring` is mapped
  `ParticipantOrOperator` (`scoring Program.cs:31-32`), so an admin can read ranking over REST yet fails
  the hub handshake. Fails closed; not an HU-24B AC. Decide separately.
- The dead `EvidenceRejectionReason` path and its DI-registered `EvidenceValidationChain`
  (`Application/DependencyInjection.cs:86-89`) — noted above; deleting or wiring it is its own ticket.

## Verification

Backend: `make -C backend test SVC=session-operations-service`. After the DI edit,
`make rewire SVC=session-operations-service` — `dotnet watch` hot-reload never re-runs startup wiring,
so the broadcaster will appear unregistered until the process restarts.

End-to-end, per the frontend plan's Verification section:
1. Operator signs in, opens `/dashboard`, selects an assigned treasure-hunt session.
2. Participant scans a QR target on mobile (`target-scanner.tsx`) → the operator's evidence panel shows
   the submission **with no reload**, then flips to its resolved state.
3. Drive a rejected scan (already-resolved or out-of-substage target) → the rejection message renders.
4. An operator **not** assigned to the session gets the unauthorized state and receives no pushes.
5. Kill/restore the connection → the panel refetches the REST snapshot and stays consistent.

Seeding note: use a **TreasureHunt** mission. The trivia seeds
(`session-answered-monitor-manual-seed.spec.ts`) exercise the already-working `TeamAnswered` path, not
the QR evidence gap this plan closes — see `session-treasure-hunt-manual-seed.spec.ts`.

## Requirements traceability

| Req | Covered by |
|---|---|
| RF-09 (evidence w/ date, team, session, validation state) | `EvidenceTraceItemDto` already carries all fields; the panel surfaces them |
| RF-13 / §6.6 (operator reflects relevant events real-time) | Closes the evidence half |
| RF-14 / RNF-05 (publish to RabbitMQ on evidence) | **Preserved** — SignalR is additive; publishers untouched |
| RF-16 / §8, RB-10 (role differentiation, assigned sessions only) | Operator-only group + `SessionAdministrationAuthorizationProxy` assignment check |
| RNF-03 (real-time over WebSockets) | SignalR |
| RNF-09 (backend coverage ≥90%) | Item 6 |
