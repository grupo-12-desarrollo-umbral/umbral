# How to Implement a HU — Generator + Driver Agents

Concrete walkthrough using HU-06 as the example.
Two agents, two human stops, one HU from Linear to draft PR.

---

## Overview

```
You type               Agent runs              You decide
──────────────────────────────────────────────────────────
"generator for         reads Linear,           Stop 1:
 HU-06 DES-13"    →   writes context  →   review both files
                       + prompt file          say "proceed"
                            │
                            ▼
                       driver creates           Phase menu:
                       worktree + branch  →  [ ] X.1 Domain
                            │               [ ] X.2 Application
                            │               [ ] X.3 Infrastructure
                            │               [ ] X.4 Api
                            │                   ↑ you pick one
                            │
                    ┌───────┴────────────────────┐
                    │  subagent implements phase  │  ← repeats
                    │  driver runs gate           │     until all
                    │  you approve the commit     │     four [✓]
                    │  menu updates [✓] status    │
                    └───────────────────────────┘
                            │
                            ▼
                       docker rebuild + curl
                            │
                            ▼
                       driver reports  →   review API contract
                       + close-out         say "run commands"
                         commands           (or not yet)
                            │
                            ▼ (you do this manually)
                       frontend slice
                       (Step 9 of the
                        prompt file)
                            │
                            ▼
                       close-out commands
                       (squash + PR + Linear)
```

---

## Session 1 — Generate

Open a new Codex session from the repo root. Point it at the generator:

```
Read @backend/.agents/generator-agent.md.

Run generator-agent for HU-06 DES-13.
```

The generator will:

1. Query Linear for `DES-13` — confirm `svc:identity-access-service` +
   `ready-for-agent` labels are present. If either is missing it stops here
   and tells you which label to add.
2. Find the PRD: query Linear for `svc:identity-access-service` + PRD ticket →
   resolve to `DES-67`. Read the local file
   `backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md`.
3. Read predecessors: Linear shows HU-01 through HU-05 as Done →
   read `backend/docs/hu01-context.md` through `hu05-context.md`.
4. Determine branch base: if HU-05 is Done and merged → `develop`.
   If HU-05 is still In Progress → `feature/hu-05-<slug>`.
5. Write `backend/docs/hu06-context.md`.
6. Write `backend/docs/prompt_example_feature_hu06.md`.
7. Stop and report:

```
Generated:
  backend/docs/hu06-context.md
  backend/docs/prompt_example_feature_hu06.md

HU-06 adds: <one paragraph summary from the generator>
```

---

## Stop 1 — You review the generated files

Read both files before proceeding.

Check:

- Pre-resolved orient lists the right predecessor work (HU-01–05 landed items)
- Scope table matches what the PRD says HU-06 adds
- Branch base is correct (develop vs predecessor branch)
- Commit Ref: lines have the right three ids
- No invented concepts outside the PRD

If anything is wrong: correct it in the files directly, then proceed.
If it looks good: open a new session and invoke the driver.

---

## Session 2 — Drive backend phases

Open a **new** Codex session from the repo root. Point it at the driver:

```
Read @backend/.agents/driver-agent.md.

Run driver-agent for backend/docs/prompt_example_feature_hu06.md.
```

After pre-flight the driver shows a phase menu and waits for you to pick a
phase. You select one at a time; the driver runs it and updates the status.

### Pre-flight (driver, not you)

```bash
# Confirms DES-13 labels in Linear
# Creates worktree:
git worktree add ../umbral-hu-06 -b feature/hu-06-<slug> develop
# Moves DES-13 to In Progress in Linear
# Verifies dotnet build is green on the base
#   (MSBUILDDISABLENODEREUSE=1 dotnet build … — sandbox-safe)
```

### Phase menu (you pick, driver runs)

```
─── HU-06 — phase selection ────────────────────────────────────
[ ] X.1  Domain layer
[ ] X.2  Application layer
[ ] X.3  Infrastructure layer
[ ] X.4  API layer + coverage gate

Select a phase to implement (X.1 / X.2 / X.3 / X.4):
────────────────────────────────────────────────────────────────
```

Open a new Codex session from the repo root. Point it at the subagent:

```
Read @backend/.agents/driver-agent.md.

Run driver-agent for backend/docs/prompt_example_feature_hu06.md.
Implement phase X.1.
```

You type e.g. `X.1`. The driver delegates to a subagent and runs the gate. On
green it **presents the commit and waits for your approval**, commits once you
approve, verifies the commit landed, then re-shows the menu with `[✓]` for the
completed phase. Repeat until all four are checked.

### Example — X.1 cycle

```
driver → subagent (backend-agent.md):
  "Implement phase X.1 for HU-06 in identity-access-service.
   Write code only. Do not commit, do not touch Linear."

driver runs gate:
  dotnet build → exit 0 ✓
  unit tests for new domain types ✓

driver presents commit, waits for your approval:
  feat(identity-access): phase X.1 — domain layer (HU-06)
  Ref: HU-06 / Ref: DES-13 / Ref: DES-67
  → you approve

driver commits + verifies it landed (git log shows "phase X.1")

→ menu re-displayed: [✓] X.1  [ ] X.2  [ ] X.3  [ ] X.4
```

### Gate failure example (driver retries once, then stops)

```
driver runs X.2 gate → dotnet build fails
driver → same subagent:
  "Gate failed: <exact error output>. Fix without touching the gate."
subagent fixes → driver reruns gate → passes ✓
driver presents commit → you approve → driver commits phase X.2 → menu re-displayed

(If gate failed again: hard stop, driver reports both failures, waits for you)
```

