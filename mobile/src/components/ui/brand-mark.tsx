import { View } from 'react-native';
import { Text } from './text';
import { colors, spacing } from '@/constants/theme';

interface BrandMarkProps {
  size?: 'sm' | 'md' | 'lg';
}

export function BrandMark({ size = 'md' }: BrandMarkProps) {
  const lanternSize = size === 'sm' ? 28 : size === 'lg' ? 52 : 40;
  const wordmarkSize = size === 'sm' ? 13 : size === 'lg' ? 22 : 17;

  return (
    <View style={{ alignItems: 'center', gap: spacing.sm }}>
      {/* Lantern glyph — filled circle with ember halo */}
      <View
        style={{
          width: lanternSize,
          height: lanternSize,
          borderRadius: lanternSize / 2,
          backgroundColor: colors.emberAccentStrong,
          alignItems: 'center',
          justifyContent: 'center',
          boxShadow: `0 0 ${lanternSize}px ${colors.emberAccentSoft}`,
        }}
      >
        <View
          style={{
            width: lanternSize * 0.45,
            height: lanternSize * 0.45,
            borderRadius: lanternSize,
            backgroundColor: colors.ivoryFog,
            opacity: 0.7,
          }}
        />
      </View>

      {/* Wordmark */}
      <Text
        variant="headline"
        style={{
          fontSize: wordmarkSize,
          letterSpacing: wordmarkSize * 0.18,
          color: colors.textInk,
          textTransform: 'uppercase',
        }}
      >
        Umbral
      </Text>
    </View>
  );
}
