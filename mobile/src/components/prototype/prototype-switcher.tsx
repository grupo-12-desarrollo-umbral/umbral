/**
 * THROWAWAY floating variant switcher for UI prototypes.
 *
 * A fixed bottom-centre pill with ◀ / label / ▶ that cycles a `?variant=` search
 * param (shareable + reload-stable). Keyboard ←/→ on web. Hidden in production
 * builds so a stray prototype merge can never ship the bar. Delete when the
 * winning variant is folded in.
 */
import { useCallback, useEffect } from 'react';
import { Platform, Pressable, View } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Text } from '@/components/ui/text';
import { colors, radii, shadows, spacing } from '@/constants/theme';

export type PrototypeVariant = { key: string; name: string };

export function PrototypeSwitcher({
  variants,
  paramName = 'variant',
}: {
  variants: readonly PrototypeVariant[];
  paramName?: string;
}) {
  const router = useRouter();
  const params = useLocalSearchParams<Record<string, string | string[]>>();

  const raw = params[paramName];
  const current = (Array.isArray(raw) ? raw[0] : raw) ?? variants[0]?.key ?? '';
  const index = Math.max(
    0,
    variants.findIndex((v) => v.key === current),
  );
  const active = variants[index] ?? variants[0];

  const cycle = useCallback(
    (delta: number) => {
      if (variants.length === 0) return;
      const nextIndex = (index + delta + variants.length) % variants.length;
      router.setParams({ [paramName]: variants[nextIndex].key });
    },
    [index, variants, router, paramName],
  );

  // Web-only keyboard cycling. Ignore when a text control is focused so arrow keys
  // still move the caret. DOM globals are cast through `any` (no DOM lib in RN tsconfig).
  useEffect(() => {
    if (Platform.OS !== 'web') return;
    const g = globalThis as any;
    const onKey = (e: any) => {
      const el = g.document?.activeElement;
      const tag = el?.tagName?.toLowerCase();
      if (tag === 'input' || tag === 'textarea' || el?.isContentEditable) return;
      if (e.key === 'ArrowLeft') cycle(-1);
      else if (e.key === 'ArrowRight') cycle(1);
    };
    g.window?.addEventListener('keydown', onKey);
    return () => g.window?.removeEventListener('keydown', onKey);
  }, [cycle]);

  // Never render in production — a merged prototype must not expose the switcher.
  if (process.env.NODE_ENV === 'production') return null;
  if (variants.length === 0) return null;

  return (
    <View
      pointerEvents="box-none"
      style={{ position: 'absolute', left: 0, right: 0, bottom: 28, alignItems: 'center', zIndex: 100 }}
    >
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.xs,
          backgroundColor: colors.charcoalRoom,
          borderRadius: radii.pill,
          borderCurve: 'continuous',
          paddingVertical: spacing.xs,
          paddingHorizontal: spacing.sm,
          boxShadow: shadows.card,
        }}
      >
        <Arrow label="Variante anterior" glyph="‹" onPress={() => cycle(-1)} />
        <View style={{ minWidth: 150, alignItems: 'center', paddingHorizontal: spacing.xs }}>
          <Text variant="label" style={{ color: colors.emberAccentSoft }}>
            {active ? `${active.key} — ${active.name}` : '—'}
          </Text>
        </View>
        <Arrow label="Variante siguiente" glyph="›" onPress={() => cycle(1)} />
      </View>
    </View>
  );
}

function Arrow({ label, glyph, onPress }: { label: string; glyph: string; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      onPress={onPress}
      hitSlop={spacing.xs}
      style={{
        width: 36,
        height: 36,
        borderRadius: radii.pill,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: colors.emberPanelNight,
      }}
    >
      <Text variant="headline" style={{ color: colors.emberAccentSoft, fontSize: 22, lineHeight: 24 }}>
        {glyph}
      </Text>
    </Pressable>
  );
}
