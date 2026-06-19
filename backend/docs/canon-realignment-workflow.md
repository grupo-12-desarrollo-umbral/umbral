# Canon-Driven Phase-Ordered Rebuild Workflow

A reusable method for executing work after a canon rewrite when the backlog has
been realigned but the codebase still holds an older domain model.

## When to use this

- A canon/spec rewrite has landed in docs, realigning the meaning of core domain
  concepts.
- The issue tracker carries a phase-ordered rebuild plan with supersession
  mapping (old Done tickets replaced by new rebuild tickets).
- Existing code was built on the pre-canon model and is tagged stale or
  `needs-rebuild`.
- Some code is canon-compatible and worth keeping; some contradicts canon and
  must be deleted or replaced.

If any of those are false, this workflow is overhead. Use a normal feature flow.

## Authority chain (who wins on disagreement)

```
canon docs  >  tracker AC (where rewritten)  >  existing code
   spec          work breakdown                   reuse candidate only
```

- **Canon docs** are the spec. Non-negotiable. Never contradict them to
  preserve old code.
- **Tracker tickets** are the work breakdown and the ordering. Rebuild-ticket AC
  supersedes original Done-ticket AC. Comment-only reword tickets may still
  carry loose AC — tighten against canon before building.
- **Existing code** is a reuse candidate, not authority. Harvest the
  canon-compatible parts; delete or replace the rest. Never extend
  canon-contradicting code.

## Input roles

| Input | Role | How to use it |
|---|---|---|
| Realignment map | The sequence + supersession map | Follow its phase order. Use its supersession table to know which Done tickets to ignore. Use its open-decisions list to know what to resolve before later phases. |
| Tracker tickets | Work breakdown + acceptance | Filter by `needs-rebuild` for rebuild work; by the realignment label for the wider touched set. Per ticket: read the realignment comment and any rewritten AC first; the issue body alone may under-build. |
| Existing code | Reuse candidate, not authority | Per file/class, classify against canon: keep / delete / decide. Never patch canon-contradicting code to "work for now." |

## Pre-flight (once, before phase 1)

1. **Read the realignment map end to end.** Confirm the phase order, the
   supersession map, and the open-decisions list. These shape everything after.
2. **Confirm the canon source set.** List the docs the realignment map names as
   canonical. These are the only docs you may treat as spec.
3. **Hygiene pass on the tracker.** Strip any `ready-for-agent`-equivalent label
   from superseded Done tickets so no agent picks up pre-canon ACs. Archive
   superseded Done tickets if the tooling exposes it. Reword any ticket whose AC
   checklist is stale from a prior cycle but whose real scope lives in a
   realignment comment.
4. **Resolve or park open decisions.** If the realignment map flags unresolved
   design decisions for later phases, do not start those phases until they are
   resolved. Park them explicitly; do not pretend they are decided.

## Per-phase method (repeat for each phase)

### 1. Load the canon for this phase's concepts

Read the realignment map's canon-delta section(s) covering this phase, plus the
per-service glossary and any cited ADRs. These define the non-negotiable model.

### 2. Read the ticket's realignment context and rewritten AC

Not just the issue body. If the ticket is comment-only (reword, not rebuild),
the AC may still be loose — tighten it against canon before building. If a
rebuild ticket exists for this concept, its AC supersedes the original
Done-ticket AC.

### 3. Audit existing code for this phase's scope

Grep the relevant service(s) for the concepts this phase touches. Classify each
piece:

- **Keep** — canon-compatible (e.g. a reusable authoring aggregate from a
  later, already-aligned slice).
- **Delete / replace** — contradicts canon (e.g. an old "X-as-session" model, a
  retired mode enum, a removed subset-selection shape).
- **Decide** — canon is silent. Flag and decide before building; do not guess
  silently.

Never extend canon-contradicting code. Delete or replace it.

### 4. Build the vertical slice, layer by layer

Order matters; each layer gates the next.

1. **Domain** — entities, value objects, domain events, policies. Apply any
   mandated patterns (e.g. Composite for hierarchies, Template Method for
   validation flows).
2. **Application** — commands, handlers, validators, DTOs, read models. Reuse
   existing pipeline behaviours (authorization, validation) where present.
3. **Infrastructure** — persistence model + migration. Do not preserve stale
   schema to avoid migration work; replace if it contradicts canon.
4. **API** — endpoints and payload shapes. Reject stale shapes that carry
   retired concepts.
5. **Tests** — unit (domain + application) and integration (persistence
   round-trip + read-model reconstruction).

### 5. Gate before closing the phase

Run the project's verification suite. Do not close the phase until all are
green:

- Build (compile all projects for the service).
- Tests (unit + integration).
- Coverage gate (if the project defines one).
- Migration sanity (the persistence snapshot matches the rebuilt domain).

### 6. Commit per phase

One logical commit per phase (or per layer if the phase is large). Use
Conventional Commits. Reference the tracker ticket in the body. Do not batch
phases into a single commit — reviewability matters when the work is a rebuild.

## Phase close-out checklist

Before moving to the next phase, confirm:

- [ ] Canon docs for this phase's concepts were loaded and not contradicted.
- [ ] Existing code was classified keep / delete / decide; nothing canon-contradicting was extended.
- [ ] All five layers built (domain, application, infrastructure, API, tests).
- [ ] Persistence snapshot matches the rebuilt domain (no stale schema).
- [ ] API payloads carry the rebuilt shape, not retired concepts.
- [ ] Build, tests, and coverage gate are green.
- [ ] Commit(s) reference the tracker ticket and follow Conventional Commits.
- [ ] Any open decision surfaced during the phase is recorded, not silently guessed.

## Hard rules

1. **Never skip the phase order.** Each phase depends on the prior. Skipping
   creates rework because later phases consume the contracts the earlier phases
   produce.
2. **Never extend canon-contradicting code.** If old code assumes a retired
   concept, delete or replace it. Patching it to "work for now" is the debt you
   are here to pay.
3. **Never start a phase with an unresolved open decision.** If the realignment
   map flags a decision for that phase, resolve it first or park the phase.
4. **Never trust the issue body alone.** Read the realignment comment and any
   rewritten AC. Stale checklists from a prior cycle will under-build.
5. **Never let superseded Done tickets stay agent-pickable.** Strip the
   ready-for-agent label and archive if tooling allows.

## Open-decisions protocol

When the realignment map flags an unresolved decision for a later phase:

1. Do not start that phase.
2. State the decision crisply: what is in tension, which canon source each side
   appeals to.
3. Decide explicitly (eliminate / merge / redefine / model-as-exception). Record
   the decision in an ADR or the realignment map's addendum.
4. Only then rewrite the affected AC and start the phase.

Silent guesses during implementation are not decisions — they are drift.
