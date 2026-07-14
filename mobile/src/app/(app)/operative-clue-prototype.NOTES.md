# Prototype — HU-28 operative-clue reveal on the Trivia surface

**Route:** `/(app)/operative-clue-prototype?variant=A` · reachable from the participant
home ("▶ Prototype · operative clue", `__DEV__` only).

Three structurally different variants of how an operator-authored operative clue
(`VisibleClueDto` with null targets + a real `operativeClueId`) surfaces on the
**trivia** participant view — which, unlike the treasure-hunt board, has no clue list
for one to land in. Switch with the floating bottom bar (or `←`/`→` on web); tap
**＋ Simulate operator clue** to fake the live push.

## Variants

- **A — Signal chip + toast** (`?variant=A`) — the current `mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`
  decision. Collapsed in-flow pill `OPERATIVE CLUES · N` (ember dot when unseen) below
  the question stage; transient viewport-top toast on arrival; tap either to expand an
  inline parchment list. *Quiet Hierarchy — the live timed question stays the focus.*
- **B — Docked drawer** (`?variant=B`) — persistent handle docked above the tab bar; it
  pulses on arrival (no toast); tap opens a slide-up bottom sheet with the full parchment
  list. *One durable, always-reachable home; heavier affordance.*
- **C — Inline artifact feed** (`?variant=C`) — no chip, no toast: each clue animates in
  as a full parchment card stacked newest-first, directly under the stage. *Immediate and
  unmissable; trades Quiet Hierarchy for presence.*

All three reuse the same durable artifact (`Card parchment` + `OPERATIVE CLUE` label +
`mono` body) and the `operative-clue-list` / `operative-clue-toast` testIDs from the doc.

## Decision — TODO

Winner: _____ · why: _____

## Cleanup once chosen (per the prototype skill)

- Fold the winner's surface into the real trivia branch (`LiveTeamSpace`, `team-space.tsx`),
  keyed on the live seen-set over `clueKey` filtered to operative clues.
- Delete this route, `operative-clue-prototype.NOTES.md`, `src/components/prototype/`
  (the switcher + the `active-question-stage.copy.tsx` backdrop), and the `__DEV__` button
  in `app/(app)/index.tsx`.
