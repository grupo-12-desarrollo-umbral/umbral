# Evidence model stays generic; first delivery ships only the QR mode

The canonical domain (`bd_umbral_entity_spec.md`, `ddd_solution_model.md`, `umbral_user_stories.md`, `session-operations-service/CONTEXT.md`) models `EvidenceSubmission` as a generic base with a `submissionType` enum (text, photo, QR, answer), `TreasureEvidenceSubmission` as the QR specialization, and `TriviaAnswerSubmission` as a separate sibling — not a kind of evidence. We keep that model intact and treat "evidence is QR-only" as a **delivery scope** statement, not a model change: the first delivery of `session-operations-service` implements only the QR/token evidence mode (`TreasureEvidenceSubmission` + `Target` + `TargetResolution`). The text/photo evidence modes and the operator-mediated review path are modeled but out of scope for this delivery.

We chose this over rewriting the canon to be QR-only because the `submissionType` enum and base/specialization split exist precisely to host more modes later, and a QR-only base cannot cleanly host the trivia-answer sibling. Narrowing the model would delete a deliberately-general structure we expect to need again.

## Decisions in scope

- **Two distinct facts in the QR flow, in order.** Intake publishes the generic `EvidenceSubmissionRegistered`; a successful target match then publishes `TargetResolved` (`TargetResolution`). "Submitted" is not "resolved." The single-event `QrTargetResolved` rename is rejected — it collapsed these two facts and stole the generic base event's name.
- **Intake is unconditional; QR resolution is automatic.** Every scan registers an `EvidenceSubmission` (`EvidenceValidationState = pending`). The system resolves target match itself: a correct scan becomes `accepted` with `TargetResolved`; a wrong scan is auto-`rejected`. Wrong scans are persisted for audit/anti-cheat history.
- **Operator-mediated review is deferred for the QR/treasure-hunt path.** `EvidenceReviewQueueProjection`, `EvidenceSubmission.reviewedByUserId`/`reviewedAt`, `EvidenceAcceptancePolicy`, and the operator review story (RF-09/RF-18) stay modeled in canon but are not exercised by QR evidence, which is system-resolved with no human in the loop. They ship with the deferred non-QR modes that actually need human review.

## Consequences

- `session-operations-service/CONTEXT.md` needs no change — its glossary already keeps `EvidenceSubmission` (generic), `TreasureEvidenceSubmission` (QR), `TargetResolution` (QR success), and `TriviaAnswerSubmission` (sibling) distinct.
- The canonical model docs keep the generic evidence model and carry a one-line pointer to this ADR rather than scattering scope carve-outs.
- Linear tickets that were edited under the discarded "rewrite the model to QR-only" reading need reconciling to this decision: the `QrTargetResolved` rename reverts to `EvidenceSubmissionRegistered` + `TargetResolved`; DES-43 (HU-32) operator review is deferred with the operator-review path.
- DES-39 (HU-29, generic evidence intake) and DES-40 (HU-30A, generic evidence context validation) are **cancelled — absorbed into HU-31**. QR is the only treasure-hunt evidence mode, so HU-31 (`TreasureEvidenceSubmission`) is a superset of their intake/validation, and trivia answers are the `TriviaAnswerSubmission` sibling (HU-34A/34B), not evidence. We keep the generic model as an extension point but plan no non-QR evidence mode; reopen those tickets only if text/photo evidence becomes a requirement. Downstream blockers that pointed at HU-30A (DES-41 HU-30B, DES-51 HU-37A) are re-anchored to HU-31.
