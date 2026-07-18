## Frontend Spanish Translation Design

### Goal

Translate only the visible UI text in `frontend/app/dashboard/TeamsPanel.tsx` and `frontend/app/dashboard/TriviasPanel.tsx` to Spanish.

### Scope

- Translate labels, headings, buttons, empty states, helper text, placeholders, confirmation text, and user-facing error messages shown by these two panels.
- Translate readiness messages generated inside `computeReadiness` in `TriviasPanel.tsx`.
- Leave technical terms untranslated when they appear in visible text, such as `webhook`, `QR`, and `target`.
- Do not change `data-testid`, component names, DTO names, action names, TypeScript types, or backend contract values.
- Do not introduce a broader i18n layer for this task.

### Approach

1. Update visible strings inline inside `TeamsPanel.tsx`.
2. Update visible strings inline inside `TriviasPanel.tsx`.
3. Preserve any backend lifecycle/status values that are rendered as contract values unless there is already a local label mapping in the file.
4. In a second pass, update frontend tests that assert the previous English copy.

### Constraints

- Keep the change minimal and local to these two panels for the UI pass.
- Avoid translating technical contract tokens or identifiers.
- Do not change behavior, request flow, or state handling.

### Risks

- Some tests may assert exact English copy and will need follow-up updates.
- Some visible status text may come directly from backend enum values and should not be changed blindly.

### Verification

- Review both updated panels for remaining visible English copy.
- Search for tests referencing the changed UI text and update them in the second pass.
