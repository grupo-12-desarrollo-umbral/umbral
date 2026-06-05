# HU-34A + HU-34B — Answer Submission Plan

> **Status of context:** HU-33A (trivia round orchestration) is **already merged**. This plan is
> aligned against `backend/docs/trivia_sprint_required_patterns_matrix.md`, the service
> `CONTEXT.md`, and the academic pattern requirements. It reflects what HU-33A already provides
> (broadcaster, participant grouping, Strategy, question DTOs/events) so we don't rebuild it.

## Context

HU-34A registers the **first valid answer per team** for the active trivia question, publishes
`AnswerRegisteredMessage` to RabbitMQ (the academic RabbitMQ producer gate), and broadcasts an
"answered" indicator over SignalR. HU-34B rejects duplicate / late answers from the same ordered
pipeline. The response carries `IsCorrect` + `PointsAwarded` so the mobile app shows correct/wrong
feedback immediately.

### One implementation, two HUs

HU-34A and HU-34B are **a single vertical slice**, not two features and not one indistinct blob:

- **HU-34A** = the happy path — the validation chain passes → record `TriviaAnswerSubmission` →
  publish `AnswerRegistered` (RabbitMQ) + broadcast "answered" (SignalR) → return `{IsCorrect, PointsAwarded}`.
- **HU-34B** = the `FirstAnswerPerTeamHandler` + `ActiveQuestionRequiredHandler` **inside that same chain**.
  When one throws, the caller gets a synchronous rejection-with-reason — no event, no broadcast.

If a grader asks "where is HU-34B," you point at the named handler classes. They are **not** collapsed
into HU-34A's logic.

---

## Required patterns (the rubric this plan is graded against)

Confirmed by the patterns matrix, the service `CONTEXT.md`, and the academic requirements — all three
name the **same failure mode**: "one oversized handler that hardcodes every branch."

| Pattern | Where in this plan | HU |
|---------|--------------------|----|
| **Chain of Responsibility** — composed validation pipeline around submissions | `Domain/Services/AnswerValidation/` handler chain | 34A + 34B |
| **Template Method** — stable validation flow with mode-specific steps | `AnswerValidationHandler` base (`Validate` skeleton, `Check` step) | 34A + 34B |
| **Facade** — narrow coordination service: session op + outbound publish | `AnswerSubmissionFacade` | 34A |
| **State** — `LiveSession` lifecycle enforcement | reuse: `RecordTeamAnswer` gates on `State is Active` | 34A |
| **Strategy** | **already shipped by HU-33A** (`IQuestionActivationStrategy`); not needed here | — |

> ❌ **Do NOT** implement the four checks as inline `if`s in a single `RecordTeamAnswer` method. That is
> the exact anti-pattern all three sources forbid and would mean HU-34A/34B's core deliverable is absent.

---

## What HU-33A already provides (do not rebuild)

| Asset | File | Consequence for this plan |
|-------|------|---------------------------|
| Question broadcaster | `Infrastructure/Realtime/SignalRSessionQuestionBroadcaster.cs` + `Application/Common/Interfaces/ISessionQuestionBroadcaster.cs` | **Extend** it for the "answered" indicator — don't build new infra. Broadcasts to `live-session:{id}` group. |
| Participant grouping | `Api/Hubs/SessionsHub.ReconnectAsync` adds participant to `live-session:{id}`, `team:{id}`, `participant:{id}` | Participants **already receive** `QuestionActivated`/`QuestionClosed`. Mobile just needs listeners. |
| Question DTOs/events | `QuestionActivatedNotificationDto`, `QuestionClosedNotificationDto`, `QuestionActivatedEvent`, `QuestionClosedEvent` | Reuse events. The notification DTO needs the Phase 0 change (options → typed). |
| Domain exceptions | `NoActiveQuestionException`, `QuestionActivationRequiresActiveSessionException`, `QuestionAlreadyActiveException`, `QuestionIndexOutOfRangeException` | **Reuse** — only create the two genuinely-new exceptions. |
| Orchestrator facade | `TriviaRoundOrchestratorFacade` (calls `BroadcastQuestionActivatedAsync`) | Convention to mirror: facade owns the broadcast call, not the hub. |
| Strategy | `IQuestionActivationStrategy` / `SequentialQuestionActivationStrategy` | Confirms DI + facade style. |
| Persistence | migration `AddTriviaRoundState` persists `ActiveQuestionIndex` | The in-memory `TriviaAnswerSubmission` tracker is **not** persisted (see caveat below). |

