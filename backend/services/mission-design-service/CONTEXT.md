# Mission Design Service

`mission-design-service` realizes the `MissionDesign` bounded context. It owns mission authoring, reusable trivia authoring, and the source-readiness decisions required before missions can create live sessions.

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
A nested mission node under a `Stage` used to refine progression. Each `Substage` has exactly one `SubstagePlayMode`.
_Avoid_: sublevel, nested stage

**SubstagePlayMode**:
The play-mode classification of a `Substage`, either `TreasureHunt` or `Trivia`.
_Avoid_: session mode, mixed mode

**Clue**:
Optional player-facing guidance under a `Substage`. A clue can guide trivia or treasure-hunt play; in treasure hunt, a `Target` may reference at most one `Clue`. A clue can be authored as visible when its `Substage` starts or held for operator release.
_Avoid_: objective, treasure, checkpoint

**ClueVisibilityPolicy**:
The authoring rule that decides whether a `Clue` becomes visible to all teams when its `Substage` starts or remains hidden until operator release.
_Avoid_: clue availability flag, clue unlock mode

**Target**:
The QR-validated treasure-hunt objective that participant teams are trying to find or validate.
_Avoid_: clue, hint, marker

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
The reusable trivia authoring aggregate that defines questions and options for trivia content attached to a `Substage`. It is not a `SessionSource`.
_Avoid_: questionnaire, game form, session source

**TriviaQuestionSelection**:
An ordered selection of questions from one published `TriviaQuiz` for use by a trivia `Substage`. Selecting the entire quiz means selecting all questions in quiz order.
_Avoid_: loose question reference, embedded mini quiz

**TriviaQuestion**:
The authored question belonging to a `TriviaQuiz`.
_Avoid_: prompt item, question row

**ScoreValue**:
The authored integer score value for a `TriviaQuestion` or treasure-hunt substage winner award. Publication requires every trivia question and every treasure-hunt winner award to have a valid `ScoreValue`.
_Avoid_: score weight, bonus value

**TriviaOption**:
One of the authored answer options available for a `TriviaQuestion`.
_Avoid_: choice row, answer candidate

## Boundary Rules

**Source Readiness**:
`MissionDesign` decides whether a `Mission` is ready to be used as a `SessionSource` for live execution. A ready mission has runtime-ready stages and substages, including target content and winner score values for treasure hunt, and published trivia question selections with timers and score values for trivia.
_Avoid_: runtime activation authority, live supervision

**Structure Ownership**:
The mission hierarchy and trivia composition belong entirely to `MissionDesign`; other services consume published source facts instead of mutating authoring structure directly.
_Avoid_: runtime-owned content editing

## Required Patterns

**Composite**:
`Mission` and `MissionNode` must be modeled as a structured tree so `Stage`, `Substage`, and `Clue` remain part of one coherent authoring hierarchy while treasure-hunt `Target`s stay attached to their owning `Substage`.
_Avoid_: flattening the hierarchy into unrelated records or scattering traversal logic through handlers

**Template Method**:
Mission and trivia validation flows should keep a stable sequence of checks while allowing mode-specific validation steps to vary underneath that sequence.
_Avoid_: duplicating near-identical validation pipelines per use case
