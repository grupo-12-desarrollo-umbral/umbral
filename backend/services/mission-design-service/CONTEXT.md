# Mission Design Service

`mission-design-service` realizes the `MissionDesign` bounded context. It owns mission and trivia authoring and the source-readiness decisions required before content can create live sessions.

## Language

### Mission Authoring

**Mission**:
The main aggregate used to define a mission, its nodes, clues, difficulty, maximum time, and base progression rules.
_Avoid_: challenge, route, experience, quiz

**MissionNode**:
The hierarchical component of a mission that represents a `Stage`, `Substage`, or `Clue`.
_Avoid_: step, item, level

**Stage**:
A top-level mission node that groups progression within the mission structure and must contain one or more `Substage` nodes.
_Avoid_: phase, level

**Substage**:
A nested mission node under a `Stage` used to refine progression and to contain `Clue` nodes.
_Avoid_: sublevel, nested stage

**Clue**:
A mission node that provides guidance, information, or direction to participant teams.
_Avoid_: hint, puzzle

**Target**:
The mission-side destination, checkpoint, or validation objective attached to a `Clue` when runtime resolution requires it. Represented by a QR code.
_Avoid_: marker, station, qr point

**Difficulty**:
The value object that expresses the academic difficulty level of a `Mission`.
_Avoid_: complexity, hardness

**MaximumTime**:
The value object that defines the allowed execution time for a `Mission` or `LiveSession`.
_Avoid_: timer setting, duration limit

**MissionActivation**:
The business condition that marks a `Mission` as available for creating live sessions.
_Avoid_: publishing, enabling, release

### Trivia Authoring

**TriviaQuiz**:
The aggregate used to define a trivia source that can later be used to create a `LiveSession` in trivia mode.
_Avoid_: questionnaire, game form

**TriviaQuestion**:
The authored question belonging to a `TriviaQuiz`.
_Avoid_: prompt item, question row

**TriviaOption**:
One of the authored answer options available for a `TriviaQuestion`.
_Avoid_: choice row, answer candidate

## Boundary Rules

**Source Readiness**:
`MissionDesign` decides whether a `Mission` or `TriviaQuiz` is ready to be used as a `SessionSource` for live execution.
_Avoid_: runtime activation authority, live supervision

**Structure Ownership**:
The mission hierarchy and trivia composition belong entirely to `MissionDesign`; other services consume published source facts instead of mutating authoring structure directly.
_Avoid_: runtime-owned content editing

## Required Patterns

**Composite**:
`Mission` and `MissionNode` must be modeled as a structured tree so `Stage`, `Substage`, `Clue`, and extension targets remain part of one coherent authoring hierarchy.
_Avoid_: flattening the hierarchy into unrelated records or scattering traversal logic through handlers

**Template Method**:
Mission and trivia validation flows should keep a stable sequence of checks while allowing mode-specific validation steps to vary underneath that sequence.
_Avoid_: duplicating near-identical validation pipelines per use case
