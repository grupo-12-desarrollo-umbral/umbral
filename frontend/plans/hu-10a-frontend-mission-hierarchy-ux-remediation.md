# HU-10A — Frontend: Mission Hierarchy Authoring — UX Remediation

**Ref:** HU-10A / DES-15 / DES-62
**Branch:** feature/hu-10a-mission-hierarchy-structure
**Date:** 2026-06-20
**Builds on:** the shipped HU-10A authoring UI (`hu-10a-frontend-mission-hierarchy-authoring.md`,
commits `bfba0ae`→`b74b901`). This is a **polish-only** follow-up: same contract, same data-testids,
no new endpoints. It fixes three concrete UX defects reported against the shipped screens.

> ⚠️ **Read first:** `frontend/AGENTS.md` — "This is NOT the Next.js you know." Before writing any
> code, read the relevant guides under `frontend/.next-docs/` (server-and-client-components, css).
> Do not rely on remembered Next.js conventions. Also: **verify every reused symbol against source**
> (`labels.ts` exports, CSS class names, data-testids) before citing it.

> **Execution shape:** **Phase 0 (foundation, sequential — lands first)** publishes the shared
> vocabulary (label module, CSS classes, QR component contract, dependency). Then **Phases A · B · C
> run fully in parallel** — they own disjoint files and only *read* Phase 0's outputs. **Phase V
> (verify)** runs last. The parallelism is real because the one shared mutable file (`dashboard.module.css`)
> and the one shared new module (`labels.ts`) are owned exclusively by Phase 0.

---

## Context — what shipped and what's wrong

The HU-10A authoring tree works functionally (all 19 endpoints wired, e2e green) but has three
UX defects visible on the live `/dashboard` mission-detail screen:

1. **The hierarchy reads as a flat pile.** `MissionTree.tsx` renders stages as `<h3>`, substages as
   `<h4>`, clues/targets as bare `<ul><li>`, with edit/add forms expanding *inline* between rows.
   With more than one stage/substage the nesting is illegible — you can't tell which substage a clue,
   target, or "+ Add" button belongs to (reported: *"the hierarchy is a mess when a mission has
   stages/substages"*).
2. **Raw backend enum tokens are shown to the admin.** The clue-visibility `<select>` and the clue
   display line render `VisibleWhenSubstageStarts` / `HiddenUntilOperatorRelease` verbatim; the
   play-mode badge/select shows `TreasureHunt`; the trivia/winner lines show `winnerScore` /
   `selected quiz #6` (reported: *"the inputs shouldn't have the same text the backend retrieves like
   visiblewhensubstagestarts all continued"*).
3. **The QR field is a bare, contextless text box.** `target-qrcode-input` is an unlabeled
   `placeholder="QR code"` input with no explanation of what the value is or how to produce one
   (reported: *"how would I select the qr from there lol"*).

### Domain grounding for the QR fix (from `backend/docs/ddd_solution_model.md` + `Domain/Entities/Target.cs`)

The QR code is **an opaque, runtime-validated token**, not human-facing content: *"a target is
resolved when its QR code is validated"*; the scan path is `TreasureEvidenceSubmission` ("the
QR/token scan"), guarded at runtime by `TargetResolutionPolicy` ("prevents duplicate or invalid
target resolution"). Backend `ValidateQrCode` only requires non-empty. The stored contract is the
**string** (`MissionTargetDto.qrCode`), and HU-10A target authoring is scoped to
`name, qrCode, sequenceOrder, isActive, winnerScore`.

→ Therefore the chosen treatment is **label + helper + Generate** (authoring-side guard mirroring the
runtime uniqueness policy: emit a collision-resistant token instead of hand-typed strings), **plus an
opt-in collapsible QR image preview as a pure add-on** (convenience for printing — does **not** change
the stored contract, which stays the code string).

---

## Hard constraints (do not violate)

- **Every existing `data-testid` is preserved** verbatim. The shipped e2e suite
  (`tests/e2e/mission-hierarchy.spec.ts`) keys off them. New testids may be *added*; none renamed.
- **Friendly labels are display-only.** `<select>` option **values** and request payloads keep the
  **raw enum string** (`TreasureHunt`, `VisibleWhenSubstageStarts`, …). Only the visible text changes.
  `page.selectOption(sel, 'Trivia')` matches by value, so option-value-preservation keeps it green.
- **One e2e coupling must be relaxed** (owned by Phase A — see below): the spec currently asserts the
  play-mode badge's *visible text* equals the raw token (`toContainText('TreasureHunt')`,
  `badge.innerText()` in `setPlayMode`). After the friendly-label change the badge shows
  `Treasure Hunt`. The badge will carry `data-playmode={raw}` and the spec switches to
  `toHaveAttribute('data-playmode', …)`. **No other spec touchpoint changes.**
- **No backend change. No contract/DTO change.** `qrCode` stays `string`; no new request fields.
- **No `SessionMode` / `SessionSource` / `TriviaQuiz`-as-`SessionSource`** copy (AC7 canon guard).
- **Additive CSS only.** No existing class renamed or restyled in a way that affects other panels —
  `dashboard.module.css` is shared app-wide.

---

## Phase 0 — Foundation & shared contract  *(sequential; lands first; unblocks A/B/C)*

**Owns (exclusive):** `app/dashboard/mission/labels.ts` *(new)*, `app/dashboard/dashboard.module.css`
*(additive only)*, `package.json` + `pnpm-lock.yaml` (one dependency). **No component behavior
changes here** — this phase only publishes the vocabulary the parallel phases consume.

### 0.1 — `labels.ts` (new pure module — the display/label seam)

Centralizes the enum→label maps (today duplicated as bare arrays in `NodeControls.tsx` and
`SubstageEditor.tsx`) and the QR helpers. Pure TS; safe to import from any client component.

```ts
// app/dashboard/mission/labels.ts
export const PLAY_MODES = ['TreasureHunt', 'Trivia'] as const
export type PlayMode = (typeof PLAY_MODES)[number]
export const PLAY_MODE_LABELS: Record<PlayMode, string> = {
  TreasureHunt: 'Treasure Hunt',
  Trivia: 'Trivia',
}

export const CLUE_VISIBILITY_POLICIES = [
  'VisibleWhenSubstageStarts',
  'HiddenUntilOperatorRelease',
] as const
export type ClueVisibility = (typeof CLUE_VISIBILITY_POLICIES)[number]
const CLUE_VISIBILITY_LABELS: Record<string, string> = {
  VisibleWhenSubstageStarts: 'Visible when substage starts',
  HiddenUntilOperatorRelease: 'Hidden until operator releases it',
}
// Tolerant: unknown server value (e.g. a future policy) degrades to a humanized fallback,
// never a crash or a raw PascalCase token.
export function clueVisibilityLabel(policy: string): string {
  return CLUE_VISIBILITY_LABELS[policy] ?? policy.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export const TARGET_QR_HELP =
  'The code embedded in the printed QR participants scan to resolve this target.'

// Authoring-side guard for the runtime TargetResolutionPolicy: an opaque, collision-resistant
// token. crypto.getRandomValues is browser-only — call ONLY from event handlers (never at module
// load / SSR). 8 Crockford-base32 chars ≈ 40 bits.
export function generateTargetQrCode(): string {
  const alphabet = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'
  const bytes = new Uint8Array(8)
  crypto.getRandomValues(bytes)
  const body = Array.from(bytes, (b) => alphabet[b % alphabet.length]).join('')
  return `TGT-${body}`
}
```

**Migration note for B & C:** `NodeControls.tsx` currently declares
`const CLUE_VISIBILITY_POLICIES = [...]` (line 14) and `SubstageEditor.tsx` declares
`const PLAY_MODES = [...]` (line 19). Those local consts are **deleted** in B/C respectively and
imported from `labels.ts`. The exported `PlayMode` / `ClueVisibility` types replace the local ones.

### 0.2 — `dashboard.module.css` (additive classes — the layout seam)

Append one block of **new** classes (existing `.missionTree`, `.nodeControls`, `.substageEditor`,
`.targetRow`, `.playModeRow`, etc. stay for now; phases migrate markup onto the new classes and the
dead ones are pruned in Phase V). Reuse existing tokens (`--accent`, `--border-subtle`,
`--surface-raised`, `.chip`, `.inlineButton`, `.smallButton`, `.dangerButton`, `.fieldLabel`,
`.formError`, `.inlineInput`, `.inlineCheck`). The class **names below are the contract** A/B/C code
against — Phase 0 must land them all even though Phase 0 renders nothing.

| Class | Purpose | Consumed by |
|---|---|---|
| `.treeStage` | stage card (border, radius, padding, gap) | A |
| `.treeStageHead` | stage header row: eyebrow + title + actions, space-between | A |
| `.treeEyebrow` | uppercase muted kicker ("STAGE 1", "SUBSTAGE", "CLUES", "TARGETS") | A |
| `.treeNodeTitle` | node title text (replaces `<h3>/<h4>` default sizing) | A |
| `.treeActions` | right-aligned Edit/Remove cluster (flex, gap) | A, B |
| `.treeSubstageRail` | indented container with a `border-left` connector rail | A |
| `.treeSubstage` | substage sub-card | A |
| `.treeSubstageHead` | substage header: title + play-mode badge + actions | A |
| `.treeSection` | labeled group inside a substage (Clues / Targets) | A |
| `.treeSectionLabel` | the `.treeEyebrow`-styled label for a section | A |
| `.treeClue` | one clue row (title + text + visibility chip) | A |
| `.treeClueText` | clue body text, muted | A |
| `.treeEmpty` | "No clues yet." / "No targets yet." muted hint | A |
| `.treeAddRow` | row holding a "+ Add …" trigger, scoped under its parent | A, B, C |
| `.nodeForm` | bordered editor container for add/edit forms (replaces inline `.confirmRow` sprawl) | B, C |
| `.nodeFormGrid` | responsive field grid inside `.nodeForm` | B, C |
| `.nodeField` | label-over-input field wrapper (column flex, gap) | B, C |
| `.nodeFormActions` | Save/Cancel cluster, right-aligned | B, C |
| `.qrField` | QR field wrapper (field + helper + actions) | C |
| `.qrInputRow` | input + Generate button on one row | C |
| `.qrHelp` | helper/explanatory text under the QR input | C |
| `.qrPreviewToggle` | "Show/Hide QR preview" link-button | C |
| `.qrPreview` | collapsible preview container (border, centered) | C |

> **Parallel-safety rule for A/B/C:** if a phase discovers it needs a class not in this table, it
> **appends** the new rule at the *end* of `dashboard.module.css` under a `/* HU-10A-UX: <phase> */`
> banner (end-of-file appends rarely textually conflict) and notes it in the PR. It does **not** edit
> Phase 0's block. This keeps concurrent edits to the shared file conflict-free.

### 0.3 — QR-preview dependency

Add **`qrcode.react`** (v4.x — React 19 compatible; renders an inline `<svg>` client-side, **no
network call**, which is correct for an opaque token). Pure add-on; consumed only by Phase C.

```
pnpm add qrcode.react
```

**Risk / fallback (Open Question Q1):** if the sandbox/registry blocks the install, Phase C falls
back to a dependency-free `<svg>` "code chip" preview (monospace token in a framed box) and the live
QR raster is deferred — the label + helper + Generate work is unaffected. Decide before C starts.

**Gate (Phase 0):** `labels.ts` exports compile and are unit-importable; `pnpm run build` +
typecheck pass with the new CSS and dependency present; **no visual/behavioral change yet** (nothing
imports the new classes). This is the merge barrier — A/B/C branch from here.

---

## File-ownership matrix  *(why A/B/C are conflict-free)*

| File | Phase 0 | A | B | C |
|---|:--:|:--:|:--:|:--:|
| `mission/labels.ts` | **own** | read | read | read |
| `dashboard.module.css` | **own** (+append rule) | append-only | append-only | append-only |
| `package.json` / lockfile | **own** | — | — | — |
| `mission/MissionTree.tsx` | — | **own** | — | — |
| `tests/e2e/mission-hierarchy.spec.ts` | — | **own** | — | — |
| `mission/NodeControls.tsx` | — | — | **own** | — |
| `mission/SubstageEditor.tsx` | — | — | — | **own** |
| `mission/QrPreview.tsx` *(new)* | — | — | — | **own** |

No two parallel phases write the same file. The only shared file (`dashboard.module.css`) is fully
populated by Phase 0; A/B/C only append at EOF if strictly necessary.

---

## Phase A — Hierarchy restructure  *(parallel; owns `MissionTree.tsx` + e2e spec)*

**Goal:** make the tree read as a nested structure — stage cards → indented substage sub-cards →
labeled Clues / Targets sections — so it's always obvious what a row or "+ Add" button belongs to.

**Scope (`MissionTree.tsx` rewrite, same props/seam `onMutated`):**
- Each stage → `.treeStage` card with a `.treeStageHead` (`.treeEyebrow` "STAGE {sequenceOrder}",
  `.treeNodeTitle` {title}, `.treeActions` holding the existing `NodeRowControls`).
- Substages → wrapped in `.treeSubstageRail` (indent + connector), each a `.treeSubstage` with a
  `.treeSubstageHead`: title + **friendly** play-mode badge + actions. Badge:
  `<span className={styles.chip} data-tone={substage.playMode === 'Trivia' ? 'accent' : 'success'}
  data-playmode={substage.playMode} data-testid={`substage-playmode-${id}`}>
  {PLAY_MODE_LABELS[substage.playMode]}</span>` — **`data-playmode` carries the raw token for e2e.**
- Winner-score line → `Winner score: {n}` (was `· winnerScore {n}`), via friendly copy.
- `SubstageEditor` mount unchanged (Phase C owns its internals).
- Clues → a `.treeSection` with `.treeSectionLabel` "Clues" + `.treeEmpty` when none; each clue a
  `.treeClue`: title, `.treeClueText` {text}, and a visibility **chip** using `clueVisibilityLabel(...)`
  instead of `({clue.visibilityPolicy})`. **Keep `data-testid={`clue-node-${id}`}` and the title text**
  (spec asserts `toContainText('First Clue')`).
- "+ Add clue" / "+ Add substage" / "+ Add stage" triggers wrapped in `.treeAddRow` so they sit
  clearly under their owning parent (controls themselves stay in `NodeControls`, owned by B).
- Preserve **all** testids: `mission-tree`, `mission-tree-empty`, `stage-node-{id}`,
  `substage-node-{id}`, `substage-playmode-{id}`, `clue-node-{id}`.

**e2e spec edits (this phase only — the single allowed coupling change):**
- `setPlayMode` helper: replace `(await badge.innerText()).trim()` comparison with
  `await badge.getAttribute('data-playmode')`; replace the trailing
  `toContainText(targetMode)` with `toHaveAttribute('data-playmode', targetMode)`.
- Test *"switching play mode warns…"*: final assertion
  `toContainText('TreasureHunt')` → `toHaveAttribute('data-playmode', 'TreasureHunt')`.
- Nothing else in the spec changes (selectOption-by-value and clue/target/title assertions still hold).

**Gate (A):** seeded multi-stage mission renders as legible nested cards; play-mode badge shows
"Treasure Hunt"/"Trivia" but exposes `data-playmode`; clue visibility shows friendly text; full e2e
suite green; build + typecheck pass; no `SessionMode`/`SessionSource` copy.

---

## Phase B — Friendly node forms & clue-visibility labels  *(parallel; owns `NodeControls.tsx`)*

**Goal:** replace the inline `.confirmRow` field sprawl with a contained `.nodeForm`, and surface the
clue-visibility enum as friendly options.

**Scope (`NodeControls.tsx`):**
- Delete the local `const CLUE_VISIBILITY_POLICIES`/`ClueVisibility` (lines 14–15); import
  `CLUE_VISIBILITY_POLICIES`, `ClueVisibility`, `clueVisibilityLabel` from `labels.ts`.
- The clue-visibility `<select>` (in `AddNodeControl` ~L122 and `NodeRowControls` ~L331): keep
  `value={p}` (raw enum), render `{clueVisibilityLabel(p)}` as the option text. Keep
  `data-testid="clue-visibility-input"`.
- Wrap the add-form (`AddNodeControl`, currently `.confirmRow`) and the edit-form (`NodeRowControls`
  editing branch) in `.nodeForm` + `.nodeFormGrid`, each field a `.nodeField` with a `.fieldLabel`
  (title, sequence, clue text, visibility), Save/Cancel in `.nodeFormActions`. **Keep every testid**:
  `node-title-input`, `node-sequence-input`, `clue-text-input`, `clue-visibility-input`,
  `add-stage-btn`/`confirm-add-stage-btn`, `add-substage-btn-{id}`/`confirm-add-substage-btn-{id}`,
  `add-clue-btn-{id}`/`confirm-add-clue-btn-{id}`, `edit-node-btn-{id}`, `remove-node-btn-{id}`,
  `confirm-remove-node-btn-{id}`, `node-error`.
- Add `<label>` text / placeholders that read naturally ("Stage title", "Clue title", "Clue text",
  "Order", "Clue visibility"). The Save-button selector used by the spec
  (`button:has-text("Save"):not([data-testid*="confirm"])`) must keep matching — Save button text
  stays "Save".

**Gate (B):** add/edit forms render as a tidy contained form; visibility dropdown shows
"Visible when substage starts" / "Hidden until operator releases it" while POSTing the raw enum;
add-stage→substage→clue and edit/remove e2e tests green; build + typecheck pass.

---

## Phase C — QR field + preview add-on & play-mode/trivia labels  *(parallel; owns `SubstageEditor.tsx` + new `QrPreview.tsx`)*

**Goal:** turn the bare QR box into a labeled, self-explanatory field with one-click Generate and an
opt-in QR image preview; show friendly play-mode and trivia/selection copy.

**Scope:**

`QrPreview.tsx` (new, `'use client'`):
- Collapsible add-on. Props `{ code: string }`. A `.qrPreviewToggle` button ("Show QR preview" /
  "Hide QR preview"); when open and `code` non-empty, render `<QRCodeSVG value={code} size={128} />`
  (from `qrcode.react`) inside `.qrPreview`. Empty code → muted "Enter or generate a code to preview."
  Add `data-testid="qr-preview-{targetId|new}"` (new testid; additive). Fallback variant per Q1.

`SubstageEditor.tsx`:
- Delete local `const PLAY_MODES` (line 19); import `PLAY_MODES`, `PlayMode`, `PLAY_MODE_LABELS`,
  `TARGET_QR_HELP`, `generateTargetQrCode` from `labels.ts`.
- `PlayModeControl` `<select>` (~L237): keep `value={m}` (raw), render `{PLAY_MODE_LABELS[m]}`.
  Keep `data-testid="playmode-select-{id}"` and the `playmode-switch-warning` testid. Warning copy
  may use `PLAY_MODE_LABELS[pending]` ("Switching to Treasure Hunt discards…").
- **QR field** in both `AddTargetControl` (~L363) and `TargetRow` edit (~L513): wrap in `.qrField` →
  `.fieldLabel` "QR code", `.qrInputRow` = the existing `target-qrcode-input` **(testid unchanged)** +
  a "Generate" `.smallButton` calling `setQrCode(generateTargetQrCode())` (new testid
  `generate-qr-btn-{...}`), `.qrHelp` = `TARGET_QR_HELP`, and `<QrPreview code={qrCode} />` below.
  Re-label the other fields too ("Target name", "Winner score (optional)", "Active") and contain the
  form in `.nodeForm`/`.nodeFormGrid`/`.nodeFormActions`. **Keep testids** `target-name-input`,
  `target-qrcode-input`, `target-winnerscore-input`, `target-active-input`, `target-sequence-input`,
  `add-target-btn-{id}`, `target-node-{id}`, `edit-target-btn-{id}`, `remove-target-btn-{id}`,
  `confirm-remove-target-btn-{id}`, `clue-select-{id}`, `associate-clue-btn-{id}`,
  `unassociate-clue-btn-{id}`. Save button text stays "Save".
- `TargetRow` display (~L565): present `name`, QR token (monospace), optional "inactive", and the
  clue link. **Keep the literal lowercase substring `clue #{clueId}`** (spec asserts
  `toContainText('clue #')`); a `<QrPreview code={target.qrCode} />` add-on here lets an admin print
  an authored target's QR.
- `TriviaSelectionControl` (~L167): replace `· selected quiz #{current}` with `Selected: {title}`
  (look the title up in the loaded `quizzes`; fall back to `#{current}` if not yet loaded). Keep
  `trivia-quiz-select-{id}` testid and the "Select quiz"/"Change quiz" button.

**Gate (C):** QR field shows label + helper + working Generate (fills a `TGT-…` token) + collapsible
preview; play-mode select/warning use friendly labels while POSTing raw enum; trivia line shows the
quiz title; treasure-hunt + play-mode-switch + trivia e2e tests green; build + typecheck pass.

---

## Phase V — Verify & prune  *(sequential; after A+B+C merge)*

**Scope:**
- Run full suite: `pnpm run lint`, typecheck, `pnpm run build`, `pnpm exec playwright test
  mission-hierarchy.spec.ts` (+ `missions.spec.ts` for regressions).
- Prune now-dead CSS classes superseded by Phase 0's set (e.g. old `.targetRow`, `.playModeRow`,
  `.substageEditor` if fully replaced) — only those confirmed unreferenced by grep.
