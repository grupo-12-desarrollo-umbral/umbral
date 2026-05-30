
---

## [001] Phase X.1 — Identity Access Domain Layer
**Date:** 2026-05-30
**Phase:** X.1 — Domain Layer
**Commits:** 7511930
**HU tickets advanced:** HU-01

**What was built**
The full Domain layer for identity-access-service: core entities (User, IdentityProviderSession), value objects (AccessDecision), enums (Role, ProtectedCapability), domain events (UserProvisioned, RoleAssigned, AccessDeactivated, IdP session lifecycle, AccessDecisionRecorded), domain services (AccessPolicy, IdentityProvisioningPolicy), base abstractions (BaseEntity, BaseAuditableEntity, ValueObject, BaseEvent), and typed exceptions — 30 files, 649 lines.

**Why this approach**
Domain-first DDD with rich domain model — entities guard their own invariants (e.g., User rejects duplicate deactivation, IdP session validates expiration), domain services encapsulate cross-entity policy, and events decouple domain logic from side effects. ValueObject base avoids identity misuse on value types. Exceptions are typed rather than generic to enable precise error handling at the application layer.

**Deliberately skipped**
- Application layer (commands, queries, handlers) — deferred to phase X.2
- Persistence / EF Core configuration — deferred to phase X.3
- Integration events / messaging — deferred until the Application layer is in place
- Unit tests — explicit skip per workflow (phase X.1 is domain-only; tests come in X.2 with Application layer)

**Next session needs to know**
Phase X.1 is complete and builds clean. Start phase X.2 — Application layer: define MediatR commands/queries, add AutoMapper profiles, and write unit tests covering domain behavior through the application handlers.

---

## [002] Phase X.2 — Identity Access Application Layer
**Date:** 2026-05-30
**Phase:** X.2 — Application Layer
**Commits:** adfc152
**HU tickets advanced:** HU-01, DES-5

**What was built**
Full Application layer for identity-access-service: MediatR commands/queries (AuthenticateUser, GetAuthenticatedActorProfile, CheckProtectedCapabilityAccess), cross-cutting pipeline behaviours (validation, authorization, performance, unhandled exception), DTOs, AutoMapper profiles, common interfaces (ICurrentUser, IUserRepository), security attributes (AuthorizeAttribute, GatewayRoleParser), DependencyInjection registration, and 5 unit test files covering all handlers and validators — 32 files, 925 lines.

**Why this approach**
MediatR with pipeline behaviours isolates cross-cutting concerns (validation via FluentValidation, authorization via attribute reflection, performance logging, exception normalization) from handler logic. The CQRS-like command/query split keeps reads and writes on separate paths without full event sourcing overhead. The GatewayRoleParser adapts the YARP gateway's role claim format (comma-separated string) into the domain's Role enum. Unit tests use the established fixture pattern from phase X.1 domain tests.

**Deliberately skipped**
- Infrastructure/persistence layer (EF Core, repositories) — deferred to phase X.3
- Integration events — deferred until persistence layer connects domain events to message bus
- API Gateway/controller layer — deferred to phase X.4
- AutoMapper — recognized as a dependency but profiles deferred; DTOs use manual mapping in handlers for now

**Next session needs to know**
Phase X.2 builds clean. Phase X.3 — Infrastructure: implement IUserRepository with EF Core, configure DbContext, wire up the real ICurrentUser from HttpContext, and connect domain events to MediatR notifications.
