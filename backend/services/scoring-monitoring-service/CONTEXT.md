# Scoring Monitoring Service

`scoring-monitoring-service` realizes the `ScoringMonitoring` bounded context. It owns scoring facts, penalty traceability, ranking derivation, audit history, and monitoring-oriented read models.

## Language

### Scoring

**ScoreEntry**:
The traceable record of points granted or penalized for a `Team`.
_Avoid_: score, total score, points record

**ScoreValue**:
The value object that represents a score quantity under domain rules.
_Avoid_: points, score number

**Penalty**:
The justified scoring adjustment applied to a `Team` during a `LiveSession`.
_Avoid_: deduction, punishment

**PenaltyReason**:
The value object that records the reason for a `Penalty`.
_Avoid_: note, comment, deduction text

### Ranking And Monitoring

**Ranking**:
The ordered competition view of teams in a `LiveSession` derived from scoring facts.
_Avoid_: leaderboard, standings

**ResolutionTime**:
The value object used as a tie-break criterion in `Ranking` when scores are equal.
_Avoid_: time to solve, elapsed time

**AuditHistory**:
The chronological view of `SessionEvent` records preserved for traceability.
_Avoid_: event log, session log

**Monitoring Projection**:
A derived supervision-oriented view built from runtime outcomes and scoring facts to support operators and observers.
_Avoid_: source of truth aggregate, live-session owner

## Boundary Rules

**Scoring Traceability**:
The accumulated score of a `Team` must always be explainable through `ScoreEntry` records and explicit `Penalty` application.
_Avoid_: opaque score total

**Derived Views**:
`Ranking`, `AuditHistory`, and monitoring views are derived models owned by `ScoringMonitoring`; they do not replace runtime authority in `SessionOperations`.
_Avoid_: session control, admission authority

## Required Patterns

**Strategy**:
Score calculation and difficulty-based, mode-specific normalization or evaluation policies must be implemented as interchangeable strategies so scoring behavior can vary without branching across handlers.
_Avoid_: central score handlers full of mode checks
