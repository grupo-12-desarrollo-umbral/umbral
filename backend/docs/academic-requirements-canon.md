# UMBRAL — Canonical Academic Requirements (Sections 7–10)

> Source: `Proyecto_Integrador_UMBRAL_UCAB.pdf` — Academic Statement of the Integrative Project (UCAB, 2026).
> This document restates sections 7 through 10 of the statement in English, mapped onto the
> ubiquitous language of each bounded context defined in [`CONTEXT-MAP.md`](../CONTEXT-MAP.md):
> `Identity`, `MissionDesign`, `SessionOperations`, and `ScoringMonitoring`.
> It preserves the original scope and intent; it does not extend it. Domain terms are written in
> their canonical form (e.g. `LiveSession`, `EvidenceSubmission`) so this file can be used to check
> academic compliance against the implemented model.

---

## 7. System Actors

Actors correspond to the `Role` concept owned by the `Identity` bounded context. Minimum permissions
describe `Access Facts`; the **final admission** into a `LiveSession` is always decided by
`SessionOperations`, never by `Identity`.

| Actor | Primary responsibilities | Minimum expected permissions |
| --- | --- | --- |
| **Administrator** | Authors `Mission`s and `TriviaQuiz`es, consults `LiveSession`s, and maintains base catalogs (including `Team` reference data). | Create/edit `Mission`, consult `LiveSession`s, manage `User`s and `Team` registration. |
| **Operator** | Starts `LiveSession`s, performs `ClueRelease`, applies `Penalty`, and supervises runtime execution. | Create `LiveSession`, change `SessionState`, release `Clue`s, observe `Ranking` and `AuditHistory`. |
| **Participant Team** | Consults its team board, receives released `Clue`s, and submits answers or evidence. | Enter its `LiveSession`, view progress, and register `EvidenceSubmission`s. |

---

## 8. Functional Requirements

| Code | Functional requirement | Owning context |
| --- | --- | --- |
| **RF-01** | The system must allow creating, editing, consulting, and deactivating `Mission`s (`MissionActivation`). | `MissionDesign` |
| **RF-02** | Each `Mission` must allow registering `Stage`/`Substage` nodes, `Clue`s, and a `MaximumTime` for execution. | `MissionDesign` |
| **RF-03** | The system must allow creating a `LiveSession` from an active `Mission` (`SessionSource`). | `SessionOperations` |
| **RF-04** | The `LiveSession` must manage at least the `SessionState`s `Preparing`, `Active`, `Paused`, `Finished`, and `Cancelled`. | `SessionOperations` |
| **RF-05** | The system must allow registering participant `Team`s and associating them with a `LiveSession`. | `Identity` / `SessionOperations` |
| **RF-06** | Each `Team` must be able to view its timer, score, and released `Clue`s. | `SessionOperations` |
| **RF-07** | The `Operator` must be able to perform `ClueRelease` manually or conditioned by progression rules. | `SessionOperations` |
| **RF-08** | `Team`s must be able to submit `EvidenceSubmission`s (answers or evidence) associated with a `MissionNode`. | `SessionOperations` |
| **RF-09** | Each `EvidenceSubmission` must be recorded with timestamp, `Team`, `LiveSession`, and `EvidenceValidationState`. | `SessionOperations` |
| **RF-10** | The system must produce a `ScoreEntry` recalculating a `Team`'s score when an `EvidenceSubmission` is accepted or penalized. | `ScoringMonitoring` |
| **RF-11** | The `Operator` must be able to apply a justified `Penalty` (with `PenaltyReason`) to a `Team`. | `ScoringMonitoring` |
| **RF-12** | The session `Ranking` must be shown and updated in real time. | `ScoringMonitoring` |
| **RF-13** | The `Operator` dashboard must reflect `SessionState` changes and relevant `SessionEvent`s in real time. | `SessionOperations` |
| **RF-14** | The application must publish domain `SessionEvent`s on RabbitMQ when evidence is registered or significant changes occur. | `SessionOperations` |
| **RF-15** | An `AuditHistory` of `SessionEvent`s must exist with minimum traceability for audit. | `ScoringMonitoring` |
| **RF-16** | The application must differentiate capabilities by `Role`. | `Identity` |
| **RF-17** | The solution must allow consulting `Mission`s, `LiveSession`s, `Team`s, and `Ranking` through queries separated from commands (CQRS). | All contexts |
| **RF-18** | The application must run business validations before accepting `SessionState` changes or `EvidenceSubmission`s. | `SessionOperations` |

---

## 9. Non-Functional Requirements

