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
| DES-23 | HU-16 | Canceled 2026-06-30 | 🔨 superseded by **DES-75** |
| DES-28 | HU-21A | Canceled 2026-06-30 | 🔨 rebuilt as **DES-76** |
| DES-30 | HU-22 | Canceled 2026-06-30 | 🔨 rebuilt as **DES-77** |
| DES-44 | HU-33A | Canceled 2026-06-30 | 🔨 rebuilt as **DES-78** |
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
| DES-54 | HU-39 | Backlog | ✅ reframed to unified session ranking (2026-07-09); absorbed DES-52 + DES-55 |
| DES-55 | HU-39B | **Canceled** | ✅ subsumed into DES-54 (2026-07-09); ACs preserved. Not archived — manual UI step |
| DES-52 | HU-37B | **Canceled** | ✅ subsumed into DES-54 (2026-07-09); ledger-source AC preserved. Not archived |
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
| 11 | Scoring & ranking | **DES-51** (HU-37 ledger), HU-38, **DES-54** (HU-39 unified ranking), HU-40A/B | `ScoreEntry` ledger + single session ranking with `ResolutionTime` tie-break | Phases 8 & 10 |
| 12 | Boards & queries | **DES-31** (HU-23), HU-24A/B, HU-25A/B | Live team/operator boards over the target-based + ranking model | Phase 11 |

### Open decisions blocking clean implementation
- **DES-37 (HU-27):** rule-based automatic clue release is not in canon — eliminate, merge into HU-26, or redefine.
- **DES-38 (HU-28):** operator-authored runtime clues conflict with the immutable snapshot — model as an explicit exception or reinterpret as *release* of pre-snapshotted clues.
- ~~**DES-54/DES-55 (HU-39A/B):** confirm the merge into a single session ranking before rewriting AC.~~
  ✅ **Done 2026-07-09.** Merged into `DES-54` (`HU-39`); AC rewritten; DES-55 Canceled.

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

These were labeled while still in `Done`; all four were moved to `Canceled` on 2026-06-30.
**Archiving is not exposed via the Linear MCP tools**, so full removal from active views
needs the Linear UI. The `ready-for-agent` label still on
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

## Addendum — A/B merge pass (2026-07-09)

Source: `ab-ticket-merge-findings-handoff-2026-07-09.md` and its validation companion
`ab-ticket-merge-validation-handoff-2026-07-09.md`.

### Supersession map

The `🔨` rows above are supersessions by **rebuild** (canon rewrote the ticket). This pass
added one supersession by **merge** (two tickets, one invariant):

| Superseded | HU | Survivor | Kind | Edge work |
|---|---|---|---|---|
| DES-23 | HU-16 | DES-75 | rebuild | none — blocks no live ticket |
| DES-47 | HU-34B | **DES-46** (`HU-34`) | merge | `blocks` → `relatedTo`; ACs absorbed |

DES-47's four rejection ACs live in DES-46 under *"Rechazo de respuestas tardías o
repetidas"*. Acceptance and rejection are the two branches of one first-write-wins guard,
not two deliverables. DES-47 carries a `⛔ SUPERSEDED` banner and **awaits manual archive**
(MCP cannot archive). It has no `ready-for-agent` label, so the generator agent will not
pick it.

### Stale-blocker repair

Twelve live tickets were blocked by the canceled DES-28/30/44 and were re-pointed at their
rebuilds:

| Canceled | Rebuild | Re-pointed dependents |
|---|---|---|
| DES-28 | DES-76 | DES-29, DES-32, DES-36, DES-37, DES-38, DES-39, DES-42, DES-53 |
| DES-30 | DES-77 | DES-31, DES-36, DES-38, DES-39, DES-42, DES-59 |
| DES-44 | DES-78 | DES-46, DES-49 *(DES-45 was already correct)* |

Startable with zero live blockers after the repair: **DES-46** (critical-path head),
DES-32, DES-53, DES-29. The canceled tickets retain `blocks` edges only among themselves —
canceled on both ends, so no live ticket reads them.

### Resolved 2026-07-09

- ~~The `DES-51 → DES-42 → DES-31 → DES-51` cycle is **live and unbroken**~~ — **broken.**
  `DES-31 → DES-51` was dropped with sign-off. It stranded **20 of the 27 open backend HUs**,
  not the six recorded here earlier; that undercount is corrected. `DES-31` is now startable.
- ~~The DES-52/54/55 three-way fold is gated on amending DES-85~~ — **DES-85 amended and the
  fold applied.** `DES-52` and `DES-55` are Canceled (folded into `DES-54`, ACs preserved);
  `DES-51` is `HU-37`, `DES-54` is `HU-39`. The six tickets DES-55 blocked
  (`DES-33/34/35/48/57/61`) were re-pointed onto `DES-54`. Both folded tickets are
  **Canceled, not archived** — archiving is a manual Linear-UI step.

### Still open

- DES-41's blocker points at DES-42, not DES-40. Re-pointing asserts that generic evidence
  rejection does not need the QR form to exist first — a design call, gated on ADR-0010.
- **Five `Done` tickets still carry `ready-for-agent`:** DES-26, DES-45, DES-76, DES-77,
  DES-78. Same stale-label bug fixed on DES-25; it was never a one-off.
- **DES-51 (`Todo`) `blocks` DES-45 (`Done`)** — a completed ticket behind an unstarted
  blocker. DES-45's body calls DES-51 "consumidor downstream," so the edge likely points
  backwards. Unresolved; not touched.

### DES-85 does not moot the HU-37/HU-39 fold — it carries the same pre-canon debt

The PRD `prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md`
is **internally inconsistent** on this point. Its domain decisions support the fold; its
slice list contradicts it.

Supporting the fold:
- `:179` — *"`Ranking` belongs to exactly one `LiveSession`"*. One ranking per session. There
  is no second ranking for a "trivia session" to own.
- `:211-215` — the functional backbone's step 3 is *"ranking recalculation **and** ranking
  snapshots"*, i.e. HU-37B and HU-39A/B in one step.

Contradicting it:
- `:198-204` — *"The first delivery should align with the existing HU split"*, then enumerates
  `HU-37A`/`HU-37B`/`HU-39A`/`HU-39B`/`HU-40A`/`HU-40B`. This **restates** the backlog split as
  a premise rather than deriving it.
- User story 21 (`:130-131`) — *"ranking snapshots for **trivia sessions**"*. Trivia is a
  `SubstagePlayMode`, not a `SessionSource` (see the canon delta above). This is the exact
  premise commit `c35f6c2` deleted.

**Verdict:** DES-85 is not an authority that overrides the merge; it inherited the stale split
from the same backlog the merge is fixing.

> ✅ **Applied 2026-07-09.** The local PRD was amended with **four** edits, not three: drop
> US-21 (and renumber 22–34), **rewrite US-20** — its *"for mission sessions / mission-mode
> supervision"* qualifier was the dangling half of the US-21 distinction — collapse the
> `HU-39A`/`HU-39B` bullet, and fold `HU-37B` into the ranking step. The fourth edit fixes the
> *Further Notes* implementation-order bullet, which restated the old split verbatim; without
> it the PRD contradicted itself in a second place. The `52 + 55 → 54` fold then proceeded,
> stripping DES-51's duplicated ranking AC. The Linear PRD ticket **DES-85 itself is not yet
> amended** — only the local copy under `backend/docs/prd/`.

**Relocation worry resolved:** DES-51, DES-54 and DES-55 already carry
`svc:scoring-monitoring-service`. The cluster is not about to move out of session-ops; it
already left.
