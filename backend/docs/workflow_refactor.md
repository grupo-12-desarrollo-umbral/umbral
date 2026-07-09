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

Run driver-agent for backend/docs/hu<NN>-brief.md.
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
| Drive | `Read @backend/.agents/driver-agent.md. Run driver-agent for backend/docs/hu<NN>-brief.md.` |
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

> **Status as of 2026-07-09 (order table rebuilt from live Linear relations).**
> Rows 1–7 are **all done**. The 4→5→6 realign chain is complete — DES-75 / HU-16
> (PR #73), DES-76 / HU-21A (PR #75), DES-77 / HU-22 (PR #79) all merged.
> **DES-78 / HU-33A DONE** (2026-07-06); **DES-45 / HU-33B DONE** (PR #119, `b849bf8`).
>
> **The Users ↔ SessionOperations realignment track is finished except the rename.**
> GitHub **#81, #82, #86, #87, #88, #89, #90, #91 are all closed**. Only **GH #85**
> (`identity-access-service` → `users-service`) is still open, and it was always
> dead last. Note it is **not** `DES-85`, an unrelated Linear PRD.
>
> ### ✅ The cycle is broken (applied 2026-07-09)
>
> The graph used to contain:
>
> ```
> DES-31 (HU-23) blockedBy DES-51 (HU-37A)   <- edge dropped
> DES-51 (HU-37A) blockedBy DES-42 (HU-31)
> DES-42 (HU-31)  blockedBy DES-31 (HU-23)
> ```
>
> That loop stranded 20 of the 27 open backend HUs. **`DES-31 → DES-51` was dropped**
> on the reasoning that a live team board can show a score of zero before the ledger
> exists. `DES-31` now has only `DES-77` (Done) and is startable, which released the
> evidence line (`DES-39`, `DES-42`) and the clue line (`DES-36/37/38`).
> **The order below is a verified topological order — no cycle remains.**
>
> ### ✅ The HU-37/39 fold is applied (2026-07-09)
>
> `HU-37A` → **`HU-37`** (DES-51); `HU-39A` → **`HU-39`** (DES-54). `DES-52` (HU-37B)
> and `DES-55` (HU-39B) are **Canceled**, folded into `DES-54`, which absorbed DES-52's
> ledger-source AC and DES-55's async-RabbitMQ-projection AC. The six tickets DES-55
> used to block — `DES-33/34/35/48/57/61` — were re-pointed onto `DES-54`.
> ⚠️ Both remain **Canceled, not archived**; archiving is a manual Linear-UI step.
>
> **Startable right now, with zero live blockers — eight tickets:** `DES-46`
> (critical-path head), `DES-31`, `DES-32`, `DES-53`, `DES-29`, `DES-13`, `DES-80`,
> and `DES-81` (mobile spike). `DES-49` opens as soon as `DES-46` lands.
>
> ℹ️ **`ready-for-agent` on `Done` tickets is cosmetic — do not strip it.** A full sweep
> (2026-07-09) found **19 of 26** `Done` tickets carry it, not the five an earlier note
> claimed. It is **not** an agent-pickup trap: the generator is invoked with an explicit
> `DES` id, never selects by this label, and classifies supersession at its front door —
> *"The generator must not depend on that strip having happened"* (`generator-agent.md:40-44`).
> `ticket-playbook.md:76` agrees: *"hygiene, not the safety net."*
> ⚠️ **Never strip it from a PRD ticket** (`DES-62/67/70/85`): PRD resolution finds them by
> `svc:<service>` **+ `ready-for-agent`** (`generator-agent.md:68-71`).

| # | Ticket(s) | HU | What | Note |
|---|---|---|---|---|
| 1 | DES-14 / DES-15 | HU-09 / 10A | Mission wrapper + composite + Target + optional Clue | ✅ **DONE** (PRs #31/#33 HU-09, #48/#49 HU-10A incl. X.4 coverage). |
| 2 | DES-22 | HU-15 | Create `LiveSession` from active mission; immutable snapshot | ✅ **DONE** (PR #50, 2026-06-22). |
| 2b | DES-79 | HU-15 f/u | Archive-time enforcement: block/cascade when archiving a quiz referenced by an active mission | ✅ **DONE** (PRs #51 + #53, 2026-06-22). |
| 2c | DES-80 | HU-14A f/u | `RemoveTriviaQuestion` command + question-removal domain slot + `TriviaQuestionRemoved` event | ⬜ **OPEN** (Backlog, Low) — deferred follow-up to HU-14A, **ungated** (HU-14A/DES-20 is Done); not a rebuild, not on the critical path — schedule any time (see below). |
| 3 | DES-24 | HU-17 | Single mission source; drop "session from quiz" | ✅ **DONE** (PR #72) — rebuild; all 4 layers (X.1–X.4), 370/370 session-operations tests green. |
| 4 | DES-75 | HU-16 | Trivia selection as a Substage, not a session | ✅ **DONE** (PR #73) — rebuild (supersedes DES-23, shipped pre-canon as PR #19). |
| 5 | DES-76 | HU-21A | State machine `Scheduled→Preparing→Active→Paused→Finished→Cancelled` | ✅ **DONE** (PR #75) — rebuild (supersedes DES-28, shipped pre-canon as PR #21). |
| 6 | DES-77 | HU-22 | Timer keyed off active `SubstagePlayMode` | ✅ **DONE** (PR #79) — rebuild (supersedes DES-30, shipped pre-canon as PR #22). |
| 7 | DES-25 | HU-18 | Attach teams during `Scheduled` | ✅ **DONE** (PR #23) — Linear now `Done`; the AC reword landed, nothing remains. |
| 8 | DES-78 + DES-45 | HU-33A/33B | Synchronized trivia substage + auto-close + final results | ✅ **DONE** — DES-78 rebuild (supersedes DES-44), pointer/advancement contract in `backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`; DES-45 by PR #119 (`b849bf8`). |
| 9 | **DES-46** | HU-34 | Register first valid team answer + reject late/repeated (one first-write-wins guard) | ⬜ **← CRITICAL PATH, START HERE.** Zero live blockers. Merged ticket: absorbed DES-47 (HU-34B) on 2026-07-09; **DES-47 is Canceled (not archived) — do not cite it.** ⚠️ Its body defers common intake/validation to HU-29/HU-30A (row 12), which carry **no blocker edge** to it — decide before generating whether to inline the intake or wait. |
| 10 | DES-49 | HU-36A | Operator sees only answered/not-answered during the open question | ⬜ Blocked only by DES-46. Owns the respondido/no-respondido **visibility-state gate** (assigned here rather than merging DES-49/50). |
| 11 | **DES-31** | HU-23 | Live team board (score, timer, available clues) | ⬜ **STARTABLE** — cycle broken 2026-07-09, only blocker `DES-77` is Done. Gates rows 12–13 and much of 17–18; running it early releases the most work. |
| 12 | DES-42 → DES-41 ; DES-39 → DES-40 ; then DES-43 | HU-31 / 30B / 29 / 30A / 32 | QR `Target` resolution → explained rejection ; evidence umbrella intake → context validation ; then traceability | ⬜ Opens with DES-31. **Two independent chains, not one.** DES-42→DES-41 and DES-39→DES-40 are parallel; DES-43 needs **all three** of DES-42/41/40. The old `39/40/41/42/43` reading was backwards — DES-41 is blocked by DES-42. Re-pointing DES-41 → DES-40 is a **design call** gated on ADR-0010 (*in revisión*). |
| 13 | DES-36 → DES-37 / DES-38 | HU-26–28 | Operator clue release | ⬜ Opens with DES-31. **Resolve the open decisions first** (see below). DES-37 is blocked by DES-36; DES-38 is blocked by DES-31 only, so it can run beside DES-36. |
| 14 | DES-53 → DES-51 | HU-38 / 37 | Justified penalties → `ScoreEntry` ledger | DES-53 is **startable now** (zero live blockers) and gates DES-51. DES-51 also needs DES-42 (row 12) + DES-46 (row 9). `HU-37A` → **`HU-37`**; DES-52 (HU-37B) folded into DES-54 and Canceled. |
| 15 | DES-54 | HU-39 | Single session `Ranking` derived from the ledger, `ResolutionTime` tie-break, refresh + real-time view | Blocked by DES-51 (+ DES-45, Done). **Fold applied 2026-07-09**: absorbed DES-52 and DES-55. `HU-39A` → **`HU-39`**. Now the sole gate for DES-33/34/35/48/57/61. |
| 16 | DES-50 ; DES-48 | HU-36B / 35 | Post-close operator review of answers + points ; trivia result reveal | **These are not row-9 work.** DES-50 is blocked by DES-51 (row 14); DES-48 by **DES-54** (row 15) after the fold re-pointed it off DES-55. Every prior version of this table ran them too early. |
| 17 | DES-32 ; DES-34 ; DES-35 ; then DES-33 ; then DES-61 | HU-24A / 25A / 25B / 24B | Operator panels + admin/participant read queries + CQRS read split | **DES-32 is startable now** (zero live blockers) — it does not wait on DES-31. DES-34 needs DES-54; DES-35 needs DES-31 + DES-54; DES-33 needs DES-32 + DES-43 + DES-54; DES-61 needs DES-34 + DES-35 + DES-54. |
| 18 | DES-56 → DES-57 ; DES-60 | HU-40A/40B / enabler | Session event history; score/ranking historical review; RabbitMQ domain-event publication | DES-56 needs DES-42, DES-36, DES-53, **DES-29**; DES-57 needs DES-56 + DES-51 + DES-54; DES-60 needs DES-42, DES-40, DES-56, DES-51. |
| 19 | DES-13 → DES-59 ; DES-58 | HU-08 / enablers | Multi-device team sync + reconnect ; multi-device enabler ; React Native participant client | **DES-13 is ungated** — DES-11/DES-12 (HU-07A/07B) both Done. DES-59 needs DES-13 + DES-31. DES-58 needs DES-13 + DES-31 + DES-35 + DES-48, so it lands last. |
| M | DES-81 → DES-82 → DES-83 / DES-84 | HU-M1–M3 / EN-M1 | Mobile trivia: contract spike → active-question display → question-closed state / answer submission | ⬜ **DES-81 is ungated and startable now.** Separate mobile track. DES-84's body names DES-46 as a backend dependency, but the graph carries only a `relatedTo` edge — treat DES-46 as a real precondition regardless. |

**Ungated pickups, runnable any time:** `DES-80` (row 2c), `DES-29` (session
state-change audit — also unblocks DES-56), `DES-32`, `DES-53`, `DES-13`, `DES-81`.

> ✅ **Done 2026-07-09.** `DES-29` was renamed from the orphaned `HU-21B` to plain `HU-21`
> (there is no `HU-21A` any more — DES-28 was canceled and rebuilt as DES-76), and its
> query AC — *"El historial de cambios puede consultarse posteriormente"* — was stripped,
> since that read surface belongs to `DES-56` (HU-40A), which this HU blocks.

(Archived/superseded, ignore: DES-71/72/73/74 — old participant-lobby sub-issues,
closed out by #108/#110/#115. DES-47 — merged into DES-46 on 2026-07-09; **Canceled, not
archived** — archiving is a manual Linear-UI step still pending.)

## Unified order with the Users realignment (updated 2026-07-09)

The Users ↔ SessionOperations realignment (GitHub issues **#81, #82, #85, #86,
#87, #88, #89, #90, #91** — decisions in `users-realignment-decisions-2026-07-06.md`)
was a **separate track** from the canon rows above that interleaved with them in
`session-operations-service`. **That interleave is over.** Every issue on the track
is closed except **GH #85**, so the remaining sequence is just the canon session-ops
line with the rename pinned to the end:

**DES-46 → DES-49 → DES-31 → DES-42 → DES-39 → DES-40 → DES-41 → DES-43
→ DES-36 → DES-37 / DES-38 → DES-53 → DES-51 → DES-54 → DES-50 → DES-48
→ DES-32 → DES-34 → DES-35 → DES-33 → DES-29 → DES-56 → DES-57 → DES-60
→ DES-61 → DES-13 → DES-59 → DES-58 → GH #85**

Mobile track, independent: **DES-81 → DES-82 → DES-83 / DES-84** (DES-84 after DES-46).

(`DES-80`, `DES-29`, `DES-32`, `DES-53`, `DES-13`, `DES-81` are ungated and may be pulled
forward any time.)

This is a **verified topological order over live `blockedBy` relations** as of
2026-07-09, not the historical phase grouping. **Re-validated 2026-07-09** against a full
relation fetch of every `Todo` (23), `Backlog` (22) and `Canceled` (7) ticket: the graph is
acyclic, no live ticket blocks a `Done` ticket, and no live ticket has any edge into a
`Canceled` one. Every edge below was checked against the order. Structural corrections
against earlier versions of this line:

1. **The cycle is gone.** `DES-31 → DES-51` was dropped, so this order is now actually
   runnable end to end. No back-edge to `DES-31` remains.
2. **`DES-48` and `DES-50` are not part of the DES-46 cluster.** They were grouped with
   it as "HU-34–36" by HU number, but `DES-50` is blocked by `DES-51` and `DES-48` by
   `DES-54` — both several rows downstream. Running them early produces empty tickets.
3. **`DES-42` precedes `DES-39`.** Both open with `DES-31`, but `DES-51` needs `DES-42`,
   so scheduling the QR chain first shortens the critical path to the ledger.
4. **`DES-60` and `DES-61` were absent from every earlier version of this line.**
   `DES-60` needs DES-42/40/56/51; `DES-61` needs DES-34/35/54.

Also corrected here: `DES-47` is gone (merged into `DES-46`; **Canceled, not archived**),
`DES-46` is plain `HU-34` not `HU-34A`, `DES-51` is `HU-37` and `DES-54` is `HU-39`, and
`DES-52` / `DES-55` are **Canceled** — folded into `DES-54`, no longer build steps.

Every entry is a Linear DES id except the last, which is a GitHub issue. ⚠️ **`GH #85`
(the rename) is not `DES-85` (the scoring PRD).** They are unrelated tickets that
collide on the number — always write the `GH` / `DES` prefix here.

Done, and no longer ordering constraints on anything:

- ~~**#81, #82, #86, #90**~~ — identity-access-side. All closed.
- ~~**#87 → #88 → #91 → #89**~~ — Users session-ops reshaping. All closed, which
  discharges the two constraints that used to shape this list: they needed #86
  first, and they had to land **before** DES-31 (boards). Both satisfied.
- ~~**DES-45**~~ (HU-33B) — closed by PR #119.
- ~~**DES-47**~~ (HU-34B) — merged into DES-46 on 2026-07-09; `Canceled`, pending manual archive.

Still to run — grouped by what actually gates them:

- **Ungated today:** `DES-46` (critical path), `DES-49` (after 46), `DES-32`, `DES-53`, `DES-29`, `DES-13`, `DES-80`.
- **Gated on the cycle break alone:** `DES-31`, then `DES-39/40/42/41/43` and `DES-36/37/38`.
- **Gated on the cycle *and* a design call:** `DES-41`'s blocker re-point (ADR-0010, *in revisión*); `DES-36/37/38`'s clue-model decisions.
- **Gated on the cycle *and* DES-85:** `DES-51/54`, then `DES-48/50/57`. The 52 + 55 fold into 54 is applied, and the PRD was amended to match (repo copy *and* Linear ticket) on 2026-07-09.
- **GH #85** — repo-wide rename `identity-access-service` → `users-service`; dead last, quiet window. Has no Linear ticket.

Not in this order — separate tracks that appeared since 2026-07-06:

- **DES-81/82/83/84** (`mobile` label) — EN-M1 trivia display contract spike + HU-M1/M2/M3 team-space question display, submit, and closed-state. A mobile line that consumes the HU-34–36 backend contract; sequence it against row 10, not against this list.
- **DES-85** — the `scoring-monitoring-service` PRD (HU-37–40). A reference doc, not buildable
  work; it governs rows 14, 15 and 18.

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

## Open decisions blocking row 13 — clue model (resolve before starting it)

- **DES-37 (HU-27):** rule-based auto clue-release is not in canon — eliminate /
  merge into HU-26 / redefine.
- **DES-38 (HU-28):** operator-authored runtime clues conflict with the immutable
  snapshot — model as explicit exception or reinterpret as *release* of
  pre-snapshotted clues.

Record the decision in an ADR or the ledger addendum before rewriting the AC.

## ✅ Resolved — scoring & ranking fold (applied 2026-07-09, rows 14–15)

This section used to hold the open decision. It is settled; kept for the reasoning.

- **"Trivia-session ranking" never existed post-canon.** Trivia is a `SubstagePlayMode`;
  ranking is single per `LiveSession`, derived from `ScoreEntry`. `DES-55` (HU-39B) and
  `DES-52` (HU-37B) were therefore **folded into `DES-54`**, not closed — DES-55 carried two
  ACs DES-54 lacked (refresh at question close; projection built by consuming RabbitMQ score
  events), and both were preserved. DES-51's duplicated ranking AC (*"El ranking se actualiza
  después de cambios que afecten el puntaje"*) was stripped in the same pass.
- **`HU-37A` → `HU-37`, `HU-39A` → `HU-39`.** Folding the B-halves orphaned the A/B split,
  exactly as `HU-21B` → `HU-21` on DES-29.
- **DES-85 did not override the fold — it inherited the same stale split**, and was amended
  (drop US-21, rewrite US-20, collapse the `HU-39A`/`HU-39B` bullet, fold `HU-37B` into the
  ranking step, fix the *Further Notes* order bullet). Both the **repo copy** under
  `backend/docs/prd/` and the **Linear ticket DES-85** were amended on 2026-07-09; they match.
  ⚠️ DES-85 keeps its `ready-for-agent` label **on purpose** — PRD resolution finds PRD tickets by
  `svc:<service>` **+ `ready-for-agent`** (`generator-agent.md:68-71`). Never strip it.
  Full argument in `canon-realignment-after-mission-runtime-rewrite.md`.
- ⚠️ **`DES-52` and `DES-55` are `Canceled`, not archived.** Archiving is a manual Linear-UI
  step — see the hygiene section below.

## Linear hygiene (human-only — MCP can't archive)

Mostly settled as of 2026-07-09:

- ✅ **DES-23/28/30/44** are now `Canceled`, and the stale `ready-for-agent` label
  is gone from all of them — no agent can grab pre-canon acceptance criteria.
  They are still **unarchived**; archiving them is cosmetic at this point.
- ✅ **DES-71/72/73/74** are archived, so the "ignore" note above is now literally true.
- ✅ **DES-47** is merged into DES-46 (2026-07-09) and `Canceled`, so it no longer appears in
  any `Todo` query. ⚠️ **Canceled, not archived** (`archivedAt: null`) — archive it by hand,
  along with DES-52 and DES-55.
- ℹ️ **DES-25** — stale `ready-for-agent` stripped (2026-07-09). That was a one-ticket fix, not
  a sweep: **19 of 26 `Done` tickets still carry the label**, and that is fine — see the
  cosmetic-drift note above. Do not strip them.
