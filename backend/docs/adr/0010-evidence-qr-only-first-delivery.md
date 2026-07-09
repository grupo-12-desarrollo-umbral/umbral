# Evidence is the umbrella term with exactly two forms: QR and trivia answer

The canonical domain (`bd_umbral_entity_spec.md`, `ddd_solution_model.md`,
`umbral_user_stories.md`, `session-operations-service/CONTEXT.md`) models
`EvidenceSubmission` as the umbrella base for a team submission that proves or
resolves progress in the active mission substage.

That umbrella has exactly two concrete forms:

- `TreasureEvidenceSubmission`: the QR/token scan used in treasure-hunt
  substages
- `TriviaAnswerSubmission`: the team answer used in trivia substages

This replaces the prior "QR-only evidence" framing. Trivia answers are evidence.
Text/photo evidence modes are not part of the current canonical model.

## Decisions in scope

- **Evidence is generic, but only across two committed forms.**
  `EvidenceSubmission` is the shared base for QR evidence and trivia-answer
  evidence. There are no additional text/photo extension points in the current
  model.
- **Two distinct facts still exist in the QR flow, in order.** Intake publishes
  `EvidenceSubmissionRegistered`; a successful target match then publishes
  `TargetResolved` (`TargetResolution`). "Submitted" is not "resolved."
- **Treasure-hunt progression is target-based, not clue-based.** A `Target` is
  the QR-validated objective. A `Clue` is optional guidance associated with a
  target; clue visibility is not required for target resolution.
- **QR resolution is automatic.** Every valid QR intake registers an
  `EvidenceSubmission`. The system resolves target match itself: a correct scan
  becomes accepted with `TargetResolved`; a wrong scan is rejected and retained
  for audit history.
- **Trivia answers keep their own concrete flow under the same umbrella.**
  `TriviaAnswerSubmission` specializes `EvidenceSubmission` but keeps its own
  validation, acceptance, correctness, and scoring behavior.
- **Operator-mediated human review is out of scope for both current forms.**
  The canonical traceability and rejection fields remain modeled, but both
  current evidence forms are system-resolved in first delivery.

## Consequences

- `session-operations-service/CONTEXT.md` needs no terminology change; its
  glossary already keeps `EvidenceSubmission`, `TreasureEvidenceSubmission`,
  `TargetResolution`, and `TriviaAnswerSubmission` distinct.
- Canonical docs must describe `EvidenceSubmission` as the umbrella base and
  remove text/photo-extension framing.
- DES-39 (HU-29) and DES-40 (HU-30A) are active backlog items again as the
  generic evidence intake and shared context-validation stories under the
  umbrella model.
- DES-41 (HU-30B), DES-42 (HU-31), DES-43 (HU-32), DES-46 (HU-34),
  DES-51 (HU-37), DES-56 (HU-40A), DES-60, and DES-70 must be interpreted
  with the umbrella terminology: treasure-hunt QR evidence is one form, trivia
  answer submission is the other.
  *(Updated 2026-07-09: DES-47/HU-34B merged into DES-46/HU-34; `HU-37A` → `HU-37`.)*
