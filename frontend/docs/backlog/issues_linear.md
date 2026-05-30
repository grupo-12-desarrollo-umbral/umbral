# UMBRAL Linear Backlog Snapshot

This document captures the Linear backlog created for team `umbral-equipo-12` in project `Umbral Proyecto Desarrollo - Equipo 12`.

## Glossary

- `Blocked by`: dependency relation. The issue should not start before the referenced issue exists or is sufficiently completed because it provides a required capability, rule, or data flow.
- `Enabler`: technical or architectural backlog item that supports one or more user-facing stories but is not itself written as a user story.
- `svc:*`: additive microservice ownership label used in Linear. These labels complement, not replace, workflow labels like `Feature` or `Improvement`.
- `Feature`: user-facing or business-facing backlog item derived from one or more `HU-*` stories.
- `Improvement`: technical backlog item used here for enablers and cross-cutting implementation capabilities.

## Labels

Linear labels added for microservice ownership:

- `svc:identity-access-service`
- `svc:mission-design-service`
- `svc:session-operations-service`
- `svc:scoring-monitoring-service`
- `svc:cross-service`

Issue type labels used:

- `Feature`
- `Improvement`

## Identity Access Service

- `DES-5` `HU-01 - Inicio de sesión general de usuarios`
  Blocked by: none
- `DES-6` `HU-02 - Gestión de acceso de usuarios`
  Blocked by: none
- `DES-7` `HU-03 - Asignación de roles y permisos`
  Blocked by: none
- `DES-8` `HU-04 - Registro y mantenimiento de equipos`
  Blocked by: none
- `DES-9` `HU-05 - Asignación de participantes a equipos`
  Blocked by: none
- `DES-10` `HU-06 - Inicio de sesión de participantes`
  Blocked by: `DES-5`
- `DES-11` `HU-07A - Validación de membresía del participante en sesión`
  Blocked by: `DES-9`, `DES-10`
- `DES-12` `HU-07B - Reconexión autorizada del participante`
  Blocked by: `DES-11`

## Mission Design Service

- `DES-14` `HU-09 - Gestión de misiones`
  Blocked by: none
- `DES-15` `HU-10A - Estructura jerárquica de misiones`
  Blocked by: `DES-14`
- `DES-16` `HU-10B - Validaciones estructurales de misión`
  Blocked by: `DES-15`
- `DES-17` `HU-11 - Creación y edición de quizzes de trivia`
  Blocked by: none
- `DES-18` `HU-12 - Publicación y archivado de quizzes de trivia`
  Blocked by: `DES-17`, `DES-20`, `DES-21`
- `DES-19` `HU-13 - Duplicación y retiro de quizzes usados`
  Blocked by: `DES-17`, `DES-18`
- `DES-20` `HU-14A - Gestión de preguntas y opciones de trivia`
  Blocked by: `DES-17`
- `DES-21` `HU-14B - Reglas de validación de preguntas de trivia`
  Blocked by: `DES-20`

## Session Operations Service

- `DES-22` `HU-15 - Creación de sesión de misión desde misión activa`
  Blocked by: `DES-14`, `DES-15`, `DES-16`
- `DES-23` `HU-16 - Creación de sesiones trivia`
  Blocked by: `DES-17`, `DES-18`, `DES-20`, `DES-21`
- `DES-24` `HU-17 - Creación de sesión desde una única fuente`
  Blocked by: `DES-22`, `DES-23`
- `DES-25` `HU-18 - Asociación de equipos a sesiones`
  Blocked by: `DES-8`, `DES-22`, `DES-23`
- `DES-26` `HU-19 - Asignación de operador a sesión`
  Blocked by: `DES-7`, `DES-22`, `DES-23`
- `DES-27` `HU-20 - Consulta de sesiones asignadas`
  Blocked by: `DES-26`
- `DES-28` `HU-21A - Transiciones válidas de estado de sesión`
  Blocked by: `DES-22`, `DES-23`, `DES-25`, `DES-26`
- `DES-29` `HU-21B - Auditoría de cambios de estado de sesión`
  Blocked by: `DES-28`
- `DES-30` `HU-22 - Temporizador autoritativo de sesión`
  Blocked by: `DES-28`
- `DES-31` `HU-23 - Tablero de equipo en vivo`
  Blocked by: `DES-30`, `DES-51`
- `DES-32` `HU-24A - Panel del operador en tiempo real de estado y progreso`
  Blocked by: `DES-27`, `DES-28`
- `DES-33` `HU-24B - Panel del operador en tiempo real de eventos, evidencias y ranking`
  Blocked by: `DES-32`, `DES-43`, `DES-54`, `DES-55`
