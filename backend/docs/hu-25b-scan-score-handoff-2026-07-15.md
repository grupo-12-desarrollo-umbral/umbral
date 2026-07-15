# HU-25B Scan → Score Handoff — 2026-07-15

**Status: COMPLETE — all three bugs fixed and verified live end-to-end.** This document is the
authoritative record of the scoring write-path investigation (Session 3). For older related history
see `/HANDOFF.md` (root, gitignored) Sessions 1–2. Nothing here is committed yet.

## Symptom (reported)

Following `mobile/docs/hu-25b-manual-test.md`, a participant scans a target QR, the scanner turns
green / the API returns **200**, but the mobile TEAMS podium never changes.

## The core discovery

The **entire real gameplay → score path had never worked**. Every score in the DB was `created_by =
seed`, which masked the failure — trivia *looked* fine only because of seed rows. Three compounding
bugs, each hidden behind the previous one:

| Bug | What broke | User-visible effect |
|---|---|---|
| **A** | MassTransit routed every scan event to `*_skipped` (never consumed) | score never moves at all |
| **B** | Score keyed on session-scoped `TeamId`, not `ReferenceTeamId` | a phantom team row appears instead of the team's row updating |
| **C** | Ranking recalc name lookup unauthenticated in a consumer context | ranking rows show raw GUIDs instead of names |

---

## Bug A — MassTransit contract URN mismatch (the reported blocker)

