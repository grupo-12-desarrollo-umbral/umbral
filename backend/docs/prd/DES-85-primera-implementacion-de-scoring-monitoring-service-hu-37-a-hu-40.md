# PRD - Primera implementacion de scoring-monitoring-service (HU-37 a HU-40)

> Linear: DES-85 (this PRD) - team `umbral-equipo-12`
> Bounded context: `ScoringMonitoring` (`scoring-monitoring-service`)
> Local copy created: 2026-07-09

## Problem Statement

The backlog already defines `HU-37` to `HU-40` for the `ScoringMonitoring`
bounded context, but the repository still lacks one local, service-scoped PRD
that turns those stories into a coherent first implementation plan for
`scoring-monitoring-service`.

Without that PRD, there is a high risk of building scoring behavior in the wrong
bounded context, especially by leaking score calculation, ranking derivation,
penalty ownership, or audit-history concerns into `SessionOperations`. There is
also a risk of treating score totals and rankings as mutable operational state
instead of derived views backed by a traceable `ScoreEntry` ledger.

The current repo state makes that risk concrete:

- `ScoringMonitoring` is already defined in canon as the owner of `ScoreEntry`,
  `Penalty`, `Ranking`, `AuditHistory`, and monitoring projections.
- `session-operations-service` is now publishing runtime facts such as trivia
  question close and final session-result events, but it does not own score
  calculation or ranking derivation.
- `scoring-monitoring-service` is still effectively skeletal, so the team needs
  one reference document that defines what this bounded context will own, which
  slices should land first, and how it must consume upstream runtime facts
  without taking runtime authority.

## Solution

Implement the first delivery of `scoring-monitoring-service` as the realization
of the `ScoringMonitoring` bounded context around an append-only `ScoreEntry`
ledger, justified `Penalty` application, derived `Ranking`,
business-readable `AuditHistory`, and monitoring-oriented projections.

The service should treat runtime facts from `SessionOperations` as inputs and
derive secondary scoring and monitoring views from them without owning session
progression, participant admission, clue release, or evidence acceptance. The
first implementation covers:

1. Score traceability foundation
   - record immutable `ScoreEntry` facts for accepted runtime outcomes and
     scoring adjustments
   - preserve origin traceability through `sourceEntityType` and
     `sourceEntityId`
   - keep accumulated score explainable from the ledger at all times
2. Penalty handling
   - allow justified operator-applied `Penalty` records with explicit
     `PenaltyReason`
   - apply deductions through the same traceable scoring model rather than a
     separate mutable total
3. Ranking derivation
   - derive one session `Ranking` from ledger facts
   - order by descending total score and use `ResolutionTime` as the tie-break
     criterion
   - refresh rankings after score-affecting events and expose real-time ranking
     snapshots
4. Audit and monitoring projections
   - maintain `AuditHistory` and supervision-oriented read models from consumed
     runtime and scoring facts
   - expose read surfaces for score history, ranking snapshots, audit review,
     and operator monitoring
5. Cross-context integration
   - consume completed runtime facts from `SessionOperations` through documented
     integration contracts
   - publish scoring-side facts only when other deployables genuinely need them

This implementation is organized around the deep modules already defined for the
context:

- `ScoreEntry` as the immutable scoring ledger root
- `Penalty` as the justified deduction fact linked to the ledger
- `Ranking` as a derived ordered competition view per `LiveSession`
- `AuditHistory` as the business-readable chronological trace of relevant
  runtime and scoring facts
- `ScorePolicy`, `RankingPolicy`, and `PenaltyPolicy` as explicit domain
  policies
- strategy-based scoring and ranking behavior, rather than handler-level
  branching

## User Stories

1. As an Operator, I want every change to a team's score to be recorded as a
   `ScoreEntry`, so the accumulated score is always explainable.
2. As an Operator, I want score changes caused by accepted treasure-hunt
   outcomes to produce traceable ledger facts, so mission scoring has business
   origin traceability.
3. As an Operator, I want score changes caused by accepted trivia outcomes to
   produce traceable ledger facts, so trivia scoring has business origin
   traceability.
4. As the system, I want score-affecting runtime facts to be consumed from
   `SessionOperations`, so scoring derives from completed business events
   instead of duplicating runtime rules.
5. As an Operator, I want the ledger to preserve the origin type and origin id
   of each scoring fact, so I can explain why points changed.
