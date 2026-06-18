# Ticket Playbook — Triaging Backlog Work onto the Agents

A triage-first companion to the three workflow docs. Use it to answer one
question per ticket: **which kind of ticket is this, and how do the agents run
for it?**

Companion docs:
- `canon-realignment-after-mission-runtime-rewrite.md` — the ledger (ticket
  disposition + supersession map + phase order).
- `canon-realignment-workflow.md` — the governing method (authority chain,
  keep/delete/decide).
- `workflow_refactor.md` — the per-ticket loop and the order to run it.
- `.agents/generator-agent.md` / `.agents/driver-agent.md` — the execution
  agents.

The key idea: the label and the top comment tell you which **mode** the agents
run in, and mode changes three things — the **canon source**, whether **old code
is authority or garbage**, and **where the real AC lives**. Everything else is
the same two-agent pipeline.

---

## Step 0 — Triage before touching an agent

Read the labels and the top comment. That puts the ticket in exactly one bucket:

| Signal on the ticket | Bucket | Meaning |
|---|---|---|
| In the supersession table's "old" column (DES-23/28/30/44) + `⚠️ Nota de canon` pointing at a rebuild | **0 — Dead** | Do not work it. Go to its successor. |
| `canon-realign` **+** `needs-rebuild` (DES-75/76/77/78), or `needs-rebuild` alone (DES-24) | **1 — Rebuild** | Code exists but contradicts canon. Harvest, don't extend. |
| `canon-realign` + `⚠️ Deuda de canon`, **code already Done** (DES-25/12/18/19) | **2a — Reword-on-built** | Mostly an AC/wording fix; code largely exists. |
| `canon-realign` + `⚠️ Deuda`/`Nota`, **not yet built** (DES-15/31/36/37/38/54/55) | **2b — Reword-then-build** | New build, but issue-body AC is stale — real scope is in the comment + canon. |
| No realignment label, `ready-for-agent` + `svc:` (DES-22, the #28 evidence HUs, net-new) | **3 — Clean feature** | Standard feature flow, no canon-delta baggage. |

Comment markers:
- `⚠️ Deuda de canon` = a **debt to pay** — the AC/wording is wrong; fix it
  before building.
- `⚠️ Nota de canon` = **informational** — supersession pointers, historical
  notes. Lighter; often no code change.

The generator enforces Bucket 0 at its front door: it stops on any superseded
ticket regardless of `ready-for-agent`, and drops superseded tickets from the
predecessor set (`generator-agent.md` resolution steps 1 and 3).

---

## The common spine (buckets 1, 2b, 3 — anything that builds code)

Every build runs the same two-agent chain (`workflow_refactor.md`):

```
generator-agent  →  Stop 1 (review 2 files)  →  driver-agent  →  Stop 2 (verify AC)  →  close out
```

1. **`generator-agent DES-N`** → writes `hu<NN>-context.md` +
   `prompt_example_feature_hu<NN>.md` (mode, predecessors, PRD, mandated pattern,
   per-phase derivation). **Stop 1:** review both. A mandated pattern missing
   from a phase gate = regenerate, don't proceed.
2. **`driver-agent <prompt file>`** → worktree + branch, then phases
   X.1 Domain → X.2 Application → X.3 Infra → X.4 Api, gating each and asking
   commit-approval per phase. **Stop 2:** docker rebuild + curl smoke.
3. **Verify** the ticket's acceptance criteria end-to-end.
4. **Close out:** squash phase commits → draft PR to `develop` → move DES to
   **Done** → remove worktree.

What changes between buckets is *inside* the generator and the driver, below.

---

## Bucket 0 — Dead / superseded (DES-23/28/30/44)

**Do nothing with agents.** Pre-canon Done tickets whose work is being torn out.

- Work the **successor** instead: DES-23→75, 28→76, 30→77, 44→78.
- **Human-only Linear hygiene** (MCP can't archive): archive the four, strip
  `ready-for-agent` from DES-23/30/44 so they leave active views. This is
  hygiene, not the safety net — the generator stops on them regardless.

---

## Bucket 1 — Rebuild (DES-75/76/77/78, DES-24)

Realignment-rebuild mode. The differences are mechanical:

- **Invoke the generator with the *realign* DES id** — never the superseded Done
  id.
- **Canon source flips** to the realignment map's canon-delta + per-service
  glossary + cited ADRs (not the standard doc set). Authority chain:
  `canon docs > rebuild-ticket AC > existing code`.
- **AC source:** the rebuild ticket's AC supersedes the old Done ticket's AC
  entirely.
- **Per-phase derivation must carry keep / delete / decide** for existing code
  in scope, and **mirror-anchors may only point at code classified `keep`** —
  never cite a canon-contradicting file as a pattern to copy
  (`generator-agent.md` constraint 7).
- **Driver X.3 deletes/replaces the pre-canon schema** — do *not* migrate stale
  schema forward. Old code is a reuse candidate, never extended.

Mental model: you're rebuilding the slice and harvesting the canon-compatible
parts, not editing the old one.

---

## Bucket 2a — Reword on already-built code (DES-25/12/18/19)

The canon change is wording/AC; `workflow_refactor.md` marks these "code largely
exists." **Do not fire the full 4-phase pipeline** for a wording delta.

1. Read the `⚠️ Deuda de canon` comment and the canon delta it cites.
2. **Tighten the AC against canon** — the issue body may still carry the loose
   pre-canon checklist.
3. Diff the existing code against the corrected AC. If it already satisfies canon
   (e.g. DES-26 was "behavior unaffected"), it's a **doc/AC-only change** — no
   code.
4. If a small gap remains, make a **surgical edit** (a single driver phase is
   fine; most of these don't need the worktree ceremony).

Decision gate: *does the built code contradict canon, or just the words
describing it?* Only the words → 2a. The code is wrong → it's really a Bucket 1
and should carry `needs-rebuild`; flag that mismatch.

---

## Bucket 2b — Reword, then build (DES-15/31/36/37/38/54/55)

New code, but **the issue-body AC is stale** — real scope is in the `⚠️` comment
plus the canon delta.

1. Generator runs in feature flow, **but its AC input is the comment + canon, not
   the issue body** (`canon-realignment-workflow.md` step 2: tighten AC before
   building).
2. Then the normal generator → driver pipeline.

**Open-decision gate (hard stop):** DES-37, DES-38, DES-54, DES-55 carry
unresolved design decisions (rule-based clue auto-release isn't in canon; runtime
clues vs. immutable snapshot; the 39A/39B ranking merge). **Do not start these
phases until the decision is recorded in an ADR or the ledger addendum.** A
silent guess during implementation is drift, not a decision.

---

## Bucket 3 — Clean feature (DES-22, the #28 evidence HUs, net-new)

Standard feature flow:

- Generator canon source = the standard set, precedence per `backend-agent.md`:
  `ddd_solution_model.md → CONTEXT.md → structure.md → bd_umbral_entity_spec.md →
  plans/…`. Read only the section(s) for this HU's aggregate.
- Predecessors from `svc:` Done/In-Progress (filtered of superseded tickets).
- Normal generator → driver → close-out.

The #28 evidence HUs (DES-39/41/43/46/47/56) are unlabeled *because* the commit
realigned them directly — genuine feature flow. **Exceptions:** DES-40 and DES-42
got `canon-realign` + binding-target drift comments on 2026-06-17 — those two are
**2b**, not 3.

---

## The order is not negotiable

Work the ledger's phase order (`workflow_refactor.md`, "Order to run the loop"):
DES-14/15 → DES-22 → DES-24 → 75 → 76/77 → 25 → evidence → clues → trivia →
scoring → boards. Each phase consumes contracts the prior one produces; skipping
creates rework.

**DES-14/15 (HU-09/10A) is already mid-flight** — Domain + Application are
committed (`c561867`, `9eef83a`); only X.3 + X.4 remain. Finish it by hand; do
not re-run the pipeline from scratch.
