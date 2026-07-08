---
name: git-graph-merge
description:
  Use when merging a feature branch or PR and the git-flow branch graph (the
  "mountains" — merge commits with an arc back to the feature branch) must be
  preserved. Covers why "Squash and merge" flattens history, how to tidy a
  branch locally before merging, and how to land it with a merge commit so the
  arc survives. Reach for it whenever the user asks to keep a linear history
  from flattening, merge without squashing, or clean up commits before a PR.
---

# Git Graph Merge

## Overview

Git-flow's readable graph comes from **merge commits**: each PR lands as a
commit with two parents, and that second parent draws the arc back to the
feature branch — the "mountain" shape. GitHub's **"Squash and merge"** button
destroys this: it collapses the branch into one commit **and** fast-forwards it
onto the base, leaving a flat `commit → commit → commit` line with no arcs.

The fix is to squash the *noise* **yourself, locally**, then land the PR with a
**merge commit**. Two different squashes — don't confuse them:

| Action | Arc in graph? | Effect |
|--------|:---:|--------|
| GitHub **"Squash and merge"** button | ❌ | squashes *and* fast-forwards → flat history |
| Squash **locally** (`rebase -i`), then merge-commit | ✅ | clean commits *under* the merge arc |

## Repo setting (one-time)

The base branch must offer merge commits. Enforce it so nobody can flatten by
accident:

```
gh repo edit --enable-merge-commit=true --enable-squash-merge=false --enable-rebase-merge=false
```

Verify: `gh repo view --json mergeCommitAllowed,squashMergeAllowed` →
`mergeCommitAllowed: true`, `squashMergeAllowed: false`.

## Workflow

### 1. Tidy the feature branch (only if it has WIP noise)

Skip this step if the commits are already meaningful. Otherwise squash the
`wip` / `fix typo` / `oops` commits into real ones:

```
git checkout feature/hu-40-whatever
git rebase -i develop
```

In the editor, keep the first commit as `pick` and mark the noise commits
`squash` (keep their text) or `fixup` (discard it). Reword the result to a
[conventional-commits](../conventional-commits/SKILL.md) message. Squash all the
way to a single commit if you want one clean commit under each mountain.

### 2. Push the rewritten branch

Rewriting history means the push is non-fast-forward, so force is required. It
is **safe** here because a feature branch is yours alone — never do this to a
shared branch:

```
git push --force-with-lease
```

### 3. Merge the PR with a merge commit

On GitHub choose **"Create a merge commit"** (the only option once squash is
disabled). Result:

```
*   Merge pull request #40 from feature/hu-40-whatever    ← the arc
|\
| * feat(hu-40): add whatever                             ← your clean commit(s)
|/
* previous develop commit
```

## Keeping feature branches current

When a feature branch falls behind the base and you want the extra
`Merge branch 'develop' into feature/…` arcs, sync with a **merge**, not a
rebase:

```
git checkout feature/whatever
git merge develop        # rebase would flatten the sync into the branch
```

## Pitfalls

- **Never `--force` push a shared branch** (`develop`, `main`). Force is only
  ever for your own feature branch. Use `--force-with-lease`, not `--force`.
- **Already-squashed history can't be un-flattened.** Squash-merge discarded the
  topology; the arcs are gone for good. This skill only fixes merges *going
  forward* — leave past history alone.
- **The GitHub button is the trap.** "Squash and merge" and "Rebase and merge"
  both flatten. Only "Create a merge commit" keeps the arc.
