# HU-02 user management: authorization on operation, not data visibility

User management (`GET /api/users`, `DELETE /api/users/{id}/access`) uses `[Authorize]` attributes on MediatR commands/queries to gate *who may call* each operation, leaving the response to include **all** users regardless of role. The `GetUsersQueryHandler` calls `ListAsync()` without filtering by role — participants appear in the catalog alongside admins and operators. This separates access control (fixed at compile time via the attribute) from data visibility (a query concern), keeping the repository a simple offset/limit provider. An operator sees participant records because the gate only protects the operation, not the rows.

Soft deactivation (`IsActive = false`) is idempotent — the `User` entity rejects a second deactivation with a domain exception — so the endpoint uses `DELETE /api/users/{id}/access` (204 No Content) as a resource-oriented mutation on the access sub-resource rather than exposing a state-changing PATCH.

**Status:** accepted

**Considered Options:**
- **Role-filtered query** — the handler filters out participants when an operator calls it. Rejected: it couples data shape to caller identity and would need revisiting when a future UI shows operators a different view.
- **Repository-level role filter** — the repository takes a role parameter. Rejected: the repository should not know about application authorization; the handler owns that concern.
- **PATCH /api/users/{id}/deactivate** — state-changing verb on the user resource. Rejected: deactivation targets the user's access grant, not the user itself; DELETE on `/access` is more resource-oriented.
