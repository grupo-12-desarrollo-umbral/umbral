
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
