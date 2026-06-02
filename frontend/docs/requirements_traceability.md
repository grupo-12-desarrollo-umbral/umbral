# UMBRAL Requirements Traceability

This document is the active traceability map between the academic requirement baseline and the current canonical document set.

Use it to answer one question quickly:

- where each academic requirement is currently owned and defended in the active docs

This document does not replace:

- `docs/umbral_user_stories.md` as the backlog and acceptance-criteria owner
- `docs/bd_umbral_entity_spec.md` as the logical-model owner
- `docs/ddd_solution_model.md` as the DDD ownership map
- `docs/condensed_roadmap_umbral.md` as the roadmap and delivery-closure owner
- `docs/adr/001_platform_shape_adr.md` and `docs/references/` as architecture and engineering-rule owners

Active proof note:

- use this document as the explicit requirement-to-owner proof for current `RF`, `RNF`, and `RB` compliance

Historical note:

- `docs/archive/roadmap_umbral.md` may still be useful for traceability history, but it is no longer required for explicit `RNF` proof or for claiming current compliance

## Reading Rule

Interpretation used here:

- `Yes`: the active document set already provides enough current documentation to defend the requirement
- `Owner docs`: the main documents that currently carry that defense

## Functional Requirements

| Requirement | Status | Owner docs | Traceability note |
| --- | --- | --- | --- |
| `RF-01` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Mission CRUD and deactivation are covered by backlog stories and the `Mission` aggregate. |
| `RF-02` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Mission structure, nodes, clues, and maximum time are explicit in stories and in `Mission` / `MissionNode`. |
| `RF-03` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Session creation from an active mission is defended through stories plus `MissionActivation` and `SessionCreationPolicy`. |
| `RF-04` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Session states and valid transitions are explicit in the backlog and `SessionStateTransitionPolicy`. |
| `RF-05` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Team registration and session association are covered by stories and by `LiveSession`, `Team`, and participant structures. |
| `RF-06` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Team-facing timer, score, and released clue visibility are covered by the stories and `TeamBoardProjection`. |
| `RF-07` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Manual and rule-based clue release is supported by stories plus `ClueReleaseRecord` and `ClueReleasePolicy`. |
| `RF-08` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Evidence submission tied to mission context is covered by stories and the `EvidenceSubmission` model. |
| `RF-09` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Evidence traceability and validation state are explicit in stories, fields, and `EvidenceAcceptancePolicy`. |
| `RF-10` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Score recalculation after validation or penalty is covered by stories and `ScoreEntry` / `ScorePolicy`. |
| `RF-11` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Justified penalties are covered by stories and by the `Penalty` model fields. |
| `RF-12` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/condensed_roadmap_umbral.md`, `docs/references/architecture.md` | The logical basis lives in `Ranking`; the active closure to real-time delivery lives in the roadmap delta and architecture docs. |
| `RF-13` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md`, `docs/condensed_roadmap_umbral.md`, `docs/references/architecture.md` | Operator dashboard projections, operator stories, and the active SignalR closure together defend the requirement. |
| `RF-14` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md`, `docs/condensed_roadmap_umbral.md`, `docs/references/architecture.md`, `docs/references/conventions.md`, `docs/references/testing.md` | The model defines publishable facts; the roadmap, architecture, conventions, and testing docs define the required RabbitMQ workflow and delivery stance. |
| `RF-15` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Session history and audit traceability are explicit in stories and `SessionAuditTrailProjection`. |
| `RF-16` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md`, `docs/ddd_solution_model.md` | Role separation is covered by stories, logical access structures, and bounded-context ownership. |
| `RF-17` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md`, `docs/condensed_roadmap_umbral.md`, `docs/references/architecture.md`, `docs/references/conventions.md` | The model defines `CommandModel` and `QueryModel`; the active architecture docs close the full CQRS implementation stance. |
| `RF-18` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md`, `docs/references/conventions.md` | Business validation before state or evidence changes is defended by the policy catalog and validation rules in the application conventions. |

## Non-Functional Requirements

