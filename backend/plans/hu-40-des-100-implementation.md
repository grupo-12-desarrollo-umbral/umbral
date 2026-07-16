# DES-100 (HU-40) — Historial de sesión: eventos, puntaje y ranking

_Session history read surface over an event-sourced projection in scoring-monitoring. Part 1
(events) + Part 2 (score/ranking) — one endpoint, one table, one view. The **write path for three
session-scoped event types already exists and is well-built**; this plan adds the read side, the
missing appenders, and one new cross-service event._

Plan shape mirrors `des-101-hu35-hu36b-backend-change-list.md` (Verified surface → Decisions →
per-change Scope+Gate → AC mapping → Open Questions → Commit Sequence).

**Decisions taken 2026-07-16 — every open question is resolved (O-1→D-1, O-2→D-10, O-3→D-4,
O-4→D-11, O-5→D-9, O-6→D-12). No section is blocked on a decision; §6 remains blocked on DES-92
alone.** ⚠️ O-2's resolution **grew §1** and pulled session-operations into the first delivery —
an earlier draft of this plan wrongly claimed O-2 blocked only §5.

**First delivery = §1 + §2 + §3.** Schema, attribution contract, read slice, endpoint, gateway route.
**No *new* integration events and no DES-92 dependency** — but §1 does extend the existing
`SessionStateChangedIntegrationEvent` across both services (D-10). Closes **RF-15** — today at zero
coverage — and satisfies every "El historial muestra…" AC *for the three event types already
ingested*. §4, §5 and §7 are small independent follow-ups; §6 is blocked regardless and cannot be in
the first delivery.

**Audience routing — the two `svc:` labels are not equal:**
- `svc:scoring-monitoring-service` → the bulk of the work (§1–§7).
- `svc:session-operations-service` → **§1** (contract + publisher snapshot, D-10) and §6
  (`ClueReleased` publisher wiring).
- **api-gateway** → §3. Not labelled on the ticket, but **required** — see D-5.

**Blocked by:** DES-92 (`ClueReleased` → RabbitMQ) gates **§6 only**. Everything else proceeds
without it.

---

## Verified existing surface (what already exists — do not rebuild)

All paths relative to `backend/services/scoring-monitoring-service/` unless noted.

| Piece | Location | Note |
|---|---|---|
| History entity | `src/Domain/Entities/SessionEvent.cs` | **plain class, not `BaseEntity`** — see D-3 |
| Event-type constants | `SessionEvent.cs:8-10` | `SessionStateChanged`, `QuestionClosed`, `SessionResultsFinalized` |
| Factories | `SessionEvent.cs:50` `ForStateChange`, `:71` `ForQuestionClosed`, `:87` `ForResultsFinalized` | the shape §1 extends |
| Idempotency key | `SessionEvent.BuildSourceEventKey` (`:98-106`) | `{eventType}:{liveSessionId:D}:{occurredAt:O}[:{discriminator}]` — pure function of the source fact |
| EF config | `src/Infrastructure/Persistence/Configurations/SessionEventConfiguration.cs` | table `session_events`; `event_type` max 80, `payload_summary` max 500, `source_event_key` max 256 |
| Unique index constant | `SessionEventConfiguration.cs:8` `SourceEventKeyIndexName` | shared with the repo's catch filter — keep in sync with the migration |
| Chronological index | `SessionEventConfiguration.cs:33` `(LiveSessionId, OccurredAt)` | EF-default name, non-unique — the index a history read pages on |
| Migration | `src/Infrastructure/Migrations/20260715205421_AddSessionEventHistory.cs` | the only migration touching `session_events` |
| Repository | `src/Infrastructure/Persistence/Repositories/SessionEventHistoryRepository.cs` | `AppendAsync` **only** |
| Idempotency impl | `SessionEventHistoryRepository.cs:20-27` pre-check, `:35-43` `23505` catch filtered on the named index, detaches on duplicate | two-layer; survives concurrent redelivery |
| History consumer | `src/Application/SessionEvents/Consumers/SessionEventHistoryConsumer.cs` | one class, three `IConsumer<>` arms; no error handling of its own (by design — MassTransit retry/error queue) |
| Consumer registration | `src/Infrastructure/Messaging/MassTransitMessagingRegistration.cs:23` | 5 consumers registered total (`:19-23`) |
| Read pattern to mirror | `src/Application/Rankings/Queries/GetOperatorRankingSnapshot/*` | query → handler → DTO; operator-scoped |
| Controller pattern | `src/Api/Controllers/RankingController.cs` | `[ApiController]`, `[Route("api/sessions")]`, primary ctor `(ISender sender)`, per-action `[Authorize(Policy = ...)]` |
| DTO home | `src/Application/Dtos/Rankings/RankingSnapshotDto.cs` | **central `Application/Dtos/<Area>/`**, not co-located (ADR-0011) |
| Operator authz | `AuthorizationPolicies.AdministratorOrOperator` + `IScoringSessionAccessResolver.EnsureAccessAsync` | two layers — the policy alone does not scope to the assigned operator |
| Cross-service test harness | `tests/IntegrationTests/Messaging/CrossServiceConsumerWiringTests.cs` (+ `CrossServiceConsumerHarness`) | publishes the **publisher's** contract type to prove wire compat |
| Wiring playbook | `backend/docs/integration-event-wiring-audit.md` | F1–F7 taxonomy, S1–S8 static checks, 8-item new-event checklist (`:436-449`) |
| `ClueReleasedEvent` | session-ops `src/Domain/Events/ClueReleasedEvent.cs:5-38` | **already carries `TeamId`** — no domain change needed for §6 |

### What does NOT exist (verified by exhaustive grep)

- **No read path of any kind.** `ISessionEventHistoryRepository` is one method. Nothing in `src/`
  reads `session_events`; the DbSet is touched only by the repo's own idempotency probe and by tests.
- **No `SessionEvents/Queries` directory**, no history DTO, no history route. `src/Api/Controllers/`
  holds only `RankingController`, `PenaltiesController`, `HealthController` (5 routes total).
