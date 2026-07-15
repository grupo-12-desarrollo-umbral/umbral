# DES-99 / PR 228 review handoff - 2026-07-14

## Purpose

Capture the defensible findings from this session's review of PR `#228` against Linear ticket `DES-99`.

References:
- Linear: `DES-99` - HU-37 + HU-39 - Ledger de puntaje y ranking en tiempo real
- GitHub PR: `https://github.com/grupo-12-desarrollo-umbral/umbral/pull/228`
- Branch reviewed: `feature/hu-37-39-ledger-ranking`
- Diff basis used in review: `git diff develop...HEAD`
- Local brief: `backend/docs/hu37-39-brief.md`
- Local context: `backend/docs/hu37-39-context.md`

## Final review stance

Revised 2026-07-14 after validating every finding against the code, and then confirming the runtime
findings against the running local stack rather than by reading config alone. The earlier stance was wrong
in both directions: it understated one finding, kept one that the code disproves, and missed the two
largest defects entirely. The list below replaces the previous one.

### Findings that realistically need fixes

1. `develop` does not compile — PR #228 dropped the OpenTelemetry package references.
   - Current behavior: `dotnet build src/Api/Api.csproj` on `develop` HEAD (`6bd38de`) fails with two
     `CS0246` errors — `ObservabilityExtensions.cs:3` and `:4` import `OpenTelemetry.Logs` and
     `OpenTelemetry.Trace`, but the scoring service's `Api.csproj` references no OpenTelemetry package.
     Scoring is the only service in this state: session-ops, identity-access and mission-design each carry
     the same `ObservabilityExtensions.cs` plus six OpenTelemetry `PackageReference` entries.
   - How it happened: the scaffold commit `2aa2c79` created `Api.csproj` with the full set (33 lines,
     including `Npgsql.OpenTelemetry`, `OpenTelemetry.Api`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`,
     `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`,
     `OpenTelemetry.Instrumentation.Http`). Commit `6e917da` (PR #228) recreates the file as new (24 lines)
     and drops that entire `ItemGroup`, while `ObservabilityExtensions.cs` — untouched since `2aa2c79` —
     still needs it. `git diff 2aa2c79:...Api.csproj` against HEAD shows exactly this removal and nothing
     else of substance.
   - Relevant code:
     - `backend/services/scoring-monitoring-service/src/Api/Api.csproj`
     - `backend/services/scoring-monitoring-service/src/Api/ObservabilityExtensions.cs:3-4`
     - Working reference: `backend/services/session-operations-service/src/Api/Api.csproj:25-30`
   - Why it matters: this is a hard blocker on the mainline, not just on the PR branch. The fix is to
     restore the six package references from the scaffold version.
   - Trap for whoever tests this: the local `scoring-monitoring-service` container runs `dotnet watch`, so
     the failed build leaves it serving a **stale binary from before PR #228**. Its OpenAPI document lists
     only `/health` and `/alive` — no ranking route — and its logs show `Build FAILED` with the same two
     `CS0246` errors. Any manual probe of the ranking endpoint against that container returns a misleading
     404. Confirm the container rebuilt cleanly before trusting any result from it.
   - Worth raising with the team: CI apparently did not block the merge on this.

2. The ranking surface is unreachable through the API gateway.
   - Current behavior: the gateway routes `/api/sessions/{**catch-all}` to the **session-ops** cluster,
     but `RankingController` serves `/api/sessions/{liveSessionId}/ranking` from the **scoring** service.
     The request lands on session-operations, which has no such action, and 404s. The only gateway route
     to scoring is `/api/scoring/{**catch-all}`, which has no path transform and matches no controller in
     the scoring service. The hub has the same problem: `/hubs/{**catch-all}` routes to session-ops, which
     maps only `/hubs/sessions`, while `ScoringHub` is mapped at `/hubs/scoring` in the scoring service.
   - Confirmed empirically against the running compose stack, authenticated with a real Keycloak token
     (`admin`, realm `umbral`), gateway on `:8000`:

     | Probe | Result | Reading |
     |---|---|---|
     | `GET /api/sessions` via gateway | `200` | `/api/sessions/**` reaches session-ops |
     | `GET :5003/api/sessions` direct to session-ops | `200` | same handler, positive control |
     | `GET /api/sessions/{id}/ranking` via gateway | `404` | lands on session-ops, which has no such route |
     | `GET :5003/api/sessions/{id}/ranking` direct to session-ops | `404` | identical result, confirms the destination |
     | `GET /hubs/sessions` via gateway | `403` | reached session-ops' hub, rejected by authorization |
     | `GET /hubs/scoring` via gateway | `404` | session-ops has no such hub — the route never reaches scoring |
     | `GET /api/scoring/health` via gateway | `404` | forwarded verbatim; scoring serves `/health`, not `/api/scoring/health` |

     The `/hubs/` pair is the decisive one: `403` versus `404` shows both hub requests are being served by
     session-ops. If `/hubs/**` reached the scoring service, `/hubs/scoring` would answer `401`/`403`, not
     `404`. This holds regardless of finding 1, since it never depends on the scoring service running.
     YARP matches exactly one route per request and no route is more specific than
     `/api/sessions/{**catch-all}`, so the ranking path cannot reach scoring as configured.
   - Relevant code:
     - `backend/api-gateway/src/appsettings.json` (routes `session-ops`, `session-ops-hubs`, `scoring`)
     - `backend/api-gateway/src/appsettings.Development.json` (same routes, same collision)
     - `backend/services/scoring-monitoring-service/src/Api/Controllers/RankingController.cs`
     - `backend/services/scoring-monitoring-service/src/Api/Program.cs` (`MapHub<ScoringHub>("/hubs/scoring")`)
   - Why it matters: DES-99 requires the ranking to be available to participants and operation in real time
     and broadcast over SignalR. As deployed, neither the HTTP read nor the hub is reachable from the
     frontend, so the frontend contract in `backend/docs/hu37-39-brief.md` cannot be satisfied. Both config
     files agree, so this is not environment drift.
   - Note: this is why the endpoint being `[AllowAnonymous]` (finding 4) is not a live exposure today.

3. The `ResolutionTime` tie-break is permanently inert.
   - Current behavior: recalculation loads resolution times from the previous ranking's rows, and
     `Ranking.Refresh` substitutes `ResolutionTime.NonComparable()` for any team missing from that
     dictionary. The only production caller of `ResolutionTime.Comparable(...)` is the EF value converter,
     which merely re-materializes a value already in the database. No code path ever originates one.
     The loop is therefore closed and unseeded: the first recalculation has no prior rows, so every team
     gets `NonComparable`, that is what persists, and the next recalculation reads it back. The
     `resolution_time` column can only ever be NULL and the tie-break branch is dead code in production;
     every tie collapses to a shared rank.
   - Relevant code:
     - `backend/services/scoring-monitoring-service/src/Application/Rankings/Commands/RecalculateRanking/RecalculateRankingCommandHandler.cs:26`
     - `backend/services/scoring-monitoring-service/src/Domain/Entities/Ranking.cs:62`
     - `backend/services/scoring-monitoring-service/src/Domain/Services/ResolutionTimeRankingPolicy.cs:15`
     - `backend/services/scoring-monitoring-service/src/Infrastructure/Persistence/Configurations/RankingConfiguration.cs:80`
   - Why it matters: DES-99 requires ties to be resolved by the defined `ResolutionTime` criterion. That
     criterion never runs. This is an unmet acceptance criterion, not a weak implementation.
   - Why the tests do not catch it: `ResolutionTimeRankingPolicyTests`, `RankingTests`,
     `RecalculateRankingCommandHandlerTests` and `RankingRepositoryIntegrationTests` all inject comparable
     values by hand, exercising a path no production caller can reach.
   - The fix is local and cheap: `AnswerRegisteredIntegrationEvent` carries `SubmittedAt`, which already
     flows into `ScoreEntry.RecordedAt`, so a real per-team resolution time can be derived from the ledger
     itself — which is also what "the ranking projection sources from the score ledger" asks for.

4. The ranking HTTP endpoint should be guarded, and the obvious fix is a no-op.
   - Current behavior: `GET /api/sessions/{liveSessionId}/ranking` is `[AllowAnonymous]`, while `ScoringHub`
     in the same service is guarded twice — `[Authorize(Policy = ParticipantOrOperator)]` on the class and
     `RequireAuthorization(...)` on the hub mapping.
   - Relevant code:
     - `backend/services/scoring-monitoring-service/src/Api/Controllers/RankingController.cs:13`
     - `backend/services/scoring-monitoring-service/src/Api/Hubs/ScoringHub.cs:7`
     - `backend/services/scoring-monitoring-service/src/Api/DependencyInjection.cs:27` (no `FallbackPolicy`)
   - Why it matters: the service authenticates via the `TrustedHeaders` scheme over gateway-injected
     `X-User-*` headers, so it is built to enforce locally; the hub already does. The controller beside it
     not doing so is an inconsistency within one service, which is the strongest argument here — stronger
     than the exposure argument, since finding 2 means the route is currently unroutable anyway.
   - Fix detail worth stating explicitly: `AddAuthorization` registers no `FallbackPolicy`, so deleting the
     `[AllowAnonymous]` attribute leaves the endpoint open. The fix must **add**
     `[Authorize(Policy = AuthorizationPolicies.ParticipantOrOperator)]`.

## Findings discussed and intentionally downgraded

These were considered during the session but should not be treated as strong blockers for DES-99 by themselves.

1. Ranking refresh trigger coverage.
   - Reason downgraded: **withdrawn — the code disproves it.** The earlier concern was that refresh fires
     only from consumed `ScoreEntryRegistered` events and might miss trivia question closure.
     (Numbering note: this was finding 2 in the pre-revision list; the "finding 2" above is the gateway
     routing collision, which is a different item entirely.)
     `QuestionClosedIntegrationEvent` does exist in session-operations, but it carries only
     `LiveSessionId`, `QuestionIndex` and `ClosedAt` — no team, no score — and its own summary states it
     carries correlation data only, "no score/ranking, which is computed downstream by ScoringMonitoring".
     The ranking is a pure fold over `ScoreEntry` rows, so a closure-triggered recalculation would produce
     an identical ranking plus a bumped `CalculationVersion` and a redundant broadcast. Question closure is
     not a score-affecting fact. `backend/docs/hu37-39-brief.md` also mandates refresh off the consumed
     score event. No product confirmation is needed.
   - Caveat: if finding 3 is fixed by deriving resolution time from closure timing rather than from the
     ledger, revisit this — but the ledger-derived fix is preferred and avoids the coupling.

2. Missing penalty flow.
   - Reason downgraded: `backend/docs/hu37-39-brief.md` explicitly marks penalty work as out of scope for
     this slice and tied to HU-38.

3. Missing QR / `TargetResolved` consumer.
   - Reason downgraded: `backend/docs/hu37-39-brief.md` explicitly says the QR path is blocked upstream and
     should not be faked in this slice.

4. `net10.0` target framework.
   - Reason downgraded: this may be a standards/process concern, but it was too aggressive to frame it as a
     DES-99 acceptance-criteria failure without proof that the repo/platform contract rejects it.

5. `Scores` vs `ScoreEntries` application-area naming.
   - Reason downgraded: real naming/structure drift, but not a convincing ticket-level defect.

## Session outcome

If another agent or reviewer continues this line of work, the most defensible review comments should focus on:

1. `develop` does not compile — restore the OpenTelemetry package references to scoring's `Api.csproj` (blocker)
2. Gateway routing collision — the ranking endpoint and the scoring hub are unreachable (blocker)
3. `ResolutionTime` tie-break never executes — unmet HU-39 acceptance criterion
4. Ranking endpoint authorization — add `[Authorize]`, do not merely drop `[AllowAnonymous]`

Finding 1 blocks everything else, including any attempt to verify the rest at runtime. Findings 2 and 3 are
what keep DES-99 from meeting its acceptance criteria. Finding 4 is a smaller consistency fix. Anything
beyond those points should be argued carefully and probably tied back to the local brief in
`backend/docs/hu37-39-brief.md` before being escalated.

Notes for whoever picks this up:
- The unit and integration tests on this branch pass while every one of these is present, so a green test
  run is not evidence against them. The `ResolutionTime` tests in particular pass only because they inject
  values that no production code path can construct.
- Do not trust a manual probe against the local `scoring-monitoring-service` container until finding 1 is
  fixed and the container has rebuilt — it currently serves a pre-#228 binary and will 404 the ranking
  endpoint for the wrong reason. Check `docker logs backend-scoring-monitoring-service-1` for `Build FAILED`
  and `curl :5004/openapi/v1.json` for the ranking path before drawing conclusions.
- The gateway routing evidence in finding 2 was gathered with the stale container and is unaffected by it:
  the `/hubs/sessions` `403` versus `/hubs/scoring` `404` contrast is decided entirely inside session-ops.

## Suggested skills

- `review` - if re-running the PR review from a different base or after fixes
- `handoff` - if this investigation needs to be continued in another session
- `rabbitmq-events-dotnet` - if follow-up work changes the event flow or trigger coverage
- `signalr-websockets-aspnetcore` - if follow-up work changes ranking broadcast/auth behavior
- `cqrs-mediatr-aspnetcore` - if follow-up work changes recalculation orchestration
