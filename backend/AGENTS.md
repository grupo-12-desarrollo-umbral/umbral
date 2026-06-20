# Agent Instructions

## Implementation work

Before writing any service code, read `.agents/backend-agent.md`. It defines the canonical documents to load, layer rules, verification gates, and constraints. Do not start implementation without it.

## Commit & PR Standards

When the user asks to commit changes, always invoke the `/conventional-commits` skill before running `git commit`. When creating a PR, always invoke the `/safe-pr-creator` skill before running `gh pr create`.

## Structure Enforcement

Before creating or moving any file, read `structure.md` and place it
according to the Concrete Target Tree and DDD/Boundary rules.

Do not invent new folder paths outside the established structure
without updating `structure.md` first.

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
    make -C backend gate  SVC=<service>   # ADR-0005 coverage gate
    make -C backend ef    SVC=<service> ARGS="migrations add Foo"

The Makefile opts out of the first-run telemetry network call and disables
MSBuild node-reuse so the build survives the agent sandbox. This host
(Ubuntu 24.04) also needs `kernel.apparmor_restrict_unprivileged_userns=0`
(set in `/etc/sysctl.d/99-userns.conf`) for the bwrap-based sandbox to start.
Docker/Testcontainers integration tests cannot be sandboxed — the Docker socket
is a sandbox escape — so they always run with full access.
