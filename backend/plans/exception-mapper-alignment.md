# Plan: Align the domain/application exception → ProblemDetails mappers

**Status:** Proposed
**Scope:** `session-operations-service`, `mission-design-service`, `identity-access-service`
**Goal:** Replace the three hand-maintained, drifted `ProblemDetailsExceptionHandler` switch statements with a single uniform, category-driven mapper, and make it structurally impossible for a new exception to fall through to HTTP 500 unmapped.

---

## 1. Why

Each service has its own `Api/Services/ProblemDetailsExceptionHandler.cs` that pattern-matches concrete exception types to RFC 7807 `ProblemDetails`. They have drifted:

- **Namespace**: `mission-design` uses `umbral_backend.Web.Services`; the other two use `umbral_backend.Api.Services` (file lives in `Api/Services/` either way).
- **Title convention**: `session-ops` / `identity-access` use generic RFC titles (`"Conflict."`, `"Forbidden."`); `mission-design` puts full human sentences in `Title` (`"Trivia quiz cannot be edited in its current state."`).
- **`Type` slug**: only `session-ops` sets stable machine-readable `Type` slugs; the other two force clients to discriminate by parsing `Title`/`Detail`.
- **422 routing**: `identity-access` uniquely routes some `ValidationException`s to 422 by **string-matching the message** `"Target user must be active."` — brittle.
- **Validation detail**: all three flatten FluentValidation errors into a space-joined string; none expose the structured `errors` dictionary.

The deeper problem behind the drift: **there is no base exception type.** All ~135 domain exceptions and ~13 application exceptions inherit directly from `System.Exception`, and each handler must name every concrete type. The handlers only name ~23 / 37 / 21 types — so **~69 domain exceptions currently fall through to the catch-all 500** (session-ops 35, mission-design 16, identity-access 18).

---

## 2. Recommendation: per-service copies, **not** a shared kernel

**Verdict for "most production-ready in this repo": per-service copies.** Reasoning is grounded in how the repo is actually built and structured:

| Evidence | Implication |
|---|---|
| **No solution (`.sln`) files anywhere** | Each service is compiled in total isolation. |
| Each `Dockerfile` build context is its own service folder, and `COPY src/ ./src/` copies **only that service's `src/`** | A shared project living outside the service folder would be **outside the Docker build context** → image builds fail. Sharing would require either re-rooting every build context + rewriting all Dockerfiles, or publishing the shared lib as a versioned NuGet package consumed by each service. Both are non-trivial infra changes. |
| `CONTEXT-MAP.md` + `adr/` define deliberate DDD **bounded contexts** | Each service is an independent context; a "Shared Kernel" is a governed coupling point in DDD, not a default. |
| The types being shared are tiny and change rarely (an enum, an interface, a ~40-line base, a ~40-line handler) | The cost of duplication is low and stable. |
| The new abstract `Category` member is **compiler-enforced** | The actual failure mode (silent 500 on a new exception) is prevented structurally **without** a shared library — you cannot compile a new domain exception without classifying it. |