- **No `TeamId` on `SessionEvent`** — every column is session-scoped. See D-2.
- **No session-history read endpoint on session-operations either.** Its own HU-21 audit trail
  (`live_session_events`, owned collection on `LiveSession`) is write-only across all 25
  `SessionsController` routes.
- **`ClueReleasedEvent` is not on the bus** — no case in `OutboxDomainEventDispatcher.DispatchAsync`
  (`:47-64`), no `Publish*` handler, no contract. Only `BroadcastClueReleasedNotificationHandler`
  (SignalR team board). This is failure mode **F2** in the audit doc.
- **`PenaltyApplied` has zero handlers** — raised at `src/Domain/Entities/ScoreEntry.cs:157`,
  matches no `INotificationHandler`/`IConsumer`, never crosses the bus. Raised and dropped.
- **`RankingRefreshed` never leaves the service** — `BroadcastRankingRefreshedHandler` → SignalR only.
- **Scoring-monitoring has no outbox.** `AddEntityFrameworkOutbox` exists only on the session-ops
  side. See D-4.

---

## Architecture Decisions

- **D-1 · Keep the bus path for state changes; amend the ticket.** ✅ **RESOLVED 2026-07-16 (O-1).**
  AC Part 1 says state changes are
  "obtenidos por lectura directa de session-operations (no vía RabbitMQ)" (scope decision
  2026-07-11). The shipped code does the opposite: `SessionEventHistoryConsumer` consumes
  `SessionStateChangedIntegrationEvent` end-to-end, with unit + Testcontainers coverage. Direct read
  would mean building a new session-ops endpoint to replace a working path, then deleting the
  consumer and its tests. Note the earlier objection that direct
  read needs new HTTP plumbing is *false* — a typed cross-service client already exists
  (`src/Infrastructure/Identity/ParticipantSessionMembershipClient.cs`, registered at
  `src/Infrastructure/DependencyInjection.cs:29-33`, forwarding `X-User-Id`/`X-User-Role`/
  `X-User-Email`, failing closed).

  **Decision: keep the bus.** The load-bearing reason is **redundancy, not sunk cost** — the data is
  already local to scoring-monitoring, so direct read means fetching over HTTP what a consumer
  already put in our own table. It is also the safer runtime: the bus path is idempotent and stays
  available when session-operations is down, while a direct read would make every history request
  depend on another service being up. Implementation cost: **zero lines**.

  ⚠️ **Follow-through required:** the AC text must actually be amended on DES-100. Agreeing in a
  comment thread is not enough — otherwise the next reader hits the same contradiction. Raised in
  the DES-100 comment of 2026-07-16.

- **D-2 · `TeamId` on history is the cross-context `ReferenceTeamId`, not the session-scoped
  `TeamId`.** Everything Part 1/Part 2 still needs (clues, evidence, penalties, score) is per-team,
  so `SessionEvent` needs a nullable `TeamId` (null for the three session-scoped types). It **must**
  carry the same value the ledger uses: both evidence consumers deliberately map `ReferenceTeamId` →
  `RecordScoreEntryCommand.TeamId` (`TargetResolvedConsumer.cs:26`, `AnswerRegisteredConsumer.cs:31`),
  each commented "otherwise the score lands on a phantom team row". A history row keyed on the
  session-scoped id will not join to the ledger or the ranking.
  ⚠️ `ApplyPenaltyCommand.TeamId` arrives straight from the HTTP caller with **no `ReferenceTeamId`
  normalization** — pre-existing exposure to the same phantom-row bug, inherited by §5.

- **D-3 · `SessionEvent` stays a plain class.** It deliberately does not derive `BaseEntity`
  (`src/Domain/Common/BaseEntity.cs:5`): it cannot raise domain events and gets no audit columns.
  Deriving `BaseEntity` would graft an `int Id` alongside its existing `Guid SessionEventId` key and
  pull it into `AuditableEntityInterceptor`. Keep it plain — appenders are called explicitly.

- **D-4 · Penalty history cannot be a naive `INotificationHandler<PenaltyApplied>`.**
  ✅ **RESOLVED 2026-07-16 (O-3).** Three facts compose into a bug: (a) `ApplyPenaltyCommandHandler`
  does **two separate** `SaveChangesAsync` with no enclosing transaction (`:63` score entry, `:64`
  penalty); (b) scoring's `DispatchDomainEventsInterceptor` fires on
  `SavedChanges`/`SavedChangesAsync` — **post-commit**, outside the transaction; (c) scoring has no
  outbox. So a domain-event handler would run after save #1, when the `Penalty` row does not yet
  exist, and a throw would roll nothing back.

  **Decision: append history inline in `ApplyPenaltyCommandHandler` after both saves, and log on
  failure.** The smallest change that observes both rows.

  **This is the one decision in this plan where "easiest" and "safest" genuinely diverge — recorded
  honestly rather than papered over.** A failed history write silently loses an audit row while the
  penalty still applies. For an audit trail that is a real hole, if a small one. Closing it properly
  needs either a transaction around the handler or an outbox in scoring-monitoring — both strictly
  larger than this HU, and both fixes to a *pre-existing* defect that HU-40 merely inherits.

  ⚠️ **Follow-through required:** file the two-saves-no-transaction problem in
  `ApplyPenaltyCommandHandler` as its own ticket. HU-40 must not silently absorb it — the whole point
  of choosing the cheap option here is that the expensive problem stays visible to someone else.

- **D-5 · The gateway needs an explicit route entry.** YARP has a catch-all
  `/api/sessions/{**catch-all}` → session-operations (`backend/api-gateway/src/appsettings.json:85-91`).
  Every scoring endpoint sharing that prefix carries a more-specific route to win on specificity
  (`scoring-ranking` `:57-63`, `scoring-ranking-operator` `:64-70`, `scoring-penalties` `:71-77`).
  A history route under `/api/sessions/...` **will silently forward to the wrong service** without
  one. Cluster `scoring` already exists (`:129-132`) — route entry only, no cluster change.

