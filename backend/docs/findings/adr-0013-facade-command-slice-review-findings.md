# ADR-0013 — Facade-in-command-slice review findings (2026-07-08)

> **RESOLVED 2026-07-08:** ADR-0013 was accepted as **Option C** — single-consumer Facades are
> realized inline by their handler (no class), shared Facades move to `<Area>/Common/`, response DTOs
> move to the central `Application/Dtos/` root, and the Proxy stays a decorator. ADR-0011/0012/
> `structure-guard.sh`/`structure.md` were amended in lockstep. One open item: confirm the sprint
> rubric accepts handler-as-Facade (else fall back to Option B). The analysis below is the pre-decision
> record and is kept for rationale only.

Handoff for a future session reviewing or acting on
[ADR-0013](../adr/0013-facades-in-application-command-slices.md). This file records
the concrete code state, the ADR's argument, the review verdict, and a gap the
ADR's option set misses. Read ADR-0004, ADR-0011, ADR-0012 first; this doc does
not re-derive them.

## What was reviewed

[ADR-0013](../adr/0013-facades-in-application-command-slices.md) — "Facades in
Application command slices (revisit)". Proposed status. Asks whether the single
-consumer mandated `Facade` (plus its interface), co-located in a command slice
alongside command/handler/validator**, is the right shape, or whether to relocate
it (`<Area>/Common/`) or relax the mandate (ADR-0004 amendment).

Produced after a reviewer flagged that a `Facade` + `IFacade` sitting next to a
`*CommandHandler` reads as non-idiomatic for CQRS/MediatR.

## Verified code state

All five Facades in the backend live in **one service**:
`session-operations-service/src/Application/Sessions/`. Three are single-consumer
(in command slices), two are shared (in `Common/`).

### Single-consumer Facades (the subjects of ADR-0013)

| Slice | Handler | Facade | Interface |
| --- | --- | --- | --- |
| `Commands/CreateSession/` | `CreateSessionCommandHandler` (22 L, 1 dep, pure `return _facade.CreateAsync(...)`) | `CreateSessionFacade` (172 L, 4 deps, all real work) | `ICreateSessionFacade` (8 L, 1 method) |
| `Commands/AssignOperatorToSession/` | `AssignOperatorToSessionCommandHandler` (22 L, 1 dep, pure delegate) | `AssignOperatorToSessionFacade` (51 L, 4 deps, real orchestration incl. access resolver + eligibility check) | `IAssignOperatorToSessionFacade` |
| `Commands/TransitionSessionState/` | `TransitionSessionStateCommandHandler` (22 L, 1 dep, pure delegate) | `TransitionSessionStateFacade` (59 L, 5 deps incl. `SessionTransitionChain`, `SessionStateTransitionPolicy`) | `ITransitionSessionStateFacade` |

