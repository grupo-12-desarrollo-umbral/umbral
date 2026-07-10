# HU-34 — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-34 — Registro y rechazo de respuestas de equipo en trivia | DES-46 | DES-70 | session-operations-service | feature/hu-34-trivia-team-answer-first-write-wins | develop |

## Required pattern(s) → owning phase
- `Template Method` (phase X.1/X.2) — first-valid acceptance and late/repeat rejection are one stable answer-registration workflow — obligation: one fixed skeleton, no split accept/reject handlers
- `Chain of Responsibility` (phase X.2) — trivia answer validation must run as ordered links — obligation: runtime participation, active question, timer window, duplicate-team-answer checks short-circuit in order
- Transport: SignalR + RabbitMQ — operator-only answered indicator + `AnswerRegistered` publish after transactional success

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit tests lock first-write-wins, late/duplicate rejection, no-active-question/session-state rejection, correctness/score snapshot on the accepted answer, and `AnswerRegisteredEvent` on success only | `Template Method` |
| X.2 Application | App build; `SubmitTriviaAnswer` is one slice; ordered validation links short-circuit on first failure; accepted-answer response leaks no correctness/points; accepted-answer fact bridges onto RabbitMQ + operator-only SignalR | `Chain of Responsibility` + application side of `Template Method` |
| X.3 Infrastructure | Infra build; migration persists evidence + trivia-answer specialization; accepted answers round-trip through the session aggregate; existing RabbitMQ publisher seam carries the new answer contract | — |
| X.4 Api | Participant answer endpoint succeeds on the first valid answer and rejects repeat/late attempts with consistent ProblemDetails; operator-only `TeamAnswered` signal does not leak to participants; coverage gate passes | — |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-34)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-34)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-34)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-34)`

Trailer (every phase): `Ref: HU-34` / `Ref: DES-46` / `Ref: DES-70`

## Acceptance criteria
- any authenticated team member can submit the active question answer
- the first valid in-time answer is recorded as the team's final answer for that question
- the accepted answer is associated with team, session, question, and timestamp
- the registration respects active-session context
- the "team answered" indicator is pushed to operator monitoring in real time without revealing the option
- after transactional success, `AnswerRegistered` is published for asynchronous consumers
- late answers are rejected
- repeated attempts for the same team/question are rejected
- the final accepted answer is not overwritten
- the rejection reason is consistent

## Endpoints + smoke (driver verifies at Stop 2)
- `POST /api/sessions/{liveSessionId}/participants/answers` — participant-auth write, body carries runtime `teamId`, selected trivia option id, and optional token; expect **200** on the first valid answer with acceptance metadata only
- `POST /api/sessions/{liveSessionId}/participants/answers` again for the same team/question, or after the question window closes — expect **RFC 7807 rejection** (repeat/late branch; consistent reason contract)
- SignalR `TeamAnswered` — operator-only notification, no option/correctness leakage; verify it reaches the operator group and does not reach participant connections
- RabbitMQ `AnswerRegistered` — observable after transactional success on the existing session-operations publisher seam; carries scoring/audit correlation fields for downstream consumers

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu34.md`.

Primary frontend outcome: participant answer submission flow over the existing trivia runtime.
Secondary frontend outcome: contract/state plumbing for the operator-only answered signal, with
full pre-close operator monitoring still bounded to HU-36A.