- **D-6 · Follow `backend/docs/integration-event-wiring-audit.md`, NOT the
  `rabbitmq-events-dotnet` skill.** The skill is stale: it mandates publishing in the MediatR
  handler, post-commit, with a 5s timeout that logs-and-swallows. The shipped code does the opposite
  on all three axes — pre-commit via `IOutboxDomainEventDispatcher`, no timeout (the outbox is a DB
  insert), and publishers **rethrow** so the enqueue rolls back with the business write
  (`PublishTargetResolvedIntegrationEventHandler.cs:36-44`). The skill predates the #164 outbox
  migration. The audit doc's checklist (`:436-449`) encodes current reality.

- **D-7 · Reuse the operator authz pattern; there is no Operator-only policy.** scoring-monitoring
  defines exactly two policies (`src/Api/Services/AuthorizationPolicies.cs`):
  `ParticipantOrOperator`, `AdministratorOrOperator`. History is operator/admin review →
  `[Authorize(Policy = AdministratorOrOperator)]` **plus** `IScoringSessionAccessResolver
  .EnsureAccessAsync(liveSessionId, ct)` for per-session scoping. Place the resolver call **in the
  handler** (mirroring `GetOperatorRankingSnapshotQueryHandler.cs:31`), not the controller — the
  service is inconsistent here (`PenaltiesController.cs:21` does it in the controller); the handler
  is the ADR-0011-consistent home.

- **D-8 · Finished/cancelled sessions need no special case.** History rows are immutable and keyed by
  `LiveSessionId`; a terminal state changes nothing about the read. The AC "sesiones finalizadas o
  canceladas siguen visibles" is satisfied by the query having no state filter — assert it in a test
  rather than building for it.

- **D-9 · `PayloadSummary` stays the only payload; no typed numeric columns.**
  ✅ **RESOLVED 2026-07-16 (O-5).** History here is a **narrative audit trail, not a computation
  surface**. The authoritative typed data already exists elsewhere: `score_entries` holds the ledger
  with a typed `ScoreValue`, `rankings` holds the snapshot with typed rows. Anything needing to sum
  penalties or compute a delta reads *those*, never a projection's parsed strings.

  The fields worth querying are already typed and indexed — `EventType`, `TeamId`, `OccurredAt` —
  which covers "this team's events in order" and "all penalties", i.e. what the ACs actually ask for.
  Typed numeric columns would be null for six of the eight event types: a sparse schema serving no
  stated requirement, and a break from the uniform string-summary shape the three existing factories
  established.

  **Cheap to reverse, which is the point.** If the view later needs typed score data, adding a
  nullable column is non-breaking *and backfillable from `score_entries`* — not duplicating the
  number now destroys no information. Keeps the §1 migration to one column plus one index.

  **What would overturn this:** a DES-60 view spec that sorts by score magnitude, renders a running
  score total inline, or aggregates within the view. That is a question about **DES-60**, not HU-40 —
  if that spec lands first, revisit before §7.

- **D-10 · Attribution is the external identity id (Keycloak sub), not the internal int.**
  ✅ **RESOLVED 2026-07-16 (O-2).** `SessionEvent.ResponsibleUserId` (`int?`) becomes
  `ResponsibleUserExternalId` (`Guid?`), and `SessionStateChangedIntegrationEvent` carries the
  external id snapshotted by the publisher.

  **The two id spaces.** Internal `User.Id` (`int`) lives in identity-access
  (`identity-access-service/src/Domain/Entities/User.cs:8`); the Keycloak sub lives on the same row as
  `User.ExternalIdentityId` (`:26`). **identity-access is the only service holding the mapping.**
  The gateway mints `X-User-Id` from `sub`/`NameIdentifier` (`api-gateway/src/Transforms/TrustedHeadersTransform.cs:22`)
  and never calls identity-access — so **the value crossing every service boundary is the Guid, and
  the int is reachable only via an HTTP hop.** Per ADR-0009
  (`backend/docs/adr/0009-resolve-operator-ownership-via-identity-actor-profile.md`), session-ops
  resolves the int through `GET /api/users/me` — which is why today's `responsible_user_id` holds an
  int at all.

  **The decisive argument: penalties cannot be attributed with an int.** scoring-monitoring has no
  identity-access client, no map table, and no int anywhere in its schema. Keeping `int?` means either
  penalties stay unattributed in history (guts the AC) or scoring grows a synchronous identity-access
  dependency on its write path (tail wagging the dog).

  Reinforcing:
  - **Availability.** The Guid is in-process at all three attribution points (penalty, state change,
    clue release); the int at exactly one, via a network hop.
  - **S7/F6 cuts *for* this, not against.** Snapshot-at-publisher means **no consumer resolves
    anything** — `SessionEventHistoryConsumer` persists what it's handed. Resolving at append time
    would put an identity lookup inside a bus consumer, precisely the shape S7 exists to prevent.
  - **The pattern is already blessed twice.** `LiveSessionOperatorAssignedEvent.AssignedOperatorExternalId`
    (`session-operations-service/src/Domain/Events/LiveSessionOperatorAssignedEvent.cs:31`) exists for
    exactly this reason — aggregate keeps the int, integration event carries the external id. Same
    shape as `ScoreEntry.TeamDisplayName` (`ScoreEntry.cs:61-62`), snapshotted so a user-less consumer
    needs no authenticated lookup.
  - **Attribution aligns with authorization.** Scoring already authorizes on the Guid
    (`SessionOperatorAssignmentProjection.cs:7`, `ScoringSessionAuthorizationProxy.cs:47`); the doc
    comment at `LiveSessionOperatorAssignedIntegrationEvent.cs:11-13` calls the external id "the axis
    scoring authorizes on". Recording *who did it* on the axis used to decide *whether they could* is
    the coherent choice.
  - **Fixes the Administrator hole for free.** `SessionAdministrationAuthorizationProxy.cs:69`
    short-circuits for Administrators and returns a `null` actor *before* the int lookup — so the most
    privileged actor is the one that cannot be attributed today. The Guid needs no lookup.

  **Type: `Guid?`**, matching `Penalty.AppliedByUserId` and `SessionOperatorAssignmentProjection`,
  scoring's existing representation of the sub. (`User.ExternalIdentityId` is declared `string(128)`,
  but every scoring surface already narrows it to `Guid`.) Stays **nullable** — `ForQuestionClosed`
  and `ForResultsFinalized` are system facts with no actor.

  **Honest cost — this is not the cheapest option, it is the cheapest option that works.** It grows §1
  (a contract change across both services, no longer purely additive) and drags session-operations
  into the first delivery. `int?` was cheaper and simply does not satisfy the penalty AC.

  **No data is corrupted.** Only `ForStateChange` ever set the column, always from the int path — every
  existing row is an int or null, nothing to untangle. **Check the row count before writing a
  backfill:** the fleet is pre-production and `20260715205421_AddSessionEventHistory` is days old, so
  the count is likely ~0. If rows do exist, the int is **not** back-convertible in-process — a backfill
  must page identity-access's catalog (as `AssignableSessionOperatorAccessClient.cs:57+` already does)
  or legacy rows stay unresolved. Decide explicitly; do not discover this later.

  **Rejected:** *(b) second column* as an end state — two nullable columns for one concept means every
  reader coalesces and the split calcifies (it is, however, the right **transition** if rows exist:
  add, dual-write, backfill, drop). *(c) resolve at append time* — S7 violation, adds retry/poison
  failure modes to history appends. *(d) a second trusted header carrying the int* — ADR-0009
  explicitly considered and rejected this, and it would force the gateway into an identity-access
  dependency on every request.