| Code | Non-functional requirement |
| --- | --- |
| **RNF-01** | The solution must be implemented with a React frontend and a .NET Core backend. |
| **RNF-02** | Primary persistence must be resolved with PostgreSQL and Entity Framework Core. |
| **RNF-03** | Real-time communication must be implemented over WebSockets. |
| **RNF-04** | Application logic must be structured with MediatR and a CQRS approach. |
| **RNF-05** | Asynchronous processes must be decoupled through RabbitMQ. |
| **RNF-06** | The solution must follow hexagonal architecture or a variant compatible with clean architecture. |
| **RNF-07** | The domain must not depend on infrastructure or web-framework details. |
| **RNF-08** | The application must incorporate consistent logging, exception handling, and validation. |
| **RNF-09** | The backend must reach an academic target of at least 90% test coverage. |
| **RNF-10** | The solution must be runnable locally through Docker Compose. |
| **RNF-11** | The repository must include a continuous-integration pipeline for build and test execution. |
| **RNF-12** | The interface must be clear, usable, and coherent with the system's main flows. |

---

## 10. Business Rules

| Code | Business rule | Owning context |
| --- | --- | --- |
| **RB-01** | A `Mission` may only be used to create `LiveSession`s if it is active (`MissionActivation` / `Source Readiness`). | `MissionDesign` |
| **RB-02** | A `LiveSession` cannot enter `Active` if it has no registered `Team`. | `SessionOperations` |
| **RB-03** | `EvidenceSubmission`s must not be accepted while the `SessionState` is `Paused`, `Finished`, or `Cancelled`. | `SessionOperations` |
| **RB-04** | A `Clue` cannot be released twice to the same `Team` for the same `Substage` (`ClueReleaseRecord`). | `SessionOperations` |
| **RB-05** | Each `EvidenceSubmission` must be associated with exactly one `Team`, one `LiveSession`, and one `MissionNode`. | `SessionOperations` |
| **RB-06** | Every `Penalty` must record its `PenaltyReason` and the moment of application. | `ScoringMonitoring` |
| **RB-07** | A `Team`'s accumulated score must never lack origin traceability — it is derived from `ScoreEntry` records (`Scoring Traceability`). | `ScoringMonitoring` |
| **RB-08** | The `Ranking` must order teams by highest score first, using `ResolutionTime` as the tie-break criterion when applicable. | `ScoringMonitoring` |
| **RB-09** | `SessionState` changes must respect valid transitions (`State` pattern). | `SessionOperations` |
| **RB-10** | The `Operator` may only administer `LiveSession`s assigned to or visible to them per the team-defined policy. | `Identity` / `SessionOperations` |

---

## Divergences from the original statement

Where the implemented domain model intentionally departs from, narrows, or under-specifies the
original UCAB statement. Recorded so this canon doc is explicit about the gaps when checking
academic compliance.

### Tensions worth a decision

| Ref | Divergence | Detail |
| --- | --- | --- |
| **RF-04** | Missing `SessionState`. | The statement lists *at least* six states (`programada`, `en preparación`, `activa`, `pausada`, `finalizada`, `cancelada`); "at least" makes this a minimum set. The `SessionOperations` model defines five (`Preparing`, `Active`, `Paused`, `Finished`, `Cancelled`) — there is no **`Scheduled`/programada** state. Either it was deliberately collapsed into `Preparing` (then this should be a recorded decision) or it is a gap below the stated minimum. |
| **RB-08 / RF-12** | Two canonical names for the tie-break time. | The statement calls it *tiempo de resolución*. `SessionOperations` names the underlying value `SolutionTime`; `ScoringMonitoring` names the ranking tie-break `ResolutionTime`. Same concept, two terms across contexts — a ubiquitous-language inconsistency independent of the PDF. |
| **RB-10** | Operator/session scoping not modeled. | The rule assumes an operator-to-`LiveSession` assignment/visibility policy. Neither `Identity` nor `SessionOperations` defines such a concept; it is only loosely covered by the `Proxy` pattern. Under-specified relative to the rule. |

### Intentional refinements (mapping, not error)

| Ref | Refinement | Detail |
| --- | --- | --- |
| **RF-08 / RF-09** | Evidence is narrowed. | The statement's generic *respuestas o evidencias* is realized as two concrete refinements: `TreasureEvidenceSubmission` (**QR-only**, per ADR-0010) and `TriviaAnswerSubmission`. Free-form evidence (photo/text) is out of scope. |
| **RB-04 / RB-05 / RF-08** | *"etapa"* maps to a finer node. | The statement says *etapa* (Stage). The model attaches `Clue`s and `EvidenceSubmission`s to `Substage` / `MissionNode`, not to the top-level `Stage`. The mapping is an interpretation, not a literal match. |
| **RF-05** | Team registration is split. | The statement treats it as one action; the model splits it across `Identity` (reference-data `Team` + `SessionTeamAssociation`) and `SessionOperations` (runtime `Team`). |
