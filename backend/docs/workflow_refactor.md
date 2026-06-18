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
→ Produces `hu<NN>-context.md` + `prompt_example_feature_hu<NN>.md`.
→ **Stop 1:** review both files before driving. A mandated design pattern missing
   from a phase gate is a generation defect — regenerate, don't proceed.

### 2. Drive — `driver-agent`
Invoke `driver-agent` on the generated prompt file. It creates the worktree +
branch, then builds four layers, gating and asking for your commit-approval at
each:

| Phase | Layer | Gate |
|---|---|---|
| X.1 | Domain | build green + a unit test per public domain type |
| X.2 | Application | handler + validator tests pass all paths |
| X.3 | Infrastructure | migration succeeds; repository integration tests pass |
| X.4 | Api | endpoint smoke (200/201) + ADR-0005 coverage gate |

For **rebuild** tickets: X.3 **deletes/replaces** the pre-canon schema — do not
migrate stale schema forward.

### 3. Verify — Stop 2
Driver runs docker rebuild + curl smoke. You confirm the ticket's acceptance
criteria end-to-end.

### 4. Close out
Squash phase commits → draft PR to `develop` → move DES to **Done** in Linear →
remove the worktree.

---

## Order to run the loop

Dependencies come from each ticket's *Blocked by* + the realignment links.
Phases 5–6 may run in parallel once DES-24 lands.

| # | Ticket(s) | HU | What | Note |
|---|---|---|---|---|
| 1 | DES-14 / DES-15 | HU-09 / 10A | Mission wrapper + composite + Target + optional Clue | **In progress; finish by hand** — Domain + Application already committed (`c561867`, `9eef83a`); only X.3 + X.4 remain. Do not re-run the pipeline from scratch. |
| 2 | DES-22 | HU-15 | Create `LiveSession` from active mission; immutable snapshot | first full pipeline run |
| 3 | DES-24 | HU-17 | Single mission source; drop "session from quiz" | rebuild |
| 4 | DES-75 | HU-16 | Trivia selection as a Substage, not a session | rebuild (supersedes DES-23) |
| 5 | DES-76 | HU-21A | State machine `Scheduled→Preparing→Active→Paused→Finished→Cancelled` | rebuild (supersedes DES-28) |
| 6 | DES-77 | HU-22 | Timer keyed off active `SubstagePlayMode` | rebuild (supersedes DES-30) |
| 7 | DES-25 | HU-18 | Attach teams during `Scheduled` | reword only (code largely exists) |
| 8 | DES-39/40/41/42/43 | HU-29–32 | Evidence intake + QR Target resolution + traceability | already #28-aligned |
| 9 | DES-36/37/38 | HU-26–28 | Operator clue release | **resolve open decisions first** (see below) |
| 10 | DES-78 + HU-33B/34–36 | HU-33A… | Synchronized trivia substage + answer registration | rebuild (supersedes DES-44) |
| 11 | DES-54/55, HU-37/38/40 | HU-39… | `ScoreEntry` ledger + single session ranking | confirm DES-54/55 merge first |
| 12 | DES-31, HU-24/25 | HU-23… | Live team/operator boards | last |

## Open decisions blocking Phase 9 (resolve before starting it)

- **DES-37 (HU-27):** rule-based auto clue-release is not in canon — eliminate /
  merge into HU-26 / redefine.
- **DES-38 (HU-28):** operator-authored runtime clues conflict with the immutable
  snapshot — model as explicit exception or reinterpret as *release* of
  pre-snapshotted clues.

Record the decision in an ADR or the ledger addendum before rewriting the AC.

## Linear hygiene (human-only — MCP can't archive)

Before agents pick up tickets: archive the superseded Done tickets
**DES-23/28/30/44** and strip the stale `ready-for-agent` label from
**DES-23/30/44** so no agent grabs pre-canon acceptance criteria.
