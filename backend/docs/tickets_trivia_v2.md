# Sprint 1 — Trivia completo

Tickets necesarios para implementar trivia de extremo a extremo (frontend + backend + mobile).
Excluye todo lo relacionado con TreasureHunt/misión.

---

## `identity-access-service` — 8 tickets

| Ticket | Título | Bloqueado por |
|---|---|---|
| HU-01 | Inicio de sesión general de usuarios | — |
| HU-02 | Gestión de acceso de usuarios | — |
| HU-03 | Asignación de roles y permisos | — |
| HU-04 | Registro y mantenimiento de equipos | — |
| HU-05 | Asignación de participantes a equipos | — |
| HU-06 | Inicio de sesión de participantes | — |
| HU-07A | Validación de membresía del participante en sesión | — |
| HU-07B | Reconexión autorizada del participante | — |

---

## `mission-design-service` — 5 tickets (solo quiz, sin misión/TreasureHunt)

| Ticket | Título | Bloqueado por |
|---|---|---|
| HU-11 | Creación y edición de quizzes de trivia | — ← empezar aquí |
| HU-14A | Gestión de preguntas y opciones de trivia | HU-11 |
| HU-14B | Reglas de validación de preguntas | HU-14A |
| HU-12 | Publicación y archivado de quizzes | HU-11 |
| HU-13 | Duplicación y retiro de quizzes usados | HU-11, HU-12 |

---

## `session-operations-service` — 11 tickets

| Ticket | Título | Bloqueado por |
|---|---|---|
| HU-16 | Creación de sesiones trivia | HU-12 |
| HU-21A | Transiciones válidas de estado de sesión | — |
| HU-21B | Auditoría de cambios de estado de sesión | HU-21A |
| HU-22 | Temporizador autoritativo de sesión | HU-21A |
| HU-33A | Orquestación automatizada de trivia por rondas | HU-16, HU-21A, HU-22 |
| HU-33B | Cierre automático de preguntas y resultados finales | HU-33A |
| HU-34A | Registro de primera respuesta válida por equipo | HU-33A |
| HU-34B | Rechazo de respuestas tardías o repetidas | HU-34A |
| HU-35 | Revelación de resultado y explicación | HU-33B |
| HU-36A | Monitoreo restringido respondido/no respondido | HU-34A |
| HU-36B | Revisión post-cierre de respuestas y puntos | HU-33B |

---

## `scoring-monitoring-service` — 3 tickets

| Ticket | Título | Bloqueado por |
|---|---|---|
| HU-37A | Ledger de puntaje por respuestas | — |
| HU-37B | Actualización de ranking tras cambios de puntaje | HU-37A |
| HU-39B | Ranking en tiempo real para sesiones de trivia | HU-33B |

---

## Mobile — 1 enabler

| Ticket | Título |
|---|---|
| ENABLER | Cliente móvil de participantes en React Native |

---

## Excluidos explícitamente (TreasureHunt / misión)

- HU-09, HU-10 — autoría de misiones
- HU-15 — creación de sesión de misión
- HU-39A — ranking en tiempo real para sesiones de **misión**
- Todos los tickets de pistas y envío de evidencia

---

## Ruta crítica

```
HU-11 → HU-12 → HU-16 → HU-21A → HU-22 → HU-33A → HU-33B → HU-39B
                                                 ↓
                                           HU-34A → HU-34B → HU-35
```

Auth (HU-01 a HU-07B) corre en paralelo — servicio independiente.
