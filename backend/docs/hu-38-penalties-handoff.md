# HU-38 Penalties — Session Handoff (2026-07-15)

> Continuation of the penalty-flow work in `/tmp/umbral-penalty-session-handoff-2026-07-15.md`
> (prior session). That session verified the penalty **write path** via `curl` with a
> `ReferenceTeamId`. This session aligned the E2E to the **real operator dashboard action** and, in
> doing so, found and fixed a genuine bug: the dashboard penalty path was keyed on the wrong team id.
>
> **Status: DONE and verified green.** All changes are uncommitted (working tree). Nothing committed,
> no PR opened.

## What this session did

1. Wrote a dashboard-aligned E2E spec + manual-test doc for HU-38 operator penalties.
2. While making the spec drive the *real* `PenaltyPanel` (not `curl`), discovered penalties were
   keyed on the runtime team id while scoring/ranking key on `ReferenceTeamId` → **a dashboard
   penalty never reduced a team's score.**
3. Fixed the bug (frontend + backend DTO), threading `ReferenceTeamId` through the operator panel.
4. Fixed collateral spec fragility (hardcoded `missionId`, a stale assertion, a wrong column name).
5. Verified everything green: backend unit/integration + frontend typecheck/unit + live E2E.

## The bug (root cause)

- The entire scoring/ranking domain keys on **`ReferenceTeamId`** (cross-context catalog id):
  `TargetResolvedConsumer.cs:24`, `AnswerRegisteredConsumer.cs:29`,
  `RecalculateRankingCommandHandler.cs:29` all say so explicitly. See project memory
  "scoring-ranking-keys-on-referenceteamid".
- But the operator `PenaltyPanel` sent `teamProgress.teamId`, which is the **runtime (session-scoped)
  team id** (`definitions.ts` `OperatorTeamProgressDto.teamId`), and
  `ApplyPenaltyCommandHandler` stores `request.TeamId` verbatim (no translation).
- Effect: the penalty `ScoreEntry` landed under the runtime id, forming a **phantom ranking group**
  (which then clamps to 0), while the real team's total stayed unchanged. The prior session missed
  this because it `curl`ed the penalty with the `ReferenceTeamId` directly, bypassing the panel.

## The fix (frontend + backend DTO)

Chosen approach: expose `ReferenceTeamId` on the operator panel so the panel can send the id scoring
expects (architecturally cleaner than a runtime→reference lookup inside scoring, which doesn't own
that mapping — grants already arrive carrying `ReferenceTeamId`).

Backend `session-operations-service` — thread `ReferenceTeamId` through the operator panel projection:
- `src/Domain/ValueObjects/OperatorTeamProgress.cs` — new `ReferenceTeamId` (field, ctor, `Create`, equality)
- `src/Domain/Entities/LiveSession.cs` (~:871) — `ProjectOperatorSessionPanel` passes `team.ReferenceTeamId`
- `src/Application/Dtos/Sessions/OperatorSessionPanelDto.cs` — `OperatorTeamProgressDto` gains `Guid? ReferenceTeamId`
- `src/Application/Sessions/Common/OperatorSessionPanelDtoFactory.cs` — forwards it

Frontend:
- `app/lib/definitions.ts` — `OperatorTeamProgressDto` gains `referenceTeamId: string | null`
- `app/lib/realtime/session-state-client.ts` — SignalR normalizer maps `referenceTeamId` off the push
- `app/dashboard/DashboardClient.tsx` — **`PenaltyPanel` now sends `referenceTeamId`** (filtering out
  teams that lack one). The sibling `OperatorClueReleasePanel` / `OperativeCluePanel` deliberately
  **keep the runtime `teamId`** — they target session-ops runtime, not scoring.

Test updates required by the above:
- `session-operations-service/tests/.../GetOperatorSessionPanelQueryHandlerTests.cs` — the
  `PanelDto_ContainsOnly...` contract test now expects `ReferenceTeamId` in the team property set.
- `frontend/tests/unit/app/dashboard/operator-team-progress-panel.test.ts` — mocks add `referenceTeamId`.

## New artifacts (this session)