6. As an Operator, I want score records to be append-only, so traceability is
   preserved and totals are never opaque.
7. As an Operator, I want the team total score to be derived from ledger
   entries, so recalculation is deterministic.
8. As an Operator, I want the scoring service to handle grants, deductions, and
   corrections through one coherent model, so totals stay auditable.
9. As an Operator, I want to apply a justified `Penalty` to a team in a session
   I am authorized to supervise, so rule breaches can affect the score.
10. As an Operator, I want every penalty to require a `PenaltyReason`, so no
    deduction is left unexplained.
11. As an Operator, I want every penalty to record who applied it and when, so
    later review is possible.
12. As an Operator, I want a penalty to impact the team's score through the
    same ledger used for positive scoring, so deductions are not a special
    hidden total.
13. As an Operator, I want penalty eligibility checked before a deduction is
    recorded, so invalid penalties are rejected.
14. As an Administrator, I want to review penalty history without mutating it,
    so governance and audit are possible.
15. As a Participant, I want the ranking of my team within the session to be
    derived from the real score facts, so the competition view is trustworthy.
16. As an Operator, I want session ranking ordered from highest to lowest score,
    so the competition standing is clear.
17. As an Operator, I want score ties resolved using `ResolutionTime`, so the
    tie-break rule is explicit and consistent.
18. As an Operator, I want ranking recalculated after every score-affecting
    fact, so live supervision reflects the current competition state.
19. As a Participant, I want updated ranking delivered during live play, so I
    can track competitive position.
20. As an Operator, I want ranking snapshots for a live session, so supervision
    has an ordered competition view across the mission's substages, whatever
    their play mode.
21. As an Operator, I want monitoring projections to consolidate live score,
    ranking, and relevant scoring-side outcomes, so I can supervise from one
    read surface.
22. As an Operator, I want the monitoring view to stay derived from canonical
    scoring facts, so it never becomes an independent source of truth.
23. As an Operator, I want a business-readable `AuditHistory` for a session, so
    score changes, penalties, and relevant events can be reconstructed
    chronologically.
24. As an Administrator, I want audit history for completed or cancelled
    sessions to remain queryable, so post-session review is possible.
25. As an Operator, I want score history for one team in one session, so I can
    explain the team's current total.
26. As an Operator, I want audit and monitoring reads separated from mutation
    commands, so CQRS remains explicit.
27. As `SessionOperations`, I want `ScoringMonitoring` to consume runtime facts
    without taking over progression authority, so the bounded-context boundary
    stays intact.
28. As `SessionOperations`, I want scoring to derive from completed facts like
    accepted evidence, closed trivia rounds, penalties, and finalized results,
    so runtime services do not compute rankings themselves.
29. As the development team, I want scoring variation implemented through
    `Strategy`, so mode-specific scoring and tie-break rules do not become
    handler-level branching.
30. As the development team, I want penalty access guarded at the application
    boundary, so unauthorized operators cannot mutate scoring data.
31. As the development team, I want the first implementation split into
    verifiable slices, so the service can grow from the ledger foundation
    toward ranking and audit views safely.
32. As the development team, I want the first scoring slice to establish the
    ledger before richer ranking and history surfaces, so downstream slices
    share one canonical source.
33. As the development team, I want the scoring service to remain a supporting
    bounded context, so it never becomes a hidden owner of live session
    behavior.

## Implementation Decisions

- `ScoringMonitoring` is implemented around two main aggregate roots:
  `ScoreEntry` and `Penalty`. `Ranking`, `AuditHistory`, and monitoring
  dashboards are owned derived models, not runtime-authority aggregates.
- `ScoreEntry` is the immutable scoring ledger. Totals, rankings, and score
  history are always derived from it.
- `Penalty` is a justified scoring fact linked to the ledger, not a side table
  that mutates totals outside the scoring model.
- `Ranking` belongs to exactly one `LiveSession` and is derived from many
  `ScoreEntry` records.
- Ranking ordering is descending by total score; when totals tie, lower
  comparable `ResolutionTime` ranks first; equal or non-comparable resolution
  times may share rank.
- `ScoreValue`, `PenaltyReason`, and `ResolutionTime` stay explicit value
  objects in the bounded context vocabulary.
- The required `Strategy` pattern is mandatory here for `ScorePolicy`,
  `RankingPolicy`, and mode-specific scoring variation. Difficulty weighting,
  normalization, and tie-break logic must not be spread across handlers through
  conditionals.
