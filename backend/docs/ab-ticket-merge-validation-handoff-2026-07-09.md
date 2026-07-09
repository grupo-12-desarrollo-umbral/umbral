# Handoff — validation of the A/B merge findings against live Linear (2026-07-09)

Validates `ab-ticket-merge-findings-handoff-2026-07-09.md` (the "findings doc") against
the live Linear graph and the code. **Nothing has been applied to Linear.** No ticket was
edited, closed, or re-pointed.

Read the findings doc first for the *arguments*. This doc records only where the graph
**contradicts** it, plus the coverage it was missing. Where this doc is silent, the
findings doc stands.

Scope of the sweep: every non-`Done`, non-`Canceled` ticket in team `umbral-equipo-12` —
31 open HU-type tickets (28 backend + 3 `mobile`), excluding 7 PRDs and 5 ENABLERs.

Companion docs — read rather than re-derive:
- `ab-ticket-merge-findings-handoff-2026-07-09.md` — the original verdicts and reasoning.
- `canon-realignment-after-mission-runtime-rewrite.md` — ticket-disposition ledger.
- `workflow_refactor.md` — per-ticket loop + run order.
- `prd/DES-85-primera-implementacion-de-scoring-monitoring-service-hu-37-a-hu-40.md`.

---

## Bottom line

Exactly **one** unconditional merge survives validation: **DES-47 → DES-46** (`HU-34`).
Two of the findings doc's merges are unsafe as written. One pair it never examined is an
orphan. One duplicate outside the A/B naming is at least as clear-cut as the one it found.

| Pair | Findings doc | Validated verdict |
|---|---|---|
| DES-46 / DES-47 (34A/34B) | Merge | **Merge — confirmed, graph-safe** |
| DES-54 / DES-55 (39A/39B) | Close DES-55, premise void | **Merge — DES-55 is not void** |
| DES-51 / DES-52 (37A/37B) | Merge 52 into DES-54 | Correct, but the duplicate is **three-way** |
| DES-15 / DES-16 (10A/10B) | Close DES-16, urgent | Close — but **already archived**, not urgent |
| DES-40 / DES-41 (30A/30B) | Merge | **Blocked — blocker runs the wrong way** |
| DES-49 / DES-50 (36A/36B) | Merge | **Do not merge — fails the doc's own test** |
| DES-28 / DES-29 (21A/21B) | *not examined* | **Orphaned B — becomes plain `HU-21`** |
| DES-32/33, DES-34/35, DES-56/57 | Keep split | Keep split — confirmed |

---

## Where the graph contradicts the findings doc

### DES-40 + DES-41 (`HU-30`) — the blocker is inverted

The doc argues "B is A's failure branch." Linear says DES-41 is `blockedBy` **DES-42
(HU-31)**, not DES-40. DES-40 is `blockedBy` DES-39 (HU-29).

Merge as-is and the combined `HU-30` inherits HU-31 as a blocker — while DES-42's own body
states it *"especializa el intake genérico de evidencia (HU-29) y la validación de contexto
(HU-30A)"*. `HU-30` would wait on a ticket that semantically depends on `HU-30`.

**Prerequisite:** re-point DES-41's blocker from DES-42 to DES-40 *before* merging. The
rest is clean — both already block DES-43 (HU-32).

### DES-49 + DES-50 (`HU-36`) — do not merge

The doc keeps DES-32/33 split because *"different blockers = a real vertical slice,"* then
merges 49/50, which have disjoint blockers:

- DES-49 `blockedBy` DES-44 (**Canceled**) + DES-46 → live blocker set is `{DES-46}`
- DES-50 `blockedBy` DES-45 (Done) + DES-51 → live blocker set is `{DES-51}`

They do not even share service labels (DES-49 is `svc:session-operations-service` only;
DES-50 adds `svc:scoring-monitoring-service`). Merging chains a ticket that is one step
from ready to the entire scoring ledger — itself blocked by HU-31, HU-38, HU-34A.

The doc's real observation is that the visibility state gate *"is owned by neither."* Fix
that by assigning the gate to DES-49. Do not fuse the tickets.

### DES-55 (`HU-39B`) — a merge, not a close

The doc says "the premise is void, there is nothing to merge." DES-55 carries two ACs that
DES-54 lacks:

- ranking refreshes **after each question closes**
- the ranking projection is built by **consuming score events from RabbitMQ**

