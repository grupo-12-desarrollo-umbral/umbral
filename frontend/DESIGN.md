---
name: Umbral Command Center
description: Warm, high-density command center UI for live game operators and admins.
colors:
  ember-accent: "oklch(72% 0.16 48)"
  ember-accent-strong: "oklch(66% 0.18 44)"
  ember-accent-soft: "oklch(90% 0.05 54)"
  ivory-fog: "oklch(98.4% 0.013 76)"
  warm-mist: "oklch(96.2% 0.018 76)"
  paper-surface: "oklch(99.4% 0.008 76)"
  raised-surface: "oklch(96.9% 0.015 76)"
  panel-surface: "oklch(98.8% 0.012 76)"
  charcoal-room: "oklch(18.5% 0.016 58)"
  ember-panel-night: "oklch(22.5% 0.018 58)"
  night-surface: "oklch(25.2% 0.018 58)"
  border-soft: "oklch(89.8% 0.018 76)"
  border-soft-night: "oklch(34% 0.016 58)"
  text-ink: "oklch(28% 0.03 54)"
  text-ink-night: "oklch(90.5% 0.018 72)"
  text-muted: "oklch(61% 0.018 60)"
  signal-success: "oklch(66% 0.14 148)"
  signal-warning: "oklch(77% 0.12 74)"
  signal-critical: "oklch(64% 0.18 27)"
  parchment: "oklch(92.4% 0.04 84)"
  parchment-deep: "oklch(86.6% 0.05 76)"
typography:
  display:
    fontFamily: "var(--font-geist-sans), Arial, Helvetica, sans-serif"
    fontSize: "clamp(1.85rem, 3vw, 2.3rem)"
    fontWeight: 600
    lineHeight: 1.05
    letterSpacing: "-0.05em"
  headline:
    fontFamily: "var(--font-geist-sans), Arial, Helvetica, sans-serif"
    fontSize: "1.08rem"
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: "-0.03em"
  title:
    fontFamily: "var(--font-geist-sans), Arial, Helvetica, sans-serif"
    fontSize: "1rem"
    fontWeight: 600
    lineHeight: 1.35
    letterSpacing: "normal"
  body:
    fontFamily: "var(--font-geist-sans), Arial, Helvetica, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
    letterSpacing: "normal"
  label:
    fontFamily: "var(--font-geist-sans), Arial, Helvetica, sans-serif"
    fontSize: "0.78rem"
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: "0.08em"
  mono:
    fontFamily: "var(--font-geist-mono), monospace"
    fontSize: "0.96rem"
    fontWeight: 400
    lineHeight: 1.72
    letterSpacing: "normal"
rounded:
  pill: "999px"
  panel: "1.4rem"
  card: "1.1rem"
  control: "1rem"
spacing:
  xs: "0.5rem"
  sm: "0.75rem"
  md: "1rem"
  lg: "1.25rem"
  xl: "1.5rem"
components:
  button-primary:
    backgroundColor: "{colors.ember-accent-strong}"
    textColor: "{colors.ivory-fog}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: "0.7rem 0.95rem"
  button-secondary:
    backgroundColor: "{colors.raised-surface}"
    textColor: "{colors.text-ink}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: "0.7rem 0.95rem"
  chip-live:
    backgroundColor: "{colors.signal-success}"
    textColor: "{colors.ember-panel-night}"
    typography: "{typography.label}"
    rounded: "{rounded.pill}"
    padding: "0.3rem 0.55rem"
  nav-item-active:
    backgroundColor: "{colors.ember-accent-soft}"
    textColor: "{colors.text-ink}"
    typography: "{typography.title}"
    rounded: "{rounded.control}"
    padding: "0.75rem 0.9rem"
  panel-default:
    backgroundColor: "{colors.panel-surface}"
    textColor: "{colors.text-ink}"
    rounded: "{rounded.panel}"
    padding: "1rem"
  clue-card:
    backgroundColor: "{colors.parchment}"
    textColor: "{colors.text-ink}"
    typography: "{typography.mono}"
    rounded: "{rounded.control}"
    padding: "1rem"
---

# Design System: Umbral Command Center

## Overview

**Creative North Star: "The Lantern Control Room"**

This system is a live operations surface that behaves like a trusted game master. It is dense, alert, and operational, but it never turns clinical. Warm authority is the core voice: operators should feel held by the interface, not scolded by it. The visual system borrows from brass instruments, parchment clues, low-glow lantern light, and quiet command-room discipline, then translates those motifs into a product UI with real legibility and real urgency.

The atmosphere is magical through materials, not decoration. The system uses restrained ember accents, warm neutrals, tactile clue surfaces, and rounded operational controls to suggest discovery and ritual without drifting into fantasy props. It explicitly rejects anything overly childish or cartoonish, corporate SaaS clichés, dark or edgy gamer aesthetic, and cold minimalism. If a screen looks like a generic observability tool with orange paint on top, it has failed.

