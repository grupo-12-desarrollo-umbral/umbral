# Trivia Ticket Graph

Last verified against Linear backlog on 2026-05-29 for team `umbral-equipo-12`.

## Scope

This file separates trivia work into three levels:

- `Core playable trivia`: enough to author quizzes, create trivia sessions, run rounds, accept answers, score them, and expose ranking.
- `Required for usable UX`: the participant and operator views needed so the flow is realistically usable.
- `Operational completeness`: extra read models, post-round review, and monitoring that make the feature more complete in product terms.

It also records where the proposed dependency chain differs from the current Linear issue graph.

## Exact Mapping

| HU | DES | Title | Service |
|---|---|---|---|
| HU-01 | DES-5 | Inicio de sesión general de usuarios | identity-access-service |
| HU-06 | DES-10 | Inicio de sesión de participantes | identity-access-service |
| HU-07A | DES-11 | Validación de membresía del participante en sesión | identity-access-service / session-operations-service |
| HU-07B | DES-12 | Reconexión autorizada del participante | identity-access-service / session-operations-service |
| HU-11 | DES-17 | Creación y edición de quizzes de trivia | mission-design-service |
| HU-12 | DES-18 | Publicación y archivado de quizzes de trivia | mission-design-service |
| HU-14A | DES-20 | Gestión de preguntas y opciones de trivia | mission-design-service |
| HU-14B | DES-21 | Reglas de validación de preguntas de trivia | mission-design-service |
| HU-16 | DES-23 | Creación de sesiones trivia | session-operations-service / mission-design-service |
| HU-18 | DES-25 | Asociación de equipos a sesiones | session-operations-service / identity-access-service |
| HU-19 | DES-26 | Asignación de operador a sesión | session-operations-service / identity-access-service |
| HU-21A | DES-28 | Transiciones válidas de estado de sesión | session-operations-service |
| HU-22 | DES-30 | Temporizador autoritativo de sesión | session-operations-service |
| HU-23 | DES-31 | Tablero de equipo en vivo | scoring-monitoring-service / session-operations-service |
| HU-24B | DES-33 | Panel del operador en tiempo real de eventos, evidencias y ranking | scoring-monitoring-service / session-operations-service |
| HU-25B | DES-35 | Consulta de tablero y ranking para participante | cross-service |
| HU-33A | DES-44 | Orquestación automatizada de trivia por rondas | session-operations-service |
| HU-33B | DES-45 | Cierre automático de preguntas y resultados finales de trivia | scoring-monitoring-service / session-operations-service |
| HU-34A | DES-46 | Registro de primera respuesta válida por equipo en trivia | session-operations-service |
| HU-34B | DES-47 | Rechazo de respuestas tardías o repetidas en trivia | session-operations-service |
| HU-35 | DES-48 | Revelación de resultado y explicación en trivia | scoring-monitoring-service / session-operations-service |
| HU-36A | DES-49 | Monitoreo restringido de respondido/no respondido en trivia | session-operations-service |
| HU-36B | DES-50 | Revisión post-cierre de respuestas y puntos en trivia | scoring-monitoring-service / session-operations-service |
| HU-37A | DES-51 | Ledger de puntaje por validaciones, respuestas y penalizaciones | scoring-monitoring-service |
| HU-39B | DES-55 | Ranking en tiempo real para sesiones de trivia | scoring-monitoring-service |
| ENABLER | DES-58 | Cliente móvil de participantes en React Native | cross-service |

## Recommended Dependency Graph

### 1. Identity foundation

These are the baseline identity tickets needed for participant access into trivia sessions.

1. `HU-01 / DES-5`
2. `HU-06 / DES-10`
3. `HU-07A / DES-11`
4. `HU-07B / DES-12`

Notes:

- `HU-07B` is trivia-relevant because its acceptance criteria explicitly forbid late join in trivia after start, except authorized reconnection.

### 2. Quiz authoring

These create playable trivia content.

1. `HU-11 / DES-17`
2. `HU-14A / DES-20`
3. `HU-14B / DES-21`
4. `HU-12 / DES-18`

Recommended sequence:

1. `HU-11`
2. `HU-14A`
3. `HU-14B`
4. `HU-12`

Linear blocker note:

- Your earlier chain listed `HU-12` as blocked only by `HU-11`.
- In Linear, `DES-18` is blocked by `HU-11`, `HU-14A`, and `HU-14B`.
- For end-to-end trivia, the Linear dependency is the stronger and safer interpretation.

### 3. Trivia session setup

These establish a valid trivia session before gameplay starts.

