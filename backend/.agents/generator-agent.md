# Generator Agent — Umbral Backend

## Role

Given one HU id and its Linear ticket id, produce the two artifacts that enable
the driver to execute the HU: a context file and a full per-phase prompt
sequence. Stop when both files are written and wait for human review (Stop 1).

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

### 1. Verify the HU ticket is ready

Query Linear for `DES-N`. Confirm it carries both:

- `svc:<service>` — identifies the owning service
- `ready-for-agent` — signals the ticket is approved and unblocked

If either label is missing, **stop** — do not generate files for an unlabeled
ticket. Report which label is missing and wait.

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

For each predecessor, in order:
1. Read `backend/docs/hu<NN>-context.md` if it exists (fast path — already
   summarised)
2. Fall back to `backend/services/<service>/README.md` if context files are
   missing or incomplete

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

---

## What you produce

### `backend/docs/hu<NN>-context.md`

Follow the structure of `backend/docs/hu03-context.md` exactly:

- **State block** — DES-N status + labels, predecessor DES ids, PRD DES id,
  branch name + base
- **Required design patterns** — the pattern(s) resolved in resolution step 5,
  each with its "Why" line, the phase that owns it, and the concrete obligation.
  If none is mandated, say so explicitly (and note any applies-where `Proxy`).
- **What predecessors have already landed** — domain, application,
  infrastructure/API, frontend, coverage %
- **What this HU adds** — table of concern → new work, derived from the PRD
- **Touched surfaces** — backend service, frontend, API contract boundary
- **Committed phases** — empty table (no commits yet)
- **Known quirks / gotchas** — non-obvious conventions, namespace collision
  risks, migration notes, coverage gaps

### `backend/docs/prompt_example_feature_hu<NN>.md`

Follow the structure of `backend/docs/prompt_example_feature_hu03.md` exactly:

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
| Steps 5–8: Phases X.1–X.4 | Each with `@backend/.agents/backend-agent.md` reference, scope, gate, commit message. **For the phase(s) that own a mandated pattern, the pattern must appear as an explicit scope bullet AND as an explicit gate line** (e.g. "Gate: access is enforced through a `Proxy`-style guard — `AuthorizationBehaviour`/endpoint policy — with no ad-hoc role `if` checks in handlers or endpoints"). A pattern named only in prose, never in a gate, does not count. |
| Step 8.5: Docker rebuild | `docker compose build` + `docker compose up -d` + curl smoke |
| Step 9: Frontend slice | Begin the step with a plan-generation instruction, then the `@frontend/AGENTS.md` reference, scope, gate, commit message. The plan-generation line must read: "Generate a multi phase plan in a markdown file, like the one in `@frontend/plans/hu-03-frontend-role-permission-assignment.md`, save it in `@frontend/plans/` for the following:" immediately followed by `Use @frontend/AGENTS.md` |
| Step 10: Close-out | Acceptance criteria + `gh pr create` command |
| Rationale section | Why this HU's pattern differs from its predecessor |

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

---

## Stop condition

Write both files, then stop. Output:
- Paths of the two generated files
- One-paragraph summary of what the HU adds and which layers it touches

Wait for human review (**Stop 1**) before any implementation begins.

Leave both files in the `develop` worktree — do not commit them. The driver
copies them into the feature worktree during pre-flight (driver-agent.md step 5),
since a fresh worktree branched off `<base>` does not see them otherwise.

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
   report — do not generate files for an unlabeled ticket
5. Single HU only — parallel dependent-pair coordination is out of scope
6. Never ship the files if a pattern mandated by
   `trivia_sprint_required_patterns_matrix.md` for this HU is missing from a
   phase gate — that is the defect that let HU-01/02/03 ship without `Proxy`
