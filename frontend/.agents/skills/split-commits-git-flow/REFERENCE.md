# Split Commits Git Flow Reference

## Purpose

This skill helps an agent turn a messy working tree into a defensible commit plan. It is general enough for any Git repository, but it should first ground itself in the local repository's actual branch model instead of blindly forcing Git Flow.

## Operating procedure

### 1. Inspect before advising

Always inspect:

- current branch
- local branch list relevant to Git Flow
- `git status --short`
- staged diff summary
- unstaged diff summary
- untracked files

If the repo inspection shows no Git Flow structure, do not fabricate one. Recommend the closest branch strategy the repo already uses and call out the mismatch.

### 2. Decide whether Git Flow applies

Treat Git Flow as active only when branch evidence supports it, such as:

- `develop` exists
- one or more of `feature/*`, `release/*`, or `hotfix/*` exists
- project docs or team instructions explicitly say Git Flow

If Git Flow is not evident:

- say that the repository does not currently expose the expected Git Flow branches
- still organize changes into coherent commits
- use the repo's current branch model as the default recommendation

### 3. Infer the right branch for each commit

Use these heuristics:

- New capability or internal refactor for upcoming work:
  - target `feature/<ticket-or-scope>`
  - base from `develop`
- Release stabilization, version bump, release notes, hardening:
  - target `release/<version-or-scope>`
  - base from `develop`
- Production break/fix, urgent regression, emergency patch:
  - target `hotfix/<ticket-or-scope>`
  - base from `main`
- If the repo lacks `develop`, prefer the dominant integration branch and say why.

When a new branch is needed, recommend both commands in this order:

```sh
git checkout <base-branch>
git checkout -b <target-branch>
```

When the branch already exists but the user is elsewhere, recommend:

```sh
git checkout <target-branch>
```

### 4. Split the changes into commits

Prefer commits that satisfy all of these:

- one user-visible behavior or one internal concern
- compile and test plausibly on their own
- do not hide unrelated formatting or cleanup
- keep infrastructure, domain, API, and tests together when they serve one behavior

Good split signals:

- one use case plus its tests
- one schema change plus the code that depends on it
- one refactor that enables later work
- one documentation update tied to one implementation change

Bad split signals:

- one commit per layer when they implement one feature
- one commit for all tests and another for all code
- mixing renames, refactors, and behavior changes without necessity
- partial staging that leaves a file in a broken or misleading intermediate state

### 5. Detect unsafe commit boundaries

Stop and explain the problem before suggesting commands when:

- the same hunk contains two inseparable concerns
- one commit would not build or would misrepresent intent without another
- generated files changed but their sources are missing
- a migration is separated from the only code that can run against it
- branch choice depends on release vs hotfix context that the repo cannot reveal

In those cases, either:

- propose a safer combined commit, or
- ask the user for the missing release context

### 6. Build the output

For each commit recommendation, output exactly:

```sh
# Title of commit
git checkout <base-branch>
git checkout -b <target-branch>
git add path/to/file1 "path with spaces/file2"
git commit -m "triggerLinearVerb (LIN-123): commit message"
```

Rules:

- If already on the correct branch, omit checkout lines.
- If the branch exists already, use a single `git checkout <target-branch>` line.
- Quote paths that contain spaces.
- Use as few `git add` paths as possible while staying explicit.
- Keep the title readable and short.

### 7. Commit message guidance

Honor the requested structure exactly:

`triggerLinearVerb (LIN-123): commit message`

Where:

- `triggerLinearVerb` is an imperative verb or team-specific automation trigger, such as `implement`, `fix`, `refactor`, or another verb the team expects
- `LIN-123` is the Linear ticket id when known
- `commit message` is a concise imperative subject

If no Linear id is known, do not invent one. Use a placeholder only when the user clearly wants a template, for example `LIN-123`.

### 8. Repository-local note

In this repository snapshot:

- current branch observed: `main`
- visible Git Flow branches observed: none

That means the skill should not assume `develop` exists here. It should verify branch topology every time and only then recommend Git Flow checkout commands.