**Persistence caveat:** the active question survives a restart (persisted), but recorded answers do
**not** (in-memory). HU-34B's duplicate guard therefore resets on restart. Acceptable for the demo.

---

## Phase 0 — Prerequisite: option identity ⚠️ touches merged HU-33A code

`QuestionActivatedNotificationDto.Options` is still `IReadOnlyList<string>` (texts only). The client
needs each option's `SequenceOrder` to identify its submission.

| File | Change |
|------|--------|
| `Application/Sessions/DTOs/QuestionOptionDto.cs` | **New** — `public sealed record QuestionOptionDto(int SequenceOrder, string OptionText)` |
| `Application/Sessions/DTOs/QuestionActivatedNotificationDto.cs` | `IReadOnlyList<string> Options` → `IReadOnlyList<QuestionOptionDto> Options` |
| `Application/Sessions/Facades/TriviaRoundOrchestratorFacade.cs` (≈L98) | `.Select(o => o.OptionText)` → `.Select(o => new QuestionOptionDto(o.SequenceOrder, o.OptionText))` |
| `tests/Application.UnitTests/Sessions/Facades/TriviaRoundOrchestratorFacadeTests.cs` (≈L45) | Assertion `Options.SequenceEqual(new[]{"Option 1A","Option 1B"})` → compare `QuestionOptionDto`s. **Will go red if skipped.** |

> This phase **modifies a finished HU's broadcast contract + its test**. Small, but not "just a new file."

---

## Phase 1 — Domain: the pattern core (Chain of Responsibility + Template Method)

### Validation chain — `Domain/Services/AnswerValidation/`

| File | Role | HU |
|------|------|----|
| `AnswerSubmissionContext.cs` | carries `State`, `ActiveQuestionIndex`, `Snapshot`, `TeamId`, `SelectedOptionSequenceOrder`, `TeamAlreadyAnswered`; receives `ResolvedOption` | — |
| `AnswerValidationHandler.cs` | **abstract base = Template Method.** `sealed Validate(ctx)` skeleton runs `Check(ctx)` then delegates to `_next` (= Chain of Responsibility); abstract `Check(ctx)` | — |
| `SessionMustBeActiveHandler.cs` | throw `QuestionActivationRequiresActiveSessionException` *(reuse)* | 34A |
| `ActiveQuestionRequiredHandler.cs` | throw `NoActiveQuestionException` *(reuse)* — covers "late, after close" | **34B** |
| `FirstAnswerPerTeamHandler.cs` | throw `TeamAlreadyAnsweredException` *(new)* | **34B** |
| `SelectedOptionExistsHandler.cs` | resolve option onto `ctx.ResolvedOption`; throw `AnswerOptionNotFoundException` *(new)* | 34A |
| `AnswerValidationChain.cs` | builder wiring the four in fixed order | — |

```csharp
// AnswerValidationHandler.cs — Template Method + Chain of Responsibility
internal abstract class AnswerValidationHandler
{
    private AnswerValidationHandler? _next;

    public AnswerValidationHandler SetNext(AnswerValidationHandler next)
    {
        _next = next;
        return next;
    }

    public void Validate(AnswerSubmissionContext context) // invariant skeleton
    {
        Check(context);            // specialized step
        _next?.Validate(context);  // ordered delegation
    }

    protected abstract void Check(AnswerSubmissionContext context);
}
```

```csharp
// AnswerValidationChain.cs
internal static class AnswerValidationChain
{
    public static AnswerValidationHandler Build()
    {
        var head = new SessionMustBeActiveHandler();
        head.SetNext(new ActiveQuestionRequiredHandler())
            .SetNext(new FirstAnswerPerTeamHandler())
            .SetNext(new SelectedOptionExistsHandler());
        return head;
    }
}
```

### `Domain/Entities/LiveSession.cs` — record the submission via the chain

