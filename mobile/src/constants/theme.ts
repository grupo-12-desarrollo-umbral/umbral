import { Platform } from 'react-native';

// --- Umbral design tokens (ember/parchment, light mode only) ---

export const colors = {
  // Ember accent family
  emberAccent: '#F3813F',        // oklch(72% 0.16 48)
  emberAccentStrong: '#E8651D',  // oklch(66% 0.18 44) — primary button fill
  emberAccentSoft: '#FAD6C0',    // oklch(90% 0.05 54) — active bg tint

  // Light surfaces
  ivoryFog: '#FFF9F0',           // oklch(98.4% 0.013 76) — page field
  warmMist: '#FAF1E5',           // oklch(96.2% 0.018 76) — secondary bg
  paperSurface: '#FFFCF7',       // oklch(99.4% 0.008 76) — brightest surface
  raisedSurface: '#FBF4EA',      // oklch(96.9% 0.015 76) — control lift
  panelSurface: '#FFFAF2',       // oklch(98.8% 0.012 76) — default panel

  // Dark surfaces (reserved for future dark mode)
  charcoalRoom: '#18110C',       // oklch(18.5% 0.016 58)
  emberPanelNight: '#221A13',    // oklch(22.5% 0.018 58)
  nightSurface: '#29201A',       // oklch(25.2% 0.018 58)

  // Borders
  borderSoft: '#E4DCD1',         // oklch(89.8% 0.018 76)
  borderSoftNight: '#3F3630',    // oklch(34% 0.016 58)

  // Text
  textInk: '#35251B',            // oklch(28% 0.03 54) — main text
  textInkNight: '#E7DED3',       // oklch(90.5% 0.018 72)
  textMuted: '#8C8179',          // oklch(61% 0.018 60) — secondary/caption

  // Semantic signals
  signalSuccess: '#4CA95F',      // oklch(66% 0.14 148)
  signalWarning: '#E2A856',      // oklch(77% 0.12 74)
  signalCritical: '#E5554C',     // oklch(64% 0.18 27)

  // Parchment (clue artifact)
  parchment: '#F3E4C8',          // oklch(92.4% 0.04 84)
  parchmentDeep: '#E6CFAF',      // oklch(86.6% 0.05 76)
} as const;

const _monoFamily = Platform.select({ ios: 'ui-monospace', default: 'monospace' });

export const typography = {
  display: {
    fontSize: 30,
    fontWeight: '600' as const,
    lineHeight: 32,
    letterSpacing: -1.5,
  },
  headline: {
    fontSize: 17,
    fontWeight: '600' as const,
    lineHeight: 20,
    letterSpacing: -0.5,
  },
  title: {
    fontSize: 16,
    fontWeight: '600' as const,
    lineHeight: 22,
    letterSpacing: 0,
  },
  body: {
    fontSize: 16,
    fontWeight: '400' as const,
    lineHeight: 24,
    letterSpacing: 0,
  },
  label: {
    fontSize: 13,
    fontWeight: '600' as const,
    lineHeight: 16,
    letterSpacing: 1,
  },
  mono: {
    fontFamily: _monoFamily,
    fontSize: 15,
    fontWeight: '400' as const,
    lineHeight: 26,
    letterSpacing: 0,
  },
} as const;

export const spacing = {
  xs: 8,
  sm: 12,
  md: 16,
  lg: 20,
  xl: 24,
  // Legacy scale kept for existing components
  half: 2,
  one: 4,
  two: 8,
  three: 16,
  four: 24,
  five: 32,
  six: 64,
} as const;

export const radii = {
  pill: 9999,
  panel: 22,
  card: 18,
  control: 16,
} as const;

export const shadows = {
  // Standard panel/control finish — crafted material without card-deck feel
  insetSheen: 'inset 0 1px 0 rgba(53, 37, 27, 0.05)',
  // Reserved for clue artifacts and signature moments
  lanternHalo: '0 10px 30px rgba(250, 214, 192, 0.12)',
  card: '0 2px 8px rgba(53, 37, 27, 0.06)',
} as const;

// --- Legacy exports (kept for existing components during migration) ---

export const Colors = {
  light: {
    text: colors.textInk,
    background: colors.ivoryFog,
    backgroundElement: colors.raisedSurface,
    backgroundSelected: colors.emberAccentSoft,
    textSecondary: colors.textMuted,
  },
  dark: {
    text: colors.textInkNight,
    background: colors.charcoalRoom,
    backgroundElement: colors.emberPanelNight,
    backgroundSelected: colors.nightSurface,
    textSecondary: colors.textMuted,
  },
} as const;

export type ThemeColor = keyof typeof Colors.light & keyof typeof Colors.dark;

export const Fonts = Platform.select({
  ios: {
    sans: 'system-ui',
    serif: 'ui-serif',
    rounded: 'ui-rounded',
    mono: 'ui-monospace',
  },
  default: {
    sans: 'normal',
    serif: 'serif',
    rounded: 'normal',
    mono: 'monospace',
  },
  web: {
    sans: 'var(--font-display)',
    serif: 'var(--font-serif)',
    rounded: 'var(--font-rounded)',
    mono: 'var(--font-mono)',
  },
});

export const Spacing = {
  half: 2,
  one: 4,
  two: 8,
  three: 16,
  four: 24,
  five: 32,
  six: 64,
} as const;

export const BottomTabInset = Platform.select({ ios: 50, android: 80 }) ?? 0;
export const MaxContentWidth = 800;
