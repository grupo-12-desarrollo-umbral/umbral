# HU-14A follow-up — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-14A follow-up — RemoveTriviaQuestion command + question-removal domain slot | DES-80 | DES-62 | mission-design-service | feature/hu-14a-follow-up-remove-trivia-question | develop |

## Required pattern(s) → owning phase
- `Template Method` (phase X.1 + X.2) — question authoring uses a shared validation flow with question-specific steps — obligation: extend the existing trivia-question authoring workflow to cover remove; no detached remove-only rule sequence

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build passes; unit tests lock successful question removal, `sequenceOrder` reconciliation, missing-question rejection, and published/archived edit-blocked rejection; removal realized through the existing trivia-question authoring workflow, not a duplicated remove-only path | `Template Method` |
| X.2 Application | Application build passes; handler tests cover valid remove, missing quiz, missing question, and edit-blocked rejection; validator tests cover valid and invalid ids; admin-only authorization preserved; remove flows through the same stable authoring workflow family | `Template Method` |
| X.3 Infrastructure | Infrastructure build passes; repository integration test proves question removal persists correctly and remaining order round-trips coherently; migration/model snapshot stay consistent, with no migration unless the mapping truly requires one | — |
| X.4 Api | Endpoint tests pass for successful delete, non-admin rejection, missing quiz/question not-found, and edit-blocked rejection; verified trivia response shows the updated question list/order after deletion; repo coverage gate passes | — (standard `[Authorize]` / `AuthorizationBehaviour`) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(mission-design): phase X.1 — domain layer (HU-14A)`
- X.2 `feat(mission-design): phase X.2 — application layer (HU-14A)`
- X.3 `feat(mission-design): phase X.3 — infrastructure layer (HU-14A)`
- X.4 `feat(mission-design): phase X.4 — api layer (HU-14A)`

Trailer (every phase): `Ref: HU-14A` / `Ref: DES-80` / `Ref: DES-62`

## Acceptance criteria
- an administrator can remove a question from an editable quiz; the question and its options no longer exist and the remaining `sequenceOrder` stays consistent
- removing a question from a published/archived quiz is rejected with a clear edit-blocked domain path
- missing quiz/question lookups return not-found
- the domain emits `TriviaQuestionRemoved` on successful delete
- remove validation reuses the same trivia authoring `Template Method` workflow instead of a duplicated rule path
- coverage includes domain remove/order/state-guard tests, handler/validator tests, and a delete-endpoint smoke

## Endpoints + smoke (driver verifies at Stop 2)
- `DELETE /api/trivias/{triviaQuizId}/questions/{questionId}` — admin-authenticated delete of one question from an editable quiz — expect success and an updated trivia detail/mutation response showing the question removed and remaining order reconciled
- `DELETE /api/trivias/{triviaQuizId}/questions/{questionId}` as non-admin — expect `403`
- `DELETE /api/trivias/{triviaQuizId}/questions/{missingQuestionId}` (or missing quiz id) — expect not-found `ProblemDetails`

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu14a_follow_up.md`.