Each Handler is **identical in shape**: injects `IFacade`, delegates in one line.
Every Facade coordinates ≥2 collaborators (genuine by ADR-0012's test — not
forwarding ceremony on the Facade's side). The interface has exactly one
consumer: the Handler in the same slice.

### Shared Facades (out of scope for ADR-0013 — already in `Common/`)

- `Common/SessionTeamAssociationFacade.cs` + `ISessionTeamAssociationFacade.cs`
- `Common/TriviaRoundOrchestratorFacade.cs` + `ITriviaRoundOrchestratorFacade.cs`

These are correctly placed per ADR-0011/0012 and are not in question.

## ADR-0013 options as written

- **Option A — Status quo.** Keep single-consumer Facade co-located in the slice.
  The ADR's recommendation. Honours ADR-0011 co-location; zero churn; the
  "Facade in a slice" shape stays unfamiliar to mainstream-CQRS readers.
- **Option B — Relocate to `<Area>/Common/`.** Slice keeps only
  command/handler/validator(+DTO); every mandated Facade lives in
  `Sessions/Common/`. Uniform slice shape, at the cost of separating a
  single-consumer unit from its sole caller (against ADR-0011 force 1), plus
  amending ADR-0011/0012 + `structure-guard.sh` in lockstep.
- **Option C — Amend ADR-0004.** Require a Facade only when it coordinates ≥2
  collaborators **and** is shared by ≥2 slices; otherwise the handler
  orchestrates directly. Removes single-consumer Facades entirely. Changes the
  sprint rubric — out of scope for this ADR.

ADR-0013 recommends **Option A**, on the grounds that the reviewer's real
discomfort is with force 3 (the mandate) and should be pursued as an explicit
ADR-0004 amendment, not a slice-layout tweak.

## Review verdict

### Recommendation is internally sound

Given the constraint set the ADR considers (keep the mandate, or explicitly
reopen it), Option A is the right pragmatic call: the Facade IS genuine by
ADR-0012's own test (coordinates ≥2 collaborators, owns a real boundary), the
rubric IS external to this ADR, and recording the rationale so the shape isn't
mistaken for an oversight later is the correct outcome for a "Proposed"
revisit.

### ADR-level nits worth fixing regardless of which option lands

1. **No decision is actually rendered.** *Decision* says "pick one"; *Status*
   says "Proposed"; the *Recommendation* paragraph below does the picking. A
   ratified ADR should state the chosen option as the decision, not present a
   menu. If the team accepts, set `Status: Accepted` and own Option A in the
   *Decision* section.
2. **Option B's cost is overstated.** ADR-0013 says "a cross-cutting move across
   all mandated-Facade slices in **three services**." Verified reality: all
   single-consumer Facades live in **one service** (session-operations), and
   there are exactly **three** of them. That's a handful of file moves, not a
   multi-service refactor. Correct the claim.
3. **The co-location counterargument (force 1) is weaker than the ADR treats
   it.** The Handler injects the Facade **by interface** — moving the file to
   `Sessions/Common/` changes the file tree, not the runtime coupling. Both the
   reviewer's "non-idiomatic" objection and the "separation from sole caller"
   counter are aesthetic (file-tree) arguments; they roughly cancel rather than
   force 1 winning decisively. Worth being explicit about that.
4. **The mandate's enforcement mechanism is never interrogated.** Does the
   phase gate check Facade presence by file name, by surface area, or by
   "pattern exists somewhere"? If the latter, Option B (and the Option D below)
   both satisfy it. Clarifying what the rubric actually enforces would remove
   an implicit assumption that drives the recommendation.

### Gap the ADR's option set misses — "Option D"

The ADR frames this as a **placement** question ("where does the Facade file
live?" — slice vs `Common/`). The reviewer's actual discomfort is a **layering**
question: the **Handler** is a 22-line pass-through sitting between MediatR and
a Facade that does all the work. That empty Handler is the ceremony the reviewer
smells, **not the Facade**.

The ADR never considers merging the Facade and the Handler into one class:

**Option D — Facade *is* the Handler.** The Facade class implements
`IRequestHandler<CreateSessionCommand, CreateSessionResultDto>` directly; the
orchestration stays in the Facade. Delete the separate
`CreateSessionCommandHandler` (and its single-use `IFacade` interface, unless
testing still needs the seam). Slice shrinks from 5 files to ~3-4, the pattern
class still exists and is still genuine (ADR-0004 rubric passes), and no ADR-0004
amendment is required.

Before/after for one slice:

```
# before (5 files)
Commands/CreateSession/
├── CreateSessionCommand.cs
├── CreateSessionCommandHandler.cs   # 22 L, pure forwarder
├── CreateSessionCommandValidator.cs
├── CreateSessionFacade.cs           # 172 L, all real work
├── ICreateSessionFacade.cs          # 8 L, 1 method, sole consumer = the handler

# after Option D (3 files)
Commands/CreateSession/
├── CreateSessionCommand.cs
├── CreateSessionFacade.cs           # implements IRequestHandler<,> directly;
│                                    # still a Facade (coordinates 4 collaborators)
├── CreateSessionCommandValidator.cs
```

Why this may be the right answer even though the ADR doesn't list it:

- It **engages the reviewer's real objection** (the empty Handler) rather than
  answering a different question (Facade file location).
- It **preserves the mandated pattern** — the `CreateSessionFacade` class still
  exists, still coordinates ≥2 collaborators, so ADR-0004's phase gate passes
  without amendment.
- It **eliminates the interface ceremony**: when the Facade *is* the Handler,
  the single-consumer `IFacade` abstraction has no remaining job; if a test
  seam is still needed, keep the interface, but that's a test-only concern, not
  a slice-shape concern.
- Cost is bounded to one service and three slices — same scope as Option B.

Caveats to resolve before proposing Option D formally:

- Does CONTEXT.md's `Facade` definition ("session orchestration should be
  exposed through a narrow coordination service... **Avoid:** endpoint-level
  orchestration or handlers that manually coordinate every side effect")
  forbid a Facade-that-is-also-the-MediatR-handler? Probably not — the Facade
  is not an "endpoint" and is not "manually coordinating every side effect"; it
  is the narrow coordination service the audit asks for, just registered as
  the MediatR handler. But this needs a written confirmation, not a hand-wave.
- Does DI registration stay clean? Today the Handler is the
  `IRequestHandler<,>` and the Facade is a separate service. Under Option D,
  `CreateSessionFacade` is registered as `IRequestHandler<,>` directly;
  MediatR's assembly scan picks it up. No special wiring.
- Tests today inject the Facade by `IFacade` and exercise it in isolation
  (`CreateSessionFacadeTests.cs`). Under Option D the test seam changes — the
  interface may need to stay (for test substitution), even if it's no longer a
  *slice-shape* concern. Confirm the test strategy before deleting interfaces.

## Cross-references

- ADR under review: [0013-facades-in-application-command-slices.md](../adr/0013-facades-in-application-command-slices.md)
- What's mandated and why: [0004-required-domain-patterns.md](../adr/0004-required-domain-patterns.md)
- Slice layout rule: [0011-application-layer-vertical-slice-organization.md](../adr/0011-application-layer-vertical-slice-organization.md)
- Pattern placement + genuine-vs-ceremony test: [0012-design-pattern-placement-convention.md](../adr/0012-design-pattern-placement-convention.md)
- Worked pattern realizations: [adr-0012-pattern-realizations-by-layer.md](../adr-0012-pattern-realizations-by-layer.md)
- Sprint HU → pattern mapping: [trivia_sprint_required_patterns_matrix.md](../trivia_sprint_required_patterns_matrix.md)
- Refactor plan (referenced for safety rules, file path unverified this session):
  `plans/application-layer-cqrs-refactor.md` per the ADRs — check
  `backend/plans/` (ADR-0013 cites `plans/application-layer-cqrs-refactor.md`)
- Service language / required-patterns guidance: `session-operations-service/CONTEXT.md`
  (`Facade` and `State` definitions; read before pursuing Option D)

## Suggested next steps for a future session

1. Decide whether to **ratify Option A** as-is (correcting the four ADR-level
   nits), or to **add Option D** to the menu and re-deliberate. They are not
   mutually exclusive: ADR-0013 can accept Option A now and open a follow-up
   ADR-0014 that proposes Option D, since D touches the Handler shape rather
   than the Facade location.
2. If pursuing Option D: confirm the CONTEXT.md `Facade` definition per the
   caveat above, sketch the DI/test-seam story on **one** slice (CreateSession
   is the highest-value prototype — 172 L of orchestration to inline), and run
   `make -C backend test SVC=session-operations-service` to verify behavior is
   preserved.
3. Whatever lands: update ADR-0013's "cross-cutting move across three services"
   claim to "one service (session-operations), three single-consumer Facades."

## Suggested skills

- `grill-with-docs` — turn the Option D caveats into a resolved decision and
  write it as a follow-up ADR if the team wants to pursue it.
- `review` — once a change exists, run a Standards+Spec review against the slice
  layout and the ADR-0004 rubric.
- `aspnet-backend-testing` — sketch the test-seam change under Option D and the
  regression candidates.

## Toolchain reminders

- Do **not** run `dotnet`/`docker` directly. Use
  `make -C backend build|test|gate|structure-guard SVC=session-operations-service`.
- Moves between slices must preserve behavior and be one-per-slice; never mix
  them with feature work (per `plans/application-layer-cqrs-refactor.md` safety
  rules cited by ADR-0013).
- Before moving any file, read `backend/structure.md` and the make
  `structure-guard` target rules (ADR-0011). ADR-0013 explicitly bans only the
  `Facades/` **bucket**, not a Facade file in or out of a slice.