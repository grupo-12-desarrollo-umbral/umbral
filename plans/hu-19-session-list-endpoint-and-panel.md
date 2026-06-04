# Plan: Session list endpoint + list-driven operator-assignment UI (HU-19)

## Context

The admin "Assign Responsible Operator" panel (`frontend/app/dashboard/SessionOperatorPanel.tsx`)
currently forces the admin to **paste a live-session UUID** into a text input and click "Load".
There is no way to see which sessions exist. Worse, the flow is actually **non-functional**
against the real backend: the frontend talks **directly** to the session-operations-service
(`SESSION_OPERATIONS_SERVICE_URL`, port 5003 — no gateway rewrite), and the paths don't match:

| Frontend calls (`app/lib/sessions.ts`)             | Backend reality (`SessionsEndpoints.cs`)                |
| -------------------------------------------------- | ------------------------------------------------------- |
| `GET /api/sessions/{id}` (getSessionOperatorState) | **does not exist**                                      |
| `POST /api/sessions/{id}/operator` (assign)        | actually `PATCH /api/sessions/{id}/operator-assignment` |

The e2e spec even comments that these endpoints "aren't wired yet".

**Goal:** add a backend endpoint that lists assignable sessions, and rework the panel so the admin
**picks a session from a list** instead of pasting a UUID. Fix the assign-path mismatch so the
whole flow works end-to-end.

**Decisions (confirmed with user):**

- List returns sessions an operator can still be assigned to → **exclude terminal states**
  (`Finished`, `Cancelled`); include `Scheduled`/`Preparing`/`Active`/`Paused`, ordered by
  `ScheduledAt` descending.
- UI: the selectable list **replaces** the paste-UUID input.

---

## Backend — new CQRS read slice (`session-operations-service`)

All under `backend/services/session-operations-service/src`. Follows the existing
command-slice conventions (`Sessions/Commands/AssignOperatorToSession/...`), but as a query.
MediatR auto-discovers handlers, and `[Authorize]` + `AuthorizationBehaviour` enforce the role,
so **no DI wiring is needed** beyond the already-registered repository.

1. **DTO** — `Application/Sessions/DTOs/SessionOperatorSummaryDto.cs`

   ```csharp
   public sealed record SessionOperatorSummaryDto(
       Guid LiveSessionId, string SessionCode, string Title,
       string SessionState, int? AssignedOperatorUserId, DateTimeOffset ScheduledAt);
   ```

   (Mirrors `CreateTriviaSessionResultDto` — `SessionState` is the enum `.ToString()`.)

2. **Query** — `Application/Sessions/Queries/ListAssignableSessions/ListAssignableSessionsQuery.cs`

   ```csharp
   [Authorize(Roles = "Administrator")]
   public sealed record ListAssignableSessionsQuery()
       : IRequest<IReadOnlyList<SessionOperatorSummaryDto>>;
   ```

   Mirror the `[Authorize(Roles = "Administrator")]` usage on `AssignOperatorToSessionCommand`.

3. **Handler** — `Application/Sessions/Handlers/ListAssignableSessionsQueryHandler.cs`
   Thin handler that calls the repository directly (reads need no facade/authorization-proxy —
   the role gate is already handled). Returns the repo result.

