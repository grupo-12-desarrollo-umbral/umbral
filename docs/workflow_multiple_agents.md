# Workflow — Multiple Agents in Parallel (Git Worktrees)

Coordination strategy when two dependent HUs run on separate agents in parallel.
Extends [backend/docs/workflow_for_prompts.md](../backend/docs/workflow_for_prompts.md)
for the multi-agent case.

## When to use

Parallel agents when:

- Two HUs are **structurally dependent** (HU-05 needs HU-04's aggregate)
- The dependency is **asymmetric** (A blocks B's infra/migrations but not B's domain)

Do **not** use when:

- HUs are independent (run them sequentially)
- The dependency is symmetric (both need each other's types — impossible in DDD)
- Both agents would need to add EF migrations at the same time

---

## Session-by-session playbook

Replace HU-04/HU-05 with your actual HUs. Each session is what you type at the CLI.

---

### Session 1 — Orient

Type this prompt:

```
Read @backend/services/identity-access-service/README.md,
@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md,
and use Linear MCP to check DES-8 and DES-9 status.

Output:
- what HU-01/02/03 have already landed (User, Role, AccessPolicy, etc.)
- what HU-04 adds per PRD (CRUD team)
- what HU-05 adds per PRD (participant-to-team assignment)
- current Linear status and labels for DES-8 and DES-9

Do not plan or implement anything.
```

Read the output. This is your reusable orient summary.

---

### Session 2 — Generate context file

Type this prompt (paste in the orient output from Session 1):

```
Based on this orient summary:

<paste-orient-output>

Create backend/docs/hu04-context.md following the pattern in backend/docs/hu03-context.md:
a standalone reference doc listing what predecessors landed, what HU-04 adds,
touched surfaces, branch name, and gotchas.

Then create backend/docs/hu05-context.md with the same structure for HU-05.
```

---

### Session 3 — Generate prompt templates (CLAUDE)

Type this prompt:

```
Read @backend/docs/hu04-context.md and @backend/docs/hu03-context.md.

Create backend/docs/prompt_example_feature_hu04.md following the pattern
in backend/docs/prompt_example_feature_hu03.md — a full per-phase prompt sequence
with pre-resolved orient, phase X.1–X.4, close out, and PR command.

Then do the same for HU-05, creating backend/docs/prompt_example_feature_hu05.md
based on backend/docs/hu05-context.md.

HU-05's branch must branch FROM feature/hu-04-team-registration (not develop),
and its infrastructure phase must not run dotnet ef migrations add until
HU-04's AddTeams migration is committed and gated.
```

---

### Session 4 — Create coordination overlay (CLAUDE)

Type this prompt:

```
Read @backend/docs/prompt_example_feature_hu04.md and
@backend/docs/prompt_example_feature_hu05.md.

Create docs/hu04-hu05-best_parallel_prompt.md that merges both into one document with:

1. A sync rule table at the top:

   | HU-04 phase done          | HU-05 may start                     |
   |---------------------------|-------------------------------------|
   | X.1 Domain committed      | Domain (Y.1)                        |
   | X.2 Application committed | Application (Y.2)                   |
   | X.3 committed AND gated   | Infrastructure (Y.3) + migration    |
   | X.4 API committed         | API (Y.4)                           |

2. A boundary decision block shared by both agents (preventing aggregate mirroring
   between Identity's Team and SessionOperations' Team)

3. The full HU-04 prompt section, then the full HU-05 prompt section

4. Migration sync point at Y.2: "Stop here. Confirm HU-04's AddTeams migration
   is committed AND gated before proceeding"

5. Branch/worktree setup instructions

6. A design notes table documenting each guard and why it matters
```

---

### Session 5 — Set up worktrees

Run these commands yourself (from the main repo, on `develop`):

```bash
# Create HU-04's branch WITHOUT switching the main worktree
git branch feature/hu-04-team-registration develop
git worktree add ../umbral-hu-04 feature/hu-04-team-registration

# Create HU-05's branch — FROM HU-04's branch, not develop
git branch feature/hu-05-participant-team-assignment feature/hu-04-team-registration
git worktree add ../umbral-hu-05 feature/hu-05-participant-team-assignment
```

Now you have two directories:

```
../umbral-hu-04  → feature/hu-04-team-registration
../umbral-hu-05  → feature/hu-05-participant-team-assignment
```

Then **commit or copy** the prompt file and support docs to each worktree so agents can reference them:

```bash
git add docs/hu04-hu05-best_parallel_prompt.md docs/archive/ backend/docs/
git commit -m "docs: context files for parallel workflow"
# The commit is now on develop — rebase each worktree to pick it up:
cd ../umbral-hu-04 && git rebase develop && cd ../umbral-hu-05 && git rebase develop
```

---

### Pre-flight before any phase

Before starting a phase in a worktree, run a quick check so the agent builds on
a known-clean base:

```bash
# 1. Check existing entity conventions before writing code
#    (e.g. UserId might be int not Guid, audit fields might be Created/LastModified)
grep -n "public int Id\|public Guid Id\|public int UserId\|public Guid UserId" \
  backend/services/identity-access-service/src/Domain/Entities/*.cs

# 2. Verify the build is green before the agent touches anything
dotnet build backend/services/identity-access-service/src/Application/Application.csproj --no-restore 2>&1 | tail -3
```

If the build fails, fix it before starting the agent — otherwise half the
session's tokens will be spent debugging pre-existing breakage.

### Sessions 6-N — Run agent A (HU-04)

In the `../umbral-hu-04` worktree, start a **new session** for each phase.
After each phase the agent commits and runs `/debrief`.

**Phase X.1 — Domain layer:**
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-04 section phase by phase.
Start with phase X.1 — Domain layer.
```

**Phase X.2 — Application layer:**
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-04 section phase by phase.
Start with phase X.2 — Application layer.
```

**Phase X.3 — Infrastructure layer:**
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-04 section phase by phase.
Start with phase X.3 — Infrastructure layer.
```

**Phase X.4 — API layer:**
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-04 section phase by phase.
Start with phase X.4 — API layer.
```

**Phase X.5 — Frontend:**
```
Read @docs/hu04-hu05-best_parallel_prompt.md and @frontend/AGENTS.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-04 frontend slice.
```

---

### Sessions 6-N — Run agent B (HU-05)

In the `../umbral-hu-05` worktree, start a **new session** for each phase.
After each phase the agent commits and runs `/debrief`.

**Phase Y.1 — Domain layer** (may start after X.1 is committed):
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-05 section phase by phase.
Start with phase Y.1 — Domain layer.
```

**Phase Y.2 — Application layer** (may start after X.2 is committed; before
starting, rebase onto `feature/hu-04-team-registration` to pick up `ITeamRepository`):
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-05 section phase by phase.
Start with phase Y.2 — Application layer.
```

**Stop at the migration sync point** (after Y.2). Wait for HU-04's X.3
to be committed and gated before continuing.

**Phase Y.3 — Infrastructure layer** (only after X.3 is committed AND gated;
rebase first to pick up the `AddTeams` migration):
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-05 section phase by phase.
Start with phase Y.3 — Infrastructure layer.
```

**Phase Y.4 — API layer** (only after X.4 is committed):
```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-05 section phase by phase.
Start with phase Y.4 — API layer.
```

**Phase Y.5 — Frontend** (only after Y.4 is committed):
```
Read @docs/hu04-hu05-best_parallel_prompt.md and @frontend/AGENTS.md.
Trust the "Boundary decision" block — it is authoritative over any other doc.
Implement the HU-05 frontend slice.
```

---

### Final session — Close out

When HU-04 finishes:

```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Verify HU-04 acceptance criteria. Open draft PR.
```

When HU-05 finishes and HU-04's PR has merged to develop:

```bash
cd ../umbral-hu-05
git rebase develop
```

Then:

```
Read @docs/hu04-hu05-best_parallel_prompt.md.
Verify HU-05 acceptance criteria. Open draft PR against develop.
```

---

## Visual timeline

```
Agent A (HU-04) :  X.1──X.2────X.3───────X.4───[PR]
                     │    │      │                 
Agent B (HU-05) :    Y.1──Y.2──[wait]──Y.3──Y.4──[rebase]──[PR]
                     ▲      ▲            ▲
                Y.1 after  Y.2 after  only after
                X.1        X.2        X.3 gated
```

## One-sentence rule

If the agent ever asks "should I wait?" — check the sync table at the top of
`hu04-hu05-best_parallel_prompt.md`. If the prerequisite column says "committed AND gated"
and the dependency hasn't reached that state yet, **wait**.
