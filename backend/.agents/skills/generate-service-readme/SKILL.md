---
name: generate-service-readme
description: Generate or rewrite a backend microservice README as professional API documentation, following the canonical section structure used across Umbral services (identity-access-service is the reference). Use when the user wants to document a microservice, write/improve a service README, document the API of a service, or asks for endpoint/config/error documentation for a backend service.
---

# Generate Service README

Produces a professional, accurate README for a backend microservice. Every fact MUST be read from the service's source — never invented or copied from a stale README. The output follows a fixed section order so all services read alike.

## Core rule: source the facts first, write second

Do **not** trust an existing README — they go stale (wrong response shapes, wrong auth, missing endpoints). Read the code, then document what is actually there. When code and the old README disagree, the code wins.

## Workflow

1. **Confirm scope and language.** Default language matches the repo's existing READMEs (Spanish for Umbral). Ask only if ambiguous.
2. **Map the service.** Read the directory tree under `src/` and `tests/`. Identify the Clean Architecture layers and the endpoint files.
3. **Gather facts from source** using the sourcing map below. Record exact request/response shapes from the DTO/record definitions, exact auth from `[Authorize]` attributes and endpoint policies, and the exact exception→status mapping.
4. **Verify against the old README** (if any): list every discrepancy you correct (shapes, auth roles, missing/removed endpoints, config). Mention these corrections to the user.
5. **Write the README** following [references/template.md](references/template.md) section order. Drop sections that don't apply (e.g. no Keycloak sync) rather than padding them.
6. **Avoid time-sensitive rot.** Do not hardcode coverage percentages, test counts, or commit hashes. Point at the command (`make gate SVC=...`) instead, or use a threshold (`>= 90%`).
7. **Review with the user.** Summarize corrections made and ask what to adjust.

## Sourcing map — where each fact lives

| Section | Read from |
|---------|-----------|
| Config / env vars | `Program.cs`, `*/DependencyInjection.cs`, `*Options.cs` (defaults), `docker-compose.yml` (service block), design-time `*DbContextFactory.cs` (migration env var). Note if there is **no `appsettings.json`**. |
| Connection string name | `Persistence/DependencyInjection.cs` → `GetConnectionString("...")` |
| Ports | `docker-compose.yml` `ports:` mapping + `ASPNETCORE_URLS` |
| Run commands | repo `Makefile` targets (`build`/`test`/`gate`/`ef` with `SVC=`), `Dockerfile`, `scripts/` |
| Auth (cabeceras de confianza) | `Api/DependencyInjection.cs` (auth scheme + policies), `Api/Services/AuthorizationPolicies.cs` |
| Endpoints + routes | `Api/Endpoints/*Endpoints.cs` (`MapGroup`, `MapPost/Get/...`, `.RequireAuthorization(...)`) |
| Per-endpoint auth role | `[Authorize(Roles = "...")]` on the matching Command/Query class (this is authoritative, not the endpoint file) |
| Request shapes | the `record ...Request(...)` in the endpoint file |
| Response shapes | the returned `*Dto` / `*Result` record definitions (serialized **camelCase**) |
| Domain model | `Domain/Entities`, `Domain/Enums` (Role, ProtectedCapability), `Domain/Services`, `Domain/Events` |
| Error mapping | `Api/Services/ProblemDetailsExceptionHandler.cs` (the `switch` arms → status codes) |
| Integrations | infrastructure services (e.g. `KeycloakAdminService.cs`) |
| Persistence | `Persistence/DependencyInjection.cs` (repositories, interceptors), `Program.cs` (auto-migrate) |
| ADRs | `backend/docs/adr/` referenced by the service |

## Accuracy checklist (verify before delivering)

- [ ] Every documented endpoint exists in an `*Endpoints.cs` file; no endpoint is missing.
- [ ] Each endpoint's auth matches the `[Authorize]` on its handler (not assumptions).
- [ ] Request/response JSON keys match the record fields, in `camelCase`; nested DTOs are nested (not flattened).
- [ ] Config table lists every env var actually read, with real defaults and whether it's required.
- [ ] Error table matches the exception handler's switch exactly.
- [ ] No invented fields, no leftover stale facts, no hardcoded coverage numbers/test counts.
- [ ] Language is consistent with the repo's other READMEs.

See [references/template.md](references/template.md) for the full section skeleton.