- **D-11 · Ranking history records position changes only; `Ranking.Refresh` computes the signal and
  `RankingRefreshed` carries it.** ✅ **RESOLVED 2026-07-16 (O-4).**

  **The volume is one ranking row per score entry, and most rows say nothing.** The chain is
  unconditional end to end: every persisted `ScoreEntry` raises `ScoreEntryRegistered`
  (`ScoreEntry.cs:103`, `:145`) → `ScoreEntryRegisteredConsumer.cs:18` → `RecalculateRankingCommand` →
  `RecalculateRankingCommandHandler.cs:46/:56` → `Ranking.Refresh` → `RankingRefreshed`
  (`Ranking.cs:97`, last statement, **no guard**). No batching, debouncing or coalescing anywhere.
  A naive `INotificationHandler<RankingRefreshed>` therefore doubles the history table's row count
  (one ranking row per score row) while adding no information for the many refreshes that reorder
  nothing: a penalty against a team already floored at 0 (`Ranking.cs:76`), a gain that doesn't cross
  a neighbour, or churn inside a tie bucket (`ResolutionTimeRankingPolicy.cs:34-43`).

  **Decision: raise `RankingRefreshed` exactly as today, add a `PositionsChanged` flag to it, and have
  the history handler append only when it is true.** `Refresh` already computes `rankedRows`
  (`Ranking.cs:83`) *before* `_rows.Clear()` (`:85`), so capturing the previous `(TeamId → Position)`
  map and comparing costs a few lines in the one place that has both states in hand.

  **Do not suppress the event itself.** `BroadcastRankingRefreshedHandler` is its only handler and
  re-broadcasts the full snapshot — which must still fire on a score change that moves no position,
  because `TotalScore` changed and the board shows it. Filtering belongs in the history handler, not
  the raise site. The field is additive and internal: `RankingRefreshed` never leaves the service
  (verified — one handler, no publisher, no contract), so there is **no S5 shape-diff and no bus
  concern**.

  **This also closes the idempotency hole, which is the load-bearing half.**
  `RecalculateRankingCommandHandler.cs:59` sets `CalculationVersion + 1` on every recalc, so the
  version **always** differs — a `SourceEventKey` discriminated on it can never dedup, and
  `ScoreEntryRegisteredConsumer.cs:16` has no dedup of its own (the only guard,
  `RecordScoreEntryCommandHandler.cs:23-31`, sits upstream at the score-entry level). A MassTransit
  redelivery would recalculate from identical data and append a duplicate history row forever. Under
  the position-change filter it recomputes the same positions, `PositionsChanged` is false, and the
  row is never written. **The filter is the dedup** — which is why the cheaper "just append every
  refresh" option is not merely noisy but wrong.

  **Rejected:** *(a) one row per refresh* — the volume and the duplicate-on-redelivery bug above.
  *(c) sampling / time-windowing* — an arbitrary threshold that drops real reorders; "position
  changed" is the actual predicate the AC wants, so encode it rather than approximate it.
  *(d) drop ranking history, keep only `ScoreChanged`* — tempting, and it is the honest fallback if
  the diff proves awkward, but "eventos de ranking" is an explicit Part 2 AC.

  ⚠️ `Row` is a `BaseEntity` with **reference equality only** — no `Equals`/`GetHashCode` override, so
  `SequenceEqual` over `_rows` silently compares references and would report "changed" every time.
  Compare an explicit `(TeamId → Position)` projection. Do **not** add structural equality to `Row` to
  make the diff read nicer; that is a domain-wide change for a local need.