| Requirement | Status | Owner docs | Traceability note |
| --- | --- | --- | --- |
| `RNF-01` | Yes | `docs/condensed_roadmap_umbral.md`, `docs/references/architecture.md` | The active stack keeps React-based clients and a .NET backend. |
| `RNF-02` | Yes | `docs/adr/001_platform_shape_adr.md`, `docs/references/architecture.md` | PostgreSQL and EF Core are explicit active persistence choices with per-service ownership. |
| `RNF-03` | Yes | `docs/umbral_user_stories.md`, `docs/condensed_roadmap_umbral.md`, `docs/adr/001_platform_shape_adr.md`, `docs/references/architecture.md`, `docs/references/conventions.md` | WebSocket real-time delivery is satisfied through SignalR in the active architecture set. |
| `RNF-04` | Yes | `docs/umbral_user_stories.md`, `docs/condensed_roadmap_umbral.md`, `docs/references/architecture.md`, `docs/references/conventions.md` | MediatR and CQRS are explicit backend application-layer rules. |
| `RNF-05` | Yes | `docs/umbral_user_stories.md`, `docs/condensed_roadmap_umbral.md`, `docs/adr/001_platform_shape_adr.md`, `docs/references/architecture.md`, `docs/references/conventions.md`, `docs/references/testing.md` | RabbitMQ is explicitly reserved for secondary asynchronous processing, with a required publish/consume workflow. |
| `RNF-06` | Yes | `docs/ddd_solution_model.md`, `docs/adr/001_platform_shape_adr.md`, `docs/references/architecture.md` | Clean/hexagonal-compatible structure is explicit at service and layer level. |
| `RNF-07` | Yes | `docs/ddd_solution_model.md`, `docs/adr/001_platform_shape_adr.md`, `docs/references/architecture.md`, `docs/references/conventions.md` | Domain isolation from infrastructure and web-framework concerns is stated directly in the active architecture rules. |
| `RNF-08` | Yes | `docs/references/architecture.md`, `docs/references/conventions.md`, `docs/bd_umbral_entity_spec.md` | Logging, validation, and cross-cutting application concerns are explicitly placed in pipeline behaviours and application rules. |
| `RNF-09` | Yes | `docs/references/testing.md` | The active testing guidance keeps the backend coverage target as an academic quality objective and defines the priority test areas. |
| `RNF-10` | Yes | `docs/condensed_roadmap_umbral.md`, `docs/adr/001_platform_shape_adr.md` | Local execution through Docker Compose remains part of the active delivery stance for the core backend stack. |
| `RNF-11` | Yes | `docs/references/testing.md`, `docs/condensed_roadmap_umbral.md` | CI build-and-test expectations remain part of the active engineering and delivery guidance. |
| `RNF-12` | Yes | `docs/umbral_user_stories.md`, `docs/bd_umbral_entity_spec.md` | Main user flows and the supporting read models for those flows are explicit in the active backlog and logical projections. |

## Business Rules

| Requirement | Status | Owner docs | Traceability note |
| --- | --- | --- | --- |
| `RB-01` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Mission activation remains the prerequisite for session creation. |
| `RB-02` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Session start requires at least one registered team. |
| `RB-03` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Evidence rejection during paused, finished, or cancelled states is explicit. |
| `RB-04` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Duplicate clue release to the same team and stage is prohibited by model and stories. |
| `RB-05` | Yes | `docs/bd_umbral_entity_spec.md` | Evidence association to exactly one team, session, and mission node is explicit in the logical model. |
| `RB-06` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Penalties require reason and application time. |
| `RB-07` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Score traceability is preserved through `ScoreEntry` and related scoring stories. |
| `RB-08` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Ranking order and tie-break by resolution time are explicit in model and backlog. |
| `RB-09` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Valid session-state transitions remain explicit and auditable. |
| `RB-10` | Yes | `docs/bd_umbral_entity_spec.md`, `docs/umbral_user_stories.md` | Operator visibility and session-administration restrictions are explicitly covered. |

## Current Verdict

The active document set now supports a defensible `Yes` for all academic `RF`, `RNF`, and `RB` requirements when read as a coordinated set of owner documents.

The archived roadmap is no longer needed as the primary proof of compliance.
