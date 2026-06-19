# Academic Requirements — UMBRAL Integrator Project (UCAB 2026)

> Canonical academic-requirements reference. Faithful English transcription of **sections 7 to 10**
> of the official brief `Proyecto_Integrador_UMBRAL_UCAB.pdf`, expressed in the project's **ubiquitous
> language** as defined by each bounded-context `CONTEXT.md` (see `CONTEXT-MAP.md` and
> `backend/CONTEXT-MAP.md`). Use it to verify that the implementation satisfies the actors,
> functional requirements, non-functional requirements, and business rules required by the course.
>
> Domain terms are written `LikeThis` to match the canonical context language. Where the brief's
> wording diverges from the canon, the canonical term is used and the divergence is footnoted.

## Bounded contexts referenced

| Context | Service | Owns |
| --- | --- | --- |
| `Identity` | `identity-access-service` | `User`, `Role`, `Access Facts`, team reference data, join authorization. |
| `MissionDesign` | `mission-design-service` | `Mission` / `MissionNode` authoring, `TriviaQuiz` authoring, source readiness. |
| `SessionOperations` | `session-operations-service` | `LiveSession` runtime, participation, `ClueRelease`, `EvidenceSubmission`, final admission. |
| `ScoringMonitoring` | `scoring-monitoring-service` | `ScoreEntry`, `Penalty`, `Ranking`, `AuditHistory`, monitoring projections. |

---

## 7. System actors

| Actor | Main responsibilities | Minimum expected permissions | Primary contexts |
| --- | --- | --- | --- |
| **Administrator** | Configures `Mission`s, consults `LiveSession`s, and maintains base catalogs. | Create/edit a `Mission`, consult `LiveSession`s, manage `User`s and `Role`s. | `MissionDesign`, `Identity` |
| **Operator** | Starts `LiveSession`s, performs `ClueRelease`, applies `Penalty`s, and supervises execution. | Create a `LiveSession`, change `SessionState`, perform `ClueRelease`, observe `Ranking` and `AuditHistory`. | `SessionOperations`, `ScoringMonitoring` |
| **Participant team** | Consults its board, receives `Clue`s, and submits answers or evidence. | Access its `LiveSession`, view `Team` progress, and register `EvidenceSubmission`s. | `SessionOperations` |

---

## 8. Functional requirements

| Code | Functional requirement | Owning context |
| --- | --- | --- |
| **FR-01** | The system must allow creating, editing, consulting, and deactivating `Mission`s. | `MissionDesign` |
| **FR-02** | Each `Mission` must allow registering `Stage`/`Substage` `MissionNode`s, `Clue`s, and a `MaximumTime` for execution. | `MissionDesign` |
| **FR-03** | The system must allow creating a `LiveSession` from an active `Mission` (`MissionActivation`). | `SessionOperations` |
| **FR-04** | The `LiveSession` must manage at least the `SessionState`s `Scheduled`, `Preparing`, `Active`, `Paused`, `Finished`, and `Cancelled`.[^states] | `SessionOperations` |
| **FR-05** | The system must allow registering participant `Team`s and associating them with a `LiveSession`. | `SessionOperations`, `Identity` |
| **FR-06** | Each `Team` must visualize its timer, score, and enabled `Clue`s. | `SessionOperations` |
| **FR-07** | The `Operator` must be able to perform `ClueRelease` manually or conditioned by progression rules (`ClueVisibilityPolicy`). | `SessionOperations` |
| **FR-08** | `Team`s must be able to submit answers or evidence (`EvidenceSubmission`) associated with a `MissionNode` of the `Mission`. | `SessionOperations` |
| **FR-09** | Each `EvidenceSubmission` must be recorded with date, `Team`, `LiveSession`, and `EvidenceValidationState`. | `SessionOperations` |
| **FR-10** | The system must recalculate the `Team`'s score (`ScoreEntry`) when an `EvidenceSubmission` is validated or penalized. | `ScoringMonitoring` |
| **FR-11** | The `Operator` must be able to apply justified `Penalty`s to a `Team`. | `ScoringMonitoring`, `SessionOperations` |
| **FR-12** | The `Ranking` of the `LiveSession` must be shown and updated in real time. | `ScoringMonitoring` |
| **FR-13** | The `Operator` panel must reflect `SessionState` changes and relevant `SessionEvent`s in real time. | `SessionOperations`, `ScoringMonitoring` |
| **FR-14** | The application must publish domain events (`SessionEvent`) to RabbitMQ when `EvidenceSubmission`s are registered or on significant changes. | `SessionOperations` |
| **FR-15** | An `AuditHistory` of `SessionEvent`s must exist with minimum traceability for auditing. | `ScoringMonitoring` |
| **FR-16** | The application must differentiate capabilities by `Role`. | `Identity` |
| **FR-17** | The solution must allow consulting `Mission`s, `LiveSession`s, `Team`s, and `Ranking` through queries separated from commands. | All contexts |
| **FR-18** | The application must provide business validations before accepting `SessionState` changes or `EvidenceSubmission`s. | `SessionOperations` |

