# HU-16 Context — Trivia Session Creation

> Paste this section into any agent session that needs context for HU-16.
> Last updated: 2026-06-03 | Branch: `feature/hu-16-trivia-session-creation`
>
> Scope note: HU-16 is the first **session-creation** slice in
> `session-operations-service`. HU-07A/07B already landed the `LiveSession`
> aggregate and its `Create` method; HU-16 does **not** recreate them. HU-16 adds
> the operator-driven *creation* use case: select a **published** trivia quiz,
> generate a **fixed copy** of it, and open a `LiveSession` bound to that single
> quiz source. The quiz content lives in `mission-design-service`, so this slice
> is dual-service: session-ops orchestrates and owns the session + snapshot;
> mission-design is read as the authoritative source of the published quiz.

## ⚠️ Resolution gaps (must close before the driver runs — Stop 1)

These were unmet when this file was generated. Resolve them, then re-confirm:

1. **DES-23 is in `Backlog` and is missing `ready-for-agent`.** It carries
   `svc:session-operations-service`, `svc:mission-design-service`, `Feature`.
   The driver pre-flight (and generator step 1) require `ready-for-agent` +
   `svc:<service>`. Apply `ready-for-agent` and move readiness through the normal
   gate before driving.
2. **No `session-operations-service` PRD exists** — not in Linear, not in
   `backend/docs/prd/`. HU-07B had the same gap and used a `<DES-PRD-SESSION-OPS>`
   placeholder. Resolve/author the owning session-ops PRD (or confirm the team
   decision to drive session-ops slices from the canonical model docs) and record
   the DES id in the state block below before implementation. Scope in this file
   is derived from the **DES-23 acceptance criteria**, the **patterns matrix**,
   and the **landed code** — not from a PRD.

## State

- `DES-23` (HU-16): status **Backlog**; labels `svc:session-operations-service`,
  `svc:mission-design-service`, `Feature`. **Missing `ready-for-agent`** (see gap 1).
- Predecessors (all **Done**):
  - session-ops: `DES-11` (HU-07A), HU-07B — `LiveSession` aggregate baseline
  - mission-design: `DES-17` (HU-11), `DES-18` (HU-12), `DES-20` (HU-14A),
    `DES-21` (HU-14B) — quiz authoring, publication/archive, questions/options
- PRD ref: **`<DES-PRD-SESSION-OPS>` — unresolved** (see gap 2). Mission-design
  supporting PRD: `DES-62`.
- Blocks (downstream): `DES-24` (HU-17), `DES-25` (HU-18), `DES-26` (HU-19),
  `DES-28` (HU-21A), `DES-44` (HU-33A).
