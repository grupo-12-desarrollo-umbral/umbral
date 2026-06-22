# HU-15 — How to Test: Create a session from an active mission

**Ref:** HU-15 / DES-22 / DES-70
**Branch:** feature/hu-15-session-creation-from-mission
**Date:** 2026-06-21
**Scope under test:** the two HU-15 commits — `feat(mission-design): mission runtime-plan read endpoint`
(`08410a0`) and `feat(sessions): create live sessions from a mission runtime snapshot` (`aca9bb0`,
which also lands the publication-aware readiness fix, Phases 1–2 of
`plans/fix-published-quiz-readiness-gap.md`).

> **Goal of this doc:** let you manually confirm HU-15 end-to-end before moving DES-22 to Done and
> opening the PR. Every endpoint, payload, status code, and message below is verified against source
> (`SessionsEndpoints.cs`, `MissionsEndpoints.cs`, `CreateSessionFacade.cs`, `SessionCreationPolicy.cs`,
> `ProblemDetailsExceptionHandler.cs`, `MissionTriviaPublicationChecker.cs`) — not assumed.

---

## What HU-15 delivers (the acceptance surface)

1. **`Mission` is the only session source.** `POST /api/sessions` takes a mission id only — no
   `SourceTriviaQuizId`, no `SessionMode`. A `TriviaQuiz` can no longer create a session.
2. **Eligibility gate.** A session is created **only** from a mission that is **active** *and*
   **runtime-ready**. Inactive or not-ready ⇒ rejected with `409 Conflict`.
3. **Immutable snapshot.** On creation the service freezes a `MissionRuntimeSnapshot` — a base copy of
   the mission's full runtime plan (stages → substages → targets/clues, and for trivia substages the
   resolved questions/options/scores/timers) — so later edits to the mission never mutate the session.
4. **Initial state `Scheduled`.** The created `LiveSession` starts in `Scheduled`.
5. **Readiness is publication-aware (Phases 1–2 of the readiness-gap fix).** If a trivia substage's
   quiz is archived after activation, the mission reports `isReady=false`, so session creation is
   refused; and if an empty trivia substage slips through the race, creation is refused at snapshot
   build with a clear `409` instead of failing at play time.

---

## Verified contract (what you'll call)

Services authenticate via **trust headers** (`X-User-Id` / `X-User-Role` / `X-User-Email`) injected by
the gateway after it validates the JWT (`TrustedHeadersAuthenticationHandler`,
`DependencyInjection.cs:88`). For manual testing you can either go **through the gateway with a real
admin JWT**, or — simpler — hit each **service port directly with the trust headers**, exactly as the
integration tests do (`AddTrustedHeaders(client, "99", "Administrator", "admin@example.com")`).

| Service | Direct host port | Through gateway |
|---|---|---|
| mission-design-service | `http://localhost:5001` | `http://localhost:8000` (JWT) |
| session-operations-service | `http://localhost:5003` | `http://localhost:8000` (JWT) |

> Even when you call session-operations directly, it calls mission-design **internally** over the
> docker network (`/api/missions/{id}/readiness` and `/api/missions/{id}/runtime-plan`) — so the whole
> stack must be up (`docker compose up -d` in `backend/`).

**Endpoints exercised:**

| Verb | Service · Route | Request | Purpose |
|------|-----------------|---------|---------|
| GET | md · `/api/missions/{id}/runtime-plan` | — | Resolved plan the snapshot freezes (200) |
| GET | md · `/api/missions/{id}/readiness` | — | `{ missionId, activationState, isReady, failures[] }` |
| POST | md · `/api/missions/{id}/activate` | — | Activate a ready mission |
| DELETE | md · `/api/missions/{id}` | — | Deactivate a mission |
| **POST** | **so · `/api/sessions`** | `{ missionId, title, maximumTimeMinutes, scheduledAt }` | **Create the session (201)** |

**`POST /api/sessions` success → `201 Created`** with body
`{ liveSessionId, sessionCode, title, sessionState, scheduledAt }` and `sessionState = "Scheduled"`
(`CreateSessionResultDto`, `CreateSessionFacade.cs:62`).

**Request validation** (`CreateSessionCommandValidator`): `missionId > 0`, `title` non-empty,
`maximumTimeMinutes > 0`, `scheduledAt` not default ⇒ otherwise `400 Validation failed.`