So: the only real downside of duplication — drift — is solved by the abstract compile-check, and the cost of a shared kernel (fighting the repo's isolated build model + coupling independent contexts) is not justified for ~4 small files.

**Revisit a shared kernel only if** the repo later adopts a repo-root build context, a multi-service solution, or an internal NuGet feed. At that point the plumbing (`ErrorCategory`, `IErrorMetadata`, `DomainException`, handler, and the common app exceptions) is the natural first thing to extract. Until then, keep it per-service. *(This decision is worth a short ADR — see Phase 8.)*

---

## 3. Target design

Three new types per service, in `src/Domain/Exceptions/` (namespace `umbral_backend.Domain.Exceptions`):

**`ErrorCategory`** — transport-agnostic vocabulary (no HTTP in the Domain layer):
`NotFound | Validation | Conflict | Forbidden | Unauthorized | Unprocessable`

**`IErrorMetadata`** — the contract the handler matches on, so it never enumerates concrete types:
```csharp
public interface IErrorMetadata
{
    ErrorCategory Category { get; }   // semantic category
    string ErrorCode { get; }         // stable kebab-case slug for clients
}
```

**`DomainException`** — abstract base; `Category` is abstract (compile-forced), `ErrorCode` auto-derives from the type name but is overridable to preserve existing slugs:
```csharp
public abstract class DomainException : Exception, IErrorMetadata
{
    protected DomainException(string message) : base(message) { }
    public abstract ErrorCategory Category { get; }
    public virtual string ErrorCode => DeriveErrorCode(GetType().Name); // "InvalidStateTransitionException" -> "invalid-state-transition"
    public static string DeriveErrorCode(string typeName) { /* strip "Exception", kebab-case */ }
}
```

**The uniform handler** (byte-identical across all three services except nothing service-specific remains — it references only the shared abstractions):
```csharp
var problem = exception switch
{
    ValidationException v        => ValidationProblem(v),                  // 400 + Extensions["errors"]
    IErrorMetadata e             => Problem(StatusFor(e.Category), e.ErrorCode, TitleFor(e.Category), exception.Message),
    UnauthorizedAccessException  => Problem(401, "unauthorized", "Unauthorized.", exception.Message),
    _                            => Problem(500, "internal-error", "An unexpected error occurred.", exception.Message),
};
```

**Category → HTTP** (the single table, identical everywhere):

| Category | Status | Generic Title |
|---|---|---|
| `NotFound` | 404 | `Resource not found.` |
| `Validation` | 400 | `Validation failed.` |
| `Conflict` | 409 | `Conflict.` |
| `Forbidden` | 403 | `Forbidden.` |
| `Unauthorized` | 401 | `Unauthorized.` |
| `Unprocessable` | 422 | `Unprocessable entity.` |

Net effect: every exception carries its own status + stable `Type` slug + (for validation) structured `errors`; the handler is identical and stable; a new exception maps itself the moment it derives from `DomainException`.

---

## 4. Categorization rubric

Apply these rules; they reproduce every currently-mapped status and classify the ~69 silent-500s consistently. (Per-exception assignments come from the three category maps produced during analysis — see §6.)

- `*NotFound*` → **NotFound** (404)
- Client-supplied input guards — `*Required`, `*MustBePositive`, `*OutOfRange`, `*FormatInvalid`, invalid-enum-value → **Validation** (400)
- State / lifecycle conflicts — `*Already*`, `*Closed`, `*Reached`, `*NotActive`, `*Locked`, invalid state transition, token replay/expired, uniqueness-within-aggregate, "not ready to publish/activate" gates → **Conflict** (409)
- Server-derived **runtime-snapshot invariants** (built during session creation) → **Conflict** (409), matching the existing `mission-not-eligible-for-session` / `trivia-substage-empty` precedent
- Authorization denials — `*NotAllowed`, `*AccessDenied`, `*NotAuthorized`, late-join → **Forbidden** (403)
- Business rule on a well-formed request — role assignment to a deactivated / non-participant user → **Unprocessable** (422)
- Unauthenticated → **Unauthorized** (401)

**Decisions on the ambiguous cases (settled — implement as written):**
1. session-ops runtime-snapshot-invariant family → **Conflict** (409). They are server-derived guards hit during session creation; Conflict matches the existing `mission-not-eligible-for-session` / `trivia-substage-empty` precedent.
2. identity `ExternalIdentityMismatchException`, `TeamAlreadyAssociatedWithSessionException`, `UserAccessAlreadyDeactivatedException` → **Conflict** (409), consistent with the other `*Already*` / state-violation exceptions in that service.

**Preserved `ErrorCode` slugs (session-ops only — override `ErrorCode`):**

| Exception | Slug |
|---|---|
| `InvalidSessionStateTransitionException` | `invalid-state-transition` |
| `MissionNotEligibleForSessionCreationException` | `mission-not-eligible-for-session` |
| `TriviaSubstageSnapshotMustContainQuestionsException` | `trivia-substage-empty` |
| `LiveSessionRequiresAtLeastOneTeamException` | `session-no-teams` |
| `SessionOperatorNotAssignedException` | `session-operator-unassigned` |

---

## 5. Phases

Use a **tracer-bullet order**: implement the whole vertical slice in `session-operations-service` first (it has the most exceptions, the preserved slugs, and the SignalR filter), get it green, review, then replicate the now-proven pattern to the other two.

### Phase 1 — Foundation (per service)
Add `ErrorCategory.cs`, `IErrorMetadata.cs`, `DomainException.cs` to each service's `src/Domain/Exceptions/`. No behavior change yet. Build Domain project.

### Phase 2 — Reparent domain exceptions + assign categories (per service)
Mechanical per file: `: Exception` → `: DomainException`, add `public override ErrorCategory Category => ErrorCategory.X;`. The `: base("message")` line is **untouched** (so `Detail` keeps its real message). Add `ErrorCode` overrides for the 5 preserved slugs. The abstract `Category` makes the compiler list any file you missed.
- session-ops: 49 files · mission-design: 47 · identity-access: 31.
- A scripted transform (reparent + insert one property, keyed by the §6 category map) is reliable here since the edit is regular and the base-call is never touched; review via `git diff`, then build.

### Phase 3 — Wire application-layer exceptions (per service)
Make the app exceptions implement `IErrorMetadata` (hardcode a stable slug + category each):
- Common: `NotFoundException` (NotFound), `ValidationException` (Validation, `validation-failed`), `ForbiddenAccessException` (Forbidden).
- session-ops also: `IneligibleSessionOperatorException` (Validation), `SourceTriviaQuizNotPublishedException` (Conflict), `SessionOperatorNotAssignedException` (Conflict, slug `session-operator-unassigned`).
- identity also: `UserNotParticipantRoleException` (Unprocessable).
- `System.UnauthorizedAccessException` stays a hardcoded handler arm (can't implement our interface).

### Phase 4 — Collapse the handler (per service)
Replace each `ProblemDetailsExceptionHandler` with the identical category-driven version: `StatusFor`/`TitleFor`, `Type = ErrorCode`, and `Extensions["errors"]` for `ValidationException`. Fix mission-design's namespace to `umbral_backend.Api.Services`. Verify DI registration (`AddExceptionHandler<ProblemDetailsExceptionHandler>()`) and `app.UseExceptionHandler` are unchanged.

### Phase 5 — Replace identity-access's 422 string-match
Introduce a typed `Unprocessable` exception (e.g. `UserInactiveForRoleAssignmentException`) thrown where the `"Target user must be active."` rule currently originates (`AssignUserRoleCommandValidator`), and delete `IsUnprocessableRoleAssignment`. This is what lets identity's handler become identical to the others. Confirm whether the existing domain `DeactivatedUserRoleAssignmentNotAllowedException` already covers this path (possible dedupe).

### Phase 6 — Align the SignalR hub filter (session-ops)
`src/Api/Hubs/DomainExceptionHubFilter.cs` maps the same exceptions to `HubException` with its own string codes, maintained in parallel to the REST handler. Refactor it to derive its code from `IErrorMetadata.ErrorCode`/`Category` so REST and SignalR share one source of truth (kills a second drift surface).

### Phase 7 — Tests + verification (per service)
- Update assertions broken by the contract change — especially `mission-design/tests/Api.UnitTests` (Title sentences → generic) and `mission-design/tests/UnitTests/Domain/Exceptions/DomainExceptionCoverageTests.cs` (read it first; it may already assert handler coverage and can be strengthened).
- Add/keep a **coverage test** per service: reflect over every `DomainException` subtype and assert the handler returns a non-500 with a `Type` and the expected status for its `Category`. This locks the "no silent 500" guarantee.
- `dotnet build` + `dotnet test` each service green.

### Phase 8 — Contract coordination + ADR
- **Contract impact (see §7):** audit frontend/mobile consumption of ProblemDetails before shipping the Title/Type/errors changes.
- Write a short ADR (`adr/0004-exception-to-problemdetails-mapping.md`) recording the category vocabulary, the category→status table, the slug convention, and the per-service (no shared kernel) decision + its revisit trigger.

---

## 6. Working input: per-service category maps

Full per-exception maps were produced during analysis and should be attached/linked when implementing. Condensed totals:

| Service | Domain | App | Currently silent-500 | Preserved slugs |
|---|---|---|---|---|
| session-operations | 49 | 6 | 35 | 5 |
| mission-design | 47/48 | 3 | 16 | 0 |
| identity-access | 31 | 4 | 18 | 0 (+1 new typed 422) |

The maps classify each exception per the §4 rubric, including the §4 settled decisions. The compiler (abstract `Category`) guarantees none are skipped — every domain exception must declare a category or the build fails.

---

## 7. Risks & contract impact

- **API contract change.** `mission-design` response `Title`s change from descriptive sentences to generic titles, and all services gain a `Type` slug + structured `errors`. Per the repo `CONTEXT-MAP.md` ownership note ("API contracts … must be coordinated; neither side should silently diverge"), **audit `frontend/` and `mobile/` for any code reading `problem.title` / `detail` / `type` before shipping.** If the frontend renders `title`, moving the human message out of `Title` could regress UX — mitigation: clients should read `detail` (which still carries the full message) and branch on `type`.
- **Status changes for silent-500s.** ~69 exceptions move from 500 to 4xx. This is the intended fix, but any client/test asserting 500 on those paths will change. Low risk (asserting on 500 is unusual) but worth a grep.
- **identity 422 refactor (Phase 5)** touches the validation flow — verify the role-assignment command tests still pass and the 422 is still produced for inactive-user role assignment.
- **SignalR codes (Phase 6)** — confirm clients keying off the existing `HubException` codes still receive the same strings (preserve them via `ErrorCode` slugs).

---

## 8. Verification checklist

- [ ] All three services `dotnet build` clean (abstract `Category` ⇒ no domain exception left unclassified).
- [ ] All three services `dotnet test` green; mission-design Title-asserting tests updated.
- [ ] Coverage test per service: every `DomainException` maps to its category's status, never 500.
- [ ] Handler files identical across services modulo nothing (same namespace, same body).
- [ ] The 5 session-ops slugs unchanged in responses; identity 422 still returned for inactive-user role assignment without the string-match.
- [ ] frontend/mobile ProblemDetails consumers audited for `title`/`type`/`errors`.
- [ ] ADR 0004 written.

---

## 9. Effort / sequencing

Tracer slice (session-ops, Phases 1–7) first and reviewable on its own; then mission-design and identity-access replicate the proven pattern in parallel. The bulk (Phase 2 reparenting) is mechanical and scriptable; the only non-mechanical work is the Phase 5 identity 422 refactor. All category assignments — including the §4 settled decisions — are fixed, so implementation has no open questions.