```csharp
// in-memory tracker — EF ignores a private Dictionary with a tuple key; not persisted
private readonly Dictionary<(int QuestionIndex, Guid TeamId), int> _triviaAnswerSubmissions = new();

public (bool IsCorrect, int PointsAwarded) RecordTeamAnswer(
    Guid teamId, int selectedOptionSequenceOrder, DateTimeOffset occurredAt)
{
    var alreadyAnswered =
        ActiveQuestionIndex.HasValue &&
        _triviaAnswerSubmissions.ContainsKey((ActiveQuestionIndex.Value, teamId));

    var context = new AnswerSubmissionContext(
        State, ActiveQuestionIndex, TriviaSnapshot,
        teamId, selectedOptionSequenceOrder, alreadyAnswered);

    AnswerValidationChain.Build().Validate(context); // throws on any failed handler

    var question = TriviaSnapshot!.Questions.ElementAt(ActiveQuestionIndex!.Value);
    var option = context.ResolvedOption!;

    _triviaAnswerSubmissions[(ActiveQuestionIndex.Value, teamId)] = selectedOptionSequenceOrder;

    var isCorrect = option.IsCorrect;
    var pointsAwarded = isCorrect ? question.ScoreValue : 0;

    AddDomainEvent(new AnswerRegisteredEvent(
        LiveSessionId, teamId, ActiveQuestionIndex.Value,
        selectedOptionSequenceOrder, isCorrect, pointsAwarded, occurredAt));

    return (isCorrect, pointsAwarded);
}
```

> Use the canonical domain term **`TriviaAnswerSubmission`** (per `CONTEXT.md`) in naming, even though
> the tracker is in-memory for the demo. Naming is not the corner we cut.

### New domain files (only what HU-33A did not already create)

| File | Content |
|------|---------|
| `Domain/Exceptions/TeamAlreadyAnsweredException.cs` | `"Team {teamId} already submitted an answer for the active question."` (plain `Exception`) |
| `Domain/Exceptions/AnswerOptionNotFoundException.cs` | `"Option with SequenceOrder {seq} not found in the active question."` (plain `Exception`) |
| `Domain/Events/AnswerRegisteredEvent.cs` | `BaseEvent` with `LiveSessionId, TeamId, QuestionIndex, SelectedOptionSequenceOrder, IsCorrect, PointsAwarded, OccurredAt` (match repo naming — `...Event`, not `...DomainEvent`) |

---

## Phase 2 — Messaging + infra (parallel with Phase 1)

MassTransit is **not present anywhere** — full install required.

- Add `MassTransit.RabbitMQ` (8.4.1) via `Directory.Packages.props` + Infrastructure `.csproj`.
- `docker-compose.yml` → `session-operations-service`:
  ```yaml
  environment:
    RabbitMQ__Host: rabbitmq
  depends_on:
    rabbitmq:
      condition: service_started
  ```
- `Infrastructure/Messaging/AnswerRegisteredMessage.cs`:
  ```csharp
  namespace UmbralContracts;

  public sealed record AnswerRegisteredMessage(
      Guid LiveSessionId, Guid TeamId, int QuestionIndex,
      int SelectedOptionSequenceOrder, bool IsCorrect, int PointsAwarded,
      DateTimeOffset OccurredAt);
  ```
  Namespace `UmbralContracts` — mirror it exactly in `scoring-monitoring-service` (HU-37A) so
  MassTransit resolves the same exchange without a shared project.

---

## Phase 3 — Application: Facade + CQRS (after Phase 1)

- `Application/Sessions/DTOs/SubmitAnswerResultDto.cs` → `record SubmitAnswerResultDto(bool IsCorrect, int PointsAwarded)`.
- `Application/Sessions/Facades/IAnswerSubmissionFacade.cs` + `AnswerSubmissionFacade.cs` — **the Facade**
  (kept separate, mirroring `TriviaRoundOrchestratorFacade` / `SessionTeamAssociationFacade`). Injects
  `ILiveSessionRepository`, `IPublishEndpoint`, `ISessionQuestionBroadcaster`, `TimeProvider`:
  1. load session (`NotFoundException` if missing)
  2. `session.RecordTeamAnswer(...)` (runs the chain)
  3. `Publish(new AnswerRegisteredMessage(...))` — RabbitMQ
  4. `BroadcastTeamAnsweredAsync(...)` — SignalR (Phase 4)
  5. return `SubmitAnswerResultDto`
  > No `UpdateAsync` — the submission tracker is not EF-tracked; nothing persisted changed.
- CQRS `Application/Sessions/Commands/SubmitAnswer/`:
  - `SubmitAnswerCommand` `[Authorize]` → `IRequest<SubmitAnswerResultDto>` with `LiveSessionId, TeamId, SelectedOptionSequenceOrder`.
  - `SubmitAnswerCommandHandler` — thin, delegates to the facade.
  - `SubmitAnswerCommandValidator` — ids `NotEmpty`, `SelectedOptionSequenceOrder >= 0`.

---

## Phase 4 — API + real-time (after Phase 3)

