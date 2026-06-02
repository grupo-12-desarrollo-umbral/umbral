---
name: Umbral Mobile — Native Design System
description: Ember/parchment token system for the Expo participant app. Light mode only.
source: Adapted from frontend/DESIGN.md for React Native (SDK 56, inline styles).
---

# Umbral Mobile Design System

## Philosophy

Same creative north star as the web Command Center: **The Lantern Control Room**. Warm authority, ember accents, parchment clues, restrained depth. Adapted for native touch interactions.

Light mode only. The `userInterfaceStyle` is locked to `"light"` in `app.json`; dark-mode surfaces are defined in the token file for completeness but are not used.

---

## Tokens — `src/constants/theme.ts`

All values are hex-converted from the frontend's oklch palette.

### Colors

| Token | Hex | Role |
|-------|-----|------|
| `emberAccent` | `#F3813F` | Primary accent — navigation, emphasis |
| `emberAccentStrong` | `#E8651D` | Primary button fill, focus anchors |
| `emberAccentSoft` | `#FAD6C0` | Active background tint, wash |
| `ivoryFog` | `#FFF9F0` | Page field (root background) |
| `warmMist` | `#FAF1E5` | Secondary background |
| `paperSurface` | `#FFFCF7` | Brightest surface |
| `raisedSurface` | `#FBF4EA` | Control lift, TextField background |
| `panelSurface` | `#FFFAF2` | Default Panel background |
| `borderSoft` | `#E4DCD1` | Panel edges, dividers, control borders |
| `textInk` | `#35251B` | Main text |
| `textMuted` | `#8C8179` | Labels, captions, secondary text |
| `parchment` | `#F3E4C8` | Clue artifact card background |
| `parchmentDeep` | `#E6CFAF` | Clue artifact border |
| `signalSuccess` | `#4CA95F` | Live status, success |
| `signalWarning` | `#E2A856` | Warning state |
| `signalCritical` | `#E5554C` | Error, critical |

### Typography

Mapped from frontend rem/ratio values to React Native points (base 16pt).

| Variant | Size | Weight | Line height | Letter spacing |
|---------|------|--------|-------------|----------------|
| `display` | 30 | 600 | 32 | −1.5 |
| `headline` | 17 | 600 | 20 | −0.5 |
| `title` | 16 | 600 | 22 | 0 |
| `body` | 16 | 400 | 24 | 0 |
| `label` | 13 | 600 | 16 | 1 |
| `mono` | 15 | 400 | 26 | 0 |

- `mono` uses `ui-monospace` on iOS, `monospace` on Android.
- All others use the system sans-serif.

### Spacing

| Name | Value | Rem equiv |
|------|-------|-----------|
| `xs` | 8 | 0.5 rem |
| `sm` | 12 | 0.75 rem |
| `md` | 16 | 1 rem |
| `lg` | 20 | 1.25 rem |
| `xl` | 24 | 1.5 rem |

### Radii

| Name | Value | CSS equiv |
|------|-------|-----------|
| `pill` | 9999 | `999px` |
| `panel` | 22 | `1.4rem` |
| `card` | 18 | `1.1rem` |
| `control` | 16 | `1rem` |

Always pair `borderRadius` with `borderCurve: 'continuous'` (continuous radius curve matches Apple HIG).

### Shadows

```ts
shadows.insetSheen   // 'inset 0 1px 0 rgba(53, 37, 27, 0.05)' — standard panel finish
shadows.lanternHalo  // '0 10px 30px rgba(250, 214, 192, 0.12)' — clue artifacts only
shadows.card         // '0 2px 8px rgba(53, 37, 27, 0.06)' — elevated card ambient
```

Use CSS `boxShadow` (New Architecture). Never use legacy `shadow*` props or `elevation`.

---

## Primitive Components — `src/components/ui/`

| File | What it renders |
|------|----------------|
| `text.tsx` | Typed text with `variant` + `muted`/`accent` props |
| `button.tsx` | `primary` (ember fill) / `secondary` (raised surface); haptics on iOS |
| `text-field.tsx` | Labeled input with focus ring, error state, show/hide for passwords |
| `panel.tsx` | Bordered large-radius surface (`radii.panel = 22`) |
| `card.tsx` | Smaller surface (`radii.card = 18`); `parchment` variant for clue artifacts |
| `screen.tsx` | Root `ScrollView` with `contentInsetAdjustmentBehavior="automatic"` |
| `brand-mark.tsx` | Umbral lantern + wordmark for login header |

---

## Named Rules (mirrored from frontend)

**The Ember Rule.** The accent (`#F3813F` / `#E8651D`) appears on a small fraction of any screen. Primary buttons, active highlights, focus rings — nothing else. If the screen glows everywhere, the hierarchy is broken.

**The No-Pure-Black Rule.** Even though the participant app is light-mode only, never use `#000000` for text. Use `textInk (#35251B)` — warm charcoal.

**The Quiet Hierarchy Rule.** Contrast from weight and placement before size jumps. Labels don't need to be large to be read; they need to be placed correctly.

**The Artifact Rule.** `mono` variant and `parchment` card are reserved for clue content. Don't spread them into general product UI.

**The Tonal Layering Rule.** Depth from surface lightness and `insetSheen`, not heavy shadows. `lanternHalo` is reserved for parchment clue cards.
