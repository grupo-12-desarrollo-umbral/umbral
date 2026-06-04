# Session ownership and operator assignment

## Status

Accepted

## Context

The current canonical product docs define both of these statements:

- `HU-15`, `HU-16`, and `HU-17` say the `Operador` creates the `LiveSession` from exactly one valid content source.
- `HU-19` says the `Administrador` assigns or changes the operator responsible for a session, and that operators should only manage assigned or visible sessions according to policy.

This creates a common ambiguity in product language:

- one interpretation says the `Administrador` is the "boss" and therefore should create or pre-assign the work before the operator touches the session
- the canonical stories instead place session creation and setup in the operator workflow, while still giving the administrator responsibility over assignment and reassignment

The frontend must not drift into a stronger model than the canonical stories currently support.

## Decision

UMBRAL keeps this model:

1. The `Operador` creates the session.
2. The creating operator becomes the initial responsible operator by default.
3. The `Administrador` may assign or reassign the responsible operator afterward.
4. The operator may only operate sessions assigned or visible to that operator under the active policy.

This means `HU-19` is interpreted as responsibility management, not as proof that the administrator creates the session first.

## Consequences

### Domain and workflow

- Session creation remains an operator capability.
- Team association before start remains an operator capability.
- Live operation remains an operator capability.
- Administrator assignment remains a control and audit capability over operational responsibility.
- Reassignment is valid after creation and does not contradict operator-created sessions.

### Frontend product language

The operator UI should not present session work as a raw "create session" surface only, because the operator's real working set is the sessions currently assigned to them.

At the same time, the UI must not imply a stricter model than the canon supports. Avoid copy that suggests:

- the administrator must always assign the session before it exists
- the operator cannot create a new session
- the operator only chooses content inside an already-created admin-owned session

Prefer copy that communicates both truths:

- the operator can create and prepare sessions
- the operator operates sessions they are responsible for

## UI rules

### Operator navigation and screen framing

- Prefer `My sessions` or `Assigned sessions` as the operator-facing section label.
- Avoid `Session creation` as the sole identity of the operator area.
- Avoid `Assigned session` in singular as the default label unless the screen truly loads one selected session only.

### Operator screen structure

The operator sessions area should be shaped as:

1. A list of sessions assigned to the current operator.
2. A clear entry point to create a new session from valid content.
3. Session-specific setup and operation actions once a session is selected.

This keeps the surface aligned with `HU-15` to `HU-19` without pretending assignment happens before creation.

### Recommended copy

- Section label: `My sessions`
- Empty state: `You have no assigned sessions yet. Create a new session or ask an administrator to reassign one.`
- Create CTA: `Create session`
- Assigned list heading: `Sessions you're responsible for`
- Selected session setup heading: `Session setup`
- Selected live controls heading: `Live operation`

### Administrator screen framing

- Use `Assign operators` or `Operator assignment`.
- Explain the action as assigning or changing the responsible operator for an existing session.
- Do not describe the admin as the creator of the session unless the canonical stories are changed.

## Follow-up if the product model changes later

If the intended product model becomes:

- administrator allocates the work first
- operator only prepares and runs pre-assigned sessions

then the canonical stories must be updated first, especially `HU-15` to `HU-19`, before the frontend is reshaped around that stronger workflow.

## Canonical references

- [User stories](/home/samu/Desktop/umbral-hu-19/frontend/docs/umbral_user_stories.md:52)
- [Admin vs operator dashboard](/home/samu/Desktop/umbral-hu-19/frontend/docs/admin-vs-operator-dashboard.md:1)
- [Role-aware dashboard ADR](/home/samu/Desktop/umbral-hu-19/frontend/docs/adr/0004-one-shared-app-role-aware-rendering.md:1)
