---
name: conventional-commits
description:
  Use when writing, reviewing, or validating git commit messages or PR titles
  that must follow Conventional Commits — e.g. the user asks to commit, craft a
  commit message, fix a message format, or another skill references Conventional
  Commits. Covers the required `type(scope): description` shape, the allowed
  type list, scope, breaking-change syntax, and examples.
---

# Conventional Commits

## Overview

A commit-message format spec for messages that are easy to review and parseable
by tooling. Canonical reference: <https://www.conventionalcommits.org/>.

**Core shape:**

```
type(scope): description

[optional body]

[optional footer(s)]
```

A message that does **not** match this shape is **invalid** — rewrite it before
committing.

## When to use

- Writing or rewriting a git commit message.
- Writing a PR title (applying Conventional Commits to titles is a common
  convention).
- Validating that an existing message follows the format.
- When a workflow skill (e.g. `safe-pr-creator`) requires Conventional Commits.

## Allowed types

| Type | Use for |
|------|---------|
| `feat` | A new feature |
| `fix` | A bug fix |
| `docs` | Documentation only |
| `style` | Formatting, whitespace, semicolons — no production code change |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf` | Code change that improves performance |
| `test` | Adding or correcting tests |
| `build` | Build system or external dependencies |
| `ci` | CI configuration files and scripts |
| `chore` | Other changes that don't modify `src` or test files |
| `revert` | Reverting a previous commit |

Use the most specific type that fits. When in doubt, `feat`/`fix` cover most
user-facing changes.

## Scope

`scope` is optional but encouraged. It identifies the area of the change (a
module, component, or package):

```
feat(auth): add OAuth login
fix(api-gateway): retry on 503
```

Omit the parentheses if you skip scope: `feat: add dark mode`.

## Breaking changes

Signal a breaking change in one of two ways:

1. **`!` after the type/scope** (terse signal):
   ```
   feat(api)!: rename getUser to fetchUser
   feat!: drop support for Node 16
   ```
2. **`BREAKING CHANGE:` footer** (use when you need to explain the migration):
   ```
   feat(api): rename getUser to fetchUser

   BREAKING CHANGE: `getUser` is now `fetchUser`. Update all callers.
   ```

## Body (optional)

Blank line after the header, then explain **what** and **why** (not an
implementation diary):

```
fix(signals): debounce resize handler

The handler fired on every animation frame during drag, causing layout
thrash. Debounce at 16ms to coalesce to one call per frame.
```

## Footer (optional)

Blank line after the body. Common footers: `BREAKING CHANGE:`, `Closes #123`,
`Refs #456`.

```
feat(checkout): add Apple Pay

Adds Apple Pay as a payment option for iOS users.

Closes #482
```

## Validation checklist

Before committing, confirm:

- [ ] Header matches `type(scope): description` (or `type: description` without scope).
- [ ] `type` is one of the allowed types above.
- [ ] Subject is lowercase, imperative mood, no trailing period.
- [ ] Breaking changes use `!` and/or a `BREAKING CHANGE:` footer.
- [ ] Body/footer (if present) are separated from the header by blank lines.

## Common mistakes

| Mistake | Fix |
|---------|-----|
| `Updated the auth module` | No type. Rewrite as `fix(auth): ...` or `refactor(auth): ...` |
| `Fix: login bug` | Capital `Fix`, wrong syntax. Use `fix(auth): ...` |
| `feat: Added new endpoint.` | Past tense + trailing period. Use `feat(api): add endpoint` |
| `FEAT: thing` | Uppercase type. Types are lowercase. |
| Breaking change with no signal | Add `!` after type/scope or a `BREAKING CHANGE:` footer. |
| Body crammed onto the header line | Insert a blank line between header and body. |