- **D-12 · O-6's drift is one field, not three; still deferred.** ✅ **RESOLVED 2026-07-16 (O-6).**
  Verified against the code, two of the three drifts are not information loss at all:
  - **`ActorType` is recoverable, exactly.** `LiveSession.cs:352-354` derives it as
    `responsibleUserId.HasValue ? Operator : System` — nothing more. The integration event already
    carries `ResponsibleUserId`, so scoring can reconstruct it with the same test. Adding the field
    would transmit a value the consumer can compute. **No action.**
  - **`CorrelationId` correlates nothing.** `SessionEvent.cs:35` and `:53` (session-ops) set
    `Guid.NewGuid()` inside the private constructors — it is not a parameter, never threaded from a
    request context, and never read anywhere in `src/`. Two rows from the same transition get
    different values. There is also **no correlation infrastructure on the bus** (no
    `CorrelatedBy<Guid>`, no send/publish filters, no header conventions — the outbox table's own
    `CorrelationId` column is MassTransit-owned). Propagating it would transmit noise. **No action** —
    file the random-GUID column as its own smell instead.
  - **`WasExpiredByTimer` is the one real gap.** `LiveSession.cs:436-437` computes it, the domain event
    carries it (`QuestionClosedEvent.cs:23`), the SignalR path uses it
    (`TriviaRoundOrchestratorFacade.cs:92`), and `PublishQuestionClosedIntegrationEventHandler.cs:34-39`
    drops it. Nothing downstream can distinguish a timer expiry from an operator-forced close, and
    `SessionEvent.ForQuestionClosed` hardcodes `$"Question {questionIndex} closed"`.

  **Decision: keep deferring, on the plan's original grounds** — additive, non-blocking, and the
  summary text it would improve is a **DES-60 view** question. The change is a one-field contract
  edit across both copies (S5) whenever the view asks for it. What changes here is the *scope*: O-6 was
  overstated, and re-deriving that each time it is read is the cost this note removes.

---

## Change §1 — Schema: team dimension + attribution (scoring-monitoring **+ session-operations**)

_Unblocked: D-1, D-9, D-10 resolved. **Spans two services** — the contract change (D-10) means the
consumer reads a field the publisher must send, so both copies and the publisher land in **one PR**
(S5 requires identical record shapes)._

**Scope — scoring-monitoring**
- **Edit** `src/Domain/Entities/SessionEvent.cs`:
  - Add `Guid? TeamId` (null for the three session-scoped types). Value is the cross-context
    `ReferenceTeamId` (D-2).
  - **Replace `int? ResponsibleUserId` with `Guid? ResponsibleUserExternalId`** (D-10).
  - Add event-type constants: `ClueReleased`, `EvidenceSubmitted`, `PenaltyApplied`,
    `ScoreChanged`, `RankingRefreshed`.
  - Add a factory per new type, each threading a distinct `SourceEventKey` discriminator so the
    unique index keeps deduplicating redeliveries. Existing keys must not change.
  - Payload stays a `PayloadSummary` string for every new type (D-9).
- **Edit** `src/Infrastructure/Persistence/Configurations/SessionEventConfiguration.cs`: map
  `team_id` nullable; map `responsible_user_external_id` (uuid, nullable) replacing
  `responsible_user_id`; add index `(LiveSessionId, TeamId, OccurredAt)`. Keep the existing
  `(LiveSessionId, OccurredAt)` — §2's default read is session-wide.
- **Edit** `src/Application/SessionEvents/Common/SessionStateChangedIntegrationEvent.cs` (consumer
  copy) — carry the external id. Keep `[MessageUrn]`/`[EntityName]` unchanged; shape must stay
  byte-identical to the publisher copy (**S5**).
- **Edit** `SessionEventHistoryConsumer.cs:29` — pass the external id through to `ForStateChange`.
- **Add** migration: `make -C backend ef SVC=scoring-monitoring-service ARGS="migrations add AddSessionEventTeamAndExternalActor"`.
  **First check `SELECT count(*) FROM session_events`** (D-10) — if ~0, drop/add the column; if not,
  do the add → dual-write → backfill → drop transition instead.

**Scope — session-operations**
- **Edit** `src/Domain/Events/SessionStateChangedEvent.cs` — add the responsible user's external id
  alongside the existing int, mirroring `LiveSessionOperatorAssignedEvent`'s
  int + `AssignedOperatorExternalId` pair (`:23`, `:31`).
- **Edit** `src/Application/Sessions/Common/SessionStateChangedIntegrationEvent.cs` (publisher copy)
  — same shape as the consumer copy.
- **Edit** `PublishSessionStateChangedIntegrationEventHandler.cs:36` — snapshot the external id.
- **Edit** `SessionAdministrationAuthorizationProxy.cs:84` — it already receives
  `actor.ExternalIdentityId` from `GET /api/users/me` (`AuthenticatedActorProfileAccessClient.cs:31-35`)
  and **throws it away**; thread it through instead. For the Administrator short-circuit (`:67-70`),
  take the external id straight from `_currentUser.Id` — no lookup needed, which is what closes the
  null-attribution hole (D-10).

**Watch**
- `payload_summary` is capped at 500 and `event_type` at 80 — new summaries must fit or the config
  changes too.
- Do not rename `SourceEventKeyIndexName` (`SessionEventConfiguration.cs:8`); the repo's catch filter
  matches `ConstraintName` against it.
- `SourceEventKey` must not incorporate the actor — it keys the *source fact*, and dedup would break
  if the same fact resolved a different actor on redelivery.

**Gate** — build + test + gate **both** services. Unit tests extend `tests/UnitTests/SessionEventTests.cs`
(new factories: same-source-fact ⇒ same key, distinct types ⇒ distinct keys). **S5 shape-diff the two
contract copies.** Extend `CrossServiceConsumerWiringTests` to assert the external id survives the
wire — a dropped field deserializes to `null`, which is **F4**, and silently looks like "no actor".
Add a test that an **Administrator** state transition now records an actor rather than `null` (D-10).

## Change §2 — Read slice: query + handler + DTO (scoring-monitoring)

**Scope**
- **Edit** `src/Application/Common/Interfaces/ISessionEventHistoryRepository.cs`: add a read method
  returning events for a session ordered by `OccurredAt`, optional `TeamId` filter.
- **Edit** `src/Infrastructure/Persistence/Repositories/SessionEventHistoryRepository.cs`: implement
  with `AsNoTracking()`, ordered to hit the composite index.
- **Add** `src/Application/SessionEvents/Queries/GetSessionHistory/GetSessionHistoryQuery.cs` —
  `sealed record GetSessionHistoryQuery(Guid LiveSessionId, Guid? TeamId) : IRequest<SessionHistoryDto>;`
  (`IRequest` comes from `GlobalUsings.cs` — no MediatR `using`).
