# Generator Agent — Umbral Backend

## Role

Given one HU id and its Linear ticket id, produce the three artifacts that drive
the HU: a context file (the subagent's per-phase spec), a full per-phase prompt
sequence (human review + frontend slice), and a compact driver brief (the
driver's only resident input). Stop when all three are written and wait for
human review (Stop 1).

You do **not** implement anything — that is the driver's job.

---

## Input

Invoke with the HU id and its Linear ticket id:

```
Run generator-agent for HU-06 DES-12.
```

These map to the first two `Ref:` lines in every phase commit. Everything else
is resolved from Linear and local files.

---

## Resolution steps (run before writing anything)

### 1. Verify the HU ticket is ready — and classify it

Query Linear for `DES-N`. Confirm it carries both:

- `svc:<service>` — identifies the owning service
- `ready-for-agent` — signals the ticket is approved and unblocked

If either label is missing, **stop** — do not generate files for an unlabeled
ticket. Report which label is missing and wait.

`ready-for-agent` alone is **not** proof the ticket is live. A superseded Done
ticket can still carry it — the realignment hygiene pass that strips it is
manual and may not have run (this is the exact case the ledger flags as
"stale `ready-for-agent` can be stripped"). The generator must not depend on
that strip having happened. So classify the ticket here, at the front door:

1. **Check supersession.** If a `canon-realign`/`needs-rebuild` cycle touched
   this service, read the realignment map's supersession table. **If `DES-N`
   appears in the superseded ("old") column → STOP**, even if it carries
   `ready-for-agent`. Report: "DES-N is superseded by DES-M — run the rebuild
   ticket, not this one." Do not generate files for a superseded ticket.
2. **Resolve mode.** Read the target's `canon-realign` / `needs-rebuild`
   labels:
   - `needs-rebuild`, or named as a rebuild ticket in the realignment map →
     **mode = realignment-rebuild**. Record this in the context file's state
     block; step 6 reads it rather than re-inferring it.
   - `canon-realign` without `needs-rebuild` → comment-only reword. The issue
     body / AC checklist may be stale — the real scope lives in the ticket's
     `⚠️ Deuda de canon` / `⚠️ Nota de canon` comment. Tighten the AC against
     canon (per `canon-realignment-workflow.md` step 2) before building.
   - neither label → **mode = feature flow**.

When mode = realignment-rebuild (or any realignment label is present in the
service), the realignment map is a **required input** for the rest of
resolution — declare it now; do not defer first contact to step 6.

### 2. Resolve the PRD ref

Query Linear for tickets with:
- label `svc:<service>` (same service as the HU ticket)
- label `ready-for-agent`
- a `DES-NN` prefix indicating a PRD (not an HU)

Find the local PRD file: `backend/docs/prd/<des-nn>-*.md`

Read the local file — **never re-fetch PRD scope from Linear**; the local file
is authoritative.

If multiple PRD tickets match the service label, stop and ask the user which
one. If the local PRD file does not exist, stop and report.

### 3. Resolve predecessors

Query Linear for all tickets with the same `svc:<service>` label in state
**Done** or **In Progress**.

**Filter out superseded tickets first.** If a realignment cycle touched this
service, drop any Done ticket that appears in the realignment map's superseded
("old") column — and if it has a rebuild successor, substitute that successor.
A superseded Done ticket is **not** a predecessor: its code is the stale model
this work tears out, so citing it under "what predecessors landed" anchors the
new HU on the model it is meant to replace. Never rely on `ready-for-agent`
having been manually stripped to catch this — filter against the map.

**Cap the full reads to the predecessors this HU builds on.** Full-reading every
surviving predecessor's context file is the back half of the backlog's biggest
avoidable cost: by the late tickets a service can carry a dozen Done/In-Progress
predecessors, so full-reading each `hu<NN>-context.md` taxes the heaviest tickets
hardest — yet most predecessors touch a different aggregate and never inform a
single line of this HU's derivation. Using this HU's target aggregate(s) (from
the PRD scope resolved in step 2), classify each surviving predecessor:

- **Build-on** — it landed surface this HU depends on: the same aggregate, **or**
  a cross-aggregate seam this HU consumes (a shared base class, an auth guard, an
  API contract, an event). Read its full context file (fast path — already
  summarised), falling back to `backend/services/<service>/README.md` if the
  context file is missing or incomplete.
- **Unrelated** — it touches only aggregates this HU does not build on. Do **not**
  full-read it; record a one-line "landed, untouched by this HU" note from its
  state block alone.

The filter is **build-on, not same-name**: when unsure whether a predecessor
landed a seam this HU consumes, treat it as build-on and read it. A dropped
dependency anchors the HU on a stale or incomplete model — the same failure the
supersession filter guards against — and costs far more than one extra read.

Build the "what predecessors have already landed" section from these sources
only. Do not re-read the PRD for predecessor scope.

### 4. Resolve branch base

- If any predecessor is **In Progress** (not yet merged to `develop`):
  branch base = `feature/<predecessor-hu-slug>`. Note this explicitly in the
  branch state section.
- Otherwise: branch base = `develop`.

### 5. Resolve required design pattern(s)

The PRD defines *what* the HU must do; it does **not** state which design
patterns are mandatory. That mapping is authoritative and lives outside the
PRD — you **must** resolve it here, or the pattern will silently never enter the
generated scope (this is exactly how HU-01/02/03 shipped without `Proxy`).

1. Read `backend/docs/trivia_sprint_required_patterns_matrix.md` — find the HU
   in the **HU → Pattern** table for its service. Record the mandated
   pattern(s) and the one-line "Why" from that row.
2. Read `backend/docs/adr/0004-required-domain-patterns.md` for *where* each
   pattern is expected to live (e.g. `Proxy` → role/policy-based access guards
   in the service and presentation layers; `State` → `LiveSession` lifecycle;
   `Strategy` → scoring/progression policies; etc.).
3. Map each mandated pattern to the phase(s) that must realize it:

   | Pattern | Typically lands in phase | Concrete obligation |
   |---|---|---|
   | `Proxy` | X.2 Application + X.4 Api | guarded handler/behaviour + endpoint authorization policy — no ad-hoc role `if` checks |
   | `Template Method` | X.1 Domain + X.2 Application | one stable validation workflow with overridable mode-specific steps |
   | `State` | X.1 Domain | explicit state type for lifecycle transitions, not enum + conditionals |
   | `Chain of Responsibility` | X.2 Application | ordered, composable validators — not one collapsed handler |
   | `Facade` | X.2 Application | single orchestration entry point over the coordinated subsystems |
   | `Strategy` | X.1/X.2 | interchangeable policy abstraction selected at runtime |
   | `Composite` | X.1 Domain | hierarchical node modeling |

If the HU is **not** listed in the matrix, record "no mandated pattern". Do
**not** infer a pattern to fill the gap — most no-pattern HUs (e.g. `HU-08`
real-time enabler, `HU-35` post-close reads) genuinely have none and must not be
forced into one.

`Proxy` is the one nuance: the matrix tags a *specific* set of no-pattern HUs
(`HU-04`, `HU-05`, `HU-36B`) with an **applies-where** note, because they expose
protected endpoints. This is **informational, not a mandate** — those endpoints
inherit the standard gateway + `AuthorizationBehaviour` guard they would get
anyway (ADR-0001/0002); they do **not** get a new pattern gate. Carry the note
only for the HUs the matrix actually tags — never auto-attach `Proxy` to every
HU that happens to have an endpoint. The bright line:

- **Mandated** `Proxy` (HU-01/02/03/06/07A/07B/19/20/36A) → required design
  obligation → explicit scope bullet **and** gate line.
- **Applies-where** `Proxy` (HU-04/05/36B only) → inherits the existing guard →
  a note in the context file, **no** new gate.

This resolution feeds both generated files: the **Required design patterns**
section of the context file and the per-phase **scope + gate** of the prompt
file (see below). A mandated pattern that does not appear in a phase gate is a
generation defect — do not ship the files without it.

### 6. Resolve per-phase derivation

This is the step that lets phase subagents skip the canon. You read the canon
**once, here**, and write the result into the context file's **Per-phase
derivation** section so the four phase subagents never re-load it.

The canon source set and precedence depend on the mode **already resolved in
step 1** — do not re-infer it here, just branch on it:

- **Feature flow** — canon source is the standard doc set; precedence per
  `backend-agent.md` (`ddd_solution_model.md` → `CONTEXT.md` → `structure.md` →
  `bd_umbral_entity_spec.md` → `plans/...`). Read only the section(s) for this
  HU's aggregate(s).
- **Realignment rebuild** (mode set in step 1) —
  follow `canon-realignment-workflow.md`. Canon source is the realignment map's
  canon-delta + per-service glossary + cited ADRs; authority chain is
  `canon docs > tracker AC > existing code`. In this mode each per-phase block
  MUST also carry:
  - a **keep / delete / decide** classification of the existing code in scope
    (realignment-workflow step 3), and
  - **mirror-anchors only to code classified `keep`** — never cite a
    canon-contradicting file as a pattern to copy.

**Read canon by section, never whole-file.** `bd_umbral_entity_spec.md` is ~64KB
and `ddd_solution_model.md` is large; full-reading either per ticket is the
generator's single biggest avoidable token cost (and this runs once per ticket
across the whole backlog). For each, `grep` the aggregate/service heading first,
then read **only that heading's line range** — never the whole file. The same
applies to the cited ADRs and `CONTEXT.md`: open the cited section, not the whole
document. If a needed detail isn't under the expected heading, widen the range;
do not fall back to a full-file read. This is the generator-side complement to
the per-phase derivation block — the block keeps the four subagents from
re-reading canon; sectioned reads keep the generator itself from over-reading it.

For each phase X.1–X.4, distill the exact types/fields/invariants this HU adds at
that layer, the target files (each with an existing file to mirror), the pattern
the phase owns (step 5), and the gate. **Cite the canon section each derivation
came from.** Derive only what this HU touches — do not transcribe whole entities.

---

## What you produce

### `backend/docs/hu<NN>-context.md`

Follow the structure of `backend/docs/hu09-context.md` exactly — it is the
current exemplar and the only context file that carries the **Per-phase
derivation** section. Produce all of these sections:

- **State block** — DES-N status + labels, **resolved mode** (feature flow /
  realignment-rebuild, from resolution step 1) and, if superseded handling
  applied, which predecessor ids were dropped or substituted, predecessor DES
  ids, PRD DES id, branch name + base
- **Required design patterns** — the pattern(s) resolved in resolution step 5,
  each with its "Why" line, the phase that owns it, and the concrete obligation.
  If none is mandated, say so explicitly (and note any applies-where `Proxy`).
- **What predecessors have already landed** — domain, application,
  infrastructure/API, frontend, coverage %. Full detail for the **build-on**
  predecessors (resolution step 3); a one-line "landed, untouched by this HU"
  note for the **unrelated** ones — do not pad them out to full detail.
- **What this HU adds** — table of concern → new work, derived from the PRD
- **Touched surfaces** — backend service, frontend, API contract boundary
- **Committed phases** — empty table (no commits yet)
- **Known quirks / gotchas** — non-obvious conventions, namespace collision
  risks, migration notes, coverage gaps
- **Per-phase derivation (X.1–X.4)** — the authoritative implementation spec per
  phase, from resolution step 6. This is what lets the phase subagent skip the
  full-canon read — if it is thin, every phase re-reads the 1000+-line entity
  spec instead. Fill one block per phase using this template:

  > ### Phase X.N — \<layer>
  > **Derive** (cite canon — e.g. `bd_umbral_entity_spec.md` §\<aggregate>,
  > `ddd_solution_model.md` §\<svc>):
  > - \<type / method / event / exception this HU adds at this layer> — \<invariants / behaviour>
  >
  > **Target files** (create | edit — file to mirror):
  > - \<create|edit> `\<path>` — mirror `\<existing canon-aligned file>`
  >
  > **Pattern this phase owns:** \<pattern from step 5, or none>
  > **Gate:** \<the phase gate — unit test per new type / handler + validator
  > tests / migration no-op + repo test / endpoint + coverage>
  >
  > _Realignment-rebuild mode only — also add:_
  > **Existing code (keep / delete / decide):**
  > - delete `\<path>` — \<canon-contradicting concept it carries>
  > - keep   `\<path>` — \<why canon-aligned; safe to mirror>
  > - decide `\<path>` — \<canon silent; resolve before building>

### `backend/docs/prompt_example_feature_hu<NN>.md`

Follow the structure of `backend/docs/prompt_example_feature_hu09.md` exactly —
it is the current exemplar; its Steps 5–8 are the thin, derivation-referencing
shape (point at the `hu<NN>-context.md` X.N block + gate + commit, no inline
scope). Produce these sections:

| Section | Content |
|---|---|
| Header | HU id, title, branch name, link to workflow_for_prompts.md |
| Key difference note | What this HU changes vs the predecessor pattern |
| Required design patterns | The mandated pattern(s) from resolution step 5, the phase that owns each, and the gate obligation. Mirror the context file's section. |
| Pre-resolved orient | Dated today; what predecessors landed + what this HU adds |
| Step 1: Orient | Skippable prompt (run only if README/Linear may have changed) |
| Step 2: Label DES-N | `ready-for-agent` via Linear MCP |
| Step 3: Confirm readiness | Both labels confirmed, acceptance criteria output |
| Step 4: Start the slice | Branch, move to In Progress, output scope |
| Steps 5–8: Phases X.1–X.4 | Each step is **thin**: the `@backend/.agents/backend-agent.md` reference, one line pointing the subagent at the **X.N derivation block in `hu<NN>-context.md`** as its spec, the gate, and the commit message. Do **not** re-list the per-type scope here, and do **not** tell the subagent to "use canonical docs" or "inspect existing code" each phase — that scope lives once in the derivation block; duplicating it causes drift and makes every phase re-read the full canon (the cost this whole flow exists to avoid). **The pattern the phase owns must still appear as an explicit gate line** (e.g. "Gate: access enforced through a `Proxy`-style guard — `AuthorizationBehaviour`/endpoint policy — no ad-hoc role `if` checks"). A pattern named only in prose, never in a gate, does not count. |
| Step 8.5: Docker rebuild | `docker compose build` + `docker compose up -d` + curl smoke |
| Step 9: Frontend slice | Begin the step with a plan-generation instruction, then the `@frontend/AGENTS.md` reference, scope, gate, commit message. The plan-generation line must read: "Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:" immediately followed by `Use @frontend/AGENTS.md`. The Step must then embed the four-point concreteness rule verbatim (see "Frontend plan concreteness rule" below). |
| Step 10: Close-out | Acceptance criteria + `gh pr create` command |
| Rationale section | Why this HU's pattern differs from its predecessor |

#### Frontend plan concreteness rule (Step 9)

Step 9 must embed this four-point rule **verbatim** in the prompt file, so the frontend plan's
altitude tracks the slice instead of blindly copying one exemplar. hu-03 is near-executable because
it is a 1-endpoint, ~4-file feature; emitting that uniform code-completeness for a 13-endpoint slice
produces large, speculative scope, and emitting hu-10a's contract-table altitude for a 1-endpoint
slice underspecifies it. The exemplar is chosen by shape (see the Step 9 row); the rule below makes
the choice operational:

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types,
   real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only
   for the **fully-knowable near-term increments** (typically the foundation + first authoring
   increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract
   table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an
   open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan
   names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor
   targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g.
   "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail
   is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context ·
   Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment**
   (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment ·
   **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no
   "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

**Commit message format for every backend phase:**

```
feat(<svc-short>): phase X.Y — <layer> (HU-NN)

Ref: HU-NN
Ref: DES-N
Ref: DES-PRD
```

**Frontend commit format:**

```
feat(frontend): <hu-title> — HU-NN

Ref: HU-NN
Ref: DES-N
Ref: DES-PRD
```

### `backend/docs/hu<NN>-brief.md`

The **driver's only resident artifact** — a compact projection of the
driver-facing fields already resolved above, so the driver never opens the full
prompt or context file. This is the lever that keeps the driver's permanent
context small: every file the driver opens is re-sent on its context every turn
for the rest of the run, and distilling-after-reading does not reclaim it (the
opened bytes stay in the transcript) — so the only way to keep the footprint at
~1.5K instead of ~12K is to hand the driver a file that *is* already small.

This is a **re-projection, not a re-derivation**: every value here is copied from
the prompt file you just wrote — resolve nothing new, and keep the two
byte-consistent (HU id, branch, gates, commit refs). The subagent's per-phase
implementation spec stays in the context file; the brief carries only what the
driver itself needs to delegate, gate, commit, and report. Template:

> # HU-NN — Driver brief
> _Driver reads only this file. The full prompt + context are for the subagent
> and human review; the driver never loads them._
>
> **Nature of this HU** _(omit for a plain feature build):_ \<verification |
> realignment-rebuild> — one line on which phases verify existing code vs.
> implement new, so the driver never authorizes a subagent to rebuild what a
> predecessor (e.g. a `needs-rebuild` HU) already shipped.
>
> ## Slice
> | HU | DES | PRD | Service | Branch | Base |
> |----|-----|-----|---------|--------|------|
> | HU-NN — \<title> | DES-N | DES-PRD | \<service> | feature/hu-NN-\<slug> | \<develop \| feature/...> |
>
> ## Required pattern(s) → owning phase
> - \<Pattern> (phase X.Y) — \<one-line Why> — obligation: \<concrete>  _(or: none mandated — say so explicitly)_
>
> ## Per phase — gate + owned pattern
> | Phase | Gate | Pattern |
> |-------|------|---------|
> | X.1 Domain | \<gate> | \<pattern \| —> |
> | X.2 Application | \<gate> | … |
> | X.3 Infrastructure | \<gate> | … |
> | X.4 Api | \<gate> + ADR-0005 coverage | … |
>
> Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8
> verbatim (the driver presents what the human approved; do not paraphrase or
> normalize punctuation):
> - X.1 `\<exact subject from prompt Step 5>`
> - X.2 `\<exact subject from prompt Step 6>`
> - X.3 `\<exact subject from prompt Step 7>`
> - X.4 `\<exact subject from prompt Step 8>`
>
> Trailer (every phase): `Ref: HU-NN` / `Ref: DES-N` / `Ref: DES-PRD`
>
> ## Acceptance criteria
> - \<from prompt Step 10>
>
> ## Endpoints + smoke (driver verifies at Stop 2)
> _If the HU adds no endpoints (e.g. a verification HU), say "none new" and list the
> existing endpoints to smoke plus any behavioral change (e.g. a status-code fix)._
> - \<METHOD path> — \<curl, or method + path + expected status when the prompt gives no literal curl> — expect \<status>; request/response shape \<for the frontend contract>
>
> ## Frontend slice
> Human-driven — see Step 9 of `prompt_example_feature_hu<NN>.md`.

Delegation never relays per-phase scope text: the subagent reads the X.N
derivation block in `hu<NN>-context.md` itself (its primary source). The brief's
per-phase row gives the driver only the gate it runs and the commit it presents.

---

## Stop condition

Write all three files, then stop. Output:
- Paths of the three generated files (context, prompt, brief)
- One-paragraph summary of what the HU adds and which layers it touches

Wait for human review (**Stop 1**) before any implementation begins.

Leave all three files in the `develop` worktree — do not commit them. The driver
copies them into the feature worktree during pre-flight (driver-agent.md step 5),
since a fresh worktree branched off `<base>` does not see them otherwise.

### Responding to Stop 1 feedback

When the human reviews the files and replies, classify the feedback before
touching anything — the two cases get opposite treatment:

- **Defect** (a mandated pattern is missing from a phase gate, a required section
  is absent, the wrong PRD/predecessor was resolved, or the canon source set is
  wrong). The derivation is structurally unsound, so **regenerate** from the
  corrected inputs — re-run the affected resolution steps and rewrite the files.
  Do not hand-patch around a structural defect. This is the
  `workflow_refactor.md` "regenerate, don't proceed" posture.

- **Refinement** (the human corrects or adds detail to *one* derivation block,
  one gate line, or one scope row). Make a **surgical edit to only that span**.
  Do **not** re-run step 6 across the whole HU and do **not** re-summarize blocks
  the human did not flag — a second pass over the same canon silently re-phrases
  invariants, drops citations, or shifts gate lines you already approved, so the
  gate the driver ends up enforcing is no longer the gate the human signed off
  on. Leave every approved derivation block and phase-gate table byte-for-byte
  unchanged; touch only what was called out. If the flagged span is a value the
  brief mirrors (a gate line, branch, commit ref, or acceptance criterion), apply
  the same change to `hu<NN>-brief.md` in the same pass — the brief is the
  driver's resident copy and must not drift from the prompt.

If you are unsure which case applies, ask — do not default to a full regenerate,
because that is the path that mutates approved content.

---

## Constraints

1. Never start implementation — that belongs to the driver
2. Never invent scope not found in the PRD or predecessor context files —
   **except** the mandated design pattern(s), which come from the patterns
   matrix (resolution step 5), not the PRD, and must always be carried into the
   scope and gate of their owning phase
3. If the PRD is ambiguous for this HU's specific scope, note the ambiguity in
   the prompt file's rationale section rather than guessing
4. If the HU ticket is missing `ready-for-agent` or `svc:<service>`, stop and
   report — do not generate files for an unlabeled ticket. Likewise, if the
   ticket is in the realignment map's superseded column, stop **regardless of
   `ready-for-agent`** — never trust a manual hygiene strip to have caught it
   (resolution step 1)
5. Single HU only — parallel dependent-pair coordination is out of scope
6. Never ship the files if a pattern mandated by
   `trivia_sprint_required_patterns_matrix.md` for this HU is missing from a
   phase gate — that is the defect that let HU-01/02/03 ship without `Proxy`
7. The **Per-phase derivation** section is mandatory and every block must cite
   its canon source — a derivation with no citation is unverifiable and must not
   ship. If the canon genuinely lacks a detail, say so in the block rather than
   inventing it. In realignment-rebuild mode the block must also carry the
   keep/delete/decide classification, and mirror-anchors may point only at code
   classified `keep`.
8. On a *refinement* at Stop 1, never re-derive an approved derivation block or
   phase-gate table — edit only the flagged span (see "Responding to Stop 1
   feedback"). Full regenerate is for structural *defects* only; using it for a
   one-line refinement silently drifts content the human already approved.
9. Step 9 must carry the **frontend plan concreteness rule** (proportion
   concreteness to certainty; verify every anchor against source; required
   sections; final-forms-only) and pick the plan exemplar by slice shape
   (hu-03 = small surface, hu-10a = large/blocked surface). Pointing at a single
   exemplar without the rule is a generation defect — it propagates that
   exemplar's altitude onto a slice of a different shape.