- `PenaltyPolicy` validates whether a penalty may be applied and what
  justification is required before a ledger deduction is persisted.
- `ScoringMonitoring` consumes completed runtime facts from `SessionOperations`;
  it does not own clue release, evidence acceptance, participant admission, or
  session progression.
- Cross-context inputs include accepted evidence outcomes, trivia answer
  outcomes, question-close/final-results facts, session-state events relevant to
  monitoring, and operator identity facts required for authorized penalties.
- Cross-context outputs are limited to scoring-side business facts and
  projections that other deployables genuinely need; this service should not
  publish noisy internal recalculation chatter.
- The first delivery should align with the existing HU split:
  - `HU-37`: establish the `ScoreEntry` ledger from validations, answers, and
    penalties
  - `HU-38`: operator-applied justified penalties
  - `HU-39`: derive the session `Ranking` from the ledger, refresh it after every
    score-affecting fact, and expose the real-time ranking view
  - `HU-40A` / `HU-40B`: audit history plus score/ranking historical review
- Read surfaces stay CQRS-separated: ranking snapshots, score history,
  monitoring dashboards, and audit history are query-side concerns, not
  mutation handlers.
- Monitoring projections and `AuditHistory` are derived views owned by
  `ScoringMonitoring`; they do not replace `SessionOperations` as the authority
  on what happened during runtime.
- The first service delivery follows one functional backbone:
  1. immutable score ledger foundation
  2. justified penalty registration
  3. ranking recalculation and ranking snapshots
  4. monitoring projections and audit-history refresh

## Testing Decisions

- Good tests verify external behavior: which score entries are recorded, whether
  invalid penalties are rejected, how ranking order is produced, whether
  tie-break rules are honored, and what read models are exposed to authorized
  actors.
- Preferred seams are the highest useful seams:
  - domain tests for `ScoreEntry`, `Penalty`, `ScoreValue`, `PenaltyReason`,
    `ResolutionTime`, and scoring/ranking policies
  - application tests for commands and queries such as `RecordScoreEntry`,
    `ApplyPenalty`, `RecalculateRanking`, `GetRankingSnapshot`, and
    `GetAuditHistory`
  - integration tests for repository persistence, projection refresh, and
    consumed/published integration contracts
  - API tests for ranking, score-history, penalty, and audit endpoints
- Dedicated coverage is expected for:
  - append-only `ScoreEntry` behavior
  - source traceability through `sourceEntityType` and `sourceEntityId`
  - penalty justification requirements and operator restrictions
  - score derivation from accepted treasure-hunt and trivia facts
  - ranking recalculation after score-affecting events
  - `ResolutionTime` tie-break behavior
  - real-time ranking publication paths
  - audit-history refresh from runtime and scoring facts
  - separation between runtime authority and derived scoring views
- Prior art to reuse:
  - repository and integration-test patterns already present in
    `session-operations-service`, `mission-design-service`, and
    `identity-access-service`
  - event-publication and integration-boundary patterns already established
    around the runtime services
  - existing CQRS handler and validator test style used across the backend
    services

## Out of Scope

- Owning session progression, participant admission, clue release, evidence
  acceptance, or any other runtime-authority rule that belongs to
  `SessionOperations`.
- Editing mission content, trivia content, or mission readiness rules owned by
  `MissionDesign`.
- Re-implementing authentication or role provisioning owned by `Identity` and
  the gateway.
- Replacing the operator or participant UI itself; this PRD is for the backend
  bounded context and its contracts.
- Inventing a mutable session score total outside the `ScoreEntry` ledger.
- Computing rankings inside `session-operations-service` instead of deriving
  them here.

## Further Notes

- This bounded context is the canonical owner of scoring, ranking, and
  monitoring views. The service must stay strict about that boundary, especially
  now that `SessionOperations` emits runtime facts that scoring is expected to
  consume.
- The implementation order matters: the score ledger (`HU-37`) is the foundation
  for ranking derivation, refresh, and the real-time ranking view (`HU-39`), and
  later audit-history review (`HU-40A` / `HU-40B`).
- `HU-38`, `HU-40A`, and parts of operator monitoring cross the service
  boundary with `SessionOperations`, but ownership of scoring traceability,
  ranking derivation, and scoring-side audit projections remains in
  `ScoringMonitoring`.