- **Add** `GetSessionHistoryQueryHandler.cs` — `EnsureAccessAsync` first (D-7), then fetch, then map.
  Unknown session ⇒ **empty DTO, not 404**, mirroring `GetRankingSnapshotQueryHandler.cs:25-27`.
- **Add** `src/Application/Dtos/SessionEvents/SessionHistoryDto.cs` (+ nested row record, + static
  `Empty(liveSessionId)`), central Dtos root per ADR-0011.
- **Add** `src/Application/SessionEvents/Common/SessionHistoryDtoFactory.cs`, mirroring
  `RankingSnapshotDtoFactory`.
- **No validator** — queries in this service are unvalidated; `ValidationBehaviour` no-ops.

**Gate** — Application unit tests (ordering, team filter, empty-session, access denied);
integration test over real Postgres reading back appended rows.

## Change §3 — Read endpoint + gateway route

**Scope**
- **Add** `src/Api/Controllers/SessionHistoryController.cs` — `[ApiController]`,
  `[Route("api/sessions")]`, primary ctor `(ISender sender)`,
  `[HttpGet("{liveSessionId:guid}/history")]`, `[Authorize(Policy = AdministratorOrOperator)]`,
  `teamId` as an unbound query param (mirroring `RankingController.cs:18`).
- **Edit** `backend/api-gateway/src/appsettings.json` — new route `scoring-session-history`,
  `"ClusterId": "scoring"`, `"Match": { "Path": "/api/sessions/{liveSessionId}/history" }`,
  `"AuthorizationPolicy": "default"` (per D-5).

**Gate** — curl through the gateway on `localhost:8000` as an operator; confirm it lands on
scoring-monitoring and **not** session-operations (the D-5 failure is silent).

> **§1–§3 land RF-15 and every "El historial muestra…" AC for the three event types already being
> ingested.** Demoable before any new publisher wiring exists. Ship as its own PR.

## Change §4 — Evidence into history (Part 1)

**Scope**
- **Edit** `src/Application/Scores/Consumers/TargetResolvedConsumer.cs` and
  `AnswerRegisteredConsumer.cs`: append a history row alongside the existing
  `RecordScoreEntryCommand`, using **`ReferenceTeamId`** (D-2) and the event-time timestamp
  (`ResolvedAt` / `SubmittedAt`), never `UtcNow`.
- ⚠️ `AnswerRegisteredConsumer.cs:21` early-returns unless `IsCorrect`. History wants **both** QR
  scans and trivia answers regardless of correctness (AC: "evidencias (QR y respuestas de trivia)").
  Append history **before** the correctness gate.

**Gate** — consumer unit tests per arm; extend `CrossServiceConsumerWiringTests` with
publisher-contract → history-row assertions.

## Change §5 — Penalties into history (Part 1)

**Scope**
- **Edit** `src/Application/Scores/Commands/ApplyPenalty/ApplyPenaltyCommandHandler.cs`: append
  history **inline after both saves**, and **log on failure** (D-4), using `penalty.AppliedAt` as
  `OccurredAt` (the single source of the instant, stamped in `Penalty.Create` — `Penalty.cs:45`).
- Attribution is now trivial (D-10 / §1): `PenaltyApplied.AppliedByUserId` is already the Keycloak
  sub, and `ResponsibleUserExternalId` is a `Guid?` — pass it straight through, no conversion.
- Leave the unhandled `PenaltyApplied` domain event as-is, or delete it — do **not** hang history
  off it (D-4). Note if left, it remains dead code.
- **Do not** fix the two-saves-no-transaction shape here (D-4) — file it separately.

**Gate** — handler unit test asserting the history append; integration test over the full
apply-penalty flow. A test should pin the accepted gap: a failing history append must **not** fail
the penalty (log-and-continue), which is the documented D-4 tradeoff, not an accident.

## Change §6 — `ClueReleased` onto the bus (Part 1) — **BLOCKED BY DES-92**

Follow the audit checklist (`integration-event-wiring-audit.md:436-449`) verbatim. `ClueReleasedEvent`
(`session-ops src/Domain/Events/ClueReleasedEvent.cs`) already carries
`(LiveSessionId, TeamId, Guid? TargetId, Guid? ClueId, ReleaseMode, int? ReleasedByUserId, ReleasedAt)`
— no domain change.

> ### ⛔ §6 blocker — fix clue attribution BEFORE putting the event on the bus
>
> `ClueReleaseFacade.cs:27-30` calls `GetAuthorizedSessionAsync` (which **discards** the actor) and
> then attributes the release to `liveSession.AssignedOperatorUserId!.Value`. **An Administrator
> releasing a clue is recorded against the assigned operator, not the admin who did it.**
>
> Today that defect is contained in session-ops' local `ClueReleaseRecord`. **Bussing the event
> without fixing it replicates a known-wrong actor into the audit history table** — the exact artifact
> this HU exists to build. Wrong data in an audit trail is worse than absent data: it is confidently
> wrong. This is **not** optional follow-up; §6 must not ship without it.
>
> **This does not fix itself under D-10.** D-10 changes the history column and the
> `SessionStateChanged` contract; the clue path is `int operatorUserId` end to end
> (`ReleaseClueToTeam(..., int operatorUserId, ...)` → `EnsureOperatorUserIdIsValid` →
> `ClueReleaseRecord.CreateManual(operatorUserId:)` → `ClueReleasedEvent.ReleasedByUserId` as `int?`).
> D-10 **enables** the fix — it does not perform it. _(An earlier draft of this plan claimed this
> "becomes naturally correct under D-10". That was wrong.)_
>
> **The fix is small:** the proxy already exposes **`GetAuthorizedSessionWithActorAsync`** — the same
> method `TransitionSessionStateCommandHandler.cs:35-44` uses. The facade is simply calling the wrong
> overload. Switch to it, and thread the actor's external id (D-10) so the **Administrator** case is
> attributable at all — `SessionAdministrationAuthorizationProxy.cs:67-70` short-circuits before the
> int lookup, so the int alone *cannot* attribute an admin.
>
> **Decide explicitly:** does the session-ops-local `ClueReleaseRecord.ReleasedByUserId` get corrected
> too (it holds the same wrong actor today, independent of history), or only the outbound event? A
> separate ticket for the local record is fine — a bussed event that disagrees with the local record
> is not.

