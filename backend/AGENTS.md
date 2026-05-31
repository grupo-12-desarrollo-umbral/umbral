# Agent Instructions

## Implementation work

Before writing any service code, read `.agents/backend-agent.md`. It defines the canonical documents to load, layer rules, verification gates, and constraints. Do not start implementation without it.

## Commit & PR Standards

When the user asks to commit changes or create a PR, always invoke the `/commit-and-pr` skill before running any `git commit` or `gh pr create` commands.

## Structure Enforcement

Before creating or moving any file, read `structure.md` and place it
according to the Concrete Target Tree and DDD/Boundary rules.

Do not invent new folder paths outside the established structure
without updating `structure.md` first.