Trivia being a `SubstagePlayMode` rather than a session type does not void "refresh ranking
at question close" — that trigger still fires inside a mission session. `canon-realignment-
after-mission-runtime-rewrite.md:83` says *subsume into DES-54*; subsume ≠ close. **Fold
the ACs into DES-54.**

### DES-52 → DES-54 — the duplicate is three-way

DES-51's fourth AC is verbatim DES-52's first: *"El ranking se actualiza después de cambios
que afecten el puntaje."* If DES-52 folds into DES-54 while DES-51 is kept as-is ("the write
model"), the duplicate rule survives in DES-51. **Strip that AC from DES-51 in the same edit.**

### DES-16 — right call, wrong urgency

Substance confirmed. DES-16's four ACs are a strict subset of DES-15's, and the enforcement
exists: `CanContain` abstract at `backend/services/mission-design-service/src/Domain/Entities/MissionNode.cs:85`,
overridden in `Stage.cs:53`, `Substage.cs:141`, `Clue.cs:54`; `InvalidMissionNodeChildException`
present; all three named tests exist in `tests/UnitTests/Domain/Entities/MissionNodeTests.cs`.

But the doc's stated risk — *"DES-16 is `Backlog`, so an agent **will** pick it up and produce
an empty PR"* — is false. DES-16 has `archivedAt: 2026-06-15`, carries no `ready-for-agent`
label, and has **zero relations**. Cosmetic cleanup, no knock-on, nothing to race.

> Note: the findings doc cites `mission-design-service/src/...`. The real path is
> `backend/services/mission-design-service/src/...`.

### DES-55 blocker fallout is undercounted 3×

The doc names DES-34/35. DES-55 actually `blocks` **six**: DES-33, DES-34, DES-35, **DES-48**,
DES-57, DES-61. The dangerous one is **DES-48 (HU-35)** — live in `Todo`, cycle-assigned,
and its only blockers are DES-45 and DES-55. It would be stranded.

Two smaller errors in the same section: DES-35's blockers are *HU-23*, HU-39A, HU-39B — not
HU-20. And the doc's *"Same for DES-35's `Blocked by HU-23`"* implies HU-23 is stale; DES-31
(HU-23) is alive in `Todo`. Nothing to fix there.

---

## Coverage the findings doc was missing

### HU-21A / HU-21B is an orphaned split — never examined

DES-28 (HU-21A) was `Done`, then **canceled 2026-06-30**, superseded by DES-76 (now `Done`).
DES-29 (HU-21B) is still `Todo`, and its *only* blocker is the canceled DES-28.

There is no "A" anymore. HU-21B cannot merge with anything — it should be **renamed `HU-21`**
and its blocker re-pointed to DES-76.

Scope caveat: DES-29's AC *"El historial de cambios puede consultarse posteriormente"* is a
read surface DES-56 (HU-40A) already owns, and DES-56 is `blockedBy` DES-29. Same leak pattern
as DES-51's ranking AC. DES-29 should keep "persist the fact + publish `SessionStateChanged`"
and drop the query.

### The other real duplicate: DES-13 vs DES-59

`HU-08 - Sincronización multi-dispositivo del equipo` (DES-13, `Todo`) and
`ENABLER - Sincronización multi-dispositivo por equipo` (DES-59, `Backlog`) have effectively
the same acceptance criteria — propagate team state to authorized devices in real time,
restore state on reconnect, never mix state across teams. DES-13 `blocks` DES-59.

One piece of work. The findings doc found the analogous cross-boundary duplicate at
DES-52/DES-54 and stopped; it only ever looked at `A`/`B` names. Direction of the merge is a
human call (HU vs enabler).

---

## Two graph defects that outrank every merge

### 1. Twelve live tickets are blocked by canceled tickets

DES-23 (HU-16), DES-28 (HU-21A), DES-30 (HU-22), DES-44 (HU-33A) were each marked `Done`, then
**canceled 2026-06-30** as canon-realignment debt. Replacements DES-75 / DES-76 / DES-77 /
DES-78 are all `Done`. DES-75, DES-76 and DES-77 **carry no `blocks` edges**; DES-78 carries one
— it blocks DES-45, whose `blockedBy` was **already re-pointed** off canceled DES-44. The
canceled originals otherwise still carry every one of theirs.

DES-23 is inert for merge purposes: its dependents are DES-24, DES-25, DES-26 (all `Done`) plus
the canceled DES-28 / DES-44. **No live ticket is blocked by DES-23.** It matters only for the
supersession ledger, which currently omits it.

Live tickets currently blocked by a canceled ticket: **DES-29, DES-31, DES-32, DES-36, DES-37,
DES-38, DES-39, DES-42, DES-46, DES-49, DES-53, DES-59.**

Consequence worth acting on now: **four tickets already have zero live blockers**, not one.

- **DES-46** (HU-34A, the critical-path head) — DES-44 Canceled, DES-11 Done.
- **DES-32** (HU-24A) — DES-28 Canceled, DES-27 Done.
- **DES-53** (HU-38) — DES-28 Canceled, DES-26 Done.
- **DES-29** (HU-21B) — DES-28 Canceled, and it has no other blocker.

DES-49's only live blocker is DES-46.

**Re-point these before acting on any merge. The graph is currently lying about what is ready,
and every merge decision reads the graph.**

### 2. A pre-existing dependency cycle

```
DES-51 (HU-37A) blockedBy DES-42 (HU-31)
DES-42 (HU-31)  blockedBy DES-31 (HU-23)
DES-31 (HU-23)  blockedBy DES-51 (HU-37A)   <- closes the loop
```

Confirmed from all three tickets' own relation lists. Taken literally, none can start.
Untouched by the 2026-06-30 cancellations. The `HU-30` and `HU-39` merges both edit nodes
adjacent to it.

Likely break: drop HU-37A from DES-31's blockers — a live team board can show a score of zero
before the ledger exists.

---

## DES-85 blocks the HU-37/HU-39 work

The findings doc's instruction to "read DES-85 first" was correct, and it comes back **negative**.
`prd/DES-85-...md:198-204` enumerates `HU-37A`, `HU-37B`, `HU-39A`, `HU-39B`, `HU-40A`, `HU-40B`
as **distinct slices**, and `:271-274` states the implementation order depends on `HU-37B` existing
as its own step. DES-85 is already `ready-for-agent`.

So the PRD contradicts the HU-39 fold. Human decision: amend the PRD, or drop the merge.

---

## Suggested order of operations

1. **Re-point blockers off DES-28/30/44 onto DES-76/77/78** (DES-45 → DES-78 is already done;
   DES-23 → DES-75 needs no edge work, only a ledger row). Nothing else is trustworthy until this lands.
2. **Break the DES-51 → DES-42 → DES-31 cycle.**
3. **Merge DES-47 → DES-46** as `HU-34`. Safe, unblocked, critical-path head.
4. Close **DES-16** (absorbed, already archived — low priority).
5. Rename **DES-29** to `HU-21`, re-point its blocker, strip its query AC.
6. **Hold** DES-52/54/55 until DES-85 is reconciled. When cleared: fold 52 **and** 55 into 54, strip DES-51's ranking AC.
7. **Do not** merge DES-49/50. Assign the state gate to DES-49 instead.
8. **Do not** merge DES-40/41 until DES-41's blocker is re-pointed to DES-40.
9. Decide the DES-13 / DES-59 direction.
10. Record outcomes in `canon-realignment-after-mission-runtime-rewrite.md` (supersession map) and `workflow_refactor.md` (order line).

Merging in Linear is human-only for the archive step; the MCP server edits titles,
descriptions, and blockers but cannot archive.

---

## Tooling gotchas (carried forward, both re-confirmed)

- `gh issue view` / `gh pr edit` **fail in this repo** with a Projects-classic GraphQL deprecation
  error. Use `gh api repos/grupo-12-desarrollo-umbral/umbral/issues/<n>` (REST).
- `mcp__linear-server__list_issues` with `limit: 250` overflows the tool-result cap. Filter by
  `state` (`Todo`, `Backlog`, `In Progress` separately) at `limit: 100` — that fits and is what
  this sweep used.
- `mcp__linear-server__get_issue` needs `includeRelations: true` to return blockers. Ticket
  *descriptions* list stale "Blocked by" text that disagrees with the real relations — **trust
  the relations, not the prose.** This is the single largest source of error in the findings doc.
- Bash under the default sandbox fails with an `apply-seccomp` error on this machine; grep over
  the repo needs `dangerouslyDisableSandbox`.

## Suggested skills

- **`grill-with-docs`** — before executing the `HU-30` or `HU-34` merges. Both touch the
  `EvidenceSubmission` threshold in ADR-0010, still *in revisión*; that ADR decides whether the
  merged rejection contract is one rule or two.
- **`cqrs-mediatr-aspnetcore`** — for the merged DES-46+47: one command handler with a
  first-write-wins guard; the reject path is a validator concern, not a second handler.
- **`to-issues`** — only if the merged tickets need re-slicing into vertical tracer bullets
  rather than a straight title-and-AC edit.