**Scope — session-operations**
- **Fix** `ClueReleaseFacade.cs:27-30` per the blocker above — actual actor, not assigned operator.
- **Edit** `src/Domain/Events/ClueReleasedEvent.cs` — carry the actor's external id alongside the
  existing `int? ReleasedByUserId`, mirroring `LiveSessionOperatorAssignedEvent`'s int + external-id
  pair (`:23`, `:31`).
- **Add** `src/Application/Sessions/Common/ClueReleasedIntegrationEvent.cs` —
  `[EntityName("session-clue-released")]`, **no `[MessageUrn]`** (own namespace is the URN default).
  Carries the **external id** for attribution (D-10), never the int.
- **Add** `src/Application/Sessions/EventHandlers/PublishClueReleasedIntegrationEventHandler.cs`,
  mirroring `PublishTargetResolvedIntegrationEventHandler` — **rethrow**, do not swallow (D-6).
- **Edit** `OutboxDomainEventDispatcher.cs:47-64` — add the `ClueReleasedEvent` case (**S3**).
- **Edit** `src/Application/DependencyInjection.cs:59-67` — `AddScoped<...>` (**S3**).

**Scope — scoring-monitoring**
- **Add** `src/Application/SessionEvents/Common/ClueReleasedIntegrationEvent.cs` — matching
  `[EntityName]` **plus** `[MessageUrn("umbral_backend.Application.Sessions.Common:ClueReleasedIntegrationEvent")]`,
  no `urn:message:` prefix (**S1/S6**). Record shape byte-identical to the publisher copy (**S5**).
- **Edit** `SessionEventHistoryConsumer.cs` — add a fourth `IConsumer<>` arm.
- **Edit** `MassTransitMessagingRegistration.cs` — no new `AddConsumer` needed (same class), but
  verify the new exchange binds.

