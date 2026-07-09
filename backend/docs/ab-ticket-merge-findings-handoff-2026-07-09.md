# Handoff — which A/B tickets collapse into one (2026-07-09)

Findings from an audit of every `HU-<n>A` / `HU-<n>B` split in the Linear backlog,
against GitHub issue state and the actual domain code. **Nothing here has been
applied to Linear.** The only artifact changed so far is
`workflow_refactor.md` (status banner, run order, hygiene section).

The question this answers: *which A/B pairs were cut along a seam that no longer
exists, so `HU-34A` + `HU-34B` should just become `HU-34`?*

Companion docs — read rather than re-derive:
- `workflow_refactor.md` — the per-ticket loop + current run order.
- `canon-realignment-after-mission-runtime-rewrite.md` — the ticket-disposition ledger.
- `users-realignment-decisions-2026-07-06.md` — the Users ↔ SessionOperations track (now closed except GH #85).
- `backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md` — pointer/advancement contract.
- ADR-0010 (*in revisión*) — the `EvidenceSubmission` threshold that DES-40/41/46/47 all hang off.

---

## Verdicts

| Pair | Verdict | Becomes |
|---|---|---|
| DES-15 / **DES-16** (HU-10A/10B) | **Close DES-16 — already built** | `HU-10` |
| DES-40 / DES-41 (HU-30A/30B) | **Merge** — B is A's failure branch | `HU-30` |
| DES-46 / DES-47 (HU-34A/34B) | **Merge** — one invariant, two viewpoints | `HU-34` |
| DES-49 / DES-50 (HU-36A/36B) | **Merge** — one projection, state-gated | `HU-36` |
| DES-54 / **DES-55** (HU-39A/39B) | **Close DES-55 — premise void** | `HU-39` |
| DES-51 / **DES-52** (HU-37A/37B) | **Split wrong** — merge B into DES-54, keep A | `HU-37` + `HU-39` |
| DES-32 / DES-33 (HU-24A/24B) | Keep split — different blockers | — |
| DES-34 / DES-35 (HU-25A/25B) | Keep split — different actors | — |
| DES-56 / DES-57 (HU-40A/40B) | Keep split for now — low value, far out | — |

---

## Close as absorbed, do not merge

### DES-16 (HU-10B) — already implemented by DES-15

Its three acceptance criteria map one-to-one onto three passing tests. DES-15's body
promised *"el dominio (no los handlers) hace cumplir qué hijos admite cada nodo
(patrón Composite)"* — and it does.

| DES-16 AC | Enforced by | Test |
|---|---|---|
| impide pistas directas dentro de etapas | `Stage.CanContain` | `Stage_RejectsClueChild` |
| impide subetapas dentro de subetapas | `Substage.CanContain` | `Substage_RejectsSubstageChild` |
| impide pistas con nodos hijos | `Clue.AddChildNode` throws | `Clue_IsLeafAndRejectsAnyChild` |
| el rechazo informa estructura inválida | `InvalidMissionNodeChildException` | all three |

Source: `mission-design-service/src/Domain/Entities/MissionNode.cs:85` (`CanContain`
abstract) + per-type overrides; tests in
`mission-design-service/tests/UnitTests/Domain/Entities/MissionNodeTests.cs`.

DES-16 is `Backlog`, so an agent *will* pick it up and produce an empty PR. Close it
as absorbed into DES-15, referencing the tests above.

### DES-55 (HU-39B) — "ranking for trivia sessions"

Trivia sessions do not exist post-canon; trivia is a `SubstagePlayMode` of a
`LiveSession`. There is nothing to merge because the premise is void. Already marked
*subsume into DES-54* in `canon-realignment-after-mission-runtime-rewrite.md:83`, and
already flagged as a Phase-11 blocking decision in `workflow_refactor.md`. This
handoff just confirms it: **close, don't merge.**

---

## Genuine merges — the A/B seam is false

### DES-46 + DES-47 → `HU-34` *(highest value — this is the critical-path head)*

"Register the **first valid** answer per team" (46) and "reject **late or repeated**
answers" (47) are one invariant seen from two sides. You cannot enforce *first*
without rejecting *repeat*. Same command, same aggregate, same endpoint, same test
class.

Shipping 46 alone leaves exactly two outcomes, both bad: either 47's logic is already
written and 47 is an empty ticket, or 46 shipped without the rule that makes it
correct. Merging also shortens the critical path, since DES-46 is its head.

Keep the merged ticket's `TriviaAnswerSubmission` type and its own domain event —
that specialization is real, per ADR-0010. It is the *A/B* split that isn't.

### DES-40 + DES-41 → `HU-30`

Same shape, one layer up. HU-30B is HU-30A's failure branch: you cannot build context
validation without deciding what a rejection says. DES-41's body defines the
reason-code contract that DES-40's rules produce.

Note DES-41 is cross-cutting by design — it generalizes rejection across the QR form
(HU-31) and the trivia form (HU-34A/34B). Merging it into DES-40 keeps that generic
contract in one place; the trivia-specific late/repeat specialization still lives in
the merged `HU-34`. Layering survives the merge.

### DES-49 + DES-50 → `HU-36`

One operator projection with a visibility predicate keyed on question state: hide the
chosen option before close, reveal answer + correctness + points after. Split, you
build the same read model and SignalR view twice, and the only interesting rule — the
state gate itself — falls *between* the two tickets and is owned by neither.

---

## The real duplicate is not a pair

**DES-52 (HU-37B) and DES-54 (HU-39A) are the same work.**

- DES-52 AC: *"El ranking se actualiza después de cambios que afecten el puntaje. La proyección de ranking toma como fuente el ledger de puntaje. El ranking actualizado queda disponible para consumo en tiempo real."*
- DES-54 AC: *"En sesiones de misión, el ranking se actualiza cuando cambian puntajes, penalizaciones o progreso relevante."*

Merge **52 into 54**, across the A/B boundary rather than along it. With DES-55 also
folding into DES-54, `HU-39` becomes the single real-time session-ranking projection,
sourced from the ledger.

**Keep DES-51 (HU-37A) separate** — it is the write model (the `ScoreEntry` ledger),
genuinely distinct from the read projection. With 37B gone it becomes just `HU-37`.

---

## Keep split — and why

- **DES-32 / DES-33 (HU-24A/24B).** DES-33 says *"Ampliar el panel"*, which reads mergeable, but it is blocked by the evidence and ranking lines and DES-32 is blocked by neither. Different blockers = a real vertical slice.
- **DES-34 / DES-35 (HU-25A/25B).** Different actors (admin+operator vs participant), different endpoints, different authorization rules. Not one handler.
- **DES-56 / DES-57 (HU-40A/40B).** Mergeable in principle, deep in `Backlog`, and DES-57 adds finished/cancelled-session visibility, which is its own product decision. Revisit when the row comes up.

---

## Knock-on that will bite if ignored

**DES-34 and DES-35 both list `Blocked by HU-20, HU-39A, HU-39B`.** If DES-55 is
closed without editing those, the boards line stays blocked forever by a ticket that
no longer exists. Same for DES-35's `Blocked by HU-23`. **Fix the blockers in the same
pass that closes DES-55**, not later.

---

## State of the world (verified 2026-07-09, do not re-derive)

- **GH #81, #82, #86, #87, #88, #89, #90, #91 are all closed.** The Users ↔ SessionOperations track is finished; only **GH #85** (rename `identity-access-service` → `users-service`) is open, and it is dead last.
- ⚠️ **GH #85 ≠ DES-85.** Unrelated tickets colliding on a number. DES-85 is a `ready-for-agent` PRD for a *separate* `scoring-monitoring-service` covering HU-37…40 — **it may relocate the whole DES-51/52/54 cluster out of session-ops. Read it before acting on the HU-37/HU-39 merges above.**
- Critical path: **DES-46 / HU-34A**, i.e. the merged `HU-34`.
- New `mobile`-labelled track not in the run order: DES-81 (EN-M1 spike), DES-82/83/84 (HU-M1/M2/M3). Consumes the HU-34–36 backend contract.
- Linear hygiene: DES-23/28/30/44 are `Canceled` with `ready-for-agent` already stripped (unarchived, cosmetic). DES-71–74 archived. **DES-25 is `Done` but still carries `ready-for-agent` — strip it.**

### Tooling gotchas

- `gh issue view` / `gh pr edit` **fail in this repo** with a Projects-classic GraphQL deprecation error. Use `gh api repos/grupo-12-desarrollo-umbral/umbral/issues/<n>` (REST) instead.
- `mcp__linear-server__list_issues` with `limit: 250` overflows the tool-result cap. It spills to a file; parse that with `python3 -c 'import json; …'` rather than re-calling.

---

## Suggested next steps

1. **Read DES-85 first.** It may moot the HU-37/HU-39 merges by moving them to a new service.
2. Close **DES-16** (absorbed) and **DES-55** (premise void), fixing DES-34/35 blockers in the same pass.
3. Merge **DES-47 → DES-46** as `HU-34` before starting the critical path.
4. Merge **DES-41 → DES-40** (`HU-30`) and **DES-50 → DES-49** (`HU-36`) when those rows come up.
5. Merge **DES-52 → DES-54** (`HU-39`); leave DES-51 as `HU-37`.
6. Record the merges in `canon-realignment-after-mission-runtime-rewrite.md` (the supersession map) and update the order line in `workflow_refactor.md`.

Merging in Linear is human-only for the archive step; the MCP server can edit titles,
descriptions, and blockers but cannot archive.

## Suggested skills

- **`grill-with-docs`** — before executing any merge, stress-test it against `CONTEXT.md` and the ADRs. The HU-30/HU-34 merges both touch the `EvidenceSubmission` threshold defined in ADR-0010, which is still *in revisión*; that ADR is the thing that decides whether the merged rejection contract is one rule or two.
- **`to-issues`** — if the merged tickets need re-slicing into vertical tracer-bullet issues rather than a straight title-and-AC edit.
- **`cqrs-mediatr-aspnetcore`** — for DES-46+47: the merged ticket is one command handler with a first-write-wins guard, and the reject path is a validator concern, not a second handler.