[^states]: The brief lists the states *scheduled, in preparation, active, paused, finished, and cancelled*. The `SessionOperations` canon maps each of these one-to-one to a distinct `SessionState`: `Scheduled` (initial, set at `LiveSession` creation), `Preparing`, `Active`, `Paused`, `Finished`, and `Cancelled`.

---

## 9. Non-functional requirements

| Code | Non-functional requirement |
| --- | --- |
| **NFR-01** | The solution must be implemented with a React frontend and a .NET Core backend. |
| **NFR-02** | Primary persistence must be resolved with PostgreSQL and Entity Framework Core. |
| **NFR-03** | Real-time communication must be implemented over WebSockets. |
| **NFR-04** | Application logic must be structured with MediatR and a CQRS approach. |
| **NFR-05** | Asynchronous processes must be decoupled via RabbitMQ. |
| **NFR-06** | The solution must follow a hexagonal architecture or a variant compatible with clean architecture. |
| **NFR-07** | The domain must not depend on infrastructure or web-framework details. |
| **NFR-08** | The application must incorporate consistent logging, exception handling, and validations. |
| **NFR-09** | The backend must reach, as an academic target, a test coverage of at least 90%. |
| **NFR-10** | The solution must be runnable locally via Docker Compose. |
| **NFR-11** | The repository must include a continuous-integration pipeline for compilation and test execution. |
| **NFR-12** | The interface must be clear, usable, and coherent with the system's main flows. |

---

## 10. Business rules

| Code | Business rule | Owning context |
| --- | --- | --- |
| **BR-01** | A `Mission` may only be used to create `LiveSession`s if it is active (`MissionActivation`). | `MissionDesign` |
| **BR-02** | A `LiveSession` cannot start if it has no registered `Team`. | `SessionOperations` |
| **BR-03** | `EvidenceSubmission`s must not be accepted if the `LiveSession` is `Paused`, `Finished`, or `Cancelled`. | `SessionOperations` |
| **BR-04** | A `Clue` may become visible to one `Team`, all `Team`s, or multiple specific `Team`s in the same `Substage`; it must not be released more than once to the same `Team` in the same `Substage` of the same `LiveSession` (`ClueReleaseRecord`). | `SessionOperations` |
| **BR-05** | Each `EvidenceSubmission` must be associated with exactly one `Team`, one `LiveSession`, and one `MissionNode`. | `SessionOperations` |
| **BR-06** | Every `Penalty` must record its `PenaltyReason` and moment of application. | `ScoringMonitoring` |
| **BR-07** | A `Team`'s accumulated score must never be left without origin traceability (`ScoreEntry`). | `ScoringMonitoring` |
| **BR-08** | The `Ranking` must be ordered from highest to lowest score and use `ResolutionTime` as the tie-break criterion when applicable. | `ScoringMonitoring` |
| **BR-09** | `LiveSession` `SessionState` changes must respect valid transitions. | `SessionOperations` |
| **BR-10** | The `Operator` may only administer `LiveSession`s that are assigned or visible to them according to the policy defined by the team. | `SessionOperations`, `Identity` |

---

## Divergences from the original statement

Where the implemented domain model intentionally departs from, narrows, or under-specifies the
original UCAB statement. Recorded so this canon doc is explicit about the gaps when checking
academic compliance.

### Intentional refinements (mapping, not error)

| Ref | Refinement | Detail |
| --- | --- | --- |
| **FR-08 / FR-09** | Evidence is narrowed. | The statement's generic *respuestas o evidencias* is realized as two concrete refinements: `TreasureEvidenceSubmission` (**QR-only**, per ADR-0010) and `TriviaAnswerSubmission`. Free-form evidence (photo/text upload) is out of scope. |
| **BR-04** | `ClueRelease` is team-scoped. | The same `Clue` can become visible to more than one `Team` in the same `Substage`. `ClueReleaseRecord` prevents duplicate visibility only for the same `LiveSession`, `Substage`, `Team`, and `Clue` combination. |
| **BR-05 / FR-08** | *"etapa"* maps to a finer node. | The statement says *etapa* (`Stage`). The model attaches `EvidenceSubmission`s to a `Substage` / `MissionNode`, not to the top-level `Stage`. This is an interpretation, not a literal match. |
| **FR-05** | Team registration is split. | The statement treats it as one action; the model splits it across `Identity` (reference-data `Team` + `SessionTeamAssociation`) and `SessionOperations` (runtime `Team` + `Final Admission Decision`). |

---

_Source: `backend/docs/Proyecto_Integrador_UMBRAL_UCAB.pdf` — sections 7 to 10. UCAB 2026. Terminology aligned to the per-context `CONTEXT.md` files referenced in `backend/CONTEXT-MAP.md`._
