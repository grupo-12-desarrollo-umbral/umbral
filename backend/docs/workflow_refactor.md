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

> **Status as of 2026-07-11 (verified against live Linear + GitHub).**
> Rows 1–7 are **all done**, and so is row 9 (`DES-46`). The 4→5→6 realign chain is complete — DES-75 / HU-16
> (PR #73), DES-76 / HU-21A (PR #75), DES-77 / HU-22 (PR #79) all merged.
> **DES-78 / HU-33A DONE** (2026-07-06); **DES-45 / HU-33B DONE** (PR #119, `b849bf8`).
>
> **The Users ↔ SessionOperations realignment track is finished except the rename.**
> GitHub **#81, #82, #86, #87, #88, #89, #90, #91 are all closed**. Only **GH #85**
> (`identity-access-service` → `users-service`) is still open, and it was always
> dead last. Note it is **not** `DES-85`, an unrelated Linear PRD.
>
> **Row 0 (engineering) partially landed:** GH #149 (branch coverage) and GH #147
> (gateway exception handler) are **closed**. GH #139 (try/catch ADR) remains open.
>
> **Mobile track (DES-81/82/83/84) is DONE.**
> **DES-49** (HU-36A) is **In Progress**.
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
> ### ✅ DES-86 landed (PR #136) — data model migrated, `TargetResolved` deferred
>
> `DES-86` migrated treasure-hunt scoring from **winner-takes-all** (`Substage.WinnerScore`)
> to **per-target `Target.Score`**, shipped as expand → migrate → contract so the two services
> stayed independently deployable. `WinnerScore` is gone from all live production code, and
> **ADR-0015** ratified the ownership split. **But DES-86 did not fully close:** its F2
> deliverable — the **`TargetResolved`** event carrying the resolved target's `ScoreValue` —
> **was not built** (it exists in no `.cs` file), and **`DES-42` (HU-31) now inherits it**.
> `GH #145`'s "run after DES-86" constraint is still **discharged** — nothing is mid-flight
> in `SubstageSnapshot`.
>
> **`DES-42` (HU-31) is no longer blocked by DES-86** (the data model it needed is in place),
> but it must now *define and emit* `TargetResolved` itself.
> ✅ `DES-86` is `Done` in Linear (2026-07-10).
> ⚠️ **Backfill changed point totals** — a five-target substage worth 100 becomes five targets
> worth 100 each. Do not compare scores across the migration boundary.

> ### ⚠️ New — thirteen GitHub-only issues (#137–#149, created 2026-07-10)
>
> Three tracks with **no Linear DES ids**: identity/Keycloak account creation, trivia
> clue + quiz-preview, and cross-cutting engineering (coverage gate, exception handling).
> Like `GH #85` they **do not run the per-ticket loop** — `generator-agent` is invoked with
> a `DES` id and resolves its PRD from Linear (`generator-agent.md:68-71`). See the
> **GitHub-only track** section below for the full order.
>
> Four of them touch this order:
> - **`GH #149`** (branch-coverage gate) rewrites the X.4 phase gate for *every* ticket after it.
>   Land it early or retrofit branch tests into everything built before it.
> - **`GH #138` → `GH #145`** (clue semantics + substage clues reaching the snapshot) **gate row 13**
>   and answer one of its two open decisions.
> - **`GH #145` no longer conflicts with `DES-86`** — both refactor `SubstageSnapshot`, but
>   `DES-86` landed in PR #136, so `#145` is runnable immediately.
> - **`GH #85`** (the rename) must stay behind the identity track, which edits the service it renames.

> ### ✅ DES-46 / HU-34 is DONE (2026-07-10, PR #133)
>
> The former critical-path head shipped. **It is no longer startable work — it is a
> predecessor.** Two tickets it gated: **`DES-49`** (operator answered/
> not-answered monitor, now **In Progress**) and **`DES-84`** (mobile answer submission, **Done**). `backend/docs/
> prompt_example_feature_hu34.md` is now a historical record of a landed slice, not a
> live prompt sequence.
>
> **Startable right now, with zero live blockers — six tickets:** `DES-31`
> (releases the most downstream work), `DES-32`, `DES-53`, `DES-29`, `DES-13`, `DES-80`.
> **GH #173** follows `DES-80`: it changes trivia question authoring RBAC from Administrator-only
> to Operator-only across `mission-design-service` and `frontend/`.
> Plus the remaining GitHub-only row 0 items and the ADRs — see below.
>
> ℹ️ **`ready-for-agent` on `Done` tickets is cosmetic — do not strip it.** A full sweep
> (2026-07-09) found **11 of 26** `Done` tickets carry it. It is **not** an agent-pickup trap: the generator is invoked with an explicit
> `DES` id, never selects by this label, and classifies supersession at its front door —
> *"The generator must not depend on that strip having happened"* (`generator-agent.md:40-44`).
> `ticket-playbook.md:76` agrees: *"hygiene, not the safety net."*
> ⚠️ **Never strip it from a PRD ticket** (`DES-62/67/70/85`): PRD resolution finds them by
> `svc:<service>` **+ `ready-for-agent`** (`generator-agent.md:68-71`).

| # | Ticket(s) | HU | What | Note |
|---|---|---|---|---|
| 0 | **GH #149 ; GH #139 ; GH #147** | — (engineering) | Branch-coverage gate ; try/catch-vs-global-handler ADR ; api-gateway exception handler | ✅ **#149 DONE** (2026-07-10); **#147 DONE** (2026-07-10). `#139` still open — settle before rows 9–13 add ~a dozen endpoints. |
| 1 | DES-14 / DES-15 | HU-09 / 10A | Mission wrapper + composite + Target + optional Clue | ✅ **DONE** (PRs #31/#33 HU-09, #48/#49 HU-10A incl. X.4 coverage). |
| 2 | DES-22 | HU-15 | Create `LiveSession` from active mission; immutable snapshot | ✅ **DONE** (PR #50, 2026-06-22). |
| 2b | DES-79 | HU-15 f/u | Archive-time enforcement: block/cascade when archiving a quiz referenced by an active mission | ✅ **DONE** (PRs #51 + #53, 2026-06-22). |
| 2c | DES-80 | HU-14A f/u | `RemoveTriviaQuestion` command + question-removal domain slot + `TriviaQuestionRemoved` event | ⬜ **OPEN** (Backlog, Low) — deferred follow-up to HU-14A, **ungated** (HU-14A/DES-20 is Done); not a rebuild, not on the critical path — schedule any time (see below). |
| 2d | GH #173 | — (RBAC follow-up) | Trivia question authoring becomes Operator-only, not Administrator-only | ⬜ **OPEN** — GitHub-only, no Linear DES id. Run **after DES-80** so add/update/remove question authoring can be swept together. Touches `mission-design-service` + `frontend/`; do not run in parallel with Lane D or Lane E. |
| 3 | DES-24 | HU-17 | Single mission source; drop "session from quiz" | ✅ **DONE** (PR #72) — rebuild; all 4 layers (X.1–X.4), 370/370 session-operations tests green. |
| 4 | DES-75 | HU-16 | Trivia selection as a Substage, not a session | ✅ **DONE** (PR #73) — rebuild (supersedes DES-23, shipped pre-canon as PR #19). |
| 5 | DES-76 | HU-21A | State machine `Scheduled→Preparing→Active→Paused→Finished→Cancelled` | ✅ **DONE** (PR #75) — rebuild (supersedes DES-28, shipped pre-canon as PR #21). |
| 6 | DES-77 | HU-22 | Timer keyed off active `SubstagePlayMode` | ✅ **DONE** (PR #79) — rebuild (supersedes DES-30, shipped pre-canon as PR #22). |
| 7 | DES-25 | HU-18 | Attach teams during `Scheduled` | ✅ **DONE** (PR #23) — Linear now `Done`; the AC reword landed, nothing remains. |
| 8 | DES-78 + DES-45 | HU-33A/33B | Synchronized trivia substage + auto-close + final results | ✅ **DONE** — DES-78 rebuild (supersedes DES-44), pointer/advancement contract in `backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`; DES-45 by PR #119 (`b849bf8`). |
| 9 | DES-46 | HU-34 | Register first valid team answer + reject late/repeated (one first-write-wins guard) | ✅ **DONE** (PR #133, `1fc6269`, completed 2026-07-10). The intake was **inlined**, not deferred to HU-29/HU-30A. Merged ticket: absorbed DES-47 (HU-34B) on 2026-07-09; **DES-47 is Canceled (not archived) — do not cite it.** |
| 10 | **DES-49** | HU-36A | Operator sees only answered/not-answered during the open question | 🔵 **IN PROGRESS.** Its only blocker (DES-46) landed 2026-07-10. Owns the respondido/no-respondido **visibility-state gate** (assigned here rather than merging DES-49/50). Builds on the operator-only `TeamAnswered` SignalR privacy boundary HU-34 introduced. |
| 11 | **DES-31** | HU-23 | Live team board (score, timer, available clues) | ⬜ **STARTABLE** — cycle broken 2026-07-09, only blocker `DES-77` is Done. Gates rows 12–13 and much of 17–18; running it early releases the most work. |
| 11b | DES-86 | — (refactor) | Per-target `Target.Score` replaces `Substage.WinnerScore` (winner-takes-all → cumulative per-objective scoring) | ✅ **LANDED** (PR #136) — data model + **ADR-0015**; `TargetResolved` deferred to **DES-42**. Two-service refactor (`mission-design` + `session-operations`) shipped expand → migrate → contract; ADR-0015 ratifies the ownership split. F2 (the `TargetResolved` event) was **not** delivered. Also removed `exception.Message` echo from classified error payloads across all three services. Linear ticket is `Done` (2026-07-10). Left a nullability residue — see `DES-87`. |
| 12 | DES-42 → DES-41 ; DES-39 → DES-40 ; then DES-43 | HU-31 / 30B / 29 / 30A / 32 | QR `Target` resolution → explained rejection ; evidence umbrella intake → context validation ; then traceability | ⬜ Opens with DES-31; **DES-86 landed**, so the per-target data model matches the code underneath. **DES-42 now inherits DES-86's undelivered F2:** it must **define and emit `TargetResolved`** carrying the resolved target's `ScoreValue`, per **ADR-0015** (DES-86 shipped the model but not the event). **Two independent chains, not one.** DES-42→DES-41 and DES-39→DES-40 are parallel; DES-43 needs **all three** of DES-42/41/40. The old `39/40/41/42/43` reading was backwards — DES-41 is blocked by DES-42. ✅ **ADR-0010 is `Accepted` (2026-07-10, PR #150)**, so the `TargetResolved` emission is no longer gated on it — the two-fact ordering is fixed and DES-42's spec may be generated. **One open design decision remains on this row:** re-pointing DES-41 → DES-40. ADR-0010 *unblocks* that call but does not decide it. ⚠️ **Run `DES-87` before `DES-42`** — it contracts `TargetSnapshot.Score` from `int?` to `int`; otherwise DES-42 propagates a null that no producer can emit into the `TargetResolved` payload, and DES-51's ledger inherits it. |
| 12b | **GH #138 → GH #145** | — (ADR + bug) | Clue semantics in trivia substages ; substage-level clues reach the runtime plan + snapshot | ⬜ **GATES ROW 13. Runnable now** — its only sequencing constraint (`DES-86`'s concurrent `SubstageSnapshot` rewrite) is discharged. `#138` answers DES-38's open decision (clues are *released*, pre-snapshotted, substage-scoped — not operator-authored at runtime). `#145` is what makes trivia clues exist in the snapshot at all: `MissionRuntimePlanSubstageDto` has no `Clues`, so today they are authored, shown in the UI, and dropped. ⚠️ **Two services.** |
| 13 | DES-36 → DES-37 / DES-38 | HU-26–28 | Operator clue release | ⬜ Opens with DES-31 **and row 12b**. DES-38's decision is settled by `GH #138`; DES-37's is not (see below). DES-37 is blocked by DES-36; DES-38 is blocked by DES-31 only, so it can run beside DES-36. |
| 14 | DES-53 → DES-51 | HU-38 / 37 | Justified penalties → `ScoreEntry` ledger | DES-53 is **startable now** (zero live blockers) and gates DES-51. DES-51 also needs DES-42 (row 12); its other blocker DES-46 is **Done**. ⚠️ **DES-51 depends on the `TargetResolved` event DES-86 did *not* deliver** — the `ScoreEntry` ledger accumulates from resolution events, and neither the event nor any `ScoreEntry` class exists yet (`scoring-monitoring-service/src` is empty). DES-42 must emit `TargetResolved` first. `HU-37A` → **`HU-37`**; DES-52 (HU-37B) folded into DES-54 and Canceled. |
| 15 | DES-54 | HU-39 | Single session `Ranking` derived from the ledger, `ResolutionTime` tie-break, refresh + real-time view | Blocked by DES-51 (+ DES-45, Done). **Fold applied 2026-07-09**: absorbed DES-52 and DES-55. `HU-39A` → **`HU-39`**. Now the sole gate for DES-33/34/35/48/57/61. |
| 16 | DES-50 ; DES-48 | HU-36B / 35 | Post-close operator review of answers + points ; trivia result reveal | **These are not row-9 work.** DES-50 is blocked by DES-51 (row 14); DES-48 by **DES-54** (row 15) after the fold re-pointed it off DES-55. Every prior version of this table ran them too early. |
| 17 | DES-32 ; DES-34 ; DES-35 ; then DES-33 ; then DES-61 | HU-24A / 25A / 25B / 24B | Operator panels + admin/participant read queries + CQRS read split | **DES-32 is startable now** (zero live blockers) — it does not wait on DES-31. DES-34 needs DES-54; DES-35 needs DES-31 + DES-54; DES-33 needs DES-32 + DES-43 + DES-54; DES-61 needs DES-34 + DES-35 + DES-54. |
| 18 | DES-92 → DES-56 → DES-57 ; DES-60 | enabler / HU-40A/40B / enabler | ClueReleased→RabbitMQ publication; session event history; score/ranking historical review; RabbitMQ domain-event publication | **DES-92** (created 2026-07-11) publishes `ClueReleased` MassTransit-native for both clue-release flows (DES-36 manual, DES-37 conditional); prereq **GH #164**, related to DES-36/37, **blocks DES-56** (its only consumer). DES-56 needs DES-42, DES-36, DES-53, **DES-29**, **and DES-92**; DES-57 needs DES-56 + DES-51 + DES-54; DES-60 needs DES-42, DES-40, DES-56, DES-51. |
| 19 | DES-13 → DES-59 ; DES-58 | HU-08 / enablers | Multi-device team sync + reconnect ; multi-device enabler ; React Native participant client | **DES-13 is ungated** — DES-11/DES-12 (HU-07A/07B) both Done. DES-59 needs DES-13 + DES-31. DES-58 needs DES-13 + DES-31 + DES-35 + DES-48, so it lands last. |
| M | DES-81 → DES-82 → DES-83 / DES-84 | HU-M1–M3 / EN-M1 | Mobile trivia: contract spike → active-question display → question-closed state / answer submission | ✅ **DES-81, DES-82, DES-83, DES-84 are all DONE.** A self-service participant does not exist yet — see `GH #143`. |
| MT | GH #164 → GH #165 → GH #166 | — (refactor) | Migrate `session-operations` event publishing from hand-rolled `RabbitMQ.Client` to **MassTransit** | ⬜ **UNGATED GitHub-only refactor sub-track, internal chain #164→#165→#166** (`#164` startable now). Replaces the ~250-line hand-rolled `RabbitMqIntegrationEventPublisher` with the canonical `AddMassTransit().UsingRabbitMq()` + `IPublishEndpoint.Publish(...)`, MassTransit-native topology with `[EntityName]` short exchange names (`session-question-closed`, etc.). **Land before the RabbitMQ consumers** (DES-51 ledger, DES-54 ranking, DES-60 enabler) so the greenfield `scoring-monitoring-service` consumers are built on MassTransit, not on a publisher slated for deletion. No Outbox / custom retry — the automatic `_error` queue only. **No Linear DES id → no generator-agent run**; drive against the issue body like the other `GH` issues. See the dedicated section below. |

**Ungated pickups, runnable any time:** `DES-80` (row 2c), `DES-29` (session
state-change audit — also unblocks DES-56), `DES-32`, `DES-53`, `DES-13`,
`GH #164` (head of the MassTransit refactor sub-track, row MT — best pulled ahead of DES-51/54/60).
`GH #173` is also ungated but should follow `DES-80`, because it sweeps add/update/remove trivia-question
authoring RBAC together across backend and frontend.

**Ungated GitHub-only pickups:** `GH #139` + `GH #137`
+ `GH #138` (three ADRs — cheap, and they unblock six issues between them), `GH #140`
(Keycloak confidential client), `GH #141` (SMTP).
`GH #149` (branch coverage) and `GH #147` (gateway handler) and `GH #146` (quiz preview)
are **closed**.

> ✅ **Done 2026-07-09.** `DES-29` was renamed from the orphaned `HU-21B` to plain `HU-21`
> (there is no `HU-21A` any more — DES-28 was canceled and rebuilt as DES-76), and its
> query AC — *"El historial de cambios puede consultarse posteriormente"* — was stripped,
> since that read surface belongs to `DES-56` (HU-40A), which this HU blocks.

(Archived/superseded, ignore: DES-71/72/73/74 — old participant-lobby sub-issues,
closed out by #108/#110/#115. DES-47 — merged into DES-46 on 2026-07-09; **Canceled, not
archived** — archiving is a manual Linear-UI step still pending. **DES-88/89/90** — the
Linear versions of the MassTransit refactor, superseded by the GitHub-only chain
**GH #164/#165/#166** and **Canceled**; that is why row MT carries "no Linear DES id."
Canceled, not archived.)

## Unified order with the Users realignment (updated 2026-07-09)

The Users ↔ SessionOperations realignment (GitHub issues **#81, #82, #85, #86,
#87, #88, #89, #90, #91** — decisions in `users-realignment-decisions-2026-07-06.md`)
was a **separate track** from the canon rows above that interleaved with them in
`session-operations-service`. **That interleave is over.** Every issue on the track
is closed except **GH #85**, so the remaining sequence is just the canon session-ops
line with the rename pinned to the end:

**~~GH #149~~ ✅ / GH #139 / ~~GH #147~~ ✅ → DES-49 [IN PROGRESS] → DES-31 → GH #138 → GH #145
→ DES-42 → DES-39 → DES-40 → DES-41 → DES-43
→ DES-36 → DES-37 / DES-38 → DES-53 → DES-51 → DES-54 → DES-50 → DES-48
→ DES-32 → DES-34 → DES-35 → DES-33 → DES-29 → DES-56 → DES-57 → DES-60
→ DES-61 → DES-13 → DES-59 → DES-58 → GH #85**

`DES-86` used to sit immediately before `DES-42`, gating the whole QR chain. **Its data-model
half landed in PR #136**, so that sequencing constraint is gone and `DES-42` opens with `DES-31`
alone — but `DES-42` now inherits DES-86's undelivered `TargetResolved` event (see row 12).

`GH #138 → GH #145` is now bounded only by *"before `DES-36`"*. It previously had to follow
`DES-86` because both rewrote `SubstageSnapshot`; with `DES-86` merged, the pair is runnable
immediately and can be pulled anywhere ahead of row 13.

`GH #139` leads the remaining row-0 work. `#149` and `#147` are **closed** — their
deliverables landed 2026-07-10. `#139` (try/catch ADR) is the last open engineering ticket
before the canon line resumes with `DES-49`.

Identity track, independent of the canon line and **entirely ahead of `GH #85`** (the rename
touches every file these issues edit):

**GH #137 → (GH #140 ∥ GH #141) → GH #142 → GH #148 ; GH #141 → GH #143 ; GH #141 → GH #144**

Mobile track — **DES-81, DES-82, DES-83, DES-84 are all Done.**
⚠️ `GH #143` (participant self-registration) belongs to this track in practice — today a
participant can only exist by being seeded into `umbral-realm.json`, so `DES-84` (answer
submission) has no real self-service participant to submit as.

Frontend, own lane (Step 9): **GH #146** (quiz question preview in the trivia substage
editor) — no API change, no backend dependency. **CLOSED.**

### The whole line, serialized (2026-07-11)

Every live ticket in one order, lanes folded in. `DES-46` (PR #133), `DES-86` (PR #136),
`DES-81`, `DES-82`, `DES-84`, `GH #149`, `GH #147`, and `GH #146` are absent because
they are **done**; the 7 PRD tickets (`DES-62/66/67/68/69/70/85`) are absent
because they are reference documents, not buildable slices.

```
~~GH#149 (branch coverage)~~ ✅ → GH#139 (try/catch ADR) → ~~GH#147 (gateway handler)~~ ✅
→ GH#137 (account-flow ADR) → GH#138 (clue-semantics ADR)
→ DES-49 (HU-36A) [IN PROGRESS] → DES-31 (HU-23) → GH#145 (substage clues bug)
→ DES-87 (score nullability contract) → DES-42 (HU-31) → DES-39 (HU-29) → DES-40 (HU-30A) → DES-41 (HU-30B) → DES-43 (HU-32)
→ DES-36 (HU-26) → DES-38 (HU-28) → DES-37 (HU-27)
→ [MassTransit refactor: GH #164 → GH #165 → GH #166 — ungated GitHub-only; land here, before the RabbitMQ consumers below]
→ DES-53 (HU-38) → DES-51 (HU-37) → DES-54 (HU-39) → DES-50 (HU-36B) → DES-48 (HU-35)
→ DES-32 (HU-24A) → DES-34 (HU-25A) → DES-35 (HU-25B) → DES-33 (HU-24B) → DES-61 (ENABLER CQRS)
→ DES-29 (HU-21) → DES-92 (ENABLER ClueReleased→RabbitMQ) → DES-56 (HU-40A) → DES-57 (HU-40B) → DES-60 (ENABLER RabbitMQ)
→ DES-13 (HU-08) → DES-59 (ENABLER multi-device)
→ GH#140 (Keycloak confidential client) → GH#141 (SMTP) → GH#142 (invitations) → GH#148 (invite UI)
→ GH#143 (participant self-registration) → GH#144 (forgot-password)
→ ~~DES-81 (EN-M1)~~ ✅ → ~~DES-82 (HU-M1)~~ ✅ → ~~DES-83 (HU-M3)~~ ✅ → ~~DES-84 (HU-M2)~~ ✅
→ DES-58 (ENABLER React Native)
→ GH#154 (target coordinates) → GH#155 (treasure-hunt play surface, Focus Tabs) → GH#156 (map view)
→ ~~GH#146 (quiz preview)~~ ✅ → DES-80 (HU-14A follow-up) → GH#173 (trivia question authoring RBAC)
→ GH#85 (rename)
```

**One valid serialization, not the only one.** What is actually forced, and what is not:

- **Forced.** `#138` + `#145` before `DES-36`. The whole identity block before `GH #85`.
  `DES-87` before `DES-42` — not a `blockedBy` edge (like `DES-86`'s old slot, the graph can't
  express it), but `DES-42` reads `TargetSnapshot.Score` into the `TargetResolved` payload, and
  `DES-87` contracts that field from `int?` to `int`; run out of order and `DES-42` propagates a
  null no producer can emit, which `DES-51`'s ledger then inherits. Everything else in the line
  follows a live `blockedBy` edge. (`DES-86` used to force two more — `#145` after it, and it
  before `DES-42` — both discharged when PR #136 landed.)
- **Free.** `DES-80` is fully independent — parked late only because nothing
  needs it. Pull it into any quiet window. `GH #173` should follow it, because the RBAC sweep
  covers add/update/remove trivia-question authoring and `RemoveTriviaQuestion` lands in `DES-80`.
  `GH #146` is **closed**.
- **Free, but with a soft ordering preference.** The **MassTransit refactor sub-track**
  (`GH #164 → GH #165 → GH #166`, row MT — GitHub-only, no Linear ticket) has no `blockedBy` edge into
  the canon line — only its own internal #164→#165→#166 chain — so it is pullable any time. It is
  placed just ahead of `DES-51` because that is the first ticket to *consume* RabbitMQ
  (`scoring-monitoring-service` is greenfield): build those consumers on MassTransit, not on the
  hand-rolled publisher `GH #166` deletes. `DES-60` (RabbitMQ enabler, row 18) should likewise follow
  it. Nothing breaks if it runs earlier.
- **The `#153` geolocation track (`GH #154`/`#155`/`#156`)** is its own feature line, child issues
  of `GH #153`. `#154` (backend target coordinates — `mission-design` + `session-operations`) and
  `#155` (mobile play surface, Focus Tabs layout) are **both ungated** — pull them forward any time;
  the Focus Tabs layout is settled by a prototype (`mobile/docs/prototype-treasure-hunt-play.md`).
  Only `#156` (real `react-native-maps` view) is forced late — it needs both `#154` and `#155`.
  Parked here only because nothing on the canon line depends on them. No Linear DES id, so **no
  generator-agent run** — drive against the issue body like the other `GH` issues.
- **Arguably misplaced.** The identity block sits late but is only required to precede
  `GH #85`. `GH #140` is a security fix (shared master-admin credential); run it early.
- **`DES-83` (HU-M3) before `DES-84` (HU-M2) reads backwards on purpose** — both depend only
  on `DES-82` and are parallel siblings. HU numbering implies no order here. Swap freely.
- ⚠️ **`DES-37` (HU-27) will stall.** Its design decision is unresolved (see the clue-model
  section). Settle it before you reach it, not when the generator is already running.

(`DES-80`, `DES-29`, `DES-32`, `DES-53`, `DES-13` are ungated and may be
pulled forward any time. `GH #173` is ungated too, but follows `DES-80`.)

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
5. **`DES-86` preceded `DES-42`** — the only ordering constraint that never came from a
   `blockedBy` edge. It was created *because* the graph could not express it: no ticket
   referenced the stale winner-takes-all model, so nothing blocked `DES-42` on fixing it.
   **Discharged — `DES-86` landed in PR #136.**

Also corrected here: `DES-47` is gone (merged into `DES-46`; **Canceled, not archived**),
`DES-46` is plain `HU-34` not `HU-34A`, `DES-51` is `HU-37` and `DES-54` is `HU-39`, and
`DES-52` / `DES-55` are **Canceled** — folded into `DES-54`, no longer build steps.

Entries are Linear DES ids except those prefixed `GH`, which are GitHub issues with no
Linear ticket and therefore **no generator-agent run**. ⚠️ **`GH #85` (the rename) is not
`DES-85` (the scoring PRD).** They are unrelated tickets that collide on the number —
always write the `GH` / `DES` prefix here.

Done, and no longer ordering constraints on anything:

- ~~**#81, #82, #86, #90**~~ — identity-access-side. All closed.
- ~~**#87 → #88 → #91 → #89**~~ — Users session-ops reshaping. All closed, which
  discharges the two constraints that used to shape this list: they needed #86
  first, and they had to land **before** DES-31 (boards). Both satisfied.
- ~~**DES-45**~~ (HU-33B) — closed by PR #119.
- ~~**DES-46**~~ (HU-34) — closed by PR #133 (`1fc6269`), 2026-07-10. Released `DES-49` and `DES-84`.
  Its remaining downstream edge is `DES-51` (row 14), which still waits on `DES-42`.
- ~~**DES-86**~~ (per-target scoring) — **data model + ADR-0015** closed by PR #136; released
  `DES-42` and `GH #145`. **Not fully done:** its F2 `TargetResolved` event was **deferred** and
  now falls to `DES-42`. Linear ticket is `Done` (2026-07-10).
- ~~**DES-47**~~ (HU-34B) — merged into DES-46 on 2026-07-09; `Canceled`, pending manual archive.
- ~~**DES-81**~~ (EN-M1, mobile contract spike) — `Done`.
- ~~**DES-82**~~ (HU-M1, active-question display) — `Done`.
- ~~**DES-83**~~ (HU-M3, question-closed state) — `Done`.
- ~~**DES-84**~~ (HU-M2, mobile answer submission) — `Done`.
- ~~**DES-91**~~ (flaky integration test) — `Done`.
- ~~**GH #149**~~ (branch-coverage gate) — closed 2026-07-10.
- ~~**GH #147**~~ (api-gateway exception handler) — closed 2026-07-10.
- ~~**GH #146**~~ (frontend quiz preview) — closed.

Still to run — grouped by what actually gates them:

- **Ungated today:** `DES-32`, `DES-53`, `DES-29`, `DES-13`, `DES-80`, then `GH #173`.
  Plus GitHub-only: `GH #139` (row 0, try/catch ADR), `GH #137`/`#138` (ADRs), `GH #140`, `GH #141`, `GH #145`.
- **Gated on the cycle break alone:** `DES-31`, then `DES-39/40/42/41/43` and `DES-36/37/38`.
  ✅ `DES-42`'s extra wait on `DES-86` (per-target scoring) is discharged — PR #136.
  ⚠️ `DES-36/37/38` (row 13) additionally wait on `GH #138` → `GH #145` (row 12b).
- **Gated on the cycle *and* a design call:** `DES-41`'s blocker re-point (ADR-0010 is now `Accepted`, which unblocks the call without deciding it); `DES-36/37/38`'s clue-model decisions.
- **Gated on the clue-release flows + MassTransit:** `DES-92` (ENABLER, created 2026-07-11) — publishes `ClueReleased` to RabbitMQ; prereq `GH #164` (MassTransit bus), publishes the events emitted by `DES-36`/`DES-37` (row 13), and **blocks `DES-56`** (its only consumer, row 18). Not startable until row 13 + `#164` land.
- **Gated on the cycle *and* DES-85:** `DES-51/54`, then `DES-48/50/57`. The 52 + 55 fold into 54 is applied, and the PRD was amended to match (repo copy *and* Linear ticket) on 2026-07-09.
  ⚠️ **`DES-51` (HU-37, the `ScoreEntry` ledger) additionally needs the `TargetResolved` event that DES-86 did *not* deliver** — the ledger accumulates from resolution events, and neither the event nor any `ScoreEntry` class exists yet (`scoring-monitoring-service/src` has zero `.cs` files). `DES-42` (row 12) must define and emit `TargetResolved` first.
- **GH #85** — repo-wide rename `identity-access-service` → `users-service`; dead last, quiet window. Has no Linear ticket.

Not in this order — separate tracks that appeared since 2026-07-06:

- **DES-81/82/83/84** (`mobile` label) — EN-M1 trivia display contract spike + HU-M1/M2/M3 team-space question display, submit, and closed-state. A mobile line that consumes the HU-34–36 backend contract; sequence it against row 10, not against this list.
  ✅ **DES-81, DES-82, DES-83, DES-84 are all Done.**
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

## MassTransit migration — GH #164 → GH #165 → GH #166 (messaging refactor, ungated)

Row **MT**. A three-slice refactor that replaces `session-operations-service`'s hand-rolled
`RabbitMQ.Client` publishing with **MassTransit over RabbitMQ**. **GitHub-only issues — no Linear
DES id**, so like the `GH #137`–`#149` track they **do not run the per-ticket loop**: `generator-agent`
resolves its PRD from Linear by DES id, and these have none. (The Linear tickets **DES-88/89/90**
that once mirrored this chain are **Canceled** — superseded by these GitHub issues.) Drive them directly against the issue
body (each carries its own acceptance criteria) and keep Stop 2 (docker rebuild + smoke) + close-out.
Confined to one service — no two-service worktree caveat.

Current state: publish-only, three integration events (`QuestionClosed`,
`SessionResultsFinalized`, `AnswerRegistered`) pushed through a single ~250-line
`RabbitMqIntegrationEventPublisher` behind the `IIntegrationEventPublisher` seam, with a manual
connection/channel/drain-loop and best-effort/drop semantics. No consumers exist yet.

The slices (tracer-bullet vertical, internal chain — each blocks the next):

1. **`GH #164`** — bootstrap MassTransit + RabbitMQ transport, real host config
   (appsettings/env + docker-compose vars), and migrate the **first** event (`QuestionClosed`)
   through `IPublishEndpoint.Publish(...)`, proven with a Testcontainers integration test.
2. **`GH #165`** — migrate the remaining two events onto the same path; the routing-key `switch`
   is no longer exercised.
3. **`GH #166`** — delete `RabbitMqIntegrationEventPublisher`, the `IIntegrationEventPublisher`
   seam, `RabbitMqOptions`, and the `RabbitMQ.Client` package; refresh the
   `rabbitmq-events-dotnet` skill doc to describe the MassTransit conventions actually in use.

Agreed conventions (kept deliberately vanilla for defensibility):
- Canonical `AddMassTransit(x => x.UsingRabbitMq(...))` registration; publish via
  `IPublishEndpoint` **directly** from the MediatR handlers — the `IIntegrationEventPublisher`
  abstraction is dropped, not reimplemented.
- **MassTransit-native topology**, with a short readable exchange name per contract via the
  built-in `[EntityName("session-question-closed")]` attribute (roughly preserves the old
  routing-key naming in the RabbitMQ management UI).
- **No transactional Outbox, no custom retry policy** — the automatic `<queue>_error`
  dead-letter queue MassTransit provides out of the box is the only reliability surface.

**Why it sits before `DES-51`.** It is ungated (no `blockedBy` into the canon line), but
`DES-51` (the `ScoreEntry` ledger) is the first RabbitMQ *consumer*, and
`scoring-monitoring-service/src` is empty — greenfield. Building those consumers on MassTransit
rather than on a publisher `GH #166` is about to delete avoids throwaway work. `DES-60` (the
RabbitMQ enabler, row 18) should follow it for the same reason.

## ✅ Landed — DES-86 (per-target scoring, PR #136) — data model only; `TargetResolved` deferred

Kept for the reasoning; it no longer gates anything. **Read this as "partly done":** the
data-model migration and ADR-0015 shipped, but the runtime resolution/relay/accumulation flow
did not.

`DES-86` migrated treasure-hunt scoring from **winner-takes-all** to **per-target
`Target.Score`**: on the mission-design side every `Target` now carries its own score as a
non-nullable `ScoreValue` value object, validated **1..100** (`ScoreValueMustBePositiveException`
/ `ScoreValueExceedsMaximumException`, `MaximumPoints = 100`). Teams are meant to accumulate
points for each objective they resolve, and the first team to resolve all targets triggers
**advancement** without zeroing anyone else's points.

Origin: `target-score-handoff.md` (2026-07-09). The canon docs
(`grilling-session-mission-restructure.md`, `bd_umbral_entity_spec.md`,
`session-operations-service/CONTEXT.md`) were corrected that day, and the code caught up
in PR #136 (commit `32fc735`), shipped **expand → migrate → contract** so the two services
stayed independently deployable at each step.

⚠️ **The migration backfills each target's score from its substage's `WinnerScore`, so point
totals changed**: a five-target substage worth 100 becomes five targets worth 100 each. Do not
compare scores across the migration boundary.

✅ `DES-86` is `Done` in Linear (2026-07-10).

### What actually shipped

- **`Target.Score` is now a non-nullable `ScoreValue`** (1..100), enforced structurally on the
  mission-design side.
- **`WinnerScore` is gone from all live production code.** Remaining occurrences are only in
  docs, historical EF migrations, and two test files that assert the backfill.
- **`SubstageSnapshot.WinnerScore` removed; `TargetSnapshot.Score` added** — but as a
  **nullable `int?`**, so the 1..100 guarantee is **not structural** on the session-operations
  side; it is enforced at snapshot construction. → **`DES-87`** contracts the nullability
  (the *expand* step's residue; no `contract` step ever ran). The `1..100` ceiling stays in
  `mission-design` per ADR-0015 — it is **not** mirrored into the relay.
- **Old exceptions deleted** (`TriviaSubstageSnapshotCannotDeclareWinnerScoreException`,
  `TreasureHuntSubstageSnapshotWinnerScoreRequiredException`) and **replaced** by
  `TreasureHuntTargetSnapshotScoreRequiredException`, thrown at `MissionRuntimeSnapshot.cs:141`
  in `EnsureSubstageInvariants` when a treasure-hunt target has `Score is null || Score.Value <= 0`.
- **`backend/docs/adr/0015-per-target-scoring-ownership.md`** added — Status *"Accepted —
  2026-07-09"*, title *"Per-target scoring: MissionDesign authors, SessionOperations relays,
  ScoringMonitoring accumulates."* It ratifies **both** contract decisions the old text below
  said had to be settled inside DES-86.
- **Stopped echoing `exception.Message`** in classified error payloads across all three services.

### Both contract decisions were ratified — but only the authoring half exists in code

The two decisions this section used to leave open — (1) that `TargetResolved` must carry the
resolved target's `ScoreValue`, and (2) which bounded context owns `ScoreValue` — are **both
settled by ADR-0015**: **MissionDesign authors** the points, **SessionOperations relays** them
in the event, **ScoringMonitoring accumulates** them in the ledger, with no mutable total
outside `ScoreEntry`. Precedent: `AnswerRegisteredEvent` (HU-34, `f2edb1e`) already carries
`isCorrect` + `scoreValue` for exactly this reason.

**Only the "authors" half is implemented.** Three pieces of ADR-0015 are **not** in code:

1. **No `TargetResolved` event.** It exists in **no `.cs` file** — not defined, not emitted,
   carries no `ScoreValue`. This was DES-86's **F2** deliverable. ADR-0015 itself concedes the
   event *"does not exist in code yet"* and **deferred** it because ADR-0010 was unratified at the
   time. ADR-0010 is now `Accepted` (2026-07-10, PR #150), so the deferral no longer holds.
   Realistically this emission now belongs to **DES-42 (HU-31)**, the QR target-resolution
   ticket — that is the flow that resolves a target.
2. **SessionOperations relays nothing.** `TargetSnapshot.Score` is stored, but no resolution
   flow snapshots it into an event.
3. **ScoringMonitoring accumulates nothing.** `scoring-monitoring-service/src` has **zero
   `.cs` files**; no `ScoreEntry` class exists anywhere.

- **Why it used to gate row 12.** The generator reads canon docs, so `DES-42` (HU-31) would
  have been specced per-target while `WinnerScore` still appeared across
  `mission-design-service` + `session-operations-service`. The driver would have built a
  per-target spec on top of winner-takes-all code. That risk is gone — but the *event* DES-42
  needs to emit still has to be built inside DES-42.
- **⚠️ Two services, one ticket (historical).** The per-ticket loop above assumes a single
  service per worktree. `DES-86` touched `mission-design-service` and `session-operations-service`
  together — the migration had to land in both or the runtime snapshot projection would break.
  That worktree/PR-shape caveat is recorded here as history; PR #136 already resolved it.

**No ticket needed editing.** The eight tickets the handoff lists (`DES-39/40/41/42/51/53/54/85`)
were re-verified against live Linear: none mentions `WinnerScoreValue`, winner-takes-all, or
"non-winning teams receive zero". But none **specifies** the `TargetResolved` emission either —
`DES-42` never mentions points in any AC — which is precisely why that deferred deliverable now
falls to `DES-42` to define and emit.

## GitHub-only track — issues #137–#149 (created 2026-07-10)

Thirteen issues with **no Linear DES id**. They came out of two backlog notes (Keycloak
account creation + per-service credentials; controller try/catch) and two authoring notes
(quiz questions invisible in the mission editor; clues in trivia mode).

### How they run — not the per-ticket loop

`generator-agent` is invoked as `Run generator-agent for HU-<NN> DES-<N>` and resolves the
governing PRD from Linear by `svc:<service>` + `ready-for-agent` (`generator-agent.md:68-71`).
These issues have neither an HU number nor a DES id, so **steps 1–2 of the loop do not apply**.
`GH #85` is the existing precedent. Drive them directly against the issue body — each carries
its own acceptance criteria — and keep the loop's Stop 2 (docker rebuild + curl smoke) and
close-out.

Three do not fit a single-service worktree, same caveat as `DES-86`:

| Issue | Shape | Why the loop breaks |
|---|---|---|
| `GH #145` | two services | `mission-design` + `session-operations` must change together or the runtime-snapshot projection breaks |
| `GH #149` | repo-wide | edits `cover-gate.sh`, ADR-0005, and nine doc sites across `.claude/` + `.agents/` |
| `GH #144` | back + front | realm `reset-credentials` flow plus login-page and mobile entry points |

`GH #146` and `GH #148` are frontend-only → **Step 9** lane (`frontend/plans/`), not the
backend driver.

### Track 1 — Identity / Keycloak (ahead of `GH #85`)

```
GH #137 (ADR: account flow)
   ├─→ GH #140 (confidential client + service account)  ─┐
   ├─→ GH #141 (SMTP + verifyEmail)  ───────────────────┬─┴─→ GH #142 (invitations) → GH #148 (invite UI)
   │                                                     ├─→ GH #143 (participant self-registration)
   │                                                     └─→ GH #144 (forgot-password)
```

- **`GH #140` is a security fix, not a feature.** `KeycloakAdminService.GetAdminTokenAsync`
  uses a password grant against `realms/master` with `client_id=admin-cli` and a shared
  realm-admin credential. The realm defines **no confidential client at all**. This is the
  *"Keycloak tiene que configurarse para darle credenciales a cada servicio"* item.
- **`GH #141` ships SMTP and `verifyEmail` together.** `verifyEmail: true` with no `smtpServer`
  block locks every new account out permanently — the verification mail never sends.
- **Decision recorded in `GH #137`:** account creation and role assignment are separate
  privileges. Participants self-register through Keycloak's hosted pages with a realm default
  role of `Participant`; Operators and Administrators are **invited** by an Administrator, who
  never sets a password (`execute-actions-email` with `UPDATE_PASSWORD` + `VERIFY_EMAIL`).
- ⚠️ **Ordering against `GH #85`.** The rename `identity-access-service` → `users-service` is
  dead last in the unified order. Every issue in this track edits that service. Run the track
  first; `#85` then sweeps the renamed paths in one pass.
- ⚠️ **Bootstrap admin.** `umbral-realm.json` seeds `admin` / `admin123`, committed. Fine for
  dev, must never reach production — `GH #137` makes that explicit.

### Track 2 — Trivia clues + quiz preview

- **`GH #138` → `GH #145`** — row 12b above. Gates row 13. Runnable now (`DES-86` landed).
- **`GH #146`** — frontend only, fully independent. **CLOSED.** `GET /api/trivias/{id}` already returns
  questions and options with `IsCorrect`, and `getTriviaQuiz(id)` already calls it; the
  question table already exists as `renderQuestionsSection` (`TriviasPanel.tsx:279`) but is an
  inner closure of `TriviasPanel` and must be extracted before `SubstageEditor` can reuse it.

Rejected inside `#138`, so nobody relitigates it during row 13: **clues cannot attach to a
single trivia question.** `Substage.TriviaQuizId` is a whole-quiz reference (`CONTEXT.md:59-61`,
ADR-0003), and `TriviaQuestionSnapshot` keys questions by `(SubstageSnapshotId, SequenceOrder)`
— there is no stable per-question identity to point at across a quiz edit.

### Track 3 — Cross-cutting engineering (row 0)

- **`GH #149` — branch coverage.** **CLOSED (2026-07-10).** The gate is `Threshold=93`, `ThresholdType=line`,
  `ThresholdStat=total` (`scripts/cover-gate.sh:35,118-119`), already per-service via
  `make gate` / `make gate-all`. It must also enforce **≥93% branch**. Add branch, do not
  replace line.
  - **Step 1 is measurement, not the flag flip.** No branch number has ever been reported by
    this repo. Branch coverage normally lands well below line coverage; expect the gate to fail
    on the first run.
  - **It changes agent stopping conditions.** `aspnet-backend-testing/SKILL.md:29` and its
    duplicate under `.agents/skills/` both say *"93% line coverage"*. Until they are updated,
    agents stop writing tests too early and the gate catches it at the wrong end of the loop.
  - **Scope is three services.** `api-gateway` is intentionally excluded (`Makefile:54-58`);
    `scoring-monitoring-service` has no tests and is silently skipped by the `GATEABLE_SERVICES`
    auto-discovery (`Makefile:58`).
  - ⚠️ **Open question inside the issue:** whether coverlet 6.0.4 supports per-type threshold
    values (`/p:Threshold="93,85"` against `/p:ThresholdType="line,branch"`). If not, a single
    value applies to every listed type and a per-service ratchet needs a different mechanism —
    likely cobertura XML parsing, which **ADR-0005 explicitly moved away from**. Resolve before
    committing to an approach.
  - ✅ **Resolved / done (2026-07-10).** coverlet 6.0.4 does **not** support per-type values — a
    single `/p:Threshold` applies to every listed type. Solution: keep the single 93% bar and gate
    both dimensions via `/p:ThresholdType=\"line,branch\"` (comma must be quoted or MSBuild fails
    with `MSB1006`). `cover-gate.sh` is flipped, all three services clear ≥93% branch (identity
    96.2%, mission 95.3%, session 95.1%), `make gate-all` is green, and ADR-0005 + `current_workflow.md`
    + the `aspnet-backend-testing` skill docs (both copies) are updated.
- **`GH #139` — controller try/catch.** A course requirement asks for it; controllers today
  contain **zero** `catch` by design, and `ProblemDetailsExceptionHandler` maps `ErrorCategory`
  to status codes in one place. Outcome is an ADR. If the requirement is non-negotiable the
  catch blocks must **log and rethrow**, never build their own `ObjectResult` — that is the
  drift scenario. Settle it before rows 9–13 add ~a dozen endpoints.
- **`GH #147` — api-gateway has no exception handler.** The one genuine gap found while
  investigating `#139`. **CLOSED.** Not affected by `#149` (api-gateway is
  outside the coverage gate).

## Open decisions blocking row 13 — clue model (resolve before starting it)

- **DES-37 (HU-27):** rule-based auto clue-release is not in canon — eliminate /
  merge into HU-26 / redefine. **Still open.**
- **DES-38 (HU-28):** operator-authored runtime clues conflict with the immutable
  snapshot — model as explicit exception or reinterpret as *release* of
  pre-snapshotted clues. → **Now owned by `GH #138`** (row 12b), which takes the
  *release* reading: clues are authored pre-snapshot, scoped to the substage, and
  `HiddenUntilOperatorRelease` is the release mechanism. Nothing is authored at runtime,
  so the snapshot stays immutable. Land `#138` and the decision is recorded.

Record the remaining DES-37 decision in an ADR or the ledger addendum before rewriting the AC.

⚠️ `GH #145` is the *implementation* half: `#138` says trivia clues are substage-scoped
guidance, and `#145` fixes the fact that they never reach the snapshot to be released from.
Row 13 without `#145` builds an operator release button for clues that do not exist at runtime.

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