---

## Setup — get a mission to `Ready` + active

You need one active, runtime-ready mission. The robust way (no guessing the structural minimums) is to
**author, then poll `/readiness` and fix whatever `failures[]` reports until `isReady:true`**, then
activate. A **TreasureHunt-only** mission is the simplest happy path (no quiz required).

```bash
MD=http://localhost:5001
HDR=(-H "X-User-Id: 99" -H "X-User-Role: Administrator" -H "X-User-Email: admin@example.com" -H "Content-Type: application/json")

# 1. Create a mission
MID=$(curl -s "${HDR[@]}" -X POST "$MD/api/missions" \
  -d '{"name":"HU-15 smoke","description":"manual test","difficulty":"Easy","maximumTimeMinutes":30}' \
  | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')

# 2. Add a Stage, then a Substage under it (capture their ids from the returned MissionResponse)
curl -s "${HDR[@]}" -X POST "$MD/api/missions/$MID/nodes" \
  -d '{"nodeType":"Stage","title":"Stage 1","sequenceOrder":0}'
# → note the stage id, then:
curl -s "${HDR[@]}" -X POST "$MD/api/missions/$MID/nodes" \
  -d '{"nodeType":"Substage","title":"Hunt","sequenceOrder":0,"stageId":<STAGE_ID>,"playMode":"TreasureHunt"}'

# 3. Add at least one Target to the TreasureHunt substage
curl -s "${HDR[@]}" -X POST "$MD/api/missions/$MID/stages/<STAGE_ID>/substages/<SUBSTAGE_ID>/targets" \
  -d '{"name":"Fountain","qrCode":"QR-001","sequenceOrder":0,"isActive":true,"winnerScore":100}'

# 4. Poll readiness — fix whatever it complains about until isReady is true
curl -s "${HDR[@]}" "$MD/api/missions/$MID/readiness"
#   { "missionId":.., "activationState":"...", "isReady":false, "failures":["..."] }

# 5. Activate once ready
curl -s "${HDR[@]}" -X POST "$MD/api/missions/$MID/activate"
```