**Root cause.** The publisher (`session-operations-service`) declares
`TargetResolvedIntegrationEvent` / `AnswerRegisteredIntegrationEvent` in namespace
`umbral_backend.Application.Sessions.Common`. The scoring consumer declared *identically-named*
records in `umbral_backend.Application.Scores.Common`. `[EntityName("...")]` only pins the RabbitMQ
**exchange** (so the message reaches the consumer's queue), but MassTransit matches a deserialized
message to a consumer by its **message-type URN** `urn:message:{namespace}:{Type}` — which defaults to
the .NET namespace. Different namespace → different URN → the consumer does not recognize the message
→ MassTransit moves it to the `<Consumer>_skipped` queue, unconsumed and silent.

**Diagnosis technique (reusable).**
```
docker compose exec rabbitmq rabbitmqctl list_queues name messages consumers
# a non-zero  TargetResolved_skipped / AnswerRegistered_skipped  is the tell
docker compose exec rabbitmq rabbitmqadmin get queue=TargetResolved_skipped count=1 ackmode=ack_requeue_true
# inspect the envelope's "messageType" URN vs. the consumer's namespace
```
Confirmed live: 2 messages in `TargetResolved_skipped`, 14 in `AnswerRegistered_skipped`; the envelope
`messageType` read `urn:message:umbral_backend.Application.Sessions.Common:TargetResolvedIntegrationEvent`.

**Fix.** Pin the URN on each scoring-side record to the publisher's namespace:
```csharp
[MessageUrn("umbral_backend.Application.Sessions.Common:TargetResolvedIntegrationEvent")]
[EntityName("session-target-resolved")]
public sealed record TargetResolvedIntegrationEvent(...);
```
> **MassTransit 8.4.1 gotcha:** the `[MessageUrn]` value must **omit** the `urn:message:` prefix — the
> library prepends it. Including the prefix throws `ArgumentException: Value should not contain the
> default prefix 'urn:message:'` at message-handling time (observed as a `TargetResolved_error`
> `TypeInitializationException` on the first attempt).

House convention (see `.claude/skills/rabbitmq-events-dotnet`) assumes publisher and consumer share the
*same contract type/namespace*; the bug came from duplicating the record per service. `[MessageUrn]`
bridges that without moving namespaces.

---

## Bug B — wrong team identity (session vs reference)

Teams carry two ids: session-scoped `TeamId` (e.g. `4e4678e5…`) and cross-context `ReferenceTeamId`
(e.g. `a0000000-…-0001`). Mobile sends/highlights by `ReferenceTeamId`, the seed writes rows under it,
and the Session-1 membership guard compares it. But the scan events carried the session-scoped
`TeamId`, so a real scan created a phantom row instead of updating the team.

**Fix.** Enrich `TargetResolvedEvent` / `AnswerRegisteredEvent` (and both integration contracts) with
`ReferenceTeamId` + `TeamDisplayName`, resolved at the raise site in `LiveSession` via
`GetTeam(submission.TeamId)` using `team.ReferenceTeamId ?? team.TeamId`. The scoring consumers now
pass `ReferenceTeamId` as the score entry's `TeamId`. The events **still also carry the session-scoped
`TeamId`** because the operator-facing `TeamAnsweredNotificationHandler` keys on it — do not repurpose
that field.

---

## Bug C — ranking rows show raw GUIDs

`RecalculateRankingCommandHandler` resolved names via `GET /api/sessions/{id}/teams/names`, which is
`[Authorize(ParticipantOrOperator)]`. The recalc runs inside a MassTransit consumer with **no HTTP
user**, so `ICurrentUser` was empty → the call was unauthorized → empty dict → rows fell back to
GUID strings.

**Fix.** The display name now rides the integration event and is snapshotted onto
`score_entries.team_display_name` (new column). Recalc builds names from the score ledger itself,
**preferring the most recent non-empty name** (directly-seeded rows predate the column and carry blank
names with artificially late `recorded_at`, so they must not win). The auth-less `ITeamNameLookupClient`
/ `TeamNameLookupClient` and its DI registration were deleted.

---

## Files changed (all UNCOMMITTED)

**session-operations-service**
- `src/Domain/Events/TargetResolvedEvent.cs`, `src/Domain/Events/AnswerRegisteredEvent.cs` — added `ReferenceTeamId` + `TeamDisplayName`.
- `src/Domain/Entities/LiveSession.cs` — both raise sites resolve the team and pass the two fields.
- `src/Application/Sessions/Common/{TargetResolved,AnswerRegistered}IntegrationEvent.cs` — added the two fields.
- `src/Application/Sessions/EventHandlers/Publish{TargetResolved,AnswerRegistered}IntegrationEventHandler.cs` — map the two fields.

**scoring-monitoring-service**
- `src/Application/Scores/Common/{TargetResolved,AnswerRegistered}IntegrationEvent.cs` — `[MessageUrn]` (Bug A) + the two fields.
- `src/Application/Scores/Consumers/{TargetResolved,AnswerRegistered}Consumer.cs` — key on `ReferenceTeamId`, pass name.
- `src/Application/Scores/Commands/RecordScoreEntry/RecordScoreEntryCommand.cs` (+Handler) — `TeamDisplayName`.
- `src/Domain/Entities/ScoreEntry.cs` — `TeamDisplayName` property (falls back to `teamId.ToString()` when blank).
- `src/Infrastructure/Persistence/Configurations/ScoreEntryConfiguration.cs` — maps `team_display_name`.
- `src/Infrastructure/Migrations/20260715072843_AddTeamDisplayNameToScoreEntry.*` — adds column `NOT NULL DEFAULT ''` (safe for existing rows); auto-applied on startup via `Program.cs` `MigrateAsync()`.
- `src/Application/Rankings/Commands/RecalculateRanking/RecalculateRankingCommandHandler.cs` — names from ledger (prefer latest non-empty); dropped the lookup client.
- **Deleted:** `src/Application/Common/Interfaces/ITeamNameLookupClient.cs`, `src/Infrastructure/Identity/TeamNameLookupClient.cs`, and its `AddHttpClient` registration in `src/Infrastructure/DependencyInjection.cs`.

**frontend** — `tests/e2e/hu-25b-ranking-manual-seed.spec.ts`: seeded `score_entries` now populate
`team_display_name` (otherwise recalc names them blank on any real recalc).

**docs** — `mobile/docs/hu-25b-manual-test.md` §7 troubleshooting row updated with the URN cause +
`*_skipped` diagnosis. `/HANDOFF.md` Session 3 "FINAL STATE" block.

**tests updated** (across both services, mostly the new constructor args + the consumer behavioral
assertion that the command now carries `ReferenceTeamId`):
- scoring: Domain **38/38**, Application **47/47**, Api **5/5** pass; IntegrationTests compile.
- session-ops: Domain **386/386**, EventHandler suite **36/36** pass.

## Verification — live, real scans (not replay)

Session (after a mid-session seed re-run): **code `04156B`, live id
`be3ad84e-7d9a-4a76-a7fd-4dd9c7ccc8d8`, Active**.

- Scanned `HR-25B-QR-1` (+150) and `HR-25B-QR-2` (+100) as Gilded Owls through the real scan endpoint → 200.
- Ranking: **Gilded Owls 600 (1st), Crimson Foxes 200 (2nd)** — both **named**, keyed by
  `ReferenceTeamId` (`a0000000-…0001/0002`), **no phantom row**. `calculation_version 3`.
- `GET /api/sessions/be3ad84e…/ranking?teamId=a0000000-…0001` → 200 with named rows (mobile-facing path).
- All `*_skipped` / `*_error` queues drained to 0; `score_entries.team_display_name` column present.

## Environment notes / gotchas for the next session

- **Both `HR-25B` targets on `04156B` are now resolved** (422 on re-scan). To watch a fresh live update:
  re-run the seed (`frontend` → `npx playwright test tests/e2e/hu-25b-ranking-manual-seed.spec.ts`, gives
  a NEW session code) or delete the relevant `live_session_treasure_evidence_submissions` row.
- The `.NET` services run `dotnet watch` with bind-mounted source; **structural edits (new types, DI,
  migrations) corrupt hot-reload** — always cold-rebuild/restart the container (a process restart =
  clean assembly build), never trust the running watch. Host rebooted mid-session; `docker compose up
  -d` recovers the full stack.
- Tests were run with raw `dotnet test`; the **house-preferred path is the Makefile**
  (`make -C backend {build,test} SVC=<service>`) — worth a confirmatory run before commit.

## Outstanding / recommended next steps

1. **Add a Testcontainers `IConsumer<T>` messaging test** for `TargetResolvedConsumer` /
   `AnswerRegisteredConsumer` — the `rabbitmq-events-dotnet` skill checklist requires it and it would
   have caught Bug A. (None exists today; that gap is why the URN mismatch went unnoticed.)
2. Confirmatory `make -C backend test SVC=…` run for both services.
3. Decide commit strategy — this is a large cross-service change layered on top of the pre-existing
   uncommitted ranking work; logically separable from the mobile error-state work in Session 2.
4. Pre-existing, **unrelated**: 2 failing `ValidateParticipantSessionMembership` unit tests (untracked
   files, not touched here).

## Suggested skills

- `rabbitmq-events-dotnet` — before touching the event contracts / consumer wiring again, and to add the missing messaging test.
- `verify` — re-drive the scan → score → podium flow if any of the above changes.
- `code-review` — once build/tests and the manual flow are green, before commit.

## Related memory (auto-memory, this repo)

- `masstransit-contract-urn-must-match` — the URN gotcha as a durable rule.
- `scoring-ranking-keys-on-referenceteamid` — the team-identity + name-on-event rule.