### Phase X.4 — API layer + coverage gate

```
driver → subagent: implement X.4

driver runs coverage gate (ADR-0005):
  backend/scripts/cover-gate.sh tests/UnitTests/<Proj>.csproj \
                                tests/IntegrationTests/<Proj>.csproj
  (script owns /p:Threshold=95 + the MergeWith chain + sandbox env vars)
  exit 0 ✓

driver runs cover.sh (report only, coverage already enforced):
  backend/scripts/cover.sh identity-access-service
  → Summary.txt: Line coverage: 96.2%

driver presents commit → you approve → driver commits phase X.4
→ all four [✓] — driver proceeds to docker rebuild
```

### Docker rebuild + curl smoke (driver, not you)

```bash
docker compose build identity-access-service
docker compose up -d identity-access-service
docker compose build api-gateway
docker compose up -d api-gateway

curl -s -X <METHOD> http://localhost:5002/api/<new-endpoint> \
  -H "X-User-Id: admin-1" \
  -H "X-User-Role: Administrator" \
  -H "X-User-Email: admin@umbral.local"
# → expected status code ✓
```

---

## Stop 2 — Driver reports, you review

The driver stops and outputs something like:

```
─── Stop 2 — Backend complete, awaiting your review ──────────────

Phases:    X.1 Domain ✓  X.2 Application ✓
           X.3 Infrastructure ✓  X.4 Api ✓
Coverage:  96.2% line (≥95% gate passed)
Endpoints: PATCH /api/users/{id}/something → 204 ✓
           GET   /api/...                  → 200 ✓

Acceptance criteria (from DES-13):
- <criterion 1>
- <criterion 2>
- <criterion 3>

New API contract for frontend:
  PATCH /api/users/{id}/something
    body: { "field": "<value>" }
    success: 204
    errors: 400 (validation), 403 (not admin), 422 (domain rule)

─── Close-out commands (run after frontend is done) ──────────────

# 1. Squash all phase commits
git -C ../umbral-hu-06 reset --soft \
  "$(git -C ../umbral-hu-06 merge-base develop HEAD)"
git -C ../umbral-hu-06 commit -m "feat(identity-access): <hu-06-slug> (HU-06)

Ref: HU-06
Ref: DES-13
Ref: DES-67"

# 2. Open draft PR
gh pr create --draft --base develop \
  --title "feat: <hu-06-title> — HU-06" \
  --body "Closes DES-13
Ref: DES-67

Touched: backend/services/identity-access-service/, frontend/"

# 3. Move HU to Done in Linear (after acceptance criteria verified)
#    [Linear MCP — move DES-13 to Done]

# 4. Remove worktree
git worktree remove ../umbral-hu-06
──────────────────────────────────────────────────────────────────

Frontend slice is at Step 9 of prompt_example_feature_hu06.md.
Tell me when to run the close-out commands.
```

Check the API contract. Curl the endpoints yourself if you want. When you're
satisfied, move on to the frontend.

---

## You — Frontend slice (manual)

Open `backend/docs/prompt_example_feature_hu06.md`. Go to **Step 9**.

Paste the frontend prompt into a new session pointing at `@frontend/AGENTS.md`.
The frontend agent implements against the live backend endpoints. You run this
session the same way you ran HU-02 and HU-03 frontend sessions manually.

---

## Close-out (you decide when)

Once the frontend is done and you've verified acceptance criteria end-to-end,
go back to the driver session and tell it:

```
Run the close-out commands.
```

The driver runs the four commands in order: squash → PR → Linear → worktree
cleanup. It confirms each one succeeded and gives you the PR URL.

---

## What you type in total

| When                | You type                                                                                                   |
| ------------------- | ---------------------------------------------------------------------------------------------------------- |
| Session 1           | `Read @backend/.agents/generator-agent.md. Run generator-agent for HU-06 DES-13.`                          |
| Stop 1              | Review files, fix anything wrong, then open Session 2                                                      |
| Session 2           | `Read @backend/.agents/driver-agent.md. Run driver-agent for backend/docs/prompt_example_feature_hu06.md.` |
| Phase menu × 4      | `X.1` → `X.2` → `X.3` → `X.4` (one reply per phase)                                                        |
| Commit approval × 4 | Approve each phase's commit when the driver presents it (one per phase)                                    |
| Stop 2              | Review API contract report                                                                                 |
| Frontend session    | Paste Step 9 from the prompt file into a new session with `@frontend/AGENTS.md`                            |
| Close-out           | `Run the close-out commands.` in the driver session                                                        |

Roughly a dozen interactions for a full HU — four phase picks plus four commit
approvals are the bulk of it.

---

## If a gate fails

The driver surfaces the failure and waits. You don't need to act immediately —
come back when you're ready. The worktree stays intact. Restart the driver
session pointing at the same prompt file and it will pick up from where the
gate failed (pre-flight will detect the existing worktree and ask whether to
reuse it).

---

## Agent docs

| Agent                        | File                                 |
| ---------------------------- | ------------------------------------ |
| Generator                    | `backend/.agents/generator-agent.md` |
| Driver                       | `backend/.agents/driver-agent.md`    |
| Phase implementation         | `backend/.agents/backend-agent.md`   |
| Architect (boundaries, ADRs) | `backend/.agents/architect-agent.md` |