> **Trivia path (needed for Scenario D):** set a substage to `Trivia` and select a **published** quiz
> via `POST .../substages/{ssid}/trivia-quiz-selection {"triviaQuizId":N}`. Scenario D below walks the
> full quiz create → publish → select → archive sequence. A trivia substage is only `Ready` while its
> quiz is `Published`. (Quiz authoring itself is mission-design's HU-12 surface, not HU-15.)

---

## Scenarios

### A — Happy path: active + ready mission ⇒ 201 with a `Scheduled` snapshot  *(AC1–4)*

```bash
SO=http://localhost:5003
curl -i "${HDR[@]}" -X POST "$SO/api/sessions" \
  -d "{\"missionId\":$MID,\"title\":\"Friday night run\",\"maximumTimeMinutes\":30,\"scheduledAt\":\"2026-07-01T18:00:00Z\"}"
```

**Expect `201 Created`** and a body like:

```json
{ "liveSessionId":"<guid>", "sessionCode":"AB12CD", "title":"Friday night run",
  "sessionState":"Scheduled", "scheduledAt":"2026-07-01T18:00:00+00:00" }
```

✔ `sessionState` is `Scheduled`. ✔ `Location` header points at `/api/sessions/<guid>`.

**Confirm the snapshot is frozen (AC3):** first `GET $MD/api/missions/$MID/runtime-plan` and eyeball
the stages/substages/targets; that resolved plan is what got copied. (The frozen copy lives in
`session_operations`; the snapshot is immutable by construction — later mission edits do not change an
already-created session.)

### B — Inactive mission ⇒ 409 "inactive"  *(AC2 — rejects deactivated source)*

```bash
curl -s "${HDR[@]}" -X DELETE "$MD/api/missions/$MID"          # deactivate
curl -i "${HDR[@]}" -X POST "$SO/api/sessions" \
  -d "{\"missionId\":$MID,\"title\":\"x\",\"maximumTimeMinutes\":30,\"scheduledAt\":\"2026-07-01T18:00:00Z\"}"
```

**Expect `409 Conflict`**, `ProblemDetails.detail`:
`Mission '<id>' cannot be used to create a new session because the mission is inactive.`
(`SessionCreationPolicy.cs:13`, exception message `MissionNotEligibleForSessionCreationException`.)

*(Re-activate with `POST /activate` before continuing.)*

### C — Not-runtime-ready mission ⇒ 409 "not runtime-ready"  *(AC2 — readiness gate)*

Use a mission that is active but **not** ready (e.g. a substage missing required content — `/readiness`
shows `isReady:false`). Then:

```bash
curl -i "${HDR[@]}" -X POST "$SO/api/sessions" \
  -d "{\"missionId\":$MID,\"title\":\"x\",\"maximumTimeMinutes\":30,\"scheduledAt\":\"2026-07-01T18:00:00Z\"}"
```

**Expect `409 Conflict`**, detail ends with `because the mission is not runtime-ready.`

### D — Readiness-gap fix: archive a referenced quiz ⇒ mission goes not-ready ⇒ 409  *(Phase 1–2)*

This is the defect the fix closes. It needs a **Trivia** substage pointing at a **published** quiz on a
mission that is active + ready, after which archiving the quiz must demote the mission. Fully
self-contained below (`$MD`/`$HDR` from Setup).

**D.1 — Create a quiz with one valid question, then publish it.** A quiz must be published to be
selectable; publish requires at least one question with a correct option.

```bash
# Create (Draft) — returns { "id":.., "status":"Draft", ... }
QID=$(curl -s "${HDR[@]}" -X POST "$MD/api/trivias" -d '{
  "title":"HU-15 quiz","description":"manual test",
  "questions":[{
    "prompt":"Capital of France?","sequenceOrder":0,"isActive":true,
    "scoreValue":100,"timeLimitSeconds":30,
    "options":[
      {"optionText":"Paris","sequenceOrder":0,"isCorrect":true},
      {"optionText":"Lyon","sequenceOrder":1,"isCorrect":false}
    ]
  }]
}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')

# Publish — returns { "status":"Published", "isSourceReady":true, ... }
curl -s "${HDR[@]}" -X POST "$MD/api/trivias/$QID/publish"
```

**D.2 — Author a Trivia substage on a mission and select the published quiz.** Build a mission as in
Setup but make the substage `Trivia` (no targets), then attach the quiz. A `Trivia` substage is `Ready`
only while its quiz is `Published`.

```bash
# (reusing $MID with its <STAGE_ID>; add a Trivia substage and capture its id)
curl -s "${HDR[@]}" -X POST "$MD/api/missions/$MID/nodes" \
  -d '{"nodeType":"Substage","title":"Quiz round","sequenceOrder":1,"stageId":<STAGE_ID>,"playMode":"Trivia"}'
# → note <TRIVIA_SUBSTAGE_ID>, then select the published quiz:
curl -s "${HDR[@]}" -X POST \
  "$MD/api/missions/$MID/stages/<STAGE_ID>/substages/<TRIVIA_SUBSTAGE_ID>/trivia-quiz-selection" \
  -d "{\"triviaQuizId\":$QID}"

# Make sure the mission is active + ready (activate if not already)
curl -s "${HDR[@]}" "$MD/api/missions/$MID/readiness"        # expect isReady:true
curl -s "${HDR[@]}" -X POST "$MD/api/missions/$MID/activate"
```

**D.3 — Archive the quiz, then watch the mission go not-ready and the session create fail.**

```bash
# Archive — returns { "status":"Archived", ... }
curl -s "${HDR[@]}" -X POST "$MD/api/trivias/$QID/archive"

# Readiness now flips to false with the publication failure
curl -s "${HDR[@]}" "$MD/api/missions/$MID/readiness"
#   { ..., "isReady":false,
#     "failures":["Trivia substage 'Quiz round' in stage 'Stage 1' must select a published trivia quiz."] }

# Session creation is refused
curl -i "${HDR[@]}" -X POST "$SO/api/sessions" \
  -d "{\"missionId\":$MID,\"title\":\"x\",\"maximumTimeMinutes\":30,\"scheduledAt\":\"2026-07-01T18:00:00Z\"}"
```

**Expect:**
1. `GET /readiness` (before archive) → `isReady:true`.
2. `POST /archive` → `200` with `"status":"Archived"`.
3. `GET /readiness` (after archive) → **`isReady:false`** with failure
   `Trivia substage '<title>' in stage '<title>' must select a published trivia quiz.`
   (`MissionTriviaPublicationChecker.cs:56`.)
4. `POST /api/sessions` → **`409 Conflict`** … `the mission is not runtime-ready.`

> **Race variant (Phase 2 guard):** if a trivia substage's runtime plan resolves to **zero questions**
> at the moment of creation, the snapshot build rejects it with `409 Conflict`,
> `Type: "trivia-substage-empty"` (`ProblemDetailsExceptionHandler.cs:65`) — instead of the old
> silent empty set that only blew up at play time.

### E — Authorization & validation guards

- **Non-admin** (`X-User-Role: Operator`) on `POST /api/sessions` ⇒ `403 Forbidden`
  (endpoint requires `AuthorizationPolicies.Administrator`, `SessionsEndpoints.cs:24`).
- **Bad payload** (`missionId:0`, empty `title`, `maximumTimeMinutes:0`, or missing `scheduledAt`) ⇒
  `400 Validation failed.`
- **Unknown mission id** ⇒ `404 Resource not found.` (`NotFoundException` in the facade).

---

## Acceptance-criteria → scenario map

| # | Acceptance criterion (DES-22) | Scenario |
|---|---|---|
| 1 | Operator can only select **active** missions to create a session | A (succeeds) + B (rejects inactive) |
| 2 | Creation from a **deactivated** mission is rejected | B |
| 3 | Session is bound to a **single source mission** | A (one `missionId`; no quiz/source fields accepted) |
| 4 | Session keeps a **base copy** of the mission at creation time | A (snapshot frozen; `runtime-plan` is the source) |
| — | Initial state is `Scheduled` | A (`sessionState:"Scheduled"`) |
| — | Readiness is **publication-aware** (gap fix) | C, D |

---

## Automated tests (run these too)

The behaviors above are also covered by suites on this branch — run them before closing:

```bash
cd backend
make test SVC=session-operations-service
make test SVC=mission-design-service
make gate SVC=session-operations-service     # ADR-0005 coverage gate
make gate SVC=mission-design-service
```

Key files that pin HU-15:
- session-operations — `tests/IntegrationTests/Api/CreateSessionEndpointTests.cs` (gate: ready ⇒ 201,
  inactive/not-ready ⇒ 409), `tests/Application.UnitTests/Sessions/Commands/CreateSession/*`
  (`CreateSessionFacadeTests`, `...HandlerTests`, `...ValidatorTests`),
  `tests/UnitTests/Domain/Entities/MissionRuntimeSnapshotTests.cs` (snapshot invariants).
- mission-design — `tests/IntegrationTests/Api/MissionEndpointsTests.cs` (runtime-plan + readiness,
  incl. archived-quiz ⇒ `isReady:false`), `tests/UnitTests/Domain/Services/MissionActivationPolicyRuntimePlanTests.cs`.

---

## Pre-close checklist

- [ ] Stack up (`docker compose up -d`), both services healthy.
- [ ] Scenario A returns `201` + `sessionState:"Scheduled"`; `runtime-plan` matches the seeded mission.
- [ ] Scenario B returns `409 … inactive`.
- [ ] Scenario C returns `409 … not runtime-ready`.
- [ ] Scenario D: archiving a referenced quiz flips `isReady` to `false` and `POST /api/sessions` → `409`.
- [ ] Scenario E: non-admin `403`; bad payload `400`; unknown mission `404`.
- [ ] `make test` + `make gate` green for **both** services.
- [ ] No `SessionMode` / `TriviaQuiz`-as-source field appears in any session request/response.

## Notes / caveats verified against source

- Session code is a random 6-char uppercase token (`CreateSessionFacade.cs:152`) — non-deterministic;
  don't assert a fixed value.
- The mission's internal source id is a deterministic MD5 of `mission-runtime:{id}`
  (`CreateSessionFacade.cs:167`) — an interim until a richer source identity lands; fine for HU-15.
- `activationState` strings are `Ready` / `Inactive` (`MissionActivation.cs`); session-ops treats
  `IsActive = activationState != "Inactive"` and gates on the independently recomputed `isReady`
  (`MissionReadinessSource`) — which is why an active "Ready" mission with an archived quiz still fails
  the gate (Scenario D).
</content>