- Manual smoke on `/dashboard` → seeded "E2E Activatable Mission": confirm nesting legibility, no raw
  enum tokens anywhere in the authoring UI, QR Generate + preview, and the canon guard (no
  `SessionMode`/`SessionSource`).
- Optional: `/code-review` on the diff.

**Gate (V):** all checks green; screenshots of before/after attached to the PR; zero raw enum tokens
in authoring copy; all original data-testids still resolve.

---

## Friendly-label dictionary  *(single source — implemented in `labels.ts`)*

| Surface | Raw (kept as value/payload) | Shown to admin |
|---|---|---|
| Play mode | `TreasureHunt` | Treasure Hunt |
| Play mode | `Trivia` | Trivia |
| Clue visibility | `VisibleWhenSubstageStarts` | Visible when substage starts |
| Clue visibility | `HiddenUntilOperatorRelease` | Hidden until operator releases it |
| Winner score line | `winnerScore {n}` | Winner score: {n} |
| Trivia selection | `selected quiz #{id}` | Selected: {quiz title} |

---

## Commit sequence

- `style(frontend): mission-tree label module, shared CSS + qr dep - HU-10A` (Phase 0)
- `feat(frontend): nested mission-tree layout + friendly badges - HU-10A` (Phase A)
- `feat(frontend): contained node forms + friendly clue visibility - HU-10A` (Phase B)
- `feat(frontend): qr field generate + preview add-on, friendly play-mode/trivia - HU-10A` (Phase C)
- `chore(frontend): prune dead tree CSS + verify - HU-10A` (Phase V)

