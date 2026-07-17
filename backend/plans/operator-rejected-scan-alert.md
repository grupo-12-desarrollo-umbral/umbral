# Plan: Operator Real-Time Alert on Rejected Evidence (Invalid QR/Target Scan)

## Decisions (locked)

| Decision | Choice | Consequence |
|---|---|---|
| Emit path | **RabbitMQ consumer** (`EvidenceSubmissionRejectedConsumer`) | Alert is produced by async consumption of a bus event → evidences module 7 "alertas". Bus round-trip latency accepted. |
| UI form | **Transient toast** | Dismissible/auto-expiring attention layer at the top of the operator view. No duplicate data table. |
| v1 alert types | **Only rejected scan** | Smallest slice (~1h). More rules are ~15 min each once the plumbing exists. |
| Persistence | **Ephemeral, no new table** | No Alert entity/migration. Audit (RF-15) already covered by the evidence trace. Alerts fired while the hub is down are lost. |

## Why this is needed (alignment with the Alcance)

Section **6. Alcance del proyecto** of `docs/proyecto_requisitos_documento_clave.md`, module
**7 – Procesamiento asíncrono**, requires:

> "publicación y consumo de eventos para auditoría, recálculo de puntajes, **alertas** y
> consolidación del historial."

Auditoría/historial, recálculo and consolidación each have an async consumer today; **"alertas" is
the one bullet with no implementation.** This plan closes that gap with the smallest scope-aligned
slice, and reinforces:

- **Módulo 4 – Panel del operador** (a relevant event surfaced for action)
- **Módulo 6 – Supervisión en tiempo real** (instant push of a relevant event)
- **RF-13** (operator panel reflects relevant events in real time), **RF-14** (domain events on the
  bus at evidence registration), **RF-15** (auditable history — already satisfied by the trace).

## What the alert means (verified in code)

Fires on **rejected evidence submissions**, which here means an **invalid QR / target scan** — NOT a
wrong trivia answer:

- `TriviaAnswerSubmission` has only an `Accept` factory → a wrong answer is stored as **Accepted**
  with `IsCorrect = false`; it never raises `EvidenceSubmissionRejectedEvent`. → **no alert.**
- `TreasureEvidenceSubmission.RejectRegisteredTarget(TargetResolutionRejectionReason)` raises the
  rejection for `ScannedValueDoesNotResolveToTarget`, `TargetOutsideActiveSubstage`,
  `TargetAlreadyResolvedByTeam`; plus intake `EvidenceRejectionReason` (`SubstageBindingMismatch`,
  `OutsideSubmissionWindow`, `UnauthorizedOrigin`). → **alert.**

Operator signal: *"Team X scanned something invalid for their current substage."* One event per
resolved submission (raised exactly once) → no dedup needed. Every rejection is alert-worthy, so no
`SubmissionType` filter is required.

## What already exists (verified end-to-end — reused, not rebuilt)

Two parallel paths already run for a rejection today:

| Path | Mechanism | Purpose | Status |
|---|---|---|---|
| `BroadcastEvidenceSubmissionRejectedNotificationHandler` | in-process `INotificationHandler` → SignalR `EvidenceSubmissionResolved` | pushes the **rejected row** to the operator Evidence panel | ✅ live |
| `EvidenceSubmissionRejectedConsumer` | outbox → RabbitMQ → consumer → `RecordEvidenceTraceResolutionCommand` | async **audit trace** persistence | ✅ live |

Confirmed: a rejected scan **already appears** in `EvidenceSubmissionsPanel` as a row showing
"Rejected" + the reason (`EvidenceSubmissionsPanel.tsx` renders `validationState` and
`rejectionReason`). **So the alert's only added value is elevation** — turning a passive table row
into an active "look here now" toast. (ROI note at the bottom.)

Reused infra:

| Piece | Location |
|---|---|
| `EvidenceSubmissionRejectedIntegrationEvent` (TeamId, RejectionReason, ActiveSubstageId, SubmittedAt, ResolvedAt, SubmissionType) | `session-operations/.../Sessions/Common/` |
| `EvidenceSubmissionRejectedConsumer` (emit point) | `session-operations/.../Infrastructure/Messaging/Consumers/` |
| Operator SignalR channel: `SessionsHub` + operator group `SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(sessionId)`; broadcaster pattern `IOperatorSessionPanelBroadcaster` / `SignalROperatorSessionPanelBroadcaster` | `session-operations/.../Api/Hubs/` |
| Frontend hub client (`connection.on(...)` + normalizers) | `frontend/app/lib/realtime/session-state-client.ts` |
| Dashboard realtime orchestrator (callback bag + reducers) | `frontend/app/dashboard/DashboardClient.tsx` |
| `role="alert"` banner styles | `frontend/app/dashboard/dashboard.module.css` |

## Backend implementation — `session-operations-service`

1. **DTO** — `src/Application/Common/Models/OperatorAlertDto.cs` (new)
   ```csharp
   public sealed record OperatorAlertDto(
       Guid LiveSessionId,
       Guid TeamId,
       string Kind,          // "EvidenceRejected"
       string Message,       // pre-rendered: RejectionReason is already a display string
       DateTimeOffset OccurredAt);
   ```

2. **Port** — `src/Application/Common/Interfaces/IOperatorAlertBroadcaster.cs` (new)
   ```csharp
   public interface IOperatorAlertBroadcaster
   {
       Task BroadcastAlertAsync(OperatorAlertDto alert, CancellationToken cancellationToken);
   }
   ```