### Hub method — `Api/Hubs/SessionsHub.cs`
```csharp
public async Task<SubmitAnswerResultDto> SubmitAnswerAsync(
    Guid liveSessionId, SubmitAnswerHubRequest request)
{
    _userContext.Principal = Context.User;
    return await _sender.Send(
        new SubmitAnswerCommand(liveSessionId, request.TeamId, request.SelectedOptionSequenceOrder),
        Context.ConnectionAborted);
}

public sealed record SubmitAnswerHubRequest(Guid TeamId, int SelectedOptionSequenceOrder);
```

### Hub error codes — `Api/Hubs/DomainExceptionHubFilter.cs`
The filter maps unknown exceptions to generic `"ERROR"`. Add two arms so the mobile client gets usable codes:
```csharp
TeamAlreadyAnsweredException => "ALREADY_ANSWERED",
AnswerOptionNotFoundException => "OPTION_NOT_FOUND",
```
(`NoActiveQuestionException` already falls through to a generic code; map it too if the UI needs a distinct "question closed" message.)

### "Answered" broadcast — extend the existing broadcaster (no new infra)
- `ISessionQuestionBroadcaster` + `SignalRSessionQuestionBroadcaster`: add
  `BroadcastTeamAnsweredAsync(TeamAnsweredNotificationDto, ct)`, const `QuestionAnsweredMethod = "TeamAnswered"`,
  broadcasting to `live-session:{id}` (operator monitoring — HU-36A) — optionally also `team:{teamId}`.
- New `Application/Sessions/DTOs/TeamAnsweredNotificationDto.cs` (`LiveSessionId, TeamId, QuestionIndex, AnsweredAt`).
- Called from `AnswerSubmissionFacade` after publish (mirrors `TriviaRoundOrchestratorFacade`).

---

## Phase 5 — DI + build + verify

- `Application/DependencyInjection.cs` → `AddScoped<IAnswerSubmissionFacade, AnswerSubmissionFacade>()`.
- `Infrastructure/DependencyInjection.cs` → `AddMassTransit(x => x.UsingRabbitMq((ctx,cfg) => { cfg.Host(Configuration["RabbitMQ:Host"] ?? "localhost", "/", h => { h.Username("guest"); h.Password("guest"); }); cfg.ConfigureEndpoints(ctx); }))`.
- Verify:
  1. `dotnet build` — zero errors; HU-33A facade test updated and green.
  2. `docker-compose up rabbitmq postgres session-operations-service`.
  3. Participant submits valid answer → response `{ isCorrect, pointsAwarded }`; RabbitMQ UI (`:15672`) shows `UmbralContracts:AnswerRegisteredMessage`; session group receives `TeamAnswered`.
  4. Same team submits again → `ALREADY_ANSWERED` (HU-34B).
  5. Submit after question close → no-active-question rejection (HU-34B).

---

## Phase 6 — Mobile hub contract (`mobile/src/lib/realtime/`)

- `sessions-hub-types.ts`: add `QuestionOptionDto {sequenceOrder, optionText}`,
  `QuestionActivatedNotificationDto {liveSessionId, questionIndex, sequenceOrder, prompt, options[], timeLimitSeconds, activatedAt}`,
  `QuestionClosedNotificationDto`, `TeamAnsweredNotificationDto`,
  `SubmitAnswerHubRequest {teamId, selectedOptionSequenceOrder}`, `SubmitAnswerResultDto {isCorrect, pointsAwarded}`.
  Mirror the backend DTOs **exactly** (Phase 0 + 3 + 4).
- `sessions-hub.ts`: add `onQuestionActivated`, `onQuestionClosed`, `onTeamAnswered`
  (`connection.on(...) / .off(...)`, method names **`"QuestionActivated"`, `"QuestionClosed"`, `"TeamAnswered"`**)
  and `submitAnswer(liveSessionId, req)` → `connection.invoke('SubmitAnswerAsync', liveSessionId, req)`.
- New `answer-policy.ts` (same shape as `reconnect-policy.ts`): parse the `HubException` JSON
  `{code, message}` → discriminated union (`already-answered`, `option-not-found`, `no-active-question`, `error`).
  This consumes the Phase 4 filter codes.

> Dependency satisfied: `ReconnectAsync` already adds the participant to `live-session:{id}`, so these
> broadcasts arrive without any HU-33A change.

---

## Phase 7 — Participant trivia UI (`mobile/src/app/(app)/team-space.tsx` + component)

