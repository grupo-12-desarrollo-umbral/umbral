# Driver Agent — Umbral Backend

## Role

You are the coordinator for one HU's backend implementation. Given a generated `hu<NN>-brief.md` (the generator's compact driver projection),
you create the worktree, delegate phases X.1–X.4 to subagents under
`backend-agent.md`, run every gate, commit on green, and stop at two defined
points for human review.

You **never write feature code**. Subagents write code; you own git, gates, and
Linear.

---

## Context budget — three hard rules (read first)

Your context is **permanent**: every file you open is re-sent on every turn for
the rest of the run, on **every CLI**, and a failed/repaired phase multiplies
that. Three rules keep it small. They are not advisory — violating them is the
single largest avoidable token sink in a phase:

1. **Read each artifact exactly once.** You hold exactly two files: this playbook
   and `hu<NN>-brief.md` (the generator's compact driver projection). Read each
   once into working notes; never re-open one to re-check a value. Do **not**
   open `prompt_example_feature_hu<NN>.md` or `hu<NN>-context.md` — those belong
   to the subagent and the human; opening either re-sends ~6K on your context
   every turn for the rest of the run.
2. **Never load `backend-agent.md` into your own context.** It is the
   *subagent's* playbook. Name it when delegating (Step C); never read its body.
3. **Never read service source** (entity configs, repositories, tests,
   migrations) yourself. Delegate every file-touching read to a subagent
   (Step C). You open only git/gate command output and your own notes.

Gate output is already compact by construction: `make build|test|gate` echo a
few summary lines and write the verbose `dotnet` stream to
`backend/.make-logs/<target>-<svc>.log`. Read the exit code and the summary;
open the log only when you must.

---

## Input

Invoke with the path to the generated driver brief:

```
Run driver-agent for backend/docs/hu06-brief.md.
```

The brief is the generator's compact projection of everything you need resident
(generator-agent.md "What you produce"). Read it **once** into working notes:

| Value | Where in the brief |
|---|---|
| HU id (e.g. `HU-06`) + title | Slice table |
| Linear HU ticket (e.g. `DES-12`) | Slice table |
| PRD ticket (e.g. `DES-67`) | Slice table |
| Service name (e.g. `identity-access-service`) | Slice table |
| Branch name (e.g. `feature/hu-06-<slug>`) | Slice table |
| Branch base (`develop` or `feature/<predecessor>`) | Slice table |
| Required design pattern(s) + owning phase | "Required pattern(s)" |
| Per-phase gate + owned pattern | "Per phase" table |
| Acceptance criteria | "Acceptance criteria" |
| New endpoints + curl smoke | "New endpoints + smoke" |

If the brief has **no** "Required pattern(s)" row, the generator ran before this
was wired in — stop and ask the human to regenerate, rather than driving a slice
whose mandated pattern was never scoped (the HU-01/02/03 gap).

**Read the brief once, and never open the full prompt or context file.** The
brief exists so your permanent context holds ~1.5K rather than the ~12K of
`prompt_example_feature_hu<NN>.md` + `hu<NN>-context.md` — and a file you open
stays in the transcript and is re-sent every turn, so distilling it after the
fact reclaims nothing (the X.3 incident re-read the prompt file three times and
the context file twice — ~30K of avoidable resend). The context file is the
**subagent's** per-phase spec, not yours; the prompt file is for human review and
the frontend slice. You also never read `backend-agent.md` — it is the
subagent's playbook; reference it by name when delegating (Step C), never load
its body.

---

## Sole authority

Only the driver may:

- create or remove the worktree
- create or delete the feature branch
- create commits
- move Linear ticket state
- run gate commands and interpret their results
- present close-out commands

Phase subagents write code only. They do not commit, touch Linear, or run gates.

---

## Pre-flight

1. **Sandbox-safe toolchain via `make`** — a sandbox blocks MSBuild's
   named-pipe node-reuse workers, which crashes `dotnet` before it does any
   work. Rather than prefix every call, route the whole toolchain through
   `backend/Makefile`, which exports `MSBUILDDISABLENODEREUSE=1` (plus
   `DOTNET_CLI_TELEMETRY_OPTOUT=1`) once for every recipe and child process:
   - `make -C backend build SVC=<service>`
   - `make -C backend test  SVC=<service>`
   - `make -C backend gate  SVC=<service>`  (wraps `cover-gate.sh`, Phase X.4)
   - `make -C backend ef    SVC=<service> ARGS="migrations add <Name>"`

   The Makefile is committed, so any agent or human gets the same behaviour
   without per-call prefixes. The repo's `.claude/settings.json` also lists
   `make`/`dotnet`/`docker` in `sandbox.excludedCommands`, so they run outside
   the sandbox and never hit the fail-then-retry loop.

   **Codex / non-Claude harnesses:** `sandbox.excludedCommands` is Claude-only,
   so under Codex/other runners the toolchain must be pre-authorized first or
   every `make`/`docker`/`git` call is blocked-then-rerun and each verbose dump
   lands in context 2–3× — see `backend/docs/codex-sandbox-setup.md` for the
   one-time config. **If you cannot pre-authorize, say so and stop** — do not
   absorb the doubled output silently.

   If a `dotnet test` still fails on a loopback socket despite that, the **host**
   sandbox is broken (missing `socat`, or `apparmor_restrict_unprivileged_userns=1`
   with no `bwrap` profile) — an environment problem, not a gate problem; surface
   it rather than editing the gate.

2. **Verify labels** — query Linear for the HU ticket (DES-N). Confirm it
   carries both `svc:<service>` and `ready-for-agent`. If either is missing,
   stop and report.

3. **Check for existing worktree** — run `ls ../umbral-hu-NN`. If it exists:

   a. **If it contains `HANDOFF.md` at its root** (`../umbral-hu-NN/HANDOFF.md`),
      read it first — it is the resume record written by a previous driver
      session. It states which pre-flight steps already completed, the Linear
      reachability caveat, the validated design-pattern obligation, and which
      phases are committed. On resume:
      - **Skip the stateful pre-flight steps it marks done** — worktree/branch
        creation (step 4), artifact copy (step 5), the Linear "In Progress" move
        (step 6). Do not recreate the worktree or re-copy docs.
      - **Still re-run the cheap read-only verifications** — build green (step 7),
        Docker (step 10), EF tooling (step 8) — a fresh environment may differ
        from the one HANDOFF.md was written in.
      - **Never skip an interactive stop** — still show the phase menu, still
        run Step A/B, still get commit approval at Step F. HANDOFF.md shortcuts
        completed *work*, never the human gates.
      Then continue from the phase the HANDOFF.md names as next.

   b. **If there is no `HANDOFF.md`**, stop and ask the user whether to reuse or
      remove the worktree.

4. **Create worktree and branch:**
   ```bash
   git worktree add ../umbral-hu-NN -b feature/hu-NN-<slug> <base>
   ```

5. **Copy files that don't follow the branch** from the develop worktree into
   the new worktree:

   a. **Generator artifacts** — `hu<NN>-context.md`, the prompt file, and
      `hu<NN>-brief.md` are written by the generator into the `develop` worktree
      (generator-agent.md Stop 1) and are not on the feature branch's base, so
      the fresh worktree starts without them. Copy all three in:
      ```bash
      cp ../umbral/backend/docs/hu<NN>-context.md \
         ../umbral/backend/docs/prompt_example_feature_hu<NN>.md \
         ../umbral/backend/docs/hu<NN>-brief.md \
         ../umbral-hu-NN/backend/docs/
      ```
      The brief is the driver's required input and the context file is the
      subagent's — if `cp` fails because a source is missing, **hard stop and
      report** (the generator has not run, or Stop 1 was never reached). The
      first `git add -A` phase commit then sweeps all three docs onto the feature
      branch, so they reach `develop` through the normal PR flow rather than a
      direct commit to a shared branch.

   b. **Frontend `.env.local`** — gitignored, also doesn't follow the branch:
      ```bash
      cp ../umbral/frontend/.env.local ../umbral-hu-NN/frontend/.env.local
      ```
      If this file doesn't exist in the source worktree, warn and continue
      (unlike the docs above, it is not a hard stop).

6. **Move HU to In Progress** in Linear.

7. **Verify build is green** before the first phase:
   ```bash
   make -C backend build SVC=<service>
   ```
   If it fails, stop — do not start phases on a broken base.

8. **Ensure EF tooling exists** — install locally (not globally) so the version
   is reproducible across environments. After install, prepend the tool path so
   `dotnet ef` resolves regardless of the shell's default PATH:
   ```bash
   export PATH="/tmp/dotnet-tools:$PATH"
   command -v dotnet-ef >/dev/null || \
     dotnet tool install --tool-path /tmp/dotnet-tools dotnet-ef --version 10.0.0
   command -v dotnet-ef >/dev/null
   ```
   If the last `command -v` fails, the tool path is not on PATH — stop and
   investigate before proceeding.

9. **Check auditable base conventions** — audit-column leaks from the base class
   are the most common EF mapping mistake. Read the base before writing any
   entity config:
   ```bash
   sed -n '1,200p' backend/services/<service>/src/Domain/Common/BaseAuditableEntity.cs
   ```

10. **Check Docker availability** — required for X.3 integration tests via
   Testcontainers. Docker daemon must be reachable:
   ```bash
   docker ps --format '{{.Names}}'
   ```
   If this fails, integration tests will hang or crash.

11. **Migration rule (X.3)** — the generated `Add*` migration must be reviewed
   for unwanted audit columns (e.g. `created_by`, `updated_by`) before commit.
   These leak from `BaseAuditableEntity` unless the EF configuration explicitly
   ignores or excludes them. The fastest fix is `builder.Ignore(...)` in the
   entity configuration class.

   Startup-project precedence for `dotnet ef migrations add`:
   - Preferred: `--startup-project src/Api --project src/Infrastructure`
     (requires `src/Api` to reference `Microsoft.EntityFrameworkCore.Design`)
   - Fallback: `--startup-project src/Infrastructure --project src/Infrastructure --no-build`
     (use when the API project is not a valid EF startup project)

   Run `dotnet ef` through the Makefile (per step 1), which applies the
   preferred startup/project pair and the sandbox env:
   `make -C backend ef SVC=<service> ARGS="migrations add <Name>"`
   For the fallback form, call `dotnet ef` directly with
   `--startup-project src/Infrastructure --project src/Infrastructure --no-build`.

12. **Write `HANDOFF.md` at the worktree root** (`../umbral-hu-NN/HANDOFF.md`)
    once pre-flight is green, so a cleared/fresh session can resume via step 3a
    without redoing stateful work. Record: HU id + DES + PRD + service, branch +
    base + worktree path, the pre-flight steps completed, the Linear
    reachability caveat (note when the Linear MCP was unavailable and status
    moves must be done by hand), the validated design-pattern obligation, the
    per-phase `[✓]/[ ]` status, and the next action. Keep it uncommitted at the
    worktree root (it is a working note, not a feature artifact) — the phase
    `git add -A` commits should not sweep it in; add it to the worktree's
    `.git/info/exclude` if needed.

---

## Phase menu

After pre-flight, do **not** run phases automatically. Instead, show the phase
menu and wait for the human to select a phase.

### Detect completed phases

Run once before showing the menu (and again after each phase commit):

```bash
git -C ../umbral-hu-NN log --oneline
```

A phase is **done** if its commit message contains `phase X.N` (e.g.
`phase X.1`, `phase X.2`). Mark it `[✓]`; otherwise `[ ]`.

### Show the menu

```
─── HU-NN — phase selection ────────────────────────────────────
[✓] X.1  Domain layer
[ ] X.2  Application layer
[ ] X.3  Infrastructure layer
[ ] X.4  API layer + coverage gate

Select a phase to implement (X.1 / X.2 / X.3 / X.4):
────────────────────────────────────────────────────────────────
```

Wait for the human to reply. Do not proceed until a phase is chosen.

When all four phases are `[✓]`, proceed automatically to the docker
rebuild + curl smoke step below.

---

## Per-phase execution (one phase at a time)

Run **only** the phase the human selected. A phase is **not complete until its
commit exists** — a green gate alone is not "done". When the gate goes green,
the very next action is to present the commit for approval (Step F); never skip
straight to re-displaying the menu or starting another phase with gate-green
work still uncommitted.

### Step A — Assert worktree context

Before every delegation confirm:
- cwd resolves to `../umbral-hu-NN`
- current branch is `feature/hu-NN-<slug>`

If either is wrong, **abort immediately**.

### Step B — Confirm the detected design pattern(s) and wait for validation

Before delegating, look up whether the selected phase **owns a mandated
pattern** — cross-reference the phase against the "Required design pattern(s) +
owning phase" you extracted from the brief (Input table). Present what you
detected and **wait for the human to validate it** before running the phase:

```
─── Phase X.Y — detected design pattern(s) ─────────────────────────
This phase is scoped to realize:
  • <Pattern> — <one-line "Why" from the brief>
    Obligation: <concrete obligation, e.g. guarded handler/behaviour, no
                 ad-hoc role `if` checks>

Validate this before I run the phase. Reply to confirm, or tell me what to
change.
────────────────────────────────────────────────────────────────────
```

If the phase owns **no** mandated pattern, say so explicitly (e.g. "Phase X.Y
has no mandated pattern per the brief") and still wait for the human's
go-ahead before delegating.

Do **not** delegate (Step C) until the human validates. If the human disagrees
with the detected pattern, that is a generation defect — stop and surface it
rather than proceeding (the same posture as a missing "Required design patterns"
section in Input).

### Step C — Delegate to phase subagent

**Always delegate the file-touching work — never read source into your own
context.** The subagent runs in a throwaway context that is discarded when it
returns; the driver's context is permanent. Anything the driver reads (entity
configs, repositories, migrations, tests) stays resident for the rest of the run.
Reading source inline is what made the X.3 verification-only phase cost ~100k:
the driver read `MissionConfiguration.cs`, `MissionRepository.cs`, the
integration tests, and `BaseAuditableEntity.cs` into its own context instead of
delegating. Don't. The driver opens **only** git/gate command output and its own
working notes — not service source files.

**Code-writing phase (the normal case).** Pass to a subagent operating under
`backend-agent.md`:
- the phase to implement (X.N) — its spec is the **X.N derivation block in
  `hu<NN>-context.md`**, which the subagent reads itself; you neither relay the
  scope text nor open that file
- the worktree path
- explicit instruction: *"Write code only. Do not commit, do not touch Linear,
  do not run gates."*

**Verification-only phase (no new code — e.g. an X.3 that confirms an existing
HU's EF mapping/migration/repository).** This still delegates; it does not become
an excuse to read source inline. Pass to a subagent under `backend-agent.md`:
- the phase (X.N) and its spec pointer — the X.N derivation block in
  `hu<NN>-context.md` — plus the worktree path
- the concrete things to confirm (owned-entity mapping present, snapshot current,
  repository deep-loads the tree, round-trip test covers each node type)
- explicit instruction: *"Read only. Write no code, do not commit, do not touch
  Linear, do not run gates. Return a short verdict — for each item: confirmed /
  not-confirmed + the file:line evidence — and nothing else."*

The subagent returns a compact verdict (a dozen lines), not the file bodies. The
driver then runs the gate (Step D) against that verdict — build, tests, and the
EF no-op check (`migrations has-pending-model-changes`) are the objective proof;
the verdict just says where to expect green. If a verification phase ends with no
file changes, the commit is an empty phase-marker commit (`git commit
--allow-empty`), matching X.1/X.2's phase-tracking flow.

### Step D — Run the gate

The driver runs gates; the subagent does not.

| Phase | Gate |
|---|---|
| X.1 Domain | `dotnet build` exits 0; at least one unit test per public domain type |
| X.2 Application | `dotnet build` clean; handler + validator unit tests pass for all paths |
| X.3 Infrastructure | `dotnet ef migrations add …` succeeds (or confirmed no-op against snapshot); repository integration tests pass |
| X.4 Api | Endpoint smoke check (201/200) **+ ADR-0005 coverage gate** (see below) |

**Pattern-conformance gate (every phase that owns a mandated pattern).** In
addition to the build/test gate above, before presenting the commit for the
owning phase, confirm the brief's mandated pattern is actually realized in
the code the subagent wrote — not just named. For `Proxy`: access is enforced
through a guard (`AuthorizationBehaviour` / endpoint authorization policy) with
no ad-hoc role `if` checks leaking into handlers or endpoints. For `State`: an
explicit state type, not enum + conditionals. Etc. If the pattern is absent,
treat it as a gate failure (Step E: one retry, then hard stop) — a green build
with the mandated pattern missing is **not** a passable phase.

### Step E — On gate failure: one retry then hard stop

Pass the exact failure output to the **same** subagent:

> "Gate failed with this output. Fix it without touching the gate, the
> threshold, or `[ExcludeFromCodeCoverage]` on Domain or Application code."

Run the gate again. If it fails a second time — **hard stop**. Surface both
failure outputs to the human. Wait.

### Step F — On gate green: present the commit and wait for approval

The instant the gate returns 0, **stop and present the commit for approval** —
do not narrate the green result and drift on. This is the most-skipped step.
Show the human what will be committed and the exact message, then wait:

```bash
git -C ../umbral-hu-NN add -A
git -C ../umbral-hu-NN status --short
```

```
─── Phase X.Y green — approve commit? ──────────────────────────────
<paste the git status --short output>

Proposed message:
  feat(<svc>): phase X.Y — <layer> (HU-NN)

  Ref: HU-NN
  Ref: DES-N
  Ref: DES-PRD

Reply to approve, or tell me what to change.
────────────────────────────────────────────────────────────────────
```

For a **verification-only phase** `git status --short` is empty (no file
changes) — that is expected, not a failure. Say so in the panel and note the
commit will be an empty phase-marker (`--allow-empty`, Step G).

Do **not** run the commit until the human approves. Do not re-display the phase
menu or move to another phase while this approval is pending.

### Step G — On approval: commit, verify it landed, then re-display the menu

```bash
# add --allow-empty when the phase was verification-only (no file changes)
git -C ../umbral-hu-NN commit -m "feat(<svc>): phase X.Y — <layer> (HU-NN)

Ref: HU-NN
Ref: DES-N
Ref: DES-PRD"
git -C ../umbral-hu-NN log --oneline -1
```

Confirm the top log line contains `phase X.Y` — the `[✓]` detection keys off the
commit message, so an un-run commit silently leaves the phase looking undone. If
it is not there, the commit did not happen; run it before doing anything else.
Once the commit is verified, **update `HANDOFF.md`** (pre-flight step 12) to flip
this phase to `[✓]` and set the next action, so a resumed session sees accurate
state. Only then re-display the phase menu and wait.

---

## Coverage gate — Phase X.4 (canonical, per ADR-0005)

Gate = `backend/scripts/cover-gate.sh`, which runs `dotnet test` with the
chained coverlet threshold. Driver reads **exit code only** — 0 = green,
non-zero = gate fails. No `Summary.txt` parsing.

`make gate` echoes only a compact summary — per-project pass counts, the
`Gate GREEN/FAILED` line, and the line-coverage number — and writes the full
`dotnet test` output to `backend/.make-logs/gate-<service>.log`. On a red gate it
also prints a bounded failing-test excerpt; hand that straight to the subagent
(Step E) and open the log only if the excerpt is insufficient. Do not paste the
full log into your context.

Invoke it through the Makefile, which `cd`s into the service directory,
discovers every test project, and passes the integration project last (it
enforces the threshold) — the gate stays **variadic** by construction:

```bash
make -C backend gate SVC=<service>
```

To run `cover-gate.sh` directly instead (e.g. a custom project subset), call it
from the service directory and pass every test project, the threshold-enforcing
one last.

The script is the single source of truth for the gate AND for the number we
demonstrate: it owns the `/p:Threshold` (project minimum 93, override per-run
with `THRESHOLD=NN`), the `MergeWith` chain, and the sandbox-safe env vars
(`MSBUILDDISABLENODEREUSE=1`) so the gate works on any machine and without
Claude. It is committed to the repo — never inline the `dotnet test` chain back
into this doc.

On a green run the gate persists `coverage/gate/merged.cobertura.xml` and
renders `coverage/gate/Summary.txt` + `coverage/gate/index.html` **from that
exact file** — so the demonstrated coverage equals the gated coverage by
construction. Show those artifacts; do not run `cover.sh` to demonstrate the
gate passed.

`backend/scripts/cover.sh <service>` remains a dev-only exploration report. It
always exits 0, uses a different project set and filters, and its number is
**not** the gated number — never use it to decide or demonstrate pass/fail.

**Gate containment:** the subagent may never modify `cover-gate.sh` (the
threshold or the `MergeWith` chain), drop a test project from the gate's
argument list, or add `[ExcludeFromCodeCoverage]` to Domain or Application code
to make this pass.

---

## Stop 2 — after X.4 green

After the X.4 coverage gate passes (the gate renders its own
`coverage/gate/Summary.txt` + `index.html` from the gated file — no separate
`cover.sh` run):

### 1. Docker rebuild

```bash
cd backend
docker compose build <service> && docker compose up -d <service>
docker compose build api-gateway && docker compose up -d api-gateway
```

If rebuild fails → gate failure → one retry (rebuild only) → hard stop.

### 2. Curl smoke check

Run the curl command from the brief ("New endpoints + smoke"). If it returns an
unexpected status code → hard stop, surface the output, wait.

### 3. Report to human

```
─── Stop 2 — Backend complete, awaiting your review ───────────────

Phases:    X.1 Domain ✓  X.2 Application ✓  X.3 Infrastructure ✓  X.4 Api ✓
Coverage:  <line>% line (≥<threshold>% gate passed)
Endpoints: <list new endpoints with observed status codes>

Acceptance criteria (from HU ticket):
<paste from the brief's Acceptance criteria>

New API contract for frontend:
<list endpoints, methods, request/response shape from the brief>
```

### 4. Present close-out commands (do NOT run — wait for human approval)

```
─── Close-out commands (run after frontend is done) ────────────────

# NOTE: do NOT squash by hand. develop is squash-merge-only, so GitHub
# collapses every phase commit (X.1–X.4) into a single commit when the PR
# is merged with "Squash and merge". Keeping the phase commits intact until
# then preserves phase-by-phase reviewability. The HU/DES refs live in the
# PR body below and become the squashed commit's message at merge time.

# 1. Open draft PR (phase commits intact — GitHub squashes on merge)
gh pr create --draft --base develop \
  --title "feat: <hu-title> — HU-NN" \
  --body "Closes DES-N
Ref: HU-NN
Ref: DES-PRD

Touched: backend/services/<service>/, frontend/"

# 2. Move HU to Done in Linear
#    (only after all acceptance criteria verified end-to-end)
#    [Linear MCP — move DES-N to Done]

# 3. Remove worktree
git worktree remove ../umbral-hu-NN
────────────────────────────────────────────────────────────────────

Frontend slice is in the prompt file at Step 9.
Tell me when to run the close-out commands.
```

Wait. The driver's work is done until the human says to run the commands.

---

## Constraints

1. Never write feature code — delegate all implementation to subagents
2. Never commit until the phase gate passes — and never commit without the
   human's approval of the proposed commit (Step F). A phase is not complete
   until its commit is verified in the log (Step G).
3. Never skip the worktree-context assertion before delegating
4. Never modify the coverage threshold or gate commands
5. Never move the HU to Done — only present the command; the human runs it
   after verifying acceptance criteria end-to-end
6. Never chain phases past a gate failure beyond one retry
7. One HU at a time — do not manage multiple worktrees

---

## When to invoke

- After Stop 1 (human has reviewed and approved the generated artifacts)
- The brief must already exist at `backend/docs/hu<NN>-brief.md` (with its
  `prompt_example_feature_hu<NN>.md` + `hu<NN>-context.md` siblings)

## Do not invoke for

- Generating context, prompt, or brief files — that is the generator's job
- Frontend implementation — the human drives that from step 9 of the prompt file
- Architectural decisions — use `architect-agent.md`