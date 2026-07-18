# Context Map

## Contexts

- [Users](./services/users-service/CONTEXT.md) — owns user records, roles, provider sessions, registered teams, and returns access facts; delegates authentication to Keycloak
- [Mission Design](./services/mission-design-service/CONTEXT.md) — manages mission and trivia authoring and prepares content for live use
- [Session Operations](./services/session-operations-service/CONTEXT.md) — runs live sessions, team participation, target progression, clue visibility, and evidence intake
- [Scoring Monitoring](./services/scoring-monitoring-service/CONTEXT.md) — calculates scoring outcomes and exposes ranking, audit, and monitoring views

## Relationships

- **Mission Design → Session Operations**: Mission Design provides active missions; Session Operations uses them to create live sessions.
- **Users → Mission Design**: Users provides authenticated actor identity and access facts for authorizing mission permissions.
- **Users → Session Operations**: Users provides authenticated actor identity, whitelist eligibility (`RegisteredTeamMembership`), and access facts (including `User Deactivation`); Session Operations owns the final admission decision, `Open Team Selection`, and runtime participation blocks.
- **Session Operations → Scoring Monitoring**: Session Operations emits runtime outcomes; Scoring Monitoring uses them to update scores, rankings, and audit history
- **Scoring Monitoring → Session Operations**: Scoring Monitoring provides derived ranking and monitoring views for live supervision
