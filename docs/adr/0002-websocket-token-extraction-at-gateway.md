# WebSocket token extraction at the gateway for SignalR connections

Browser and mobile WebSocket clients cannot set `Authorization` headers on the upgrade request. SignalR's standard pattern passes the token as a `?access_token=` query parameter instead. The YARP gateway uses a custom request transform that detects WebSocket upgrade requests (`Connection: Upgrade` + `Upgrade: websocket`) and extracts the token from the query parameter rather than the `Authorization` header. Validation and downstream header injection are identical to the standard HTTP path — the only difference is where the raw token is read from.

## Considered Options

**Per-service SignalR auth (rejected):** allow `/hubs/*` routes to bypass gateway JWT validation and let `session-operations-service` validate the token itself against Keycloak. Rejected because it reintroduces Keycloak JWKS config into a service that should not own it, and fractures the single-trust-boundary model established in ADR-0001.

## Consequences

- The YARP gateway project contains a `WebSocketTokenExtractionTransform` (or equivalent) applied only to routes that match SignalR hub paths.
- `session-operations-service` receives the same three trusted headers for WebSocket connections as for HTTP connections — no special handling needed in the hub or hub filters.