- `frontend/tests/e2e/hu-38-operator-penalty.spec.ts` — live E2E driving the real `PenaltyPanel`
  (select team by label → reason → Apply), asserting UI "−100" **and** the DB recalc (350→250,
  `calculation_version` bump, team name preserved). Mirrors `session-operator-panel.spec.ts`.
- `frontend/docs/hu-38-manual-test.md` — brief manual-test guide (same structure as
  `frontend/docs/hu-26-manual-test.md`).

## Spec robustness fixes made along the way

- **Mission id:** both `hu-38-operator-penalty.spec.ts` and `session-operator-panel.spec.ts` now
  resolve the mission by NAME (`'E2E Seed Mission'`) instead of a hardcoded `missionId: 1`. The dev
  DB volume is long-reused and reseeds push mission ids up (seed mission was id **94**, not 1), so
  `missionId: 1` 404'd silently. `tests/setup/global-setup.ts:122` already resolves by name for this
  exact reason — the specs now match that convention.
- **Stale assertion:** `session-operator-panel.spec.ts:96` narrowed from `/ranking|winner|penalt/i`
  to `/ranking|winner/i` — the HU-38 `PenaltyPanel` legitimately renders a "Penalty" heading on the
  operator hero now, so `penalt` is no longer a leak.
- **Wrong column:** the spec's helper queried `penalties.reason`; the actual column is
  `penalty_reason` (`PenaltyConfiguration.cs:26`). Fixed.
- **Loud setup:** `hu-38` `beforeAll` now asserts the session-create response is ok / has a
  `liveSessionId`, instead of letting a silent non-2xx surface later as `uuid: "undefined"`.

## Verification (all green)

- `make -C backend build SVC=session-operations-service` — OK
- `make -C backend test  SVC=session-operations-service` — **1140/1140** pass (367 + 386 + 387)
- `frontend`: `tsc --noEmit` clean; `operator-team-progress-panel.test.ts` 12/12
- **Live E2E** (docker stack up; `session-operations-service` rebuilt/restarted onto the new DTO):
  `pnpm exec playwright test session-operator-panel.spec.ts hu-38-operator-penalty.spec.ts --workers=1`
  → **session-operator-panel 2/2 PASS, hu-38-operator-penalty 1/1 PASS.**
- DB-confirmed for the E2E session: penalty `ScoreEntry.team_id = a0000000-…-0001` (the
  `ReferenceTeamId`, not a runtime GUID); ranking `total_score = 250`, `calculation_version = 2`,
  `team_display_name = "Gilded Owls"` preserved.

## Gotchas for the next session

- **Run the two specs with `--workers=1`** (or in isolation). Their `beforeAll` hooks both
  DELETE+INSERT the same admin-sub identity row (`ExternalIdentityId`, uniquely indexed), so running
  them in parallel races to a duplicate-key error. Pre-existing property of the pair; not re-architected.
- **`session-operations-service` runs in dev hot-reload** (`dotnet watch`, bind-mounted source, see
  `docker-compose.override.yml`). `docker compose up --build` is a no-op for it. To pick up backend
  source changes, `docker compose restart session-operations-service`. During this session its
  `dotnet watch` was found wedged on a stale `obj/project.assets.json` (`NETSDK1064 MediatR.Contracts`)
  while an old process served the stale DTO — a `restart` forced a clean restore/build and fixed it.
- The dev DB volume is drifted/reused; do **not** assume fresh-seed ids. Resolve by name.

## Known-unrelated issues (still open, from the prior handoff — NOT addressed here)