1. `HU-16 / DES-23`
2. `HU-18 / DES-25`
3. `HU-19 / DES-26`
4. `HU-21A / DES-28`
5. `HU-22 / DES-30`

Recommended sequence:

1. `HU-16`
2. `HU-18`
3. `HU-19`
4. `HU-21A`
5. `HU-22`

Linear blocker notes:

- `HU-16` is blocked by `HU-11`, `HU-12`, `HU-14A`, and `HU-14B`.
- `HU-18` is not only blocked by `HU-16`; in Linear it is blocked by `HU-04`, `HU-15`, and `HU-16`.
- `HU-19` does not currently declare `HU-16` as a blocker in Linear, but it is still a practical setup dependency for a real trivia session flow.

### 4. Core trivia round loop

These are the tickets that make the round execution and answer handling actually work.

1. `HU-33A / DES-44`
2. `HU-34A / DES-46`
3. `HU-34B / DES-47`
4. `HU-33B / DES-45`
5. `HU-35 / DES-48`
6. `HU-36A / DES-49`

Recommended sequence:

1. `HU-33A`
2. `HU-34A`
3. `HU-34B`
4. `HU-33B`
5. `HU-35`
6. `HU-36A`

Rationale:

- `HU-33A` activates timed rounds.
- `HU-34A` and `HU-34B` govern answer acceptance.
- `HU-33B` closes questions and moves the session across rounds and to final results.
- `HU-35` reveals outcome to participants after closure.
- `HU-36A` gives the operator minimal live supervision during the active question.

### 5. Scoring foundation and ranking

These make trivia scoring and leaderboard behavior durable and defensible.

1. `HU-37A / DES-51`
2. `HU-39B / DES-55`

Recommended sequence:

1. `HU-37A`
2. `HU-33B`
3. `HU-39B`

Rationale:

- `HU-37A` is the score ledger foundation.
- `HU-33B` is where round closure actually computes score and ranking progression.
- `HU-39B` exposes ranking updates after each closed question.

Important note:

- If the goal is truly `trivia completely`, `HU-37A` should be considered required even though it was not in the original shorter chain.

## Core Playable Trivia

This is the minimum recommended set for a playable end-to-end trivia implementation.

1. `HU-01 / DES-5`
2. `HU-06 / DES-10`
3. `HU-07A / DES-11`
4. `HU-07B / DES-12`
5. `HU-11 / DES-17`
6. `HU-14A / DES-20`
7. `HU-14B / DES-21`
8. `HU-12 / DES-18`
9. `HU-16 / DES-23`
10. `HU-18 / DES-25`
11. `HU-19 / DES-26`
12. `HU-21A / DES-28`
13. `HU-22 / DES-30`
14. `HU-33A / DES-44`
15. `HU-34A / DES-46`
16. `HU-34B / DES-47`
17. `HU-33B / DES-45`
18. `HU-35 / DES-48`
19. `HU-36A / DES-49`
20. `HU-37A / DES-51`
21. `HU-39B / DES-55`
22. `ENABLER / DES-58`

Total: `22` tickets.

## Required For Usable UX

These are not strictly lower-level gameplay mechanics, but without them the product is hard to call complete from the participant/operator point of view.

1. `HU-23 / DES-31` for the participant live board
2. `HU-25B / DES-35` for participant read-only board/ranking queries
3. `HU-36B / DES-50` for operator post-close answer review

These bring the running total to `25` tickets.

## Operational Completeness

These are important if the intended meaning of “complete” includes richer supervision and read models.

1. `HU-24B / DES-33` operator real-time panel with events/evidence/ranking

This brings the richer end-to-end total to `26` tickets.

## Summary

### Strict core

- `22` tickets
- Enough to author quizzes, run trivia rounds, accept answers, score them, rank teams, and support the mobile client baseline

### Product-complete baseline

- `25` tickets
- Core plus participant board, participant ranking access, and operator post-close review

### Operationally complete baseline

- `26` tickets
- Product-complete baseline plus richer operator real-time monitoring

## Differences From The Original Proposed Chain

1. `HU-12 / DES-18` should depend on `HU-11`, `HU-14A`, and `HU-14B`, not only `HU-11`.
2. `HU-18 / DES-25` is blocked in Linear by `HU-04`, `HU-15`, and `HU-16`, not only `HU-16`.
3. `HU-19 / DES-26` is not currently blocked by `HU-16` in Linear.
4. `HU-37A / DES-51` should be included if “trivia completely” means real scoring integrity, not just visible ranking.
5. `HU-23 / DES-31` should be included if “trivia completely” includes an actual participant-facing live experience.
