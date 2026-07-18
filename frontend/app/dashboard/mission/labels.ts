// Display/label seam for mission-hierarchy authoring (HU-10A / DES-15 / DES-62).
//
// Friendly labels are DISPLAY-ONLY: <select> option values and request payloads keep the
// raw backend enum string (TreasureHunt, VisibleWhenSubstageStarts, …). Only the visible
// text changes. Pure TS — safe to import from any client component.

export const PLAY_MODES = ['TreasureHunt', 'Trivia'] as const
export type PlayMode = (typeof PLAY_MODES)[number]
export const PLAY_MODE_LABELS: Record<PlayMode, string> = {
  TreasureHunt: 'Búsqueda del tesoro',
  Trivia: 'Trivia',
}

// Backend enum (Domain/Enums/ClueVisibilityPolicy.cs) — sent as the string name and
// parsed with Enum.Parse on the server. The UI never invents values outside this set.
export const CLUE_VISIBILITY_POLICIES = [
  'VisibleWhenSubstageStarts',
  'HiddenUntilOperatorRelease',
] as const
export type ClueVisibility = (typeof CLUE_VISIBILITY_POLICIES)[number]
const CLUE_VISIBILITY_LABELS: Record<string, string> = {
  VisibleWhenSubstageStarts: 'Visible al iniciar la subetapa',
  HiddenUntilOperatorRelease: 'Oculta hasta que el operador la libere',
}
// Tolerant: unknown server value (e.g. a future policy) degrades to a humanized fallback,
// never a crash or a raw PascalCase token.
export function clueVisibilityLabel(policy: string): string {
  return CLUE_VISIBILITY_LABELS[policy] ?? policy.replace(/([a-z])([A-Z])/g, '$1 $2')
}

export const TARGET_QR_HELP =
  'El código incrustado en el QR impreso que los participantes escanean para resolver este target.'

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