4. **Repository** — add to `Application/Common/Interfaces/ILiveSessionRepository.cs`:

   ```csharp
   Task<IReadOnlyList<SessionOperatorSummaryDto>> ListAssignableSummariesAsync(CancellationToken ct);
   ```

   Implement in `Infrastructure/.../Repositories/LiveSessionRepository.cs` with a lightweight,
   tracking-free projection (no `.Include` of teams/participants/snapshot):

   ```csharp
   var rows = await _context.LiveSessions.AsNoTracking()
       .Where(s => s.State != SessionState.Finished && s.State != SessionState.Cancelled)
       .OrderByDescending(s => s.ScheduledAt)
       .Select(s => new { s.LiveSessionId, s.SessionCode, s.TitleSnapshot,
                          s.State, s.AssignedOperatorUserId, s.ScheduledAt })
       .ToListAsync(ct);
   return rows.Select(r => new SessionOperatorSummaryDto(
       r.LiveSessionId, r.SessionCode, r.TitleSnapshot, r.State.ToString(),
       r.AssignedOperatorUserId, r.ScheduledAt)).ToList();
   ```

   (Map `State.ToString()` in memory after `ToListAsync` — enum→string isn't translated by Npgsql.)

5. **Endpoint** — `Api/Endpoints/SessionsEndpoints.cs`

   ```csharp
   sessions.MapGet("/", ListAssignableSessionsAsync)
       .RequireAuthorization(AuthorizationPolicies.Administrator);
   ```

   Handler method sends `ListAssignableSessionsQuery` and returns
   `Ok<IReadOnlyList<SessionOperatorSummaryDto>>`. Place above/below the existing `MapPost("/")`.

### Backend tests

- **Integration** — `tests/IntegrationTests/Api/ListAssignableSessionsEndpointTests.cs`
  (mirror `AssignOperatorToSessionEndpointTests.cs`: `PostgreSqlCollection`, `AddTrustedHeaders`,
  `SeedSessionAsync`):
  - Administrator caller → 200, returns seeded non-terminal sessions; a `Finished`/`Cancelled`
    seed is **excluded**; an assigned session reports its `AssignedOperatorUserId`.
  - No trusted headers → 401. Non-Administrator role → 403.
- **Unit** (optional, light) — handler returns whatever the mocked repo yields.

---

## Frontend — list-driven panel

1. **`app/lib/definitions.ts`** — add the summary type; fix the stale endpoint comments:

   ```ts
   export type SessionAssignmentSummaryDto = {
     liveSessionId: string
     sessionCode: string
     title: string
     sessionState: string
     assignedOperatorUserId: number | null
     scheduledAt: string
   }
   ```

   Remove (or repurpose) `SessionOperatorStateDto` — the list item carries the same fields.

2. **`app/lib/sessions.ts`**
   - Add `listAssignableSessions(): Promise<SessionAssignmentSummaryDto[]>` →
     `GET ${SESSION_OPERATIONS_SERVICE_URL}/api/sessions` with identity headers, `cache: 'no-store'`,
     mapping 401→IdentityError, 403→Administrator-required.
   - **Fix** `assignSessionOperator` to `PATCH .../{id}/operator-assignment` (was `POST .../operator`).
   - Remove `getSessionOperatorState` (no backend GET-single endpoint; list replaces it).

3. **`app/actions/sessions.ts`**
   - Add `listSessionsForAssignment()` server action with the `Administrator` role guard
     (same pattern as `getAssignableOperators`).
   - Remove the `getSessionOperatorState` action.

4. **`app/dashboard/SessionOperatorPanel.tsx`** (the main UX change)
   - On mount: load operators (existing) **and** `listSessionsForAssignment()`.
   - Render a session **list/table** (`data-testid="session-operator-list"`), one row per session
     (`session-operator-item`) showing title, session code, state, and current operator label
     (reuse `currentOperatorLabel`). A "Refresh" button re-fetches.
   - Selecting a row sets the working `state` directly from the summary item (no separate GET).
     The operator `<select>` + Assign/Cancel block stays as-is, keyed off the selected session.
   - On successful assign, update that row's `assignedOperatorUserId` in the list too.
   - Remove the paste-UUID input, `loadSession`, and the load-error path tied to it.
   - Empty state: "No active sessions to assign." Keep `data-testid="session-operator-panel"`.

5. **`app/dashboard/dashboard.module.css`** — add a few list/row classes (reuse existing
   `panel`, `sessionCard`, `sessionMeta`, `chip`, `smallButton`).

6. **`tests/e2e/session-operator.spec.ts`** — update for the new UI (this file is uncommitted WIP):
   - Keep: nav opens the panel; operator role does **not** see it; "Assign operators" hero button
     navigates to the panel.
   - Replace input/load-button assertions with list assertions (`session-operator-list` visible;
     assign controls hidden until a row is selected).

---

## Verification

**Backend** (per memory: dotnet runs via make in the sandbox):

- `make -C backend test` (or the session-operations-service test projects). Confirm the new
  `ListAssignableSessionsEndpointTests` and the full suite pass.

**Frontend** (in `frontend/`):

- `npm run lint` and `npx tsc --noEmit` — no type/lint errors.
- `npx playwright test tests/e2e/session-operator.spec.ts` (needs the dev stack per
  `playwright.config.ts`).

**Manual / end-to-end** (`backend/docker-compose.yml` brings up the service on 5003):

- Log in as an Administrator, open dashboard → **Sessions** nav → panel shows a list of active
  sessions with their current operator. Select one, change the operator, click Assign, and confirm
  the row updates and the change persists after Refresh.

---

## Out of scope / notes

- No EF migration needed — only reads of existing columns.
- The api-gateway is not involved for these calls (frontend → service direct).
- Optionally update `backend/services/session-operations-service/README.md` to document
  `GET /api/sessions` (there is an untracked `generate-service-readme` skill for this).