3. **SignalR adapter** — `src/Api/Hubs/SignalROperatorAlertBroadcaster.cs` (new)
   Clone of `SignalROperatorSessionPanelBroadcaster`; reuse the operator group; new method const.
   ```csharp
   public sealed class SignalROperatorAlertBroadcaster : IOperatorAlertBroadcaster
   {
       public const string OperatorAlertRaisedMethod = "OperatorAlertRaised";
       private readonly IHubContext<SessionsHub> _hubContext;
       public SignalROperatorAlertBroadcaster(IHubContext<SessionsHub> hubContext) => _hubContext = hubContext;

       public Task BroadcastAlertAsync(OperatorAlertDto alert, CancellationToken ct) =>
           _hubContext.Clients
               .Group(SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(alert.LiveSessionId))
               .SendCoreAsync(OperatorAlertRaisedMethod, [alert], ct);
   }
   ```

4. **DI registration** — `src/Api/DependencyInjection.cs` (edit; next to the other broadcasters ~line 36)
   ```csharp
   builder.Services.AddSingleton<IOperatorAlertBroadcaster, SignalROperatorAlertBroadcaster>();
   ```

5. **Emit from the consumer (async path)** —
   `src/Infrastructure/Messaging/Consumers/EvidenceSubmissionRejectedConsumer.cs` (edit)
   Inject `IOperatorAlertBroadcaster`. Keep the existing `RecordEvidenceTraceResolutionCommand` send
   (audit), then broadcast the alert:
   ```csharp
   await _alertBroadcaster.BroadcastAlertAsync(
       new OperatorAlertDto(
           message.LiveSessionId,
           message.TeamId,
           "EvidenceRejected",
           $"Escaneo rechazado ({message.SubmissionType}): {message.RejectionReason}",
           message.ResolvedAt),
       context.CancellationToken);
   ```
   The consumer is an infrastructure adapter that already depends on `ISender`; depending on the
   Application port `IOperatorAlertBroadcaster` is consistent (Infrastructure → Application).

## Frontend implementation — operator dashboard

6. **Subscribe** — `frontend/app/lib/realtime/session-state-client.ts` (edit)
   Add an `OperatorAlertDto` type, a `normalizeOperatorAlert` (mirror `normalizeOperatorPanel`,
   PascalCase ?? camelCase), an `onOperatorAlert?` option, and:
   ```ts
   if (onOperatorAlert) {
     connection.on('OperatorAlertRaised', (raw: unknown) => {
       onOperatorAlert(normalizeOperatorAlert(raw))
     })
   }
   ```

7. **Wire the callback** — `frontend/app/dashboard/DashboardClient.tsx` (edit)
   In the `createSessionStateRealtimeClient({ ... })` options, next to `onEvidenceSubmissionResolved`:
   ```ts
   onOperatorAlert: (alert) => {
     if (alert.liveSessionId !== selectedRealtimeSessionId) return
     setAlerts((prev) => [makeToast(alert), ...prev].slice(0, 5))
   },
   ```
   A small `alerts` state (last ~5) with per-toast auto-dismiss (e.g. `setTimeout` 8s) — no reducer
   file needed for v1.

8. **Render the toast stack** — new `frontend/app/dashboard/OperatorAlertToasts.tsx` (small) rendered
   at the top of the operator view in `DashboardClient.tsx`. Reuse `role="alert"` banner styling from
   `dashboard.module.css`. Each toast: ⚠ team display name + message + time, dismiss button.
   **Team name is already solved:** `DashboardClient` already holds a `teamNames: Record<string,string>`
   (runtime teamId → display name, from the operator-panel rollup) and passes it to
   `EvidenceSubmissionsPanel`. The rejected event's `TeamId` is the same runtime teamId, so the toast
   looks up `teamNames[alert.teamId]` (fall back to a short teamId slice) — no new resolution logic.

## Testing (match existing patterns)

- **Backend unit** — `EvidenceSubmissionRejectedConsumerTests`: assert that on consume it (a) still
  sends `RecordEvidenceTraceResolutionCommand` and (b) calls `IOperatorAlertBroadcaster.BroadcastAlertAsync`
  once with the mapped `OperatorAlertDto`.
- **Backend integration (optional)** — extend the existing rejected-evidence messaging/hub test to
  assert an `OperatorAlertRaised` frame reaches the operator group.
- **Frontend unit** — `session-state-client` test: firing `OperatorAlertRaised` invokes
  `onOperatorAlert` with a normalized DTO. Optional `DashboardClient` test: a toast renders and
  auto-dismisses.
- **Manual (Docker Compose)** — submit an invalid QR scan (target outside active substage) → operator
  dashboard shows the toast instantly; confirm a wrong trivia answer does NOT.

## Out of scope for v1 (deliberate)

- No new `Alert` table / migration (audit already in the evidence trace; RF-15 covered).
- No acknowledge/resolve workflow, no severities.
- No reconnect refetch for alerts — ones fired while the hub was down are lost (acceptable for a
  transient signal; the rejection is still in the Evidence panel and the audit trace).
- No time-based alerts (idle team, timer expiry) — those need a `BackgroundService` tick, separate
  effort.
- Only the rejected-scan rule. Additional rules (e.g. cambio de líder) reuse steps 1–7 and are ~15 min
  each once this plumbing lands.

## Honest ROI note

The rejection is **already visible** in `EvidenceSubmissionsPanel`. This feature adds **elevation**,
not new data — a toast so an operator watching the live game (not the table) notices immediately.
If operators already watch that panel, ROI is modest; the primary justification here is closing the
module-7 "alertas" scope gap with a real async flow, which is worthwhile for the academic defense
regardless.
