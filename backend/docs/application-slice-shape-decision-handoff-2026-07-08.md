# Application slice-shape decision — handoff (2026-07-08)

Handoff for a future session. Records a **governance decision** made this session and the doc
edits that implement it. **No production code was changed** — this session only amended ADRs,
the structural guard, and planning docs. The actual code refactor (moving files) is still to do.

## The decision

For the Application layer of every service, a command/query **slice holds pipeline files only**:

| Slice type | Keeps | Moves out |
|---|---|---|
| Command | `Command` · `Handler` · `Validator` | Facade, DTO, Proxy |
| Query | `Query` · `Handler` | DTO |

Three rulings drive it:

1. **Single-consumer `Facade` → realized inline by its handler** (no `*Facade.cs`/`I*Facade.cs`).
   The handler coordinates the collaborators directly; its `IRequestHandler<,>` *is* the Facade's
   one interface. A Facade **shared by ≥2 consumers** stays a discrete class in `<Area>/Common/`
   (`SessionTeamAssociationFacade`, `TriviaRoundOrchestratorFacade`). This is **ADR-0013 Option C**.
2. **All response DTOs → central `Application/Dtos/<Area>/`** (command results *and* query
   responses). A command returning `Guid`/`Unit` gets no DTO — delete, don't relocate. This
   deliberately overrides ADR-0013's earlier "owned DTO stays co-located" note; it's house style
   from `docs/findings/issue-option-c-and-dto-relocation.md`.
3. **`Proxy` stays a decorator** — relocated to `<Area>/Common/Authorization/`, **never inlined**
   (authorization is cross-cutting; inlining would delete ADR-0012's reference capability-guard).

## Why Facade-inline but Proxy-not

The Facade is the use case's *own* orchestration → belongs in the handler. The Proxy is a
*cross-cutting* guard wrapped around the use case → belongs outside it. Same word ("move into the
handler"), opposite verdict, because they're different kinds of thing.

## Patterns are KEPT, not removed

ADR-0004 and both patterns matrices are **unchanged in substance**. Every mandated Facade/Proxy is
still *realized* — the Facade by its handler, the Proxy as a decorator — so the phase gate stays
green with no matrix edits and no pattern un-mandated.

## ⚠️ One open item before the refactor branch merges

ADR-0013 Option C rests on "the handler realizes the Facade." **Confirm the sprint grading rubric
accepts a handler-as-Facade** rather than demanding a discretely named `*Facade.cs` class. If the
rubric requires a named class, fall back to **Option B** (keep the class, relocate it to
`<Area>/Common/`) — same clean slice, discrete Facade preserved.

## Docs changed this session

- `docs/adr/0013-…` — **Accepted → Option C**, with the rubric caveat recorded.
- `docs/adr/0011-…` §1/§2/§3 + Consequences — slice = pipeline-only; central `Dtos/`; single-consumer
  Facade realized by handler; Proxy in `Common/Authorization/`.
- `docs/adr/0012-…` — Facade row (home = the handler) + Proxy row (relocate, never inline) + Status.
- `scripts/structure-guard.sh` — Rule A now exempts the central `Application/Dtos/` root, still bans
  per-area `DTOs/`/`Dtos/` buckets. Verified: syntax OK, all 4 services pass, carve-out tested.
- `structure.md` — tree + DTOs/vertical-slice prose.
- `plans/application-layer-cqrs-refactor.md` — rules block + the session-operations Facade verdict
  table (3 single-consumer → inline; 2 shared → `Common/`).
- `docs/refactors/application-layer-overengineering-checklist.md`, `docs/adr-0012-pattern-realizations-by-layer.md`
  — aligned to central `Dtos/` + inline-Facade.
- `docs/findings/adr-0013-facade-command-slice-review-findings.md` — RESOLVED banner.

## Implementation still TODO (not done here)

Per `docs/findings/issue-option-c-and-dto-relocation.md` + the plan's Phase-2 safety rules
(own refactor branch, one behaviour-preserving commit per slice, never mixed with feature work;
`make -C backend test SVC=<svc>` + `make -C backend structure-guard SVC=<svc>` per service):

1. Inline the 3 single-consumer session-operations Facades into their handlers; delete class +
   interface + the 3 `AddScoped` lines.
2. Move the 2 shared Facades to `Sessions/Common/`; re-point DI + namespaces.
3. Relocate the 3 Proxies to `<Area>/Common/Authorization/`; fix namespaces (DI behaviour-unchanged).
4. Move all response DTOs to `Application/Dtos/<Area>/` (40 pre-check, minus `Guid`/`Unit` deletions);
   fix namespaces + usings across src, Api, tests.
5. Split `AssociateTeamToSession/` (2 commands in one folder) into two slices — ADR-0011 §1.
6. Sweep slice residuals (reason codes, constants) → `<Area>/Common/` — ADR-0011 Part 4.