- `DES-34` `HU-25A - Consultas operativas para administrador y operador`
  Blocked by: `DES-27`, `DES-54`, `DES-55`
- `DES-35` `HU-25B - Consulta de tablero y ranking para participante`
  Blocked by: `DES-31`, `DES-54`, `DES-55`
- `DES-36` `HU-26 - Liberación manual de pistas`
  Blocked by: `DES-28`, `DES-30`, `DES-31`
- `DES-37` `HU-27 - Liberación condicionada por reglas de avance`
  Blocked by: `DES-28`, `DES-31`, `DES-36`
- `DES-38` `HU-28 - Agregar pistas operativas durante sesión en vivo`
  Blocked by: `DES-28`, `DES-30`, `DES-31`
- `DES-39` `HU-29 - Envío de evidencias por parte del equipo`
  Blocked by: `DES-28`, `DES-30`, `DES-31`
- `DES-40` `HU-30A - Validaciones de contexto para aceptación de evidencias`
  Blocked by: `DES-39`
- `DES-41` `HU-30B - Rechazo explicado de evidencias inválidas`
  Blocked by: `DES-40`
- `DES-42` `HU-31 - Validación server-side de objetivo QR`
  Blocked by: `DES-39`, `DES-40`
- `DES-43` `HU-32 - Trazabilidad de evidencias`
  Blocked by: `DES-39`, `DES-40`, `DES-41`
- `DES-44` `HU-33A - Orquestación automatizada de trivia por rondas`
  Blocked by: `DES-23`, `DES-28`, `DES-30`
- `DES-45` `HU-33B - Cierre automático de preguntas y resultados finales de trivia`
  Blocked by: `DES-44`, `DES-51`, `DES-55`
- `DES-46` `HU-34A - Registro de primera respuesta válida por equipo en trivia`
  Blocked by: `DES-44`, `DES-11`
- `DES-47` `HU-34B - Rechazo de respuestas tardías o repetidas en trivia`
  Blocked by: `DES-46`
- `DES-48` `HU-35 - Revelación de resultado y explicación en trivia`
  Blocked by: `DES-45`, `DES-55`
- `DES-49` `HU-36A - Monitoreo restringido de respondido/no respondido en trivia`
  Blocked by: `DES-44`, `DES-46`
- `DES-50` `HU-36B - Revisión post-cierre de respuestas y puntos en trivia`
  Blocked by: `DES-45`, `DES-51`

## Scoring Monitoring Service

- `DES-51` `HU-37A - Ledger de puntaje por validaciones, respuestas y penalizaciones`
  Blocked by: `DES-40`, `DES-46`, `DES-53`
- `DES-52` `HU-37B - Actualización de ranking tras cambios de puntaje`
  Blocked by: `DES-51`
- `DES-53` `HU-38 - Aplicación de penalizaciones justificadas`
  Blocked by: `DES-26`, `DES-28`
- `DES-54` `HU-39A - Ranking en tiempo real para sesiones de misión`
  Blocked by: `DES-51`, `DES-52`
- `DES-55` `HU-39B - Ranking en tiempo real para sesiones de trivia`
  Blocked by: `DES-45`, `DES-51`, `DES-52`
- `DES-56` `HU-40A - Historial de eventos de sesión`
  Blocked by: `DES-29`, `DES-36`, `DES-39`, `DES-53`
- `DES-57` `HU-40B - Historial de puntaje, ranking y revisión post sesión`
  Blocked by: `DES-56`, `DES-51`, `DES-54`, `DES-55`

## Cross Service Enablers

- `DES-58` `ENABLER - Cliente móvil de participantes en React Native`
  Blocked by: `DES-10`, `DES-13`, `DES-31`, `DES-35`, `DES-48`
- `DES-59` `ENABLER - Sincronización multi-dispositivo por equipo`
  Blocked by: `DES-12`, `DES-13`, `DES-30`, `DES-31`
- `DES-60` `ENABLER - Publicación de eventos de dominio a RabbitMQ`
  Blocked by: `DES-39`, `DES-40`, `DES-51`, `DES-56`
- `DES-61` `ENABLER - Consultas separadas de comandos`
  Blocked by: `DES-34`, `DES-35`, `DES-54`, `DES-55`

## Notes

- The backlog was created using the finer-grained Option B split.
- Some issues carry more than one `svc:*` label in Linear when they cross service boundaries.
- The canonical source for story scope remains `docs/umbral_user_stories.md`.
- Linear now contains actual dependency links for the `Blocked by` relations listed here.