**Key Characteristics:**
- Warm, restrained dual-theme palette with ember accents and tinted neutrals
- Dense, control-room layout with clear hierarchy and fast scan paths
- Tonal depth over heavy shadow, surfaces step forward by lightness and border discipline
- Tactile signature moments, especially clue cards and live-status chips
- Rounded, dependable controls with crisp keyboard focus and short motion

## Colors

The palette is ember parchment by design: low-chroma light surfaces, warm charcoal dark surfaces, one concentrated amber accent, and direct semantic signals.

### Primary
- **Lantern Ember** (`oklch(72% 0.16 48)`): The main accent. Use it for primary actions, active navigation, control emphasis, and mission moments that need a pulse without becoming loud.
- **Banked Ember** (`oklch(66% 0.18 44)`): The deeper accent stop used in primary button fills, strong emphasis, and focus-visible anchors.
- **Ember Wash** (`oklch(90% 0.05 54)`): A soft support tint for active backgrounds, accent glows, and panel warmth. It is never the main event.

### Neutral
- **Ivory Fog** (`oklch(98.4% 0.013 76)`): The light-mode page field. Use it for the broadest background plane.
- **Warm Mist** (`oklch(96.2% 0.018 76)`): Secondary light background used for gradients and quiet separation.
- **Paper Surface** (`oklch(99.4% 0.008 76)`): The brightest light-mode control surface.
- **Raised Surface** (`oklch(96.9% 0.015 76)`): Hover and control lift in light mode.
- **Panel Surface** (`oklch(98.8% 0.012 76)`): Default light-mode panel background.
- **Charcoal Room** (`oklch(18.5% 0.016 58)`): The dark-mode page field. Never use pure black.
- **Ember Panel Night** (`oklch(22.5% 0.018 58)`): The default dark panel surface.
- **Night Surface** (`oklch(25.2% 0.018 58)`): Raised dark-mode controls and surfaces.
- **Soft Border** (`oklch(89.8% 0.018 76)`): Light-mode dividers, strokes, and panel edges.
- **Soft Border Night** (`oklch(34% 0.016 58)`): Dark-mode dividers and strokes.
- **Ink** (`oklch(28% 0.03 54)`): Main light-mode text color.
- **Ink Night** (`oklch(90.5% 0.018 72)`): Main dark-mode text color.
- **Muted Caption** (`oklch(61% 0.018 60)`): Secondary labels, metadata, and table headings.

### Named Rules
**The Ember Rule.** The primary accent appears on a small fraction of any screen. Its rarity is the point. If the interface starts glowing everywhere, the hierarchy is broken.

**The No-Pure-Black Rule.** Dark mode is a room, not a void. Use warm charcoal surfaces, not pure black, so text, borders, and depth retain nuance.

## Typography

**Display Font:** Geist Sans (with `Arial, Helvetica, sans-serif` fallback)
**Body Font:** Geist Sans (with `Arial, Helvetica, sans-serif` fallback)
**Label/Mono Font:** Geist Mono for clue artifacts and code-like content

**Character:** This system stays inside one sans family for operational clarity, then uses mono only where the product needs a more artifact-like or encoded texture. The result is modern and controlled, but softened by tracking, curvature, and warm color rather than by swapping to decorative type.

### Hierarchy
- **Display** (`600`, `clamp(1.85rem, 3vw, 2.3rem)`, `1.05`): Session titles, admin hero lines, and other top-level route anchors.
- **Headline** (`600`, `1.08rem`, `1.2`): Panel titles and major sectional headings.
- **Title** (`600`, `1rem`, `1.35`): Queue titles, control labels, and strong UI labels.
- **Body** (`400`, `1rem`, `1.5`): Default application copy. Keep long explanatory copy under roughly `65ch`.
- **Label** (`600`, `0.78rem`, `0.08em`, uppercase where used): Pills, chips, compact metadata, live state tags, and panel eyebrows.
- **Mono** (`400`, `0.96rem`, `1.72`): Clue text, audit details, and any content that should feel like a found artifact or system payload.

### Named Rules
**The Quiet Hierarchy Rule.** Contrast comes from weight, spacing, and placement before it comes from giant size jumps. If a label has to scream to be noticed, the layout failed first.

**The Artifact Rule.** Mono is reserved for clue content and system-detail moments. Do not spread it across the general product UI.

## Elevation

This system uses tonal layering, not shadow theater. Most depth is created through surface lightness shifts, restrained strokes, soft inset highlights, and atmosphere from color temperature. Shadow exists only in small doses, mainly on tactile special cases like the parchment clue card or a small ambient glow under a live surface. Flat by default, lifted only when state or signature content earns it.