- Branch: `feature/hu-16-trivia-session-creation`, base = **`develop`**
  (HU-07B merged via PR #17).

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Facade` (mandated) | X.2 Application | "Session creation from a quiz orchestrates source checks, fixed copy, and side effects." | A single application-layer orchestration entry point (`CreateTriviaSession` handler / facade) coordinates the subsystems — published-quiz lookup, publication-state check, fixed-copy construction, `LiveSession.Create`, persistence, event publication — instead of scattering those steps across the endpoint or leaking them into the domain. The endpoint stays thin and delegates to the one facade. |

**No `Proxy` mandate for HU-16.** The matrix lists HU-16 under `Facade` only. The
create endpoint is still operator-authorized, but that authorization **inherits**
the existing gateway + `AuthorizationBehaviour`/endpoint-policy guard (ADR-0001/0002,
applies-where) — it does **not** add a new pattern gate. Reuse the policy plumbing
landed in HU-07B; add an operator authorization policy if one is not present.

Transport note: HU-16 has **no** SignalR/RabbitMQ obligation. It is a synchronous
REST creation slice. Live transport begins at HU-21A/22/33A.

## What predecessors have already landed (reuse candidates)

All on `develop`.

**session-operations-service (HU-07A/07B)**
- `LiveSession` aggregate with `LiveSession.Create(sessionMode, source, sessionCode,
  titleSnapshot, maximumTimeMinutes, scheduledAt)` — already validates source-type
  matches mode (`ValidateSourceForMode`: `Trivia` ⇒ `SessionSourceType.TriviaQuiz`),
  requires non-empty code/title, sets `State = Scheduled`, raises
  `LiveSessionCreatedEvent`.
- `SessionSource` value object = `(SessionSourceType SourceType, Guid SourceEntityId)`
  — gives single-source association (AC #3) for free.
- Enums: `SessionMode` (TreasureHunt/Trivia), `SessionSourceType` (Mission/TriviaQuiz),
  `SessionState` (Scheduled/Preparing/Active/Paused/Finished/Cancelled).
- Value objects: `MaximumTime`, `TeamCode`. Entities: `Team`, `SessionParticipant`,
  `JoinContext`. `TitleSnapshot` persisted column on `LiveSession`.
- Cross-service HTTP client pattern: `ParticipantMembershipAccessClient`
  (`Infrastructure/Identity/`) + `IParticipantMembershipAccessClient`
  (`Application/Common/Interfaces/`) + options class — the template to mirror for
  the mission-design read client.
- Application plumbing: MediatR, `AuthorizationBehaviour`, `ValidationBehaviour`,
  `ICurrentUser`, `Authorize` attribute, `NotFoundException`/`ForbiddenAccessException`/
  `ValidationException`.
- API: `SessionsEndpoints` group at `/api/sessions` (currently only the reconnect
  POST), `EndpointGroupBase`, `AuthorizationPolicies`, SignalR `SessionsHub`.
- Persistence: `ApplicationDbContext`, `LiveSessionConfiguration`, EF migrations,
  `ILiveSessionRepository`.

**mission-design-service (HU-11/12/13/14A/14B)**
- `TriviaQuiz` aggregate (keyed by **`int`**) with `Status`
  (`TriviaQuizStatus.Draft=0 / Published=1 / Archived=2`), `PublishedAt`,
  `IsSourceReady => Status == Published`, `Questions` → `TriviaOption`s.
- API: `GET /api/trivias/{id:int}` (full detail incl. `Status` + questions/options),
  `GET /api/trivias` catalog, plus publish/archive/retire/duplicate. The detail
  endpoint is the read contract HU-16 consumes for the fixed copy + publication gate.

## What this HU adds

| Concern | New work |
|---|---|
| Domain | A **fixed, immutable copy** of the quiz owned by `LiveSession` — a snapshot of questions, options, correct answer, and explanation captured at creation (e.g. `TriviaSnapshot`/`SessionQuiz` + child value objects). This is the real new domain work; HU-07B stored only a `TitleSnapshot` string + a `SessionSource` reference, **not** the content copy. Extend/overload `LiveSession.Create` (or add a `CreateTrivia` factory) to attach the snapshot at creation. |
| Application | `CreateTriviaSession` command + handler as the mandated **`Facade`**: orchestrate operator authz, published-quiz lookup, **publication-state assertion** (Published only; reject Draft/Archived), fixed-copy construction, `LiveSession.Create`, persistence, event. Add an application port `IPublishedTriviaQuizSource` (read the published quiz by id, returning status + full content). Command validator for required inputs. |
| Infrastructure | Implement `IPublishedTriviaQuizSource` as an HTTP client to mission-design `GET /api/trivias/{id}` (mirror `ParticipantMembershipAccessClient` + options + DI). EF configuration + migration for the snapshot tables; repository wiring to persist `LiveSession` with its snapshot. |
| API | `POST /api/sessions` (trivia) endpoint, operator-authorized, thin — delegates to the `CreateTriviaSession` facade. Returns the created session id + summary. |
| Frontend | Operator "create trivia session" flow: list **published** quizzes only (mission-design catalog filtered to Published), pick one, submit creation, show the created session. |

## Touched surfaces

- `backend/services/session-operations-service/` — Domain (snapshot), Application
  (facade + port), Infrastructure (mission-design client + migration), Api (endpoint).
- Cross-service read dependency on `mission-design-service` `GET /api/trivias/{id}`.
- Frontend operator session-creation screen.
- New API contract: `POST /api/sessions` (request: source quiz id, title, max time,
  scheduled-at; response: live session id + summary).

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **ID type mismatch (decide first).** mission-design quizzes are keyed by `int`
  (`/api/trivias/{id:int}`), but `SessionSource.SourceEntityId` is a `Guid`. HU-16
  must reconcile this before writing the command — either carry the source quiz id
  as the `int` it actually is (separate field/VO) or change/extend `SessionSource`.
  Do not silently coerce an `int` into a `Guid`.
- **"Copia fija" = full content snapshot, immutable.** AC #2 means a frozen copy of
  questions/options/correct-answer/explanation, captured at creation, so later
  edits, archival (HU-12), retire/duplicate (HU-13) of the source quiz do **not**
  affect a scheduled/running session. Don't model it as a live reference back into
  mission-design at gameplay time. The downstream spine (HU-33A present, HU-34A/34B
  answer, HU-35 reveal, HU-37A score) reads this snapshot.
- **AC #1 vs AC #4 are two sides of the same rule.** "Only select published" (#1)
  is partly a read-side catalog filter (mission-design catalog → Published); the
  **authoritative** enforcement is the server-side publication-state assertion at
  creation (#4) inside the facade. The gate lives in X.2, not the UI.
- **Don't build the state machine here.** `LiveSession` is created in `Scheduled`.
  Valid state transitions are HU-21A's `State`/`Chain of Responsibility` scope —
  HU-16 only creates. Don't pre-empt it.
- **`LiveSession.Create` already exists** — reuse it, don't fork a parallel
  creation path. It needs a `sessionCode`; decide the code-generation strategy in
  the application facade (unique per active session).
- **Namespace is `umbral_backend.*`** across all session-ops layers (not a
  per-service root namespace) — match the existing files.
- **Coverage gate ≥ 93%** (ADR-0005) on X.4; the snapshot domain + facade handler
  paths (published OK, draft rejected, archived rejected, missing quiz) carry most
  of the new coverage.
