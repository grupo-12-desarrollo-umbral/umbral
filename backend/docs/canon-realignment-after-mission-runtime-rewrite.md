# Canon Realignment — Backlog Alignment After the Mission-Runtime Rewrite

Date: 2026-06-16

This document records how the Linear backlog (team `umbral-equipo-12`, project
*Umbral Proyecto Desarrollo - Equipo 12*) was realigned against two canon-changing
documentation commits, and the order in which the affected work should now be
implemented.

## Source commits

- `c35f6c2` — docs(backend): rewrite mission runtime model (#29)
- `df06e54` — docs(session-operations): realign evidence umbrella canon (#28)

Canonical language source (do not contradict): `backend/docs/grilling-session-mission-restructure.md`,
`backend/docs/bd_umbral_entity_spec.md`, `backend/docs/ddd_solution_model.md`,
`backend/docs/adr/0010-evidence-qr-only-first-delivery.md`, and the per-service
`CONTEXT.md` glossaries.

## Canon delta (what changed)

**Session source & snapshot**
- `Mission` is the **only** source for a `LiveSession`. `TriviaQuiz` is **not** a `SessionSource`.
- There is **no session-level `SessionMode`**. Trivia and treasure-hunt are `SubstagePlayMode`s inside one mission; a team can play both in one session.
- A `LiveSession` snapshots the full mission runtime plan at creation (`MissionRuntimeSnapshot`); the snapshot is **immutable**.

**Mission hierarchy**
- `Mission` (wrapper) → ordered `Stage`s → ordered `Substage`s. Each `Substage` has exactly one `SubstagePlayMode`: `TreasureHunt` or `Trivia`.
- Treasure-hunt progression is **target-based, not clue-based**. `Target` is the QR-validated objective. `Clue` is **optional** guidance attached to a `Target` (max one per target); releasing a clue does **not** advance the substage.
- Trivia substage references one whole published `TriviaQuiz` through a `TriviaQuizSelection`.

**Lifecycle**
- Canonical `SessionState`s: `Scheduled → Preparing → Active → Paused → Finished → Cancelled`. `LiveSession` is created in `Scheduled`; team association is allowed only while `Scheduled`; operator-driven readiness moves it to `Preparing` before it can go `Active`.

**Evidence (#28)**
- `EvidenceSubmission` is the umbrella with exactly two concrete forms: `TreasureEvidenceSubmission` (QR, HU-29–32) and `TriviaAnswerSubmission` (HU-34). No text/photo modes.

**Scoring & ranking**
- One ranking per session, derived from traceable `ScoreEntry` records (not direct total mutation), sorted high→low, tie-broken by `ResolutionTime` (active play time, pauses excluded); equal/incomparable times share rank.

## How to find these in Linear

Every issue **commented or rebuilt** by this realignment carries the **`canon-realign`**
label (team `umbral-equipo-12`): 18 issues from the initial pass (4 rebuild tickets
DES-75–78 + 14 commented), plus 3 drift fixes from a 2026-06-17 second review (DES-67,
DES-40, DES-42) and 4 superseded Done tickets labeled in the same pass (DES-23/28/30/44 —
see the addendum at the end). The 4 rebuild tickets also carry `needs-rebuild`.
Each commented issue has a comment starting with `⚠️ Deuda de canon` or `⚠️ Nota de canon`.

Labeling is **selective, not uniform**. Untagged rows: the ✅ #28-umbrella issues
(DES-39/41/43/46/47/56, realigned directly by commit `df06e54`); DES-26 (✅ historical note,
behavior unaffected); the foundation / `needs-rebuild` rows DES-22 and DES-24; and the
archived DES-16 (HU-10B, intentionally unlabeled).

## Ticket disposition

Legend: ✅ aligned · 📝 canon-drift comment added · 🔨 rebuild ticket filed · 🗄️ archived

> Rows are **not** uniformly tagged `canon-realign`: see the selective-labeling note above
> and the 2026-06-17 addendum below. The ✅-#28, DES-26, DES-22/24 and archived rows
> carry no label; the 📝 rows, the 4 rebuild tickets (DES-75–78), and the 🔨 superseded
> Done tickets (DES-23/28/30/44, labeled 2026-06-17) do.

| Ticket | HU | Status | Disposition |
|---|---|---|---|
| DES-23 | HU-16 | Done (cycle 1) | 🔨 superseded by **DES-75** |
| DES-28 | HU-21A | Done (cycle 1) | 🔨 rebuilt as **DES-76** |
| DES-30 | HU-22 | Done (cycle 1) | 🔨 rebuilt as **DES-77** |
| DES-44 | HU-33A | Done (cycle 1) | 🔨 rebuilt as **DES-78** |
| DES-25 | HU-18 | Done (cycle 1) | 📝 wording (team association is allowed in `Scheduled`) |
| DES-12 | HU-07B | Done (cycle 1) | 📝 wording (trivia-as-session → substage) |
| DES-18 | HU-12 | Done (cycle 1) | 📝 wording (quiz selectable into substage) |
| DES-19 | HU-13 | Done (cycle 1) | 📝 wording ("used quiz" redefinition) |
| DES-26 | HU-19 | Done (cycle 1) | ✅ historical note only, behavior unaffected |
| DES-14 | HU-09 | Done | 📝 + `needs-rebuild`: node model needs `Target`/`SubstagePlayMode` |
| DES-15 | HU-10A | Todo | 📝 hierarchy needs `SubstagePlayMode` + `Target`; `Clue` optional |
| DES-16 | HU-10B | 🗄️ archived | none (folded into HU-10A) |
| DES-31 | HU-23 | Todo | 📝 board = target-based progress; clues optional |
| DES-36 | HU-26 | Todo | 📝 minor: clue scoped to `Target`; release doesn't advance |
| DES-37 | HU-27 | Todo | 📝 auto clue-release by advancement rules conflicts with canon |
| DES-38 | HU-28 | Todo | 📝 runtime clues vs immutable snapshot tension |
| DES-54 | HU-39A | Backlog | 📝 reframe to unified session ranking |
| DES-55 | HU-39B | Backlog | 📝 subsume into DES-54 (no trivia-session ranking) |
| DES-70 | PRD HU-15–36 | Backlog | 📝 authority pointer to rewritten PRD/arch docs |
| DES-62 | PRD HU-09–14 | Backlog | 📝 authority pointer to rewritten entity/DDD docs |
| DES-22 | HU-15 | Todo | foundation (mission→session) — see order below |
| DES-24 | HU-17 | Todo | `needs-rebuild`: single-source = mission-wrapper |
| DES-39/40/41/42/43/46/47/56 | HU-29–32/34/40A | Backlog/Todo | ✅ already realigned to `EvidenceSubmission` umbrella (#28). DES-40 & DES-42 also received 📝 binding-target drift comments + `canon-realign` on 2026-06-17 (see addendum) |

## What to implement next, in order

Dependencies come from each ticket's *Blocked by* plus the realignment links
(DES-75–78 are blocked by DES-22 and DES-24).

| # | Phase | Ticket(s) | What | Depends on |
|---|---|---|---|---|
| 1 | Mission authoring | **DES-14** (HU-09 rebuild), **DES-15** (HU-10A) | Mission wrapper + `Stage`/`Substage`/`SubstagePlayMode`/`Target`/optional `Clue`; structural validations & readiness rules | — |
| 2 | Session creation | **DES-22** (HU-15) | Create `LiveSession` from an active mission; immutable `MissionRuntimeSnapshot` | Phase 1 |
| 3 | Session creation | **DES-24** (HU-17) | Single-source rule = mission-wrapper (drop "trivia session from quiz") | DES-22 |
| 4 | Session creation | **DES-75** (HU-16 realign) | Trivia selection as a `Substage` (`TriviaQuizSelection`), not a session | DES-22, DES-24 |
| 5 | Lifecycle | **DES-76** (HU-21A realign) | State machine `Scheduled/Preparing/Active/Paused/Finished/Cancelled` | DES-22, DES-24 |
| 6 | Lifecycle | **DES-77** (HU-22 realign) | Authoritative timer keyed off active `SubstagePlayMode` | DES-22, DES-24 |
| 7 | Setup | **DES-25** (HU-18 reword) | Attach teams during `Scheduled` (before `Preparing`) | DES-76 |
| 8 | Treasure-hunt play | HU-29/30A/30B/31/32 (DES-39/40/41/42/43) | Evidence intake + QR `Target` resolution + traceability (already canon-aligned) | DES-75/76 |
| 9 | Clue model (decision first) | **DES-36** (HU-26), **DES-37** (HU-27), **DES-38** (HU-28) | Operator clue release per team; decide fate of rule-based auto-release & runtime-authored clues | Phase 8 |
| 10 | Trivia play | **DES-78** (HU-33A realign), HU-33B/34A/34B/35/36A/36B | Synchronized trivia substage orchestration + answer registration/rejection | DES-75/76 |
| 11 | Scoring & ranking | HU-37A/B, HU-38, **DES-54** (HU-39A unified), **DES-55** (HU-39B subsume), HU-40A/B | `ScoreEntry` ledger + single session ranking with `ResolutionTime` tie-break | Phases 8 & 10 |
| 12 | Boards & queries | **DES-31** (HU-23), HU-24A/B, HU-25A/B | Live team/operator boards over the target-based + ranking model | Phase 11 |

### Open decisions blocking clean implementation
- **DES-37 (HU-27):** rule-based automatic clue release is not in canon — eliminate, merge into HU-26, or redefine.
- **DES-38 (HU-28):** operator-authored runtime clues conflict with the immutable snapshot — model as an explicit exception or reinterpret as *release* of pre-snapshotted clues.
- **DES-54/DES-55 (HU-39A/B):** confirm the merge into a single session ranking before rewriting AC.

## Addendum — second-review drift fixes (2026-06-17)

A second review against the canon docs surfaced three residual mismatches not
covered by the #28 / #29 passes above. Each was labeled `canon-realign` and given
a `⚠️ Deuda de canon` comment; the two living HU issues also had their acceptance
criteria reworded. DES-62's `2–4` options was confirmed a **false positive**
(backed by accepted ADR-0002) — no ticket change, only a doc cross-ref added to
`bd_umbral_entity_spec.md` (TriviaQuestion constraints now cite ADR-0002 for the
options / score / timer ranges).

| Issue | HU | Fix | Canon source |
|---|---|---|---|
| **DES-67** | PRD HU-01–08 | 📝 only (PRD is a historical artifact; ADR-0001 overrides). HU-04/05 assignment to Identity stays correct; the "nunca doble ownership" wording is superseded — Identity owns the reference-data `Team` + `TeamMembership`, SessionOperations owns the runtime `Team` (two aggregates with different shapes, not double ownership). | ADR-0001, `ddd_solution_model.md:151` |
| **DES-40** | HU-30A | 📝 + AC reword. Binding AC changed from "etapa, subetapa, pista" to exactly one `MissionNode` of the active substage, form-agnostic (trivia → active question; treasure-hunt → target scope of the substage). | `requisitos` BR-05 / FR-08, `bd_umbral_entity_spec.md` |
| **DES-42** | HU-31 | 📝 + AC reword. (a) binding → active treasure-hunt `Substage` / `MissionNode`; (b) **Target↔Clue inversion fixed** — validate the scanned QR against the `Target` (which owns an optional `Clue`), never "against the active clue"; releasing a clue does not advance the substage. | `canon-realignment…:29`, `grilling-session-mission-restructure.md:47`, `hu09-context.md:82` |

This brings the `canon-realign`-labeled set to 25 issues (18 initial + 3 drift fixes above
+ 4 superseded Done tickets below). No code changed in this pass; only Linear issues and
these docs.

### Backlog hygiene — superseded Done tickets (same pass)

The 4 🔨 superseded Done tickets were labeled `canon-realign` + given a `⚠️ Nota de canon`
supersession comment pointing at their rebuild ticket, so the Linear filter is honest and no
agent picks up pre-canon ACs:

| Issue | HU | Rebuilt as | Pre-canon drift |
|---|---|---|---|
| DES-23 | HU-16 | DES-75 | "sesión de trivia" (quiz as `SessionSource`) → trivia `Substage` |
| DES-28 | HU-21A | DES-76 | state machine realigned to canonical `Scheduled → … → Cancelled` |
| DES-30 | HU-22 | DES-77 | timer keyed off active `SubstagePlayMode`, not session-level mode |
| DES-44 | HU-33A | DES-78 | trivia orchestration as `Substage`, not a standalone session |

These remain in `Done` state — **archiving is not exposed via the Linear MCP tools**, so
full removal from active views needs the Linear UI. The `ready-for-agent` label still on
DES-23/30/44 is now stale (superseded issues should not be agent-picked) and should be
stripped in that same UI pass — but stripping it is **hygiene, not the safety mechanism**.
The generator-agent stops on any ticket in the superseded column above **regardless of
`ready-for-agent`** (`generator-agent.md` resolution step 1), and excludes superseded Done
tickets from the predecessor set (step 3), so a missed strip cannot cause a stale ticket to
be agent-picked or cited as a predecessor.

### DES-71–74 — verified subsumed, already archived

The four were suspected orphaned AC-fragment sub-issues of HU-07A. Verified: each has
`parentId: DES-11`, an empty description, and a title that is **verbatim AC text** from
DES-11 (HU-07A, Done 2026-06-03, all 6 ACs checked `[X]`) — pure fragments, no independent
scope. They were **already archived** on 2026-06-15 (`archivedAt` set) shortly after
creation. No action needed.
