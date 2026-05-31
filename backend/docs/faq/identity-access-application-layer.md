# Identity Access — Application Layer Code Review

## Question 1: Should `ICurrentUser` be an interface?

Yes. `ICurrentUser` must remain an interface.

`backend-agent.md` explicitly states that `CurrentUserService` implements `ICurrentUser` by reading the trusted headers forwarded by the api-gateway (`X-User-Id`, `X-User-Role`, `X-User-Email`). That implementation lives in the Infrastructure layer. If `ICurrentUser` were a concrete class, the Application layer would depend on Infrastructure, which violates the dependency rule of Clean Architecture.

The interface is the correct abstraction point — Application defines the contract, Infrastructure fulfills it.

## Question 2: Is `using umbral_backend.Domain.Entities` in `IUserRepository` correct?

Yes. Application → Domain is an allowed dependency direction in Clean Architecture.

What `backend-agent.md` explicitly prohibits in repository interfaces is **EF references** — no `DbSet<T>`, no `ApplicationDbContext`. Using domain entities (`User`) in the interface is the standard pattern; the repository abstraction exists precisely to let the Application layer work with domain objects without knowing anything about persistence.

This is not explicitly spelled out in a doc but follows directly from the layer rules.

## Question 3: Why did `GatewayRoleParser` have two names per role, and what was the fix?

The parser originally accepted both English (`"Administrator"`) and Spanish (`"Administrador"`) aliases for each role. The intent was defensive — the Keycloak realm happened to be configured with Spanish names, but the application domain used English.

Having two aliases per role is a smell: it implies two valid wire formats and makes it unclear what the canonical value is. The fix was to pick one canonical form end-to-end:

- The `Role` enum uses English names.
- The Keycloak realm import (`umbral-realm.json`) was updated to use English role names (`Administrator`, `Operator`, `Participant`).
- `GatewayRoleParser` was simplified to accept only the English names.
- Test fixtures were updated to use the English names.

**Rule:** the Keycloak realm config is the source of the `X-User-Role` header value. The parser must accept exactly what Keycloak emits — no more, no fewer. When in doubt, verify against `backend/deploy/keycloak/import/umbral-realm.json`.

## Question 4: Are the Application layer DTOs really DTOs?

Yes. All three comply with `backend-agent.md` ("DTOs are output-only records — no domain types leak through them"):

| DTO | Type | Domain types? |
|---|---|---|
| `AuthenticateUserResultDto` | `sealed record` | No — composes the two below |
| `AuthenticatedActorProfileDto` | `sealed record` | No — `Role` enum projected to `string` before mapping |
| `ProtectedAccessDecisionDto` | `sealed record` | No — `ProtectedCapability` projected to `string` before mapping |

All three use only primitive types (`string`, `bool`). None expose domain enums or entities to callers.