- **Operator-assignment projection gap (Outstanding Issue #1):** session-ops does not publish
  `LiveSessionOperatorAssignedIntegrationEvent`, so scoring's `session_operator_assignments` stays
  empty and the penalty POST 403s until the row is patched. The E2E spec patches it in `beforeAll`
  (with op-1's Keycloak sub, matched in `ScoringSessionAuthorizationProxy`); the manual-test doc has
  a step-4 patch. Real fix = publish that integration event from session-ops.
- **~~Ranking REST/mobile 403~~ — MISDIAGNOSED, see below.** This was recorded as
  "`ParticipantSessionMembershipClient` gets a 404 from the session-ops membership endpoint, blocking
  the read-side ranking API." That is **wrong on every count** and should not be carried forward.

  **Corrected 2026-07-15, verified against the live stack:**
  - The membership endpoint **cannot return 404** — `ValidateParticipantSessionMembershipQueryHandler`
    has no 404 path; it always returns **200** with a reason code (`allowed`, `participant-not-in-session`,
    `team-not-in-session`, …). Probed live: `200 {"isAllowed":true,…,"reasonCode":"allowed"}`.
  - The **read path is not blocked**. `GET /api/sessions/{id}/ranking` returns **200** for a genuine
    participant member.
  - **Mobile updates live on a penalty.** Proven end-to-end: `participant-1` self-joined a real session,
    connected to `/hubs/scoring` through the gateway over WebSockets, `JoinSessionGroup` was **allowed**
    by the same guard, and the operator's penalty pushed `RankingChanged v=2` carrying
    `Gilded Owls totalScore=250` to that connection. The push keys on `ReferenceTeamId`, which is what
    `team-space.tsx` matches on, so `useScoreDrop` fires the "PENALTY APPLIED" toast.
  - **The real bug is a role mismatch:** `RankingController` admits `ParticipantOrOperator`, but the
    guard behind it calls a `Participant`-only session-ops endpoint. Same session/team, role header the
    only difference: `Participant → 200`, `Operator → 403`. An operator probing the ranking endpoint
    (likely how the original 403 was seen) is refused by design-accident. **Still open — needs a product
    call on whether operators may read rankings.**
  - **Why it was misdiagnosed:** `ParticipantSessionMembershipClient.Deny(...)` accepted a `reason`
    argument and then **discarded it**, returning the constant `"session-ops-unavailable"` for every
    failure. An authorization refusal was reported as a transport outage, which sent the investigation
    hunting for a missing/unreachable endpoint. **Fixed** in this session: the real reason code is now
    preserved on the DTO *and* logged (`session-ops-http-403`, `session-ops-timeout`, …).

## Suggested next steps

1. Commit this work (branch off `develop`; note the working tree also carries unrelated prior-session
   penalty changes — scope the commit to the files listed under "The fix" + "New artifacts" + the
   test updates). Open a PR; call out the transport-matrix note from the prior handoff.
2. Close the operator-assignment projection gap (publish `LiveSessionOperatorAssignedIntegrationEvent`
   from session-ops) so the `beforeAll` DB patch can go. The ranking read path needs **no** work for
   participants — it already returns 200; what remains is the operator-vs-participant role mismatch on
   `RankingController`, which is a product decision, not a defect to fix blind.
3. Consider a backend unit/integration test asserting the projection actually populates
   `ReferenceTeamId` (currently proven only via the live E2E).

## Suggested skills

- `verify` / `run` — before claiming any further penalty/ranking path is green (this repo rewards
  live E2E over unit-only confidence).
- `code-review` — review the diff before the PR.
- `rabbitmq-events-dotnet` — if you take on the `LiveSessionOperatorAssignedIntegrationEvent` gap.
- `handoff` — if the next session pauses mid-work again.

## Key files (quick index)

- Bug fix (backend): `session-operations-service/src/{Domain/ValueObjects/OperatorTeamProgress.cs,
  Domain/Entities/LiveSession.cs, Application/Dtos/Sessions/OperatorSessionPanelDto.cs,
  Application/Sessions/Common/OperatorSessionPanelDtoFactory.cs}`
- Bug fix (frontend): `frontend/app/{dashboard/DashboardClient.tsx, lib/definitions.ts,
  lib/realtime/session-state-client.ts}`
- Handler that stores the id verbatim: `scoring-monitoring-service/.../ApplyPenalty/ApplyPenaltyCommandHandler.cs`
- E2E + manual doc: `frontend/tests/e2e/hu-38-operator-penalty.spec.ts`, `frontend/docs/hu-38-manual-test.md`
- Prior handoff: `/tmp/umbral-penalty-session-handoff-2026-07-15.md`
