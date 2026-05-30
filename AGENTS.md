# Agent Instructions

## Monorepo Structure

This repo has two independently deployable workloads:

- `backend/` — .NET microservices (api-gateway + bounded-context services). Read `backend/AGENTS.md` before touching anything here.
- `frontend/` — Next.js app. Read `frontend/AGENTS.md` before touching anything here.

For the integration boundary between them, read `CONTEXT-MAP.md` in this directory.

## Boundary Rules

- Do not import or reference backend source files from frontend tasks, or vice versa.
- API contracts (routes, payloads, event shapes) are the only shared surface. If a contract changes, flag it explicitly — both sides must be updated together.
- Do not create files at the monorepo root except for shared configuration (e.g. `.editorconfig`, `docker-compose.yml`) or documentation that applies to both workloads.
