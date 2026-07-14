# HU-28 — Mobile Operative-Clue Trivia UX (B2 design decision)

**Ref:** HU-28 · **Branch:** feature/hu-28-operative-clues · **Date:** 2026-07-14
**Implements:** the B2 phase of `frontend/plans/hu-28-ui-operative-clue-authoring-and-reveal.md`.
**Design system:** all choices below use existing tokens/rules in `mobile/DESIGN.md` — no new tokens.

> This is a **feature UX decision**, not a design-system rule — it lives here, not in `DESIGN.md`.
> `DESIGN.md` stays the reusable system; this doc records how HU-28 applies it.

## Problem

An operator-authored operative clue arrives in `board.visibleClues` (with `targetSnapshotId == null`)
in any play mode. On the **treasure-hunt** board it renders as a normal `ClueCard` on the Clues tab and
the existing new-clue **ember dot** already signals it — no change, no toast (avoid double-signalling).
On a **Trivia** substage the participant view (`team-space.tsx` non-treasure-hunt branch / `LiveTeamSpace`)
has **no clue list**, so operative clues have nowhere to land. B2 adds two surfaces to fix that; both
mount once in the trivia branch and are read-only off the existing board path.

## Decisions

**1. Persistent affordance — collapsed clue chip (the durable store).**
A single-line pill (`raisedSurface` fill, `borderSoft` 1px, `radii.control`, `insetSheen`, styled after
the existing segmented control) reading `OPERATIVE CLUES · N` (`typography.label`, muted). When unseen
clues exist it carries the 8×8 `emberAccentStrong` new-clue dot (identical to the treasure-hunt
indicator). **Collapsed by default** — the dot + toast are the arrival cues; tapping toggles an inline
expansion rendering the team's operative clues as the **same `Card parchment` + `"OPERATIVE CLUE"`
label + `mono` body** used elsewhere. Sits in the normal scroll flow below the question stage, above the
team strip. Container `testID="operative-clue-list"`. A missed toast never loses the guidance — this is
the reviewable home.

**2. Arrival toast — transient, trivia-only, viewport-top-pinned.**
A compact non-parchment card (`panelSurface`/`paperSurface`, `radii.card`, `borderSoft`, `shadows.card`)
with a leading `emberAccentStrong` dot, a `"New operative clue"` `label`, and **one truncated line**
(`numberOfLines={1}`) of clue text. **Parchment + `mono` are deliberately kept out of the toast** and
reserved for the durable list (Artifact Rule) — the toast is a pointer, not the artifact.
`testID="operative-clue-toast"`.
- **Pinned to the viewport top:** `LiveTeamSpace` renders inside the `Screen` ScrollView, so an in-flow
  toast would scroll away. Lift the toast to a **sibling of `Screen` (or a portal)** so it stays fixed.
- **Trivia-only:** the treasure-hunt board already has its Clues-tab ember dot; a toast there would
  double-signal the same event.

**3. Motion + auto-dismiss.**
Slide-down + fade in (~220ms) → hold **~4000ms** → slide-up + fade out (~180ms), via the built-in
`Animated` API (no new dependency). Pair with `accessibilityLiveRegion` / `announceForAccessibility` so
the arrival is spoken — a purely visual auto-dismiss is inaccessible.

**4. Tap behavior — opens the list.**
Tapping the toast body expands the chip's clue list **and** dismisses the toast; the toast also
auto-dismisses on its timer, and the list persists regardless. A small close control covers
dismiss-without-opening. (This diverges from the plan's original "dismiss-only" default — approved
because wiring the transient signal to the durable store is more useful.)

## Not a design choice (fixed by the plan)

- **Trigger:** a `useRef` seen-set over `clueKey` filtered to operative clues (`targetSnapshotId == null`)
  — prop-diff bookkeeping only, never owns board state.
- **testIDs:** `operative-clue-list`, `operative-clue-toast`.

## Rules honored

Ember Rule (accent only on the dot), Artifact Rule (`parchment`/`mono` only in the durable list), Quiet
Hierarchy (collapsed chip + minimal toast keep the live timed question the focus), No-Pure-Black,
Tonal Layering. No new tokens.
