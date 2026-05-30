# Admin Dashboard vs Operator Dashboard — RBAC on the Same Web Page

> Both roles share the same SPA (Next.js). The base layout (sidebar, header, active session) is common. The visibility of sections, widgets, and actions is governed by the **authenticated user's RBAC role** — there are no separate routes per role.

| Dimension | Admin | Operator |
|---|---|---|
| **User management** (`HU-02`) | CRUD of registered users, account deactivation. | Not visible. |
| **Roles & permissions** (`HU-03`) | Assign and change user roles. | Not visible. |
| **Teams** (`HU-04`, `HU-05`) | CRUD of teams, assign participants to teams. | Read-only view of teams linked to assigned sessions. |
| **Missions / Content** (`HU-09`–`HU-14`) | Full CRUD of missions, quizzes, questions. Publish and archive. | Not visible (only picks active content when creating a session). |
| **Operator-to-session assignment** (`HU-19`) | Assign/change the operator responsible for a session. | Not visible (only sees sessions they were assigned to). |
| **Session creation** (`HU-15`–`HU-18`) | Not visible (operator task). | Create sessions from active content, associate teams. |
| **Session queries** (`HU-20`, `HU-25`) | Global read-only view of all sessions. | View only their assigned sessions. |
| **Live operation panel** (`HU-24`) | Not visible (operator instrument). | Real-time monitoring via SignalR: session state, ranking, evidence, team progress, events. |
| **Session state control** (`HU-21`) | Not visible. | Transitions: scheduled → setup → active → paused → finished / cancelled. |
| **Clue release** (`HU-26`–`HU-28`) | Not visible. | Manual release, rule-based automatic release, and in-session operative clue addition. |
| **Evidence validation & traceability** (`HU-30`, `HU-32`) | Not visible. | Validate evidence, review detail and history per session. |
| **Trivia supervision** (`HU-33`, `HU-36`) | Not visible. | Monitor answered/unanswered during active round; full detail after round closes. |
| **Score & penalties** (`HU-37`, `HU-38`) | Not visible. | Register penalties with justification, view updated scores. |
| **Session event history** (`HU-40`) | Global read-only query of any session. | View history of assigned sessions only. |

## Implementation principles

1. **Single route:** The web app has a shared layout (`/dashboard`). No `/admin/*` vs `/operator/*` paths.
2. **Dynamic sidebar:** Sidebar menu entries are filtered based on the `role` claim.
3. **Conditional widgets:** Each dashboard widget (e.g. "Users", "Live sessions", "Penalties") renders only if the role has permission.
4. **Backend enforcement:** The frontend hides elements visually, but the backend also rejects unauthorized commands via policy checks (`HU-03` criterion 4, `HU-24` criterion 4).
5. **Shared read models:** Queries (`HU-25`) use the same projections but filter by `operatorId` or scope based on the user's claims.
