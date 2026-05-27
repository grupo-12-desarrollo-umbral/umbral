# Context Map

## Contexts

- [Identity Access](./services/identity-access-service/CONTEXT.md) — handles authentication and access-related flows for administrators, operators, and participants
- [Mission Design](./services/mission-design-service/CONTEXT.md) — manages mission and trivia authoring and prepares content for live use
- [Session Operations](./services/session-operations-service/CONTEXT.md) — runs live sessions, team participation, clue progression, and evidence intake
- [Scoring Monitoring](./services/scoring-monitoring-service/CONTEXT.md) — calculates scoring outcomes and exposes ranking, audit, and monitoring views

## Relationships

- **Mission Design → Session Operations**: Mission Design provides active missions; Session Operations uses them to create live sessions.
- **Identity Access → Mission Design**: Identity Access provides authenticated actor identity and access facts for authorizing mission permissions.
- **Identity Access → Session Operations**: Identity Access provides authenticated actor identity and access facts for authorizing live session entry.
- **Session Operations → Scoring Monitoring**: Session Operations emits runtime outcomes; Scoring Monitoring uses them to update scores, rankings, and audit history
- **Scoring Monitoring → Session Operations**: Scoring Monitoring provides derived ranking and monitoring views for live supervision
