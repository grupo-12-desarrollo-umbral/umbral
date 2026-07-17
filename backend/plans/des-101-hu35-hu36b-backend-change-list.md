# DES-101 (HU-35 + HU-36B) — Backend implementation plan (session-operations)

_Trivia results at question close: participant reveal (HU-35) + operator post-close review
(HU-36B). This is a **read + push-enrichment** slice — **no new domain aggregate, no
persistence, no migration**. All work is Application-layer reads over already-persisted data,
plus two fields on one notification DTO._

Companion client plans (implement **after** this lands + curl-verifies):
- Operator dashboard → `frontend/plans/des-101-frontend-trivia-results.md`
- Participant reveal → `mobile/plans/hu-m4-result-reveal.md`

Plan shape mirrors the client plans (Verified surface → Decisions → per-change Scope+Gate →
AC mapping → Open Questions → Commit Sequence).

**Audience routing — the two `svc:` labels are not equal:**
- `svc:scoring-monitoring-service` → **zero change** (already built — see §3).
- `svc:session-operations-service` → all changes below (small).

---

## Verified existing surface (what already exists — do not rebuild)

| Piece | Location | Note |
|---|---|---|
| Close orchestration | `Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs:50` `CloseAndAdvanceAsync` | already broadcasts `QuestionClosed` (lines 75-81) |
| Close notification DTO | `Application/Sessions/Common/Notifications/QuestionClosedNotificationDto.cs` | `(LiveSessionId, QuestionIndex, ClosedAt, WasExpiredByTimer)` — correlation-only |
| Close broadcaster | `Api/Hubs/SignalRSessionQuestionBroadcaster.cs` (`QuestionClosedMethod`, group `live-session:{id}`) | operators + participants both in group |
| Snapshot selector | `Application/Sessions/Common/TriviaQuestionSnapshotSelector.cs` | ⚠️ returns options as `string[]` — **strips `IsCorrect`** |
| Reveal data (in-domain) | `TriviaQuestionSnapshot.Explanation` (:59), `TriviaOptionSnapshot.IsCorrect` (:16) / `SequenceOrder` (:14) | present, not projected at close |
| Per-team answer + points | `Domain/Entities/TriviaAnswerSubmission.cs:45-49` (`SelectedOptionSequenceOrder`, `IsCorrect`, `ScoreValue`) | persisted |
| Operator pre-close monitor (mirror) | `Application/Sessions/Queries/GetOperatorTriviaAnsweredMonitor/*` + `TriviaAnsweredMonitorDtoFactory.cs` + `LiveSession.ProjectActiveQuestionAnsweredStatus()` (`LiveSession.cs:515`) | HU-36A — the pattern §2/§4 mirror; **deliberately omits** option/correctness/points |
| Operator endpoint (mirror) | `Api/Controllers/SessionsController.cs:329` `GET {id}/answered-monitor` `[Authorize(Policy = Operator)]` | mirror for §4's route + authz |
| Resolver Proxy | `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (ADR-0009) | reuse for §4 authz — no ad-hoc role checks |

## Architecture Decisions

- **A-1 · Signal-triggered fetch, not fat push.** The enriched `QuestionClosed` push carries
  only the *question-level* reveal (correct option + explanation), identical for everyone on
  the `live-session:{id}` group. Team-specific result (HU-35) and the operator review table
  (HU-36B) are served by **reads** the clients fire on the close signal — keeps per-audience
  authorization on the read path, keeps the push small.
- **A-2 · §1 reads `question.Options` directly, not the selector's string list.**
  `TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion` returns the `TriviaQuestionSnapshot`
  **and** a stripped `string[]`; take the question object and read
  `question.Options.Single(o => o.IsCorrect).SequenceOrder` + `question.Explanation`. Do not
  try to derive the correct order from the string list.
- **A-3 · §2/§4 mirror the HU-36A monitor stack** (query → handler → DTO → factory), but over
  the *closed* question and including the withheld fields. **A-3a (resolve at implementation,
  O-2):** confirm whether `LiveSession` loads its `TriviaAnswerSubmission` children (so a
  domain projection like `ProjectClosedQuestionAnswerReview(seq)` mirrors
  `ProjectActiveQuestionAnsweredStatus`), or whether they load via a separate repository (then
  the handler queries that repo). The monitor projects from the aggregate; verify the
  submissions are on it before copying that shape.
  **Resolved (O-2):** `LiveSession` loads its `TriviaAnswerSubmission` children as part of the
  aggregate — owned collection (`LiveSessionConfiguration.cs:727` `OwnsMany`, flattened onto the
  session table) and eagerly hydrated by `LiveSessionRepository.GetByIdAsync`
  (`.Include(session => session.TriviaAnswerSubmissions)`, line 25). So §2/§4 use a **domain
  projection** mirroring `ProjectActiveQuestionAnsweredStatus` — no separate repository query.
- **A-3b · Key the closed projection on `(SubstageSnapshotId, QuestionSequenceOrder)`, not just
  `sequenceOrder`.** `ProjectActiveQuestionAnsweredStatus` resolves its question through
  `ActiveSubstageId`/`ActiveQuestionIndex` (`LiveSession.cs:517,731,740`) — the live window,
  which is exactly wrong for a *closed* question. The new projection
  (`ProjectClosedQuestionAnswerReview`) must take the substage snapshot id + sequence order
  pair (the stable snapshot key — `TriviaAnswerSubmission.cs:41-42`,
  `TriviaQuestionSnapshot.SubstageSnapshotId`/`SequenceOrder` at `:49,53`) and carry a
  **"question is closed" guard** in place of the active-window guard (A-5). The team→submission
  left-join itself is unchanged: iterate `_teams`, `SingleOrDefault` on
  `(TeamId, SubstageSnapshotId, QuestionSequenceOrder)`, emit a not-answered row on null
  (`LiveSession.cs:519-536`) — never-answered teams are already first-class.
- **A-3c · `AnsweredAt` is a factory-boundary rename of `SubmittedAt`.** There is no `AnsweredAt`
  field in the domain; the timestamp is `EvidenceSubmission.SubmittedAt` (`:78`,
  `DateTimeOffset`, non-nullable). The HU-36A factory already renames it
  (`LiveSession.cs:536`); §2/§4 do the same. The `?` on the four submission-derived DTO fields
  models "no submission for this team" (never-answered), **not** a nullable domain field — all
  four are non-nullable on `TriviaAnswerSubmission`.
- **A-3d · Identity is the session-scoped `TeamId`, not `ReferenceTeamId`.** Legitimate here
  because `ScoreValue` is pre-snapshotted on the submission, not recomputed from ranking. But
  this is the same aggregate where scoring/ranking keys on `ReferenceTeamId`
  (`Team.cs:56`, `LiveSession.cs:629,815,872`) — **if** the operator review ever cross-joins to
  leaderboard/ranking data, switch the key to `ReferenceTeamId`.
- **A-4 · §4 authz = the mandated `Proxy`** (matrix HU-36 → `Proxy`): gate through
  `ISessionAdministrationAccessResolver` exactly like the monitor handler — no ad-hoc role
  `if` checks. `[Authorize(Policy = Operator)]` on the endpoint.
- **A-5 · No reveal leaks pre-close.** Activation already strips `IsCorrect`
  (`QuestionActivatedNotificationDto` sends bare option strings); only `CloseAndAdvanceAsync`
  emits the enriched payload. §2/§4 must reject reads for a question that has not closed.

---

## Change §1 — Enrich the `QuestionClosed` reveal payload (HU-35, question-level)

**Scope**
- **Edit** `QuestionClosedNotificationDto.cs` — add `int CorrectOptionSequenceOrder`,
  `string? Explanation`.
- **Edit** `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` (lines 70-81). After the close,
  resolve the closed question:
  `var (question, _) = TriviaQuestionSnapshotSelector.GetOrderedTriviaQuestion(session, closedQuestionIndex);`
  then populate `CorrectOptionSequenceOrder = question.Options.Single(o => o.IsCorrect).SequenceOrder`
  and `Explanation = question.Explanation` (per A-2).
- Broadcaster/interface unchanged (same method, same group).

**Gate**
- `make -C backend test SVC=session-operations-service` — `TriviaRoundOrchestratorFacade`
  tests assert the correct-option order + explanation are on the close push; a test asserts
  they are **absent** from the activate push (A-5). DTO/broadcaster tests updated.

## Change §2 — Participant "my team result" read (HU-35, team-scoped)

**Scope**
- **New** `Application/Sessions/Queries/GetTeamTriviaQuestionResult/` —
  `GetTeamTriviaQuestionResultQuery(LiveSessionId, QuestionSequenceOrder)` + handler +
  `TriviaTeamQuestionResultDto(SelectedOptionSequenceOrder?, IsCorrect?, ScoreValue?, CorrectOptionSequenceOrder, Explanation)`.
  Participant-authorized, scoped to the caller's own team; return that team's
  `TriviaAnswerSubmission` for the **closed** question, keyed on
  `(SubstageSnapshotId, QuestionSequenceOrder)` (A-3b — resolve the snapshot id from the closed
  question, **not** `ActiveSubstageId`, which points at the live window), nullable when the team
  never answered.
- **New endpoint** on `SessionsController` — mirror the monitor endpoint (line 329);
  `GET {liveSessionId}/trivia/questions/{sequenceOrder:int}/my-result`, Participant policy.
- Reject if the question has not closed (A-5).

**Gate**
- Handler unit tests: caller's team → its answer + reveal; team never answered → null
  answer fields + reveal still present; a not-yet-closed question → rejected.

## Change §4 — Operator post-close answer/points review (HU-36B) — the one net-new read

**Scope**
- **New** `Application/Sessions/Queries/GetOperatorTriviaAnswerReview/` —
  `GetOperatorTriviaAnswerReviewQuery(LiveSessionId, QuestionSequenceOrder)` + handler +
  `TriviaAnswerReviewDto(LiveSessionId, QuestionSequenceOrder, IReadOnlyList<TriviaTeamAnswerReviewDto>)`
  where each row = `(TeamId, TeamCode, DisplayName, SelectedOptionSequenceOrder?, IsCorrect?, ScoreValue?, AnsweredAt?)`
  (nullable for never-answered teams). Mirror `GetOperatorTriviaAnsweredMonitor*` +
  `TriviaAnsweredMonitorDtoFactory`; add the withheld fields. `TeamId`/`TeamCode`/`DisplayName`/
  `AnsweredAt` are already carried by the monitor projection (from the `Team` entity +
  `SubmittedAt→AnsweredAt` rename, A-3c); the three withheld fields come off
  `TriviaAnswerSubmission` (`:45,47,49`). New projection
  `LiveSession.ProjectClosedQuestionAnswerReview(substageSnapshotId, sequenceOrder)` keyed per
  A-3b (not the active resolver), with the A-5 closed guard. `TeamId` is session-scoped (A-3d).
- **Authorization = `Proxy`** via `ISessionAdministrationAccessResolver` (A-4).
  `[Authorize(Policy = Operator)]`.
- **New endpoint** on `SessionsController` — mirror line 329;
  `GET {liveSessionId}/trivia/questions/{sequenceOrder:int}/answer-review`, Operator policy.
- **Real-time:** none new — operators already receive the enriched `QuestionClosed` on
  `live-session:{id}` (via `JoinLiveSessionAsOperatorAsync`) and use it as the fetch trigger.

**Gate**
- Handler unit tests: Administrator sees any session; assigned Operator sees only theirs;
  unassigned → `ForbiddenAccessException` (403). Projection covers answered + never-answered
  teams. Endpoint smoke returns the per-team review after close.
- **Pattern gate:** authz genuinely flows through the resolver Proxy — no role `if` in the
  handler/endpoint.

---

## §3 · scoring-monitoring — ranking after close: NOTHING TO DO

HU-35 AC "team sees the updated ranking after close" is **already served**:
`GET /api/sessions/{id}/ranking` (`RankingController.cs:14`) + live `RankingChanged` push
(`BroadcastRankingRefreshedHandler.cs:29`, reacting to the `RankingRefreshed` notification →
`ScoringHub`). Clients already consume it (mobile
`podium-leaderboard`). **No change in this service.**

## §4b · Participant reveal consumer is the mobile app (not a blocker)

✅ The participant surface is the **`mobile/` Expo app** — already built end-to-end (join,
reconnect into `live-session:{id}`, answer, close-lock; ranking already shown). The enriched
push (§1) reaches connected participants with no hub change. Mobile withholds correctness/points
today, waiting on this reveal — the consuming UI is the **HU-M4** slice
(`mobile/plans/hu-m4-result-reveal.md`), fed by §1 + §2.

## §5 · Cross-cutting checklist

- [ ] **API gateway routes** — add the two new session-operations endpoints (§2, §4) to
      `api-gateway` routing (`appsettings*.json` are already dirty — confirm the new paths).
- [ ] **Coverage gate** — new queries/handlers clear the ADR-0005 threshold (`make gate`);
      thin projections, so handler unit tests carry it.
- [ ] **No migration** — verify `dotnet ef migrations has-pending-model-changes` stays a
      no-op (no schema touched).
- [ ] **Curl-verify shapes** before the clients wire against them — closes O-1 in both client
      plans.

---

## Acceptance Criteria → change mapping

| AC (DES-101) | Change | Surface that renders it |
|---|---|---|
| HU-36B: operator sees each team's answer after close | §4 | frontend panel |
| HU-36B: operator sees correctness | §4 | frontend panel |
| HU-36B: operator sees points | §4 | frontend panel |
| HU-36B: review real-time, no reload | §4 (reuses §1 signal) | frontend panel |
| HU-35: reveal correct option | §1 | mobile HU-M4 |
| HU-35: explanation if configured | §1 | mobile HU-M4 |
| HU-35: team told correct/incorrect | §2 | mobile HU-M4 |
| HU-35: reveal real-time, no reload | §1 (existing channel) | mobile HU-M4 |
| HU-35: team sees updated ranking | none (scoring §3 already built) | mobile (already shipped) |

## Open Questions

- **O-1** — Endpoint paths (§2/§4) are proposals; confirm final routes and curl-verify shapes
  before the clients consume them.
- **O-2 (A-3a) — RESOLVED.** `LiveSession` loads its `TriviaAnswerSubmission` children as part
  of the aggregate (owned collection in `LiveSessionConfiguration.cs:727`, eagerly `.Include`-ed
  in `LiveSessionRepository.GetByIdAsync:25`). §2/§4 use a domain projection mirroring
  `ProjectActiveQuestionAnsweredStatus` — no separate repository query.

## Bottom line

| Service | Domain | Persistence | New reads | Push change |
|---|---|---|---|---|
| session-operations | none | none | 2 (§2 participant, §4 operator) | enrich 1 DTO (§1) |
| scoring-monitoring | none | none | none | none |

## Commit Sequence

1. `feat(session-operations): reveal correct option + explanation on question close — HU-35`  _(§1)_
2. `feat(session-operations): participant trivia question result read — HU-35`  _(§2)_
3. `feat(session-operations): operator post-close trivia answer review — HU-36B`  _(§4)_

_All three are one small slice in one service; land together on one branch, curl-verify, then
unblock the mobile + frontend plans in parallel._