### Shadow Vocabulary
- **Inset Sheen** (`inset 0 1px 0 color-mix(in oklab, var(--text-primary) 5%, transparent)`): Standard panel and control finish. Use it to suggest crafted material without reading as a card deck.
- **Lantern Halo** (`0 10px 30px color-mix(in oklab, var(--accent-soft) 12%, transparent)`): Reserved for clue artifacts and other signature moments with theatrical weight.

### Named Rules
**The Tonal Layering Rule.** Surfaces move forward through lightness, border clarity, and inset sheen. If a shadow is doing all the work, the system has slipped back into generic app UI.

## Components

Components should feel tactile and dependable. They are rounded enough to feel warm, but never swollen. They transition quickly, accept keyboard focus gracefully, and never hide state behind decoration.

### Buttons
- **Shape:** Rounded operational controls (`1rem` radius) with enough padding to feel substantial in dense layouts.
- **Primary:** Ember-filled action buttons (`oklch(66% 0.18 44)` to `oklch(72% 0.16 48)`) with light text and compact control padding (`0.7rem 0.95rem`). Use for clue release, create, and commit actions.
- **Hover / Focus:** Hover brightens slightly; active state drops by a single pixel. Focus-visible uses a `2px` outline in `Banked Ember` with `2px` offset.
- **Secondary / Ghost:** Raised-surface backgrounds with ink text, used for pause, schedule, and utility actions. They rely on surface contrast, not loud borders.

### Chips
- **Style:** Pill shapes (`999px`) with semantic tinting. Live chips get a success-toned background and a strong label; warning and critical chips use the same structure with role-specific tint shifts.
- **State:** Chips are state markers first, filters second. They must remain readable at a glance and never become colorful ornaments.

### Cards / Containers
- **Corner Style:** Primary panels use a generous curve (`1.4rem`). Smaller utility cards use a tighter curve (`1.1rem`).
- **Background:** Panel surfaces inherit from the active theme and are slightly translucent through `color-mix`, keeping the page field alive beneath them.
- **Shadow Strategy:** Tonal layering with inset sheen by default, halo only for signature content.
- **Border:** Always a full perimeter border (`1px`). No side-stripe accents.
- **Internal Padding:** Panel internals generally sit on `1rem` to `1.25rem` spacing steps.

### Inputs / Fields
- **Style:** Inputs and selects use raised surfaces, rounded corners (`1rem`), and quiet strokes. They should feel like instrument controls, not flat form fields.
- **Focus:** Use the same ember outline system as buttons for consistency.
- **Error / Disabled:** Error states shift to the critical family. Disabled states should reduce contrast and pointer affordance, not disappear.

### Navigation
- **Style, typography, default/hover/active states, mobile treatment:** Sidebar navigation is panel-native. Default items sit quietly in secondary text; hover raises the surface slightly; active items get ember-washed backgrounds, clearer text, and a tiny end marker. On narrow screens the navigation collapses into the same rounded language, not a different visual family.

### Signature Component
- **Clue Artifact Card:** The clue card is the signature motif. It uses parchment tones (`oklch(92.4% 0.04 84)` and `oklch(86.6% 0.05 76)`), mono text, a soft halo, and a layered sheen overlay. It should feel like a found object inside a disciplined control system.

## Do's and Don'ts

### Do:
- **Do** keep the palette restrained: warm neutrals everywhere, ember accent on key actions and active state only.
- **Do** use full-perimeter borders (`1px`) and tonal layering to separate panels.
- **Do** preserve the tactile clue artifact treatment as a signature moment inside the operator view.
- **Do** keep operational density high, with strong panel titles, short metadata, and tabular numbers for live data.
- **Do** honor WCAG AAA aspirations on critical controls, scoreboard text, and state indicators.
- **Do** use quick motion (`100ms` to `160ms`) with exponential easing for feedback, never decorative choreography.

### Don't:
- **Don't** drift into **Overly childish / cartoonish** territory: no candy-bright palettes, no bubbly rounded everything, no toy-like surfaces.
- **Don't** ship **Corporate SaaS clichés**: no generic blue-gradient dashboards, no big hero metrics, no sterile feature panels, no stock-illustration energy.
- **Don't** adopt a **Dark / edgy gamer aesthetic**: no neon-on-black, no cyberpunk glow stacks, no esports aggression.
- **Don't** fall into **Cold minimalism**: no lifeless white voids, no flat grayscale emptiness, no austere silence where the product needs warmth.
- **Don't** use gradient text, side-stripe borders, or nested cards. These are prohibited.
- **Don't** let the accent color spread beyond the core interaction path. If every surface is warm orange, nothing is important anymore.
