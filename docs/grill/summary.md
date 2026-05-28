# Grill Session Summary — Backend Workflow
**Date:** 2026-05-28

Grilled the end-to-end backend implementation workflow. All decisions below are now reflected in `docs/current_workflow.md`, `docs/agent-skills-reference.md`, `.agents/backend-agent.md`, `.agents/skills/to-prd/SKILL.md`, and `.agents/skills/to-issues/SKILL.md`.

---

## Decisions reached

### 1. Issue tracker routing

`/to-prd` targets **Linear only** (+ local copy). `/to-issues` targets **GitHub only**.

- `/to-prd` → publishes `DES-XX` to Linear (team `umbral-equipo-12`) + saves `docs/prd/DES-XX-<slug>.md` locally
- `/to-issues` → creates GitHub issues via `gh issue create` on the current repo — never Linear

**Why:** Linear is the product board (HU user stories + PRDs). GitHub Issues are implementation-level tracking. Mixing them pollutes both views.

---

### 2. GitHub issue numbers in commits — no `Advances:` keyword

Drop `Advances:` — it implies automation that doesn't exist. Use `Ref: #N` throughout all phase commits. `Closes:` goes in the **PR description** of phase 1.4, not in the commit message.

```
Ref: HU-09, HU-10A, ...   ← Linear (all phases)
Ref: #1, #2, #3            ← GitHub (all phases)
```

Phase 1.4 PR description:
```
Closes #1, Closes #2, Closes #3
```

**Why:** `Closes:` in commit messages only auto-closes when the commit lands on the default branch directly. In git-flow, commits land via PR — so `Closes:` must be in the PR description to trigger auto-close reliably.

---

### 3. HU tickets move to Done only when acceptance criteria are verified

Phase 1.4 gate passing is necessary but not sufficient. Each HU ticket's acceptance criteria must be independently confirmed before moving it to Done.

**Why:** Phase 1.4 could pass (API responds, build green) while some HU stories remain partially unimplemented — e.g. TriviaQuiz HUs if only Mission was implemented.

---

### 4. One feature branch per service, one draft PR after phase 1.4

All 4 phases commit to a single feature branch. One draft PR merges everything into `develop` after phase 1.4.

**Why:** Phases 1.1–1.3 are not independently demoable. A single PR keeps the review focused on the complete deliverable.

---

### 5. Git-flow with `develop` as integration branch

```
main        ← production releases only
develop     ← integration branch (created 2026-05-28, pushed to origin)
  └── feature/<service-short-name>
```

Branch naming: `feature/mission-design`, `feature/identity-access`, `feature/scoring-monitoring`, `feature/session-operations`.

Agent creates the branch, commits all 4 phases, opens a **draft PR** to `develop` after phase 1.4.

**Why:** Enforces git-flow discipline. Phases stay off `develop` until the full service layer stack is verified.

---

### 6. `/to-issues` and `/to-prd` skills updated

Both skills were updated with explicit target rules so agents don't need to infer the tracker from context.

---

### 7. DES-63, DES-64, DES-65 canceled and archived

These were AFK slices mistakenly created in Linear instead of GitHub. Canceled in Linear, content preserved in `docs/archive/old-des/`. Recreate as GitHub issues via `/to-issues DES-62`.

---

### 8. PRD local copies in `docs/prd/`

Naming: `docs/prd/DES-XX-<kebab-slug>.md`. Both existing PRDs migrated:
- `docs/prd/DES-62-mission-design-service-baseline.md`
- `docs/prd/DES-66-local-backend-platform-baseline.md`

`previously/` directory removed.

---

### 9. `develop` branch created

```bash
git checkout -b develop main && git push -u origin develop
```

Run once — already done on 2026-05-28.

---

## Files updated in this session

| File | Change |
|---|---|
| `docs/current_workflow.md` | Full workflow rewrite with git-flow, dual Ref:, PR protocol, acceptance criteria gate |
| `docs/agent-skills-reference.md` | Updated tracker routing, git-flow, commit format, skill descriptions |
| `.agents/backend-agent.md` | git-flow branching, dual Ref:, draft PR, acceptance criteria gate |
| `.agents/skills/to-prd/SKILL.md` | Linear + local `docs/prd/DES-XX-<slug>.md` target |
| `.agents/skills/to-issues/SKILL.md` | GitHub-only via `gh issue create` |
| `docs/prd/DES-62-mission-design-service-baseline.md` | Created |
| `docs/prd/DES-66-local-backend-platform-baseline.md` | Created |
| `docs/archive/old-des/DES-63-*.md` | Archived from Linear |
| `docs/archive/old-des/DES-64-*.md` | Archived from Linear |
| `docs/archive/old-des/DES-65-*.md` | Archived from Linear |

## Next actions

1. Run `/to-issues DES-62` to recreate DES-63/64/65 as proper GitHub issues in `umbral-backend`
2. Create PRDs for the 3 remaining services: `identity-access-service`, `scoring-monitoring-service`, `session-operations-service`
3. Cut `feature/mission-design` from `develop` and start phase 1.1
