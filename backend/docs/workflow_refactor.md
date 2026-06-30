# Refactor Workflow — Finishing the Canon-Realigned Tickets

How to build out the backlog after the mission-runtime canon rewrite. One
repeatable loop, run **once per ticket, in the order below**.

Companion docs:
- `ticket-playbook.md` — triage-first guide: which bucket a ticket is in
  (dead / rebuild / reword / clean feature) and how the agents run for each.
- `canon-realignment-after-mission-runtime-rewrite.md` — the ledger (ticket
  disposition + supersession map + phase order).
- `canon-realignment-workflow.md` — the governing method (authority chain,
  keep/delete/decide).
- `.agents/generator-agent.md` / `.agents/driver-agent.md` — the execution
  agents invoked in steps 1–2 below.

## Golden rule (applies to every ticket)

```
canon docs  >  rewritten ticket AC  >  existing code
   spec          work breakdown          harvest, never extend
```

For **rebuild** tickets, old code is a reuse candidate only. Delete or replace
what contradicts canon — never patch it to "work for now."

---

## Per-ticket loop

Run this once for each ticket in the order list below.

### 1. Generate — `generator-agent`
Invoke `generator-agent` for the ticket. **For rebuilds, use the realign DES id**
(DES-75/76/77/78/24), not the superseded Done ticket.

**Prompt** — `<NN>` is the HU number, `<N>` the Linear id; both come straight from
the order table below:

```text
Read @backend/.agents/generator-agent.md.

Run generator-agent for HU-<NN> DES-<N>.
```

Worked examples from the order list:
- Phase 2 (first full run): `Run generator-agent for HU-15 DES-22.`
- Phase 3 rebuild: `Run generator-agent for HU-17 DES-24.` — the realign id, never
  the superseded Done ticket it replaces.

