# Issue #173 — Make trivia authoring Operator-only: implementation decisions

> Locked decisions for GH issue #173. Records *what* we're changing and *why*, including the
> deliberate divergence from the academic canon. Branch: `feature/issue-173-trivia-authoring-operator`.

## Scope decision

**All trivia authoring → `Operator`.** All 10 trivia commands in `mission-design-service` move from
`Administrator` to `Operator`:

- Question authoring: `AddTriviaQuestion`, `UpdateTriviaQuestion`, `RemoveTriviaQuestion`
- Quiz lifecycle: `CreateTriviaQuiz`, `UpdateTriviaQuiz`, `PublishTriviaQuiz`, `ArchiveTriviaQuiz`,
  `RetireTriviaQuiz`, `DuplicateTriviaQuiz`, `DeleteTriviaQuiz`

`Mission` commands stay `Administrator` (canon §7 assigns Mission authoring to Admin — out of scope).

Issue #173's title says "question authoring", but the chosen direction is the broader "all trivia →
Operator" for a single, consistent ownership model rather than a split where Operators author
questions into quizzes only an Admin can create.

## Canon conflict (must be recorded, not silent)

`backend/docs/academic-requirements-canon.md` §7 currently assigns **all `TriviaQuiz` authoring to the
Administrator** and scopes the **Operator to runtime session operations** (start `LiveSession`,
`ClueRelease`, `Penalty`, supervision). This change therefore **diverges from the graded academic
canon**. We proceed as a deliberate product decision (#173) and record it in the canon's
"Divergences" section, cross-linked to the issue.

## Backend

1. `Domain/Constants/Roles.cs` — add `public const string Operator = nameof(Operator);`
2. All 10 trivia commands: `[Authorize(Roles = Roles.Administrator)]` → `[Authorize(Roles = Roles.Operator)]`

## Backend tests (`tests/IntegrationTests/Api/TriviaEndpointsTests.cs`)

3. Happy-path trivia tests seeded with `AddAdministratorHeaders()` → `AddOperatorHeaders()` (the shared
   client drives the create/publish/etc. helper flow, so switching the caller to Operator makes the
   whole flow authorized).
4. The 4 negative "non-admin returns 403" tests **invert** — rename to `WithNonOperatorCaller_Returns403`
   and use `AddAdministratorHeaders()` as the now-forbidden caller (Admin is no longer authorized for
   trivia), still expecting `403 Forbidden`:
   - `AddTriviaQuestion_WithNonAdminCaller_Returns403`
   - `RemoveTriviaQuestion_WithNonAdminCaller_Returns403`
   - `PublishTriviaQuiz_WithNonAdminCaller_Returns403`
   - `ArchiveTriviaQuiz_WithNonAdminCaller_Returns403`
5. Any unit tests asserting `Roles.Administrator` on trivia handlers/authorization → `Roles.Operator`.

## Frontend

6. `app/actions/trivias.ts` — all 9 `session.role !== 'Administrator'` → `!== 'Operator'`.
7. `app/lib/trivias.ts` — "Forbidden. Administrator role required." → "Forbidden. Operator role required."
8. `app/dashboard/DashboardClient.tsx` — nav filter stops hiding `trivias` from operators.
9. `app/dashboard/TriviasPanel.tsx` — question/quiz control guards flip from `role === 'admin'` to
   `role === 'operator'`.
10. Frontend tests updated for Operator-allowed / Administrator-denied.

## Docs

11. Add a row to `academic-requirements-canon.md` ("Divergences → Tensions worth a decision"): trivia
    authoring reassigned from Administrator (canon §7) to Operator per issue #173, cross-linked.

## Delivery

12. Work in worktree `../umbral-issue-173`. Build + test backend and frontend. Squash to one commit,
    then open PR — no commit/push until approved.

## Acceptance criteria mapping (#173)

- Operators can add/update/remove trivia questions → commands 2, integration tests 3.
- Administrators no longer implicitly required for trivia authoring → commands 2, negative tests 4.
- Frontend nav/RBAC exposes trivia authoring to Operators → 6, 8, 9.
- Backend rejects unauthorized non-Operator users → negative tests 4.
- Tests updated across backend and frontend → 3, 4, 5, 10.
- Related Linear tickets (DES-20, DES-80, DES-17, DES-18, DES-7) cross-linked → PR body + canon note 11.
