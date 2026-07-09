# 0013 — Facades in Application command slices (revisit)

## Status

**Accepted (2026-07-08) — Option C**, with the rubric reconciliation in §Decision. Revisits the
`Facade` row of [ADR-0012](0012-design-pattern-placement-convention.md) and ADR-0011 §2 for the
**single-consumer** case: a single-consumer mandated Facade is now **realized by its handler** (no
standalone class). Does **not** un-mandate any pattern in
[ADR-0004](0004-required-domain-patterns.md) or the patterns matrix — the Facade is realized, not
removed (see §Decision). Two Context assumptions below are **superseded**: the co-located owned DTO
moves to the central `Application/Dtos/` root (separate decision, recorded in
[ADR-0011](0011-application-layer-vertical-slice-organization.md) §2), and ADR-0011/ADR-0012/`structure-guard.sh`
were amended in lockstep. Options A/B/C are retained below as the recorded analysis.

## Context

ADR-0011 and ADR-0012 place a **mandated single-consumer `Facade` inside its command slice**:

```
Application/Sessions/Commands/CreateSession/
    CreateSessionCommand.cs
    CreateSessionCommandHandler.cs
    CreateSessionCommandValidator.cs
    CreateSessionResultDto.cs        # owned response DTO — co-located (NOT in question)
    CreateSessionFacade.cs           # mandated Facade — the item under review
    ICreateSessionFacade.cs
```

A reviewer raised that a `Facade` (plus its interface) sitting in the command folder alongside
command/handler/validator reads as non-idiomatic. Assessment of that objection:

- **The co-located owned DTO is not the issue.** Placing a single-use-case response model next to
  its handler is standard vertical-slice practice (Jason Taylor Clean Architecture, Jimmy Bogard
  Vertical Slice Architecture) and is settled by ADR-0011. It stays.
- **The Facade genuinely is atypical for mainstream CQRS/MediatR.** In the common convention the
  **handler itself is the orchestrator** — it injects the repositories/domain services and
  coordinates them directly; there is no separate `Facade` interface between the handler and its
  collaborators. A reader coming from those codebases will not expect a `Facade` in the slice.
- **It exists here for one reason:** ADR-0004 + `trivia_sprint_required_patterns_matrix.md` mandate
  specific GoF patterns per HU, and the phase gate **fails** if the named pattern is absent
  (e.g. HU-16 mandates `Facade`, HU-21A/33A/33B mandate `State` + `Facade`). The Facade is a
  didactic/sprint deliverable, not an orchestration need the domain would independently force.
- **ADR-0012 already deems these Facades *genuine*, not ceremony.** `CreateSessionFacade` coordinates
  four collaborators (`IMissionReadinessSource`, `IMissionRuntimeSource`, `LiveSession.Create`,
  `ILiveSessionRepository`) behind one interface — it passes the genuine-vs-ceremony test. So this is
  **not** a "collapse the forwarding layer" case; the pattern is real. The question is purely
  **where the real Facade file lives**, given the mandate keeps it.

Forces in tension:

1. **Co-location (ADR-0011's own logic):** a unit used by exactly one consumer belongs next to that
   consumer. Moving a single-consumer Facade to `<Area>/Common/` separates it from its sole caller.
2. **Idiomatic familiarity:** keeping command slices to command/handler/validator(+owned DTO) matches
   what most CQRS practitioners expect and makes the slice's "shape" uniform.
3. **The mandate is fixed for this sprint:** ADR-0004's requirement is an external sprint rubric, so
   "just don't have a Facade" is not available without amending ADR-0004 — a broader governance move
   affecting every mandated pattern, not only Facade placement.

## Decision — Option C (adopted 2026-07-08)

A single-consumer mandated `Facade` is **realized by its MediatR handler**: the handler injects and
coordinates the collaborators directly and owns the transaction / event-publication boundary. No
standalone `*Facade.cs` / `I*Facade.cs`. A discrete Facade class remains **only where shared by ≥2
consumers** (`SessionTeamAssociationFacade`, `TriviaRoundOrchestratorFacade`), in `<Area>/Common/`.
The `Proxy` is **not** treated this way — it stays a decorator, relocated to
`<Area>/Common/Authorization/`, never inlined (authorization is cross-cutting; ADR-0012 Proxy row).

**Pattern realized, not removed — with one honest caveat (the force-3 concern this ADR raised).** By
ADR-0012's genuine-vs-ceremony test a Facade "coordinates ≥2 collaborators behind one interface,
owning a transaction/orchestration/event-publication boundary." The handler's `IRequestHandler<,>`
is that one interface and it coordinates the four collaborators `CreateSessionFacade` did, so the
mandated pattern is satisfied **by the handler**. This is an accepted *reinterpretation* of the
ADR-0004 mandate, **not an amendment** — no HU loses its mandated pattern. **Caveat:** if the sprint
grading rubric expects a *discretely named* `*Facade.cs` class as the didactic deliverable rather than
accepting the handler as the realization, that is a rubric conflict. It must be **confirmed with
whoever owns the rubric before the refactor branch merges.** If the rubric requires a named class,
fall back to **Option B** (relocate the class to `<Area>/Common/`) — a discrete Facade that still
clears the slice.

## Options considered

**Option A — Status quo (keep ADR-0012 as-is).** Single-consumer mandated Facade stays co-located in
its command slice. *Pro:* honours ADR-0011's co-location logic; zero churn; already documented and
machine-passing (`structure-guard.sh` only bans `Facades/` buckets, not a Facade file in a slice).
*Con:* the slice shape stays unfamiliar to mainstream-CQRS readers.

**Option B — Relocate mandated Facades to `<Area>/Common/`.** Command slices then contain only
command/handler/validator + owned DTO; every mandated Facade (single- or multi-consumer) lives in
`Sessions/Common/` grouped by concern. *Pro:* uniform, idiomatic slice shape. *Con:* separates a
single-consumer unit from its sole caller (against force 1); `structure-guard.sh` and ADR-0011/0012
must be amended in lockstep; a cross-cutting move across all mandated-Facade slices in three services.

**Option C — Relax the mandate (ADR-0004 amendment).** Require a separate `Facade` only where it
genuinely coordinates ≥2 collaborators **and** is shared by ≥2 slices; otherwise the handler
orchestrates directly (mainstream practice), eliminating single-consumer Facades entirely. *Pro:*
removes pattern-for-pattern's-sake ceremony and best matches the reviewer's intuition. *Con:* changes
ADR-0004 — the sprint's didactic requirement — so it may conflict with the grading rubric; it is a
decision about **all** mandated patterns, far larger than Facade placement, and out of scope for a
single ADR.

**Recommendation (superseded by the Decision above).** The original draft recommended Option A as
lowest-cost. The team chose **Option C** on 2026-07-08, resting on the "handler realizes the Facade"
reinterpretation rather than a full ADR-0004 amendment — the mandate stands, its realization moves
into the handler. The rubric caveat above is the one open item.

## Consequences

- ADR-0011 §2, ADR-0012's Facade row, and `structure-guard.sh` were amended in lockstep (done).
- Execute the moves on their **own** refactor branch, one behaviour-preserving commit per slice —
  never mixed into a feature branch (`plans/application-layer-cqrs-refactor.md` safety rules). HU-33B
  is unaffected and must not carry this change.
- **Before merge:** confirm the sprint rubric accepts handler-as-Facade (the caveat above); if not,
  switch to Option B.
- No HU loses its mandated pattern; ADR-0004 and the patterns matrix are unchanged in substance. The
  co-located owned-DTO convention **is** superseded — response DTOs move to the central
  `Application/Dtos/` root ([ADR-0011](0011-application-layer-vertical-slice-organization.md) §2).
