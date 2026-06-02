# TriviaQuestion score and timer have explicit bounded ranges (score 1–100, timer 5–120s)

The PRD (DES-62) names the "rango permitido" rule for `TriviaQuestion` score and `QuestionTimer` but deliberately leaves the numbers open ("cuando el rango permitido se materialice"), instructing in its Further Notes that any missing numeric ranges for score or timer **must be fixed as explicit domain rules and not left implicit in UI or infrastructure**. HU-14A (DES-20) accordingly shipped score and timer as positive-only authored fields (`> 0`) and recorded the range ambiguity for HU-14B (per `hu14a-context.md`). This ADR fixes those ranges so HU-14B (DES-21) can enforce them as domain invariants: **score is an integer in `[1, 100]`** and **`QuestionTimer` is an integer number of seconds in `[5, 120]`**.

## Status

accepted

## Considered Options

- **Keep `> 0` only (no upper bound).** Rejected: the PRD explicitly requires the range to be materialized as an explicit domain rule, and an unbounded score/timer lets authors create non-comparable or unplayable questions (e.g. a 1-second timer or a 10,000-point question), which is exactly what HU-14B (DES-21) exists to prevent.
- **Score 1–1000 / timer 10–300s (wider).** Rejected for the first implementation: wider bounds add no demonstrated authoring need and weaken comparability between questions. Ranges can be widened later via a follow-up ADR if real content requires it.
- **Score 1–100, timer 5–120s (chosen).** A percentage-like score cap that keeps questions comparable, and a per-question window typical of live trivia — long enough to read and answer, short enough to prevent stalling.

## Consequences

- HU-14B enforces these as **domain rules**, not just request validation: `TriviaQuestion.ValidateScoreValue` rejects scores outside `[1, 100]` and `QuestionTimer.Create` rejects values outside `[5, 120]`. Per the PRD's value-object guidance, score may be promoted to its own value object alongside `QuestionTimer`. The `TriviaQuestionAuthoringCommandValidator` upper bounds mirror the domain rule for early/friendly feedback.
- The other two HU-14B acceptance criteria — 2–4 options and exactly one correct option — were already satisfied by HU-14A (validator + domain invariants), so HU-14B's net new work is only these range bounds.
- Existing HU-14A tests that author questions must use score/timer values inside the new ranges; new tests cover rejection at each boundary (0, 1, 100, 101 for score; 4, 5, 120, 121 for timer).
- Frontend trivia authoring should surface these limits (min/max on the score and timer inputs) so the bound is visible at authoring time rather than only on a rejected submit.
