# Agent Instructions

## Implementation work

Before writing any service code, read `.agents/backend-agent.md`. It defines the canonical documents to load, layer rules, verification gates, and constraints. Do not start implementation without it.

## Commit & PR Standards

When the user asks to commit changes, always invoke the `/conventional-commits` skill before running `git commit`. When creating a PR, always invoke the `/safe-pr-creator` skill before running `gh pr create`.

## ADR citations

The backend has two ADR sequences — `backend/adr/` and `backend/docs/adr/` — whose numbers
collide on `0001`–`0005`. A bare `ADR-0005` names both the substage-advancement ADR and the
coverage-gate ADR; likewise `ADR-0004` names both ProblemDetails mapping and the required-patterns
ADR. **Cite anything in that range by path**, not by number. `0006`+ exist only under
`backend/docs/adr/` and are unambiguous.

Never invent a sub-anchor like `ADR-0005 D-4` — these ADRs have no numbered decisions; quote the
heading or the sentence instead. A new ADR under `backend/adr/` starts at `0014`, so the collision
stops growing.

## Structure Enforcement

Before creating or moving any file, read `structure.md` and place it
according to the Concrete Target Tree and DDD/Boundary rules.

Do not invent new folder paths outside the established structure
without updating `structure.md` first.

The Application layer organizes **by vertical slice** (ADR-0011): one folder per
use case at `Application/<Area>/{Commands|Queries}/<UseCase>/`, holding the
request, its handler, and its validator. Response DTOs are not co-located: every
command result and query response lives in the central `Application/Dtos/<Area>/`
root, and a command returning `Guid`/`Unit` carries no result DTO at all.
Outbound contracts that no handler returns — SignalR notification payloads,
integration events — are not response DTOs and stay in `<Area>/Common/`.
No `Handlers/` or `Facades/` type-buckets, and no per-area `Dtos/` bucket;
shared helpers and mandated patterns go in `<Area>/Common/`, cross-cutting
concerns in `Application/Common/`. `make -C backend structure-guard` (run
automatically by `build` for converged services) fails the build if the layout
regresses.

## Local dev loop (hot reload)

`docker compose up` auto-loads `docker-compose.override.yml`, which runs every
.NET service from the SDK image with its `src/` bind-mounted under `dotnet watch
run`. Editing a `.cs` file recompiles and restarts that service in place — no
`--build`, no manual rebuild. The frontend/mobile dev servers hot-reload on
their own and only talk to the gateway on `localhost:8000`.

For a production-like build that ignores the override (bakes a Release publish
into each image), run `docker compose -f docker-compose.yml up --build`.

## Toolchain & sandbox

Run the .NET toolchain through the sandbox-hardened Makefile — never call
`dotnet`/`docker` directly:

    make -C backend build SVC=<service>   # compile Api + test projects
    make -C backend test  SVC=<service>   # run unit + integration tests
    make -C backend gate  SVC=<service>   # coverage gate (docs/adr/0005-coverlet-msbuild-*)
    make -C backend structure-guard [SVC=<service>]   # ADR-0011 vertical-slice layout guard
    make -C backend ef    SVC=<service> ARGS="migrations add Foo"
    make -C backend clean-artifacts SVC=<service>   # reclaim foreign-owned bin/obj if a build/test pre-flight fails

The Makefile opts out of the first-run telemetry network call and disables
MSBuild node-reuse so the build survives the agent sandbox. This host
(Ubuntu 24.04) also needs `kernel.apparmor_restrict_unprivileged_userns=0`
(set in `/etc/sysctl.d/99-userns.conf`) for the bwrap-based sandbox to start.
Docker/Testcontainers integration tests cannot be sandboxed — the Docker socket
is a sandbox escape — so they always run with full access.