- New `useTriviaRound` hook: subscribes to `onQuestionActivated` / `onQuestionClosed`, holds the active
  question, a per-question answered/locked flag, and the last result.
- New `<TriviaQuestionCard>` rendered inside `LiveTeamSpace`: prompt, options as `Button`s, question
  countdown (reuse `SessionTimerBar` driven by the question timer), submit → `client.submitAnswer(...)`,
  then correct/wrong state + `fireHaptic('success'|'error')` (helper already in `team-space.tsx`).
- Lock the card after a submit or on `QuestionClosed`; render the mapped `answer-policy` reason on reject.

---

## Out of scope — web frontend (Expo-only UI)

**All UI for this slice is Expo/mobile.** The Next.js operator frontend (`frontend/`) is **not touched**.

- The Phase 4 `TeamAnswered` SignalR broadcast still fires server-side (it is HU-34A's required
  "answered indicator broadcast" per the matrix, independent of any consumer). The mobile app may use it
  (e.g. teammates seeing their team has answered); no web client is required to receive it.
- The operator "who answered" dashboard belongs to **HU-36A**, not this slice. If/when it's built, it
  only adds `onTeamAnswered` to `frontend/app/lib/realtime/session-state-client.ts` against the same
  broadcast — no backend change needed.

---

## Critical path (full slice)

```
Backend 0 → 1 → 3 → 4   +   Expo 6 → 7
```
Phase 2 (RabbitMQ publish) satisfies HU-34A's "event published" requirement even with no consumer
running. The web frontend, the operator monitoring view (HU-36A), and the scoring consumer (HU-37A)
are all out of this slice.

---

## File summary

| Phase | Action | Path |
|-------|--------|------|
| 0 | New | `Application/Sessions/DTOs/QuestionOptionDto.cs` |
| 0 | Modify | `Application/Sessions/DTOs/QuestionActivatedNotificationDto.cs` |
| 0 | Modify | `Application/Sessions/Facades/TriviaRoundOrchestratorFacade.cs` |
| 0 | Modify (test) | `tests/Application.UnitTests/Sessions/Facades/TriviaRoundOrchestratorFacadeTests.cs` |
| 1 | New | `Domain/Services/AnswerValidation/{AnswerSubmissionContext, AnswerValidationHandler, SessionMustBeActiveHandler, ActiveQuestionRequiredHandler, FirstAnswerPerTeamHandler, SelectedOptionExistsHandler, AnswerValidationChain}.cs` |
| 1 | Modify | `Domain/Entities/LiveSession.cs` (`RecordTeamAnswer` + tracker) |
| 1 | New | `Domain/Events/AnswerRegisteredEvent.cs` |
| 1 | New | `Domain/Exceptions/{TeamAlreadyAnsweredException, AnswerOptionNotFoundException}.cs` |
| 2 | Modify | `Directory.Packages.props` + Infrastructure `.csproj` |
| 2 | Modify | `../../docker-compose.yml` |
| 2 | New | `Infrastructure/Messaging/AnswerRegisteredMessage.cs` |
| 3 | New | `Application/Sessions/DTOs/SubmitAnswerResultDto.cs` |
| 3 | New | `Application/Sessions/Facades/{IAnswerSubmissionFacade, AnswerSubmissionFacade}.cs` |
| 3 | New | `Application/Sessions/Commands/SubmitAnswer/{SubmitAnswerCommand, SubmitAnswerCommandHandler, SubmitAnswerCommandValidator}.cs` |
| 4 | Modify | `Api/Hubs/SessionsHub.cs` |
| 4 | Modify | `Api/Hubs/DomainExceptionHubFilter.cs` |
| 4 | Modify | `Application/Common/Interfaces/ISessionQuestionBroadcaster.cs` + `Infrastructure/Realtime/SignalRSessionQuestionBroadcaster.cs` |
| 4 | New | `Application/Sessions/DTOs/TeamAnsweredNotificationDto.cs` |
| 5 | Modify | `Application/DependencyInjection.cs` + `Infrastructure/DependencyInjection.cs` |
| 6 | Modify | `mobile/src/lib/realtime/{sessions-hub-types.ts, sessions-hub.ts}` |
| 6 | New | `mobile/src/lib/realtime/answer-policy.ts` |
| 7 | Modify | `mobile/src/app/(app)/team-space.tsx` |
| 7 | New | `mobile/src/components/trivia-question-card.tsx` + `mobile/src/lib/realtime/use-trivia-round.ts` |

> No `frontend/` (web) files are part of this slice — UI is Expo-only.
