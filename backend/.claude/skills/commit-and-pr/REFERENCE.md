# PR Body Template

```markdown
## Summary

Implements HU-XX (DES-Y): [hu title] — [one-line description of what was built and why it matters].

**[service-name]**
- `[Aggregate]` with `Method()`/`Method()`, emits `EventName`
- `[Command]` / handler — [what it does, key guard]
- `[Query]` / handler — [what it returns, who can call it]
- `POST /api/resource` (Role, status), `GET /api/resource` (Role), ...
- `I[Repo]` + `[Repo]` — EF Core, [index or notable config]
- `[MigrationName]` migration — [table/column summary]
- `ProblemDetailsExceptionHandler` — maps [ExceptionType] to [status]

**Frontend (Next.js)** *(omit section if no frontend changes)*
- `app/lib/resource.ts` — API client (`listX`, `getX`, `createX`, `updateX`, `deactivateX`), `cache: 'no-store'`
- `app/actions/resource.ts` — server actions with role guards, `revalidatePath('/dashboard')`
- `app/dashboard/ResourcePanel.tsx` — [what UI does: list, detail, forms; role differences]
- E2E tests — [N lines covering what scenarios]

**Infrastructure** *(omit section if none)*
- ADR-XXXX: [decision title and one-line rationale]
- `docs/decisions/[service].md` — [what was documented]
- `README.md` — [what was added/updated]

## Acceptance criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | [criterion from HU ticket] | ✅ |
| 2 | [criterion from HU ticket] | ✅ |

## Test plan

- [x] Unit tests: [N new] ([breakdown: X domain + Y handler + Z validator]) covering [key scenarios]
- [x] Integration tests: [N new] ([breakdown: X repository + Y API endpoint]) covering [key scenarios]
- [x] Frontend E2E: [N lines covering role visibility, CRUD flows, error handling] *(omit if none)*
- [x] All existing tests continue to pass

## References

- Closes HU-XX (DES-Y)
- Ref: DES-67 (PRD)
- [ADR-XXXX: title] *(omit if none)*
- Builds on `feature/hu-XX-slug` (DES-Z) *(omit if none)*
```

## Verb Reference

| Situation | Verb |
|-----------|------|
| New feature tied to a HU | `feat` |
| Change/improvement to existing code | `update` |
| Bug fix | `fix` |
| Documentation only | `docs` |
| ~~Tooling/maintenance~~ | ~~`chore`~~ → use `update` |

## Layer Names (for phase commits)

| Layer | Label to use |
|-------|-------------|
| Domain entities, value objects, events, exceptions | `domain layer` |
| Commands, queries, handlers, behaviours, validators | `application layer` |
| EF Core, repositories, migrations, external services | `infrastructure layer` |
| Endpoints, DI wiring, middleware | `api layer` |

## Branch Naming (git-flow)

Feature branches: `feature/hu-XX-slug` (e.g. `feature/hu-05-participant-team-assignment`)