**Gate** — S1–S8 greps; Testcontainers cross-service test via `CrossServiceConsumerHarness`;
post-deploy **R1** (no `_skipped`/`_error` messages — the highest-signal check) and **R2** (the row
landed, and is *correct* — the audit doc's Finding 1 lesson: a present row does not prove a correct one).
**Plus a test that an Administrator-released clue is attributed to the admin, not the assigned
operator** — this is precisely the R2-shaped failure the audit doc warns about, where a green error
queue and a present row both look healthy while the row is wrong.

`BroadcastClueReleasedNotificationHandler` stays untouched — SignalR and the bus are parallel paths.

## Change §7 — Score + ranking history (Part 2)

_Unblocked: O-4 resolved (D-11)._

**Scope**
- **Add** `INotificationHandler<ScoreEntryRegistered>` appending score history. Safe as a domain-event
  handler (unlike D-4): `ScoreEntry` is `BaseAuditableEntity`, and `RecordScoreEntry` is a single
  save. Carries `TeamId`, `ScoreValue`, `ReasonCode`, `RecordedAt`, `RecordedByUserId` (`int?`).
  ⚠️ `RecordedByUserId` is **dead — every caller passes `null`** (see the follow-through list). Do not
  map it to `ResponsibleUserExternalId` expecting an actor; score entries are consumer-driven system
  facts. Leave attribution null here.
- **Edit** `src/Domain/Events/RankingRefreshed.cs` — add `bool PositionsChanged` (D-11). Internal
  event, one handler, never bussed: additive, no S5 concern.
- **Edit** `src/Domain/Entities/Ranking.cs` — in `Refresh`, capture the previous `(TeamId → Position)`
  projection **before `_rows.Clear()` (`:85`)**, compare against `rankedRows` (`:83`), and pass the
  result into `RankingRefreshed` (`:97`). Raise **unconditionally**, exactly as today — the SignalR
  broadcast must still fire when only `TotalScore` moved. Compare a projection, **not** `_rows`
  (`Row` is reference-equal — D-11).
- **Add** `INotificationHandler<RankingRefreshed>` appending ranking history **only when
  `PositionsChanged`** (D-11). `RankingRefreshed` carries no team, so these rows are session-scoped,
  `TeamId` null. `CalculationVersion` is the `SourceEventKey` discriminator — note it always differs
  (`RecalculateRankingCommandHandler.cs:59`), so it does **not** dedup; the `PositionsChanged` filter
  is what makes a redelivered recalc a no-op.
- Both handlers run post-commit, outside the transaction — a throw does not roll back the score.
  Acceptable for a projection; log on failure.

**Gate** — handler unit tests; `Ranking.Refresh` unit tests pinning `PositionsChanged` **false** on a
recompute over identical entries and on a penalty against a team already floored at 0
(`Ranking.cs:76`), **true** on a real reorder. Integration test asserting a penalty produces both a
`PenaltyApplied` and a `ScoreChanged` row. **A test that recalculating twice over the same entries
appends exactly one ranking row** — this is the redelivery case D-11 turns on.

---

## Acceptance-criteria mapping

### Part 1 (HU-40A)

| AC | Changes | Note |
|---|---|---|
| Operador consulta historial por sesión | §2, §3 | |
| Cambios de estado, lectura directa de session-ops | §2, §3 | ⚠️ **satisfied via the bus, not direct read** — D-1. AC text to be amended; decision taken 2026-07-16 |
| Liberación de pistas | §6 | blocked by DES-92 |
| Evidencias (QR + trivia) | §4 | both forms, regardless of correctness |
| Penalizaciones | §5 | |
| Secuencia conservada para auditoría | §1, §2 | `OccurredAt` ordering, event-time not ingest-time |
| Consolidación asíncrona vía consumidores MassTransit | §4, §6 | §5/§7 are in-process by design — same-service, no bus round trip needed |

### Part 2 (HU-40B)

| AC | Changes | Note |
|---|---|---|
| Cambios de puntaje | §7 | |
| Eventos de ranking | §7 | session-scoped |
| Finalizadas/canceladas visibles | §2 | no state filter — D-8 |
| Contexto suficiente para revisar decisiones | §1, §2 | `PayloadSummary` narrative only — D-9 |
| Consolidación asíncrona de puntaje/ranking | §7 | |

### Requisitos funcionales

- **RF-15** (historial con trazabilidad para auditoría) — **fully owned**; currently at zero coverage
  despite the write model existing, because nothing can read the table. §2+§3 close it.
- **RF-14** (publicar eventos de dominio en RabbitMQ) — §6 closes a live F2 gap (`ClueReleased`
  raised but never bussed).
- **RF-17** (consultas separadas de comandos) — §2 is a pure read model fed asynchronously.
- **RF-09** (evidencia registrada con fecha/equipo/sesión/estado) — the record is HU-31's; §4 makes it
  consultable.
- RF-10/RF-11/RF-12 are **not** claimed: §7 displays what HU-37/38/39 produce; it does not compute.

---

## Open questions

_IDs are stable — resolved entries stay listed so references elsewhere keep resolving._

### Resolved 2026-07-16

- **O-1 → D-1 · Keep the bus; amend the AC on DES-100.** Zero implementation cost. Follow-through:
  the ticket text still needs editing.
- **O-3 → D-4 · Inline append in `ApplyPenaltyCommandHandler`, log on failure.** Accepts a small
  audit-row-loss gap. Follow-through: file the two-saves-no-transaction defect separately.
- **O-5 → D-9 · `PayloadSummary` string only, no typed columns.** Revisit only if a DES-60 view spec
  demands in-view aggregation.
- **O-2 → D-10 · Attribute on the external identity id (Keycloak sub), not the internal int.**
  `X-User-Id` *is* the sub; the int is an identity-access detail reachable only via HTTP, and scoring
  cannot resolve it at all. **This grew §1 and pulled session-operations into the first delivery** —
  the earlier "blocks §5 only" note was wrong.
- **O-4 → D-11 · Ranking rows on position change only; `Refresh` computes the flag, the event carries
  it, the history handler filters on it.** Not just a volume fix — it is also the only thing dedup'ing
  a redelivered recalc, since `CalculationVersion` always differs. **§7 is unblocked.**
- **O-6 → D-12 · Drift is one field (`WasExpiredByTimer`), not three; still deferred.** `ActorType` is
  exactly recoverable from the transmitted `ResponsibleUserId`; `CorrelationId` is a per-row random
  GUID that correlates nothing. Both need no contract change — ever, not just not-yet.

### Still open

_None. All open questions resolved as of 2026-07-16._

Two items moved from "open question" to "file a ticket" rather than closing silently — see the
follow-through list: the random-GUID `CorrelationId` column (D-12) and, if the DES-60 view needs it,
`WasExpiredByTimer` on `QuestionClosedIntegrationEvent` (D-12).

---

## Commit sequence

Each step is an independently reviewable PR. Repo allows **merge commits only**; local-squash each
branch to one commit before opening the PR.

1. **§1** — schema + attribution contract + factories + migration. **Two services, one PR.**
   _(unblocked — O-1/O-2/O-5 resolved)_
2. **§2 + §3** — read slice + endpoint + gateway route. **Ship here: RF-15 goes green.**
   ← **first delivery ends here**
3. **§4** — evidence into history.
4. **§5** — penalties into history. _(unblocked — O-2 resolved)_
5. **§7** — score + ranking history (Part 2). _(unblocked — O-4 resolved)_
6. **§6** — `ClueReleased` on the bus. _(blocked by DES-92 — last, so nothing else waits on it)_

Steps 3–5 are mutually independent and can go in parallel — but each depends on §1's schema, so none
is truly parallel *with* step 1.

**Follow-through items outside the code** — all from decisions taken 2026-07-16. None is optional;
forgetting any is how the reasoning gets lost:
- Amend the state-changes AC on DES-100 (D-1).
- File the `ApplyPenaltyCommandHandler` two-saves-no-transaction defect as its own ticket (D-4).
- File the **clue-release wrong-actor bug** as a ticket **for the session-ops-local
  `ClueReleaseRecord`** (`ClueReleaseFacade.cs:27-30`). Note the *outbound event* half of this is
  **in-scope blocking work inside §6**, not follow-up — see the §6 blocker. What is deferrable is only
  whether the locally-persisted record gets corrected too.
- Consider filing two adjacent smells found with O-2, both latent rather than live:
  `ScoreEntry.RecordedByUserId` is dead (every caller passes `null`); and
  `ApplyPenaltyCommandHandler.cs:41` uses unguarded `Guid.Parse` where
  `ScoringSessionAuthorizationProxy` uses `TryParse` — a non-Guid `X-User-Id` on the Administrator
  path yields 500 instead of 401/403.
- File the session-ops `SessionEvent.CorrelationId` column as a smell (D-12): `SessionEvent.cs:35`
  and `:53` populate it with a fresh `Guid.NewGuid()` per row, so it correlates nothing, is never read
  in `src/`, and the only tests on it assert `NotBeEmpty()` — which a random GUID always passes. Either
  thread a real request-scoped correlation id or drop the column; today it is a field that looks like
  infrastructure and isn't. **Not HU-40 work** — but D-12 declines to propagate it onto a contract, and
  that reasoning should not evaporate.
- If the DES-60 view spec asks to distinguish a timer expiry from an operator-forced close, add
  `WasExpiredByTimer` to **both** copies of `QuestionClosedIntegrationEvent` (S5) and thread it in
  `PublishQuestionClosedIntegrationEventHandler.cs:34-39`, which drops it today (D-12). Additive; the
  domain already carries it at `QuestionClosedEvent.cs:23`.

**Every step:** `make -C backend build SVC=<svc>` → `make -C backend test SVC=<svc>` →
`make -C backend gate SVC=<svc>` → `make -C backend structure-guard SVC=<svc>` (ADR-0011).
Read `.agents/backend-agent.md` before writing service code, per `backend/AGENTS.md`.
