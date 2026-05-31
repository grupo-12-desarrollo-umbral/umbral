---
name: commit-and-pr
description: Generates standardized commit messages and PR titles/bodies for this project following its git-flow conventions (feat(hu-XX), phase commits, update/fix/docs verbs, Linear DES IDs). Use when the user wants to write a commit message, prepare a PR, format a git commit, create a pull request description, or mentions HU numbers, DES IDs, or phase commits.
---

# commit-and-pr

Generates commit messages and PR descriptions following the project's conventions.

## Workflow

1. Read `git diff --stat` and `git branch --show-current` to understand what changed and which branch you're on.
2. Ask the user which output they want: **commit message** or **PR description**.
3. Collect the required context (see inputs per type below).
4. Generate the output and present it for review — do not run `git commit` or `gh pr create` unless the user explicitly asks.

## Commit Messages

### Inputs to collect
- HU number (e.g. `hu-05`)
- Is this a **phase commit** (mid-branch, per layer) or a **squash commit** (final, merging into develop)?
- If phase: service name, phase number (e.g. `X.3`), layer (`domain` / `application` / `infrastructure` / `api`)
- If neither: which verb applies (`update` / `fix` / `docs`)

### Formats

**Squash commit** (final merge into develop):
```
feat(hu-XX): user story name from linear backlog
```
Example: `feat(hu-05): participant team assignment`

**Phase commit** (mid-branch, per layer):
```
feat(service-name): phase X.N — layer name (hu-XX)
```
Example: `feat(identity-access): phase X.3 — infrastructure layer (hu-03)`

**Other verbs** (non-feature work):
```
update: short description
fix: short description
docs(scope): short description
```

### Rules
- Never add `Co-Authored-By` trailers.
- Never use `chore:` — use `update:` instead.
- Keep the subject line under 72 characters.
- Body bullets (if any) explain *why*, not *what*.

## PR Descriptions

### Inputs to collect
- HU number (e.g. `hu-04`)
- HU title (from Linear backlog)
- Linear DES ID (e.g. `DES-8`)
- Previous HU this builds on (if any)
- Acceptance criteria (from the HU ticket)
- Test counts (unit / integration / E2E)

### Title format
```
feat(hu-XX): hu title — DES-Y
```
Example: `feat(hu-04): team registration and maintenance — DES-8`

### Body template

See [REFERENCE.md](REFERENCE.md) for the full PR body template with all sections.

## Tips
- Infer the service name from the branch name or changed file paths.
- If the diff touches both backend and frontend, include both sections in the PR body.
- If acceptance criteria aren't provided, leave the table rows as `[ criterion ]` placeholders.
