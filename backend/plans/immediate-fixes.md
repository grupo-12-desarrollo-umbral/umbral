# Immediate fixes — exception-mapper-alignment follow-ups

Non-blocking findings from the `/code-review` of the exception → ProblemDetails refactor
(see `exception-mapper-alignment.md`). None block the commit. The two that were already
applied are listed under **Done** for context; the rest are open, ranked by value.

## Done (applied during review)

- **Frontend activate 409 conflation.** `frontend/app/lib/missions.ts` — the 409 branch now reads
  `problem.type` to pick the right fallback (`mission-not-ready-for-activation` vs
  `mission-already-active`) instead of always saying "Mission is already active."; the 400 branch
  comment/fallback was de-staled (readiness moved to 409).
- **Handler docstring overclaim.** All three `ProblemDetailsExceptionHandler.cs` docstrings now state
  the guarantee accurately (domain exceptions compile-enforced via abstract `Category`; app-layer
  exceptions opt in via `IErrorMetadata`).

## Open — ranked

### 1. Coverage `[Theory]` only sweeps the Domain assembly (medium)

The reflection coverage test in each service enumerates only `typeof(DomainException).Assembly`, so
the ~4–6 Application-layer `IErrorMetadata` exceptions are **not** covered. A new exception under
`Application/Common/Exceptions` that forgets `: IErrorMetadata` would hit the handler's
`_ => Problem(500, …)` arm at runtime and the test would still pass.

- **Files:** `tests/.../ProblemDetailsExceptionHandlerTests.cs` (session-ops in `IntegrationTests/Api`,
  mission-design in `Api.UnitTests/Services`, identity-access in `UnitTests/Api/Services`).
- **Fix:** extend the type sweep to the Application assembly's `IErrorMetadata` implementers, but
  special-case `ValidationException` (its `Category`/`ErrorCode` path NREs under
  `GetUninitializedObject` because `ValidationProblem` reads `.Errors`). The docstring is now honest,
  so this is hardening, not a correctness gap.

### 2. `ValidationProblem` duplicates the Validation mapping (low — maintainability)

`ValidationProblem` hardcodes the `400` / `"validation-failed"` / `"Validation failed."` triple that
`ValidationException`'s own `IErrorMetadata` already encodes, so the Validation row of the mapping now
lives in two places. A future change to `StatusFor`/`TitleFor` for `Validation` silently won't apply
to validation responses.

- **File:** `src/Api/Services/ProblemDetailsExceptionHandler.cs:55` (all three services — keep them
  byte-identical when changing).
- **Fix:** build the base problem from `StatusFor(v.Category)` / `v.ErrorCode` / `TitleFor(v.Category)`
  and only override `Detail` (joined errors) + `Extensions["errors"]`.

### 3. `IneligibleSessionOperatorException` category vs rubric (low — design decision)

Categorized `Validation` (400), but it's a business-rule rejection of an ineligible operator on a
well-formed, authorized request — the rubric maps that shape to `Unprocessable` (422), same as
role-assignment-to-ineligible-user. It is **not a regression** (it was 400 before) and matches plan
§3's explicit choice.

- **File:** `session-operations-service/src/Application/Common/Exceptions/IneligibleSessionOperatorException.cs`
- **Action:** confirm with product whether 422 is wanted; if so, flip `Category` to `Unprocessable`
  and update the assign-operator endpoint test. Otherwise leave as-is and note the rubric exception.

### 4. Minor duplication / test efficiency (very low — optional)

- App-layer exceptions hand-write `ErrorCode` literals (`not-found`, `forbidden-access`,
  `user-not-participant-role`, …) identical to what `DomainException.DeriveErrorCode` would derive.
  Could subclass `DomainException` to inherit the auto-derived slug (leave `ValidationException`,
  which keeps a custom code). Borderline — a literal is cheaper at runtime than the derivation.
- The coverage `[Theory]` yields `type.FullName` and re-resolves it via `Assembly.GetType(...)` per
  case. `TheoryData<Type>` (xUnit serializes `Type`) drops the per-case round-trip. Test-only.
