# Resolve operator ownership via Identity actor profile

`session-operations-service` treats the gateway-forwarded `X-User-Id` as an external identity id and resolves the current actor through `identity-access-service` `GET /api/users/me` before applying operator ownership checks, because `LiveSession.AssignedOperatorUserId` is persisted as Identity's internal numeric `User.Id`. We deliberately extend the authenticated-actor profile payload with `userId` and keep the gateway header contract unchanged, rather than introducing a new trusted header or weakening authorization, so operator session listing, hub joins, and state-transition authorization all use the same canonical ownership mapping.

## Consequences

- `identity-access-service` now owns a backend-to-backend contract requirement: `/api/users/me` must continue returning the internal `userId` needed by `session-operations-service`.
- Operator-only realtime flows reuse the same ownership resolution path as REST authorization, so hub admission and HTTP authorization stay aligned instead of drifting into separate identity rules.
