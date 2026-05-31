# Team reference data lives in Identity; SessionOperations owns runtime Team state

The PRD (DES-67) assigns HU-04 (team registration) and HU-05 (team membership) to `identity-access-service`, but the canonical DDD model places `Team` in `SessionOperations`. This is not a contradiction: Identity holds the **team catalog** (id, name, code, active status), a reference-data registry used for pre-session administration. SessionOperations holds the **runtime team** (score, progress, live participation state) that exists inside a `LiveSession`. The two are separate representations of the same real-world concept in different bounded contexts, connected by explicit contract — Identity provides the registration fact, SessionOperations consumes it for runtime admission.

## Status

accepted

## Considered Options

- **Put everything in SessionOperations.** Would require adding CRUD admin endpoints to a service designed for live runtime operations, mixing concerns. SessionOperations would need to expose team catalog queries to Identity for authorization checks, creating a reverse dependency.
- **Duplicate Team in both contexts without explicit boundary.** Risked confusing the two representations and accidentally leaking runtime fields (score, progress) into Identity's model.
- **Single Team in Identity, SessionOperations references it by ID.** Rejected because SessionOperations needs its own Team entity for runtime state (score per team, progress node, etc.) that doesn't exist in Identity.

## Consequences

- Engineers must be aware that `Team` appears in two services with different shapes and purposes. The `backend/docs/ddd_solution_model.md` now lists both. Agents are instructed to read the boundary decision block before implementing.
- HU-07 (JoinToken) must consult Identity's membership record for authorization and SessionOperations for runtime admission — a two-step check that is explicit in the architecture.
- The `AddTeams` migration in Identity must exist and be gated before HU-05 generates `AddTeamMemberships`, because EF Core snapshots are cumulative.
