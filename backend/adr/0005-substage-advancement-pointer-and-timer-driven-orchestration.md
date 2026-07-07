# Substage runtime is driven by one authoritative pointer, advanced generically by the timer, activated only for trivia

The cycle-1 runtime (DES-44/HU-33A) treated trivia as one flat list of questions on the whole `MissionRuntimeSnapshot`, ending the session when the list ran out. The canon rewrite (`grilling-session-mission-restructure.md`) makes trivia a **substage within a mission**, so the runtime must know which substage is live and how teams cross substage boundaries. This ADR fixes that contract for DES-78 (realign HU-33A) and the tickets that build against it: `LiveSession.ActiveSubstageId` is the single authoritative pointer for the live substage (walking strict stage→substage order); **`SubstageAdvancement` is timer-driven and generic across play modes** (the operator never forces it), while **auto-activation is trivia-only** — advancing *into* a treasure-hunt substage sets the pointer and stops, leaving that substage for its own runtime to drive; and the `TriviaSubstageWinner` is **emitted, not computed** here (via `SubstageAdvancedEvent`), because scoring owns the ledger.

## Status

accepted

## Considered Options

- **Keep the flat question list, extend it per substage inline.** Rejected: without an explicit live-substage pointer, every consumer (timer worker, treasure-hunt runtime, scoring) re-derives "which substage are we in" from question indices, and substage boundaries live implicitly in offset math. The canon models a substage sequence; the runtime should name it.
- **Compute `TriviaSubstageWinner` in session-operations on substage completion.** Rejected: it crosses the boundary in `CONTEXT.md` (`Runtime Authority` — other services supply derived scoring views; session-ops owns progression). It is also not computable in this slice — trivia answers (HU-34A/34B) and the score ledger (HU-37A) do not exist yet. Session-ops emits the completion signal; the winner is derived downstream.
- **Per-play-mode advancement branches (trivia advances one way, treasure-hunt another) inside the orchestrator.** Rejected: advancement itself is uniform (next substage in strict order, all teams together, no operator override). Only *entry activation* differs by play mode. Branching advancement duplicates the ordering rule and invites an operator-forced-advance path that violates canon.
- **One authoritative pointer + generic timer-driven advance + trivia-only activation (chosen).** The pointer is the seam both treasure-hunt runtime and scoring build against; advancement stays a single rule; each play mode owns only its own entry behavior.

## Consequences

- **The future treasure-hunt runtime (DES-42/HU-31, DES-37/HU-27) reads `ActiveSubstageId`** to drive its own substage — it does not reach into orchestrator internals. When it lands, advancing into a treasure-hunt substage becomes "drive it" instead of "park"; no change to the advancement rule.
- **Scoring (HU-37A) consumes `SubstageAdvancedEvent`** (`fromSubstageId`, `fromPlayMode`, `toSubstageId`) plus the existing `SessionStateChangedEvent(→Finished)` to compute the trivia substage winner. No dedicated `TriviaSubstageCompletedEvent` is added until a consumer needs a signal these two cannot supply.
- **A mixed trivia/treasure-hunt mission run before the treasure-hunt runtime exists parks at the treasure-hunt substage** (pointer advanced, nothing drives it) rather than crashing. DES-78 verifies end-to-end only on an all-trivia multi-substage mission and does not claim mixed missions run.
- **`SubstageAdvancement` has no operator command.** Advancement is emitted solely by the authoritative timer worker on last-question expiry, which is what satisfies "the operator does not control substage advancement."
