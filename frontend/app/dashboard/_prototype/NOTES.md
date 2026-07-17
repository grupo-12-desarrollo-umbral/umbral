# Prototype — operator live-session layout

**Question:** The operator live-session view (`activeNav === 'operator'`) stacks ~13 panels in
one column and reads as cluttered. What layout should replace it?

**Shape:** UI prototype, sub-shape A — same `/dashboard` route, gated by `?variant=`, floating
bottom switcher (dev-only). The real panels are passed in as `slots` from `DashboardClient`;
these files only decide arrangement, so all data/state/mutations are untouched.

## Variants (← / → to switch, or the bottom bar)

- **current** — today's single stacked column. Baseline; the default when no `?variant=`.
- **A — Tabbed workspace** (`?variant=A`) — sticky session header + both clocks (question timer
  and mission timer) always visible; the rest split into tabs (Live / Evidence & clues / Trivia /
  Timeline / Controls). One group on screen at a time. Covers every panel of the old stack:
  Live = team progress + ranking; Evidence & clues = evidence + (clue release, operative clue,
  penalty); Trivia = round + answered monitor + answer review; Timeline = history + activity;
  Controls = session controls + session detail.
- **B — Two-column console** (`?variant=B`) — "Monitor" (watch: timers, progress, ranking,
  evidence) beside "Operate" (act: controls, clues, penalties, trivia). Timeline full-width below.
- **C — Command dock + accordion** (`?variant=C`) — cockpit up top (header + timer + controls +
  team progress), everything else in collapsible sections, collapsed by default.

## Verdict

_TBD — fill in the winning variant (and any "header from B, sidebar from C" mixes) before folding
it into `DashboardClient` and deleting this `_prototype/` folder._

## Cleanup when decided

1. Inline the chosen arrangement into the `role === 'operator' && selectedOperatorSession` branch
   of `DashboardClient.tsx` (replacing `operatorLiveSlots` / `<OperatorLiveSessionPrototype>`).
2. Move any keeper layout CSS into `dashboard.module.css`.
3. Delete this `_prototype/` folder and the prototype import in `DashboardClient.tsx`.
