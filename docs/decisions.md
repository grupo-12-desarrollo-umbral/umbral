---

## [001] Phase 1 — Proven public auth path (api-gateway scaffold + Compose baseline)
**Date:** 2026-05-28
**Phase:** 1 — Proven public auth path
**Commits:** 0b2fc67, 45cbb2a, 38dc89c, da51517, e02a238, 1f26783, 68cfc48
**HU tickets advanced:** HU-11, HU-12, HU-13, HU-15, HU-17, HU-18, HU-41, HU-42, HU-43

**What was built**
`api-gateway` scaffolded as an ASP.NET Core + YARP project with JWT Bearer validation against Keycloak, `TrustedHeadersTransform` that injects `X-User-Id`/`X-User-Role`/`X-User-Email` and strips `Authorization`, and a `WebSocketTokenExtractionTransform` for SignalR. `mission-design-service` `CurrentUser` flipped from JWT claim reads to trusted header reads. Multi-stage Dockerfiles added for both services. Root `docker-compose.yml` wired: postgres, keycloak, rabbitmq, api-gateway, mission-design-service.

**Why this approach**
Gateway-central validation keeps the trust boundary visible and explicit: a service that somehow gets traffic without going through the gateway will receive no trusted headers and `CurrentUser.Id` will be null — a loud failure rather than a silent bypass. The alternative (per-service `AddJwtBearer`) duplicates Keycloak SDK wiring across four services and makes the boundary invisible in code.

YARP was chosen over a hand-rolled proxy because it handles connection pooling, HTTP/2 upgrades, and header forwarding correctly out of the box. Writing a minimal proxy in ASP.NET Core middleware is feasible but replicates solved problems.

Trusted headers (not forwarded JWT) as the downstream contract keeps services completely decoupled from token format. If Keycloak's claim names or JWT structure changes, only the gateway's `TrustedHeadersTransform` changes — the four services are unaffected. It also makes `CurrentUser` trivially testable: inject any header values, no token signing required.

The `OnAuthenticationFailed` handler distinguishes infrastructure failures (Keycloak unreachable → 503) from token validation failures (bad/expired token → 401 via the normal challenge path). Without this split, a temporary Keycloak outage causes clients to receive 401s and silently re-authenticate, burning refresh tokens and masking the real problem. 503 signals "retry later" to the caller and to any upstream health monitors.

**Deliberately skipped**
No real `GET /api/missions/` endpoint exists yet — mission-design-service returns 404 for all routes. Confirming that `CurrentUser.Id` returns the correct value at the application layer requires a real endpoint (Phase 2). Gateway health/readiness endpoint not added (not in Phase 1 scope). `rabbitmq` has no healthcheck and no `depends_on` wiring; that's Phase 2+.

**Next session needs to know**
Verify Phase 2 with a real endpoint to confirm the `CurrentUser.Id` → `X-User-Id` round-trip end-to-end. Nothing else blocking.
