# Prototype — treasure-hunt participant play surface

**Question:** What should the treasure-hunt participant PLAY surface look like?
**Source:** GitHub #153, slice 3 (marked HITL — the layout is an open design decision).
**Shape:** UI prototype — three radically different layouts were explored; one was chosen.

## Where it lives

- Route: `mobile/src/app/(app)/treasure-hunt-play-prototype.tsx` (dev-only preview).
- Entry: a dev-only button on the participant home (`app/(app)/index.tsx`).

## How to run

```bash
cd mobile
pnpm start          # or: pnpm web  for the browser
```

Log in as a participant, then tap **"▶ Preview · treasure-hunt play"** on the home screen.

The map is a **labelled stub** — no map library is installed yet (react-native-maps would
be added in the real build). This preview is about layout, not the map engine. All data is
in-memory stub state; nothing is fetched or persisted.

## The three variants (explored)

| Key | Name | Idea | Primary affordance |
|-----|------|------|--------------------|
| A | Field Map | Immersive full-bleed map; title top-left, score top-centre, roster as a tap-to-expand bottom sheet, clue floats as an artifact. Closest to the literal brief. | The map / target location |
| B | Ops Dossier | One long scroll: header (title + score) → framed map card → released clues → team standings. Everything visible by scrolling. | Reading / briefing |
| **C** | **Focus Tabs** ✅ | Compact sticky header (title + score + timer) + segmented Map / Clues / Teams; a persistent "your team" strip keeps identity across tabs. | One thing at a time (one-handed) |

## Verdict — **C (Focus Tabs), kept as-is**

Picked by the user, unchanged. Variants A and B and the floating variant switcher were
removed; the route renders only the Focus Tabs surface. The only tweak was dropping the
bottom strip's padding, which had been reserved to clear the (now-deleted) switcher.

**Still to do before it goes live** — this remains a dev-only preview on stub data:
1. #153 slice 1 — backend target coordinates + runtime/roster/clue contracts (GH #154).
2. A real map library (react-native-maps) in place of `MapStub` (GH #156).
3. Fold the surface into the live `team-space.tsx` flow (wire to the SignalR reconnect
   result + timer) and drop the dev-only launch button (GH #155).