Each carries `Ref: HU-10A` / `Ref: DES-15` / `Ref: DES-62`. (No Claude co-author trailer.)

---

## Open Questions / Dependencies

- **Q1 — QR-preview dependency install.** `qrcode.react` must be installable in the build/CI
  environment. If blocked, Phase C uses the dependency-free framed-token fallback (above). **Resolve
  before Phase C.**
- **Q2 — Generate-button placement.** Generate is offered in both the add-target and edit-target
  forms. Edit-on-an-existing-code overwrites a possibly-printed token; acceptable for Draft missions
  (authoring only), but confirm no live session references it (runtime is a separate context — out of
  scope here).
- **Phase 0 is a true barrier:** A/B/C must branch from a merged Phase 0 so the `labels.ts` API and
  CSS class names are stable. Starting a parallel phase against an unmerged Phase 0 risks contract drift.

## Out of Scope

- Any backend change; any DTO/contract/endpoint change (`qrCode` stays a string).
- Rendering/printing/exporting QR rasters as a first-class authoring capability beyond the opt-in
  preview (a separate operator/export concern).
- Reorder UX (drag/drop) for stages/substages/clues — `sequenceOrder` editing stays as-is.
- Hoisting `MISSION_DESIGN_SERVICE_URL` / `getIdentityHeaders` (tracked in the original HU-10A plan).
- `ActivationBar.tsx` and `MissionsPanel.tsx` detail chrome — not part of the reported defects.
