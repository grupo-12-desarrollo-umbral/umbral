# Monorepo Context Map

## Workloads

| Workload | Path | Role |
|----------|------|------|
| Backend | `backend/` | Microservices exposing REST + WebSocket APIs via an api-gateway |
| Frontend | `frontend/` | Next.js app consumed by operators, administrators, and participants |

For the backend's internal bounded-context map, see `backend/CONTEXT-MAP.md`.

## Integration Boundary

The frontend communicates exclusively through the `api-gateway`. It has no direct access to individual backend services.

### Backend → Frontend (what the backend exposes)

- **REST API** — resource and command endpoints for mission management, session control, identity, and scoring.
- **WebSocket / real-time** — live session events (clue progression, team updates, score changes) pushed to connected participants and operators.

### Frontend → Backend (what the frontend drives)

- Operator flows: create/manage missions, launch and supervise live sessions.
- Participant flows: join sessions, submit evidence, track clue progression.
- Admin flows: user and access management.

## Ownership

Changes to API contracts (routes, request/response shapes, event payloads) affect both workloads and must be coordinated. Neither side should silently diverge from the agreed contract.