→ Produces `hu<NN>-context.md` + `prompt_example_feature_hu<NN>.md` +
   `hu<NN>-brief.md` (the driver's compact resident brief).
→ **Stop 1:** review both files before driving. A mandated design pattern missing
   from a phase gate is a generation defect — regenerate, don't proceed.

### 2. Drive — `driver-agent`
Invoke `driver-agent` on the generated prompt file. It creates the worktree +
branch, then builds four layers, gating and asking for your commit-approval at
each.

**Kick off the driver** — once per ticket; runs pre-flight (worktree, branch,
labels, green base) and stops at the phase menu:

```text
Read @backend/.agents/driver-agent.md.

Run driver-agent for backend/docs/prompt_example_feature_hu<NN>.md.
```

| Phase | Layer | Gate |
|---|---|---|
| X.1 | Domain | build green + a unit test per public domain type |
| X.2 | Application | handler + validator tests pass all paths |
| X.3 | Infrastructure | migration succeeds; repository integration tests pass |
| X.4 | Api | endpoint smoke (200/201) + ADR-0005 coverage gate |

**Drive one phase at a time** in order X.1 → X.2 → X.3 → X.4. Per phase the loop
is three replies:

```text
# 1 — pick the phase (just reply "X.1" if the driver session is already live)
Run @backend/.agents/driver-agent.md and select X.1 from the phase menu.

# 2 — validate the detected design pattern (driver Step B)
Confirmed — proceed with X.1.

# 3 — approve the commit once the gate is green (driver Step F)
Approved — commit X.1.
```

Swap `X.1` for `X.2` / `X.3` / `X.4` on each pass; the driver re-shows the menu
with `[✓]` after every committed phase, then proceeds to the docker rebuild +
curl smoke (Stop 2) once all four are checked. If you disagree with the pattern
at reply 2, or a gate fails twice, the driver **hard-stops** — surface it, don't
push past it.

For **rebuild** tickets: X.3 **deletes/replaces** the pre-canon schema — do not
migrate stale schema forward.

### 3. Verify — Stop 2
Driver runs docker rebuild + curl smoke. You confirm the ticket's acceptance
criteria end-to-end.

### 4. Close out
Squash phase commits → draft PR to `develop` → move DES to **Done** in Linear →
remove the worktree.

### At a glance — what you type per ticket

One full pass, generate → close-out. `<NN>` is the HU number, `<N>` the Linear id.

| When | You type |
|---|---|
| Generate | `Read @backend/.agents/generator-agent.md. Run generator-agent for HU-<NN> DES-<N>.` |
| Stop 1 | Review both generated files; regenerate on a defect, otherwise proceed |
| Drive | `Read @backend/.agents/driver-agent.md. Run driver-agent for backend/docs/prompt_example_feature_hu<NN>.md.` |
| Phase pick × 4 | `X.1` → `X.2` → `X.3` → `X.4` (one reply per phase) |
| Pattern validate × 4 | Confirm the detected design pattern when the driver presents it (driver Step B) |
| Commit approval × 4 | Approve each phase's commit when the gate is green (driver Step F) |
| Stop 2 | Confirm acceptance criteria end-to-end against the API contract |
| Frontend slice | **Step 9** — paste into a new session with `@frontend/AGENTS.md` to generate the plan under `frontend/plans/`; **review the plan** (right altitude, source-verified anchors) as a frontend Stop 1; then **Step 9b** — paste to implement it phase by phase per the plan's own Scope/Gate/Commit Sequence |
| Close-out | `Run the close-out commands.` — draft PR to `develop` (GitHub squash-merges the phase commits on merge), move DES to **Done**, remove the worktree |

Roughly a dozen interactions per HU — the four phase picks, four pattern
validations, and four commit approvals are the bulk of it.

---

## Order to run the loop

Dependencies come from each ticket's *Blocked by* + the realignment links.
Phases 5–6 may run in parallel once DES-24 lands.

> **Status as of 2026-06-30:** rows 1, 2, 2b done, and row 7's code is done
> (PRs #31/#33/#48/#49/#50/#51/#53/#23). The critical path now **starts at
> row #3 (DES-24 / HU-17)**, then the 75 → 76 → 77 rebuild chain. DES-80
> (row 2c) is an ungated low-priority pickup runnable any time.

| # | Ticket(s) | HU | What | Note |
|---|---|---|---|---|
| 1 | DES-14 / DES-15 | HU-09 / 10A | Mission wrapper + composite + Target + optional Clue | ✅ **DONE** (PRs #31/#33 HU-09, #48/#49 HU-10A incl. X.4 coverage). |
| 2 | DES-22 | HU-15 | Create `LiveSession` from active mission; immutable snapshot | ✅ **DONE** (PR #50, 2026-06-22). |
| 2b | DES-79 | HU-15 f/u | Archive-time enforcement: block/cascade when archiving a quiz referenced by an active mission | ✅ **DONE** (PRs #51 + #53, 2026-06-22). |
| 2c | DES-80 | HU-14A f/u | `RemoveTriviaQuestion` command + question-removal domain slot + `TriviaQuestionRemoved` event | ⬜ **OPEN** (Backlog, Low) — deferred follow-up to HU-14A, **ungated** (HU-14A/DES-20 is Done); not a rebuild, not on the critical path — schedule any time (see below). |
| 3 | DES-24 | HU-17 | Single mission source; drop "session from quiz" | ⬜ **OPEN** (Todo) — rebuild. **← next critical-path ticket** |
| 4 | DES-75 | HU-16 | Trivia selection as a Substage, not a session | ⬜ **OPEN** (Todo) — rebuild (supersedes DES-23, shipped pre-canon as PR #19) |
| 5 | DES-76 | HU-21A | State machine `Scheduled→Preparing→Active→Paused→Finished→Cancelled` | ⬜ **OPEN** (Todo) — rebuild (supersedes DES-28, shipped pre-canon as PR #21) |
| 6 | DES-77 | HU-22 | Timer keyed off active `SubstagePlayMode` | ⬜ **OPEN** (Todo) — rebuild (supersedes DES-30, shipped pre-canon as PR #22) |
| 7 | DES-25 | HU-18 | Attach teams during `Scheduled` | ✅ code **DONE** (PR #23); only an AC reword may remain — confirm before re-running. |
| 8 | DES-39/40/41/42/43 | HU-29–32 | Evidence intake + QR Target resolution + traceability | already #28-aligned |
| 9 | DES-36/37/38 | HU-26–28 | Operator clue release | **resolve open decisions first** (see below) |
| 10 | DES-78 + HU-33B/34–36 | HU-33A… | Synchronized trivia substage + answer registration | rebuild (supersedes DES-44) |
| 11 | DES-54/55, HU-37/38/40 | HU-39… | `ScoreEntry` ledger + single session ranking | confirm DES-54/55 merge first |
| 12 | DES-31, HU-24/25 | HU-23… | Live team/operator boards | last |

## Deferred follow-up — DES-79 (resolve the product decision before starting it)

DES-79 hardens the published-quiz readiness gap at its source (the existing fix in
`plans/fix-published-quiz-readiness-gap.md` Phases 1–2 closes the create path reactively).
It is **gated**, so it is intentionally last in the order:

- **Blocked by DES-22** — Phases 1 & 2 of the fix must land first.
- **Product decision:** archiving a quiz referenced by an active mission must either
  **(A) be blocked** or **(B) cascade** a mission deactivation. Record the choice in an ADR
  or the canon ledger addendum before writing the final AC.
- **Missing capability:** needs a quiz→mission inverse query that does not exist today
  (`IMissionRepository` is CRUD-only).

## Deferred follow-up — DES-80 (RemoveTriviaQuestion)

`RemoveTriviaQuestion` is a PRD (`DES-62`) minimum application interface that was
**deliberately deferred** out of HU-14A because the `TriviaQuiz` aggregate had no
per-question removal slot (only `AddQuestion` / `UpdateQuestion`). Unlike DES-79 it is
**not gated** by any open decision — it is well-scoped and ready to run:

- **No open blocker** — HU-14A (`DES-20`) is **Done**; this builds question removal on
  top of that authoring baseline. Not a rebuild, not on the critical path → schedule any time.
- **Net-new domain behavior** — `TriviaQuiz.RemoveQuestion` + `TriviaQuestionRemoved`
  event + `sequenceOrder` reconciliation + published/archived edit guards, then the
  `RemoveTriviaQuestionCommand` use case and `DELETE /trivia/{id}/questions/{qid}` endpoint.
- **Pattern** — reuses the HU-14A `Template Method` authoring-validation workflow; no new
  pattern, no handler base-class restoration.

Run it through the standard per-ticket loop (generate → drive X.1→X.4 → close-out).
Rationale recorded in `hu14a-context.md` ("Deferred scope — RemoveTriviaQuestion") and the
`DES-62` PRD note.

## Open decisions blocking Phase 9 (resolve before starting it)

- **DES-37 (HU-27):** rule-based auto clue-release is not in canon — eliminate /
  merge into HU-26 / redefine.
- **DES-38 (HU-28):** operator-authored runtime clues conflict with the immutable
  snapshot — model as explicit exception or reinterpret as *release* of
  pre-snapshotted clues.

Record the decision in an ADR or the ledger addendum before rewriting the AC.

## Open decision blocking Phase 11 (resolve before starting it)

- **DES-54/55 (HU-39A/39B):** "trivia-session ranking" no longer exists — trivia is
  a `SubstagePlayMode`, ranking is single per `LiveSession` derived from `ScoreEntry`.
  DES-55's body AC is stale (`canon-realign` + `⚠️ Deuda de canon` comment); the
  ledger (`canon-realignment-after-mission-runtime-rewrite.md:83`) marks it **subsume
  into DES-54**. Confirm the merge into one unified session ranking — and whether
  DES-55 survives as its own ticket or is superseded — before rewriting either AC.

## Linear hygiene (human-only — MCP can't archive)

Before agents pick up tickets: archive the superseded Done tickets
**DES-23/28/30/44** and strip the stale `ready-for-agent` label from
**DES-23/30/44** so no agent grabs pre-canon acceptance criteria.
