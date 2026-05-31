---
name: split-commits-git-flow
description: Analyzes the current git diff, separates unrelated work into reviewable commits, and recommends Git Flow branch and commit commands. Use when the user asks to split current changes into commits, prepare commit batches, suggest `git add` and `git commit -m` commands, clean up a dirty worktree, or organize work by branch/ticket.
---

# Split Commits Git Flow

Turn the current working tree into a small set of coherent commits, then present the exact commands needed to create them.

## Outcomes

- Group changes by behavior, risk, and deployability rather than by file type.
- Align the work with Git Flow when the repository supports it.
- Detect when the current branch is wrong before suggesting staging or commit commands.
- Output suggestions in the required format:

```sh
# Title of commit
git checkout <target-branch>
git add path1 "path 2"
git commit -m "triggerLinearVerb (LIN-123): commit message"
```

Omit `git checkout <target-branch>` only when the user is already on the correct branch.

## Workflow

1. Inspect branch state, staged vs unstaged changes, and changed files before making any recommendation.
2. Determine whether the repo actually uses Git Flow conventions or only partially resembles Git Flow.
3. Split the diff into the smallest safe commit set where each commit has one reason to exist.
4. For each commit, name the correct target branch first, then list exact `git add` commands, then the `git commit -m` command.
5. If changes are mixed across branches or a partial commit would be unsafe, say so plainly and explain the constraint before suggesting commands.

## Branch rules

- Verify the current branch instead of assuming it.
- If Git Flow branches exist, map work to them:
  - `feature/*` for new capability from `develop`
  - `release/*` for release hardening from `develop`
  - `hotfix/*` for urgent production fixes from `main`
- If `develop` or the expected Git Flow branch family does not exist, state that explicitly and fall back to the repo's observable branch strategy.
- If the user is on the wrong branch, suggest `git checkout <base>` and `git checkout -b <target-branch>` or `git checkout <target-branch>` before any `git add`.

This repository currently appears to expose `main` but no visible `develop`, `feature/*`, `release/*`, or `hotfix/*` branches, so future invocations must verify whether Git Flow is active before prescribing it.

## Output contract

- Use one markdown heading per suggested commit.
- Keep headings human-readable, not branch names.
- Prefer imperative commit subjects after the Linear prefix.
- Preserve the user's requested message shape exactly: `triggerLinearVerb (LIN-123): commit message`.

See [REFERENCE.md](REFERENCE.md) for the full decision process and [EXAMPLES.md](EXAMPLES.md) for output patterns.
