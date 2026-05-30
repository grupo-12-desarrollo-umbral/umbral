---
name: debrief
description: Write a structured decision log entry after finishing a phase or meaningful chunk of work. Captures what was built, why, what was skipped, which HU tickets were advanced, and what the next session needs to know.
---

# Debrief

Append a structured entry to the target decisions file capturing the context from this session before it evaporates.

## When to run

After completing a meaningful chunk of work — a phase, a feature, a significant refactor, or any session where non-obvious decisions were made. Do not run for trivial changes (typos, renames, config tweaks).

## Process

### 1. Gather context

Run these in parallel:

```bash
git log --format="%h %ad %s" --date=short -20
git diff HEAD~1..HEAD --stat
```

Infer the affected service from the diff. If changes span multiple services or touch shared infrastructure, treat it as cross-cutting.

Extract the HU ticket IDs from the `Ref:` field of the most recent commit(s) — these are the user stories this session advanced.

### 2. Determine the target file

- Single service → `docs/decisions/<svc>.md`
- Cross-cutting (multiple services, shared infra, architecture decisions) → `docs/decisions.md`

Read the last 3 entries of the target file to find the last entry number (for incrementing). Do not read the full file.

### 3. Run a quick build check

Run `dotnet build` on the most recently touched service. If it fails, prepend a `⚠️ BUILD FAILING` line at the top of the entry — do not skip writing the entry.

### 4. Write the entry

Append to the target file. Create the file if it doesn't exist.

Entry format:

```markdown
---

## [NNN] <Short title — what this session produced>
**Date:** YYYY-MM-DD
**Phase:** X.Y — <layer name>
**Commits:** <hash1>, <hash2>
**HU tickets advanced:** HU-XX, HU-YY, ...

**What was built**
One or two sentences. Concrete — name the classes, layers, or behaviors added.

**Why this approach**
The reasoning that won't be obvious from reading the code. Tradeoffs accepted, alternatives rejected, constraints that shaped the design.

**Deliberately skipped**
What was in scope but left out, and why. "Not yet" decisions that future sessions need to know about.

**Next session needs to know**
The single most important piece of context for whoever picks this up next. If nothing is critical, write "Nothing blocking."
```

NNN is zero-padded to 3 digits (001, 002, …). Increment from the last entry in the target file independently — each file has its own counter.

### 5. Reading context at session start

When asked to catch up or resume work on a service, read only the **last 3 entries** of `docs/decisions/<svc>.md`. Do not read the full file unless explicitly asked. If uncertain which service, list the files in `docs/decisions/` so the user can pick one.

### 6. Report back

Output only:

```
Debrief written → docs/decisions/<svc>.md [NNN]
HU tickets: HU-XX, HU-YY, ...
```

Or for cross-cutting:

```
Debrief written → docs/decisions.md [NNN]
```

If the build was failing, prepend:

```
⚠️ Build failing on <service> — fix before next session.
```

Output nothing else.
