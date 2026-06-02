import { ActivityIndicator, Pressable, type PressableProps } from 'react-native';
import * as Haptics from 'expo-haptics';
import { Text } from './text';
import { colors, radii, shadows, spacing, typography } from '@/constants/theme';

type Variant = 'primary' | 'secondary';

interface ButtonProps extends Omit<PressableProps, 'style'> {
  label: string;
  variant?: Variant;
  loading?: boolean;
}

export function Button({ label, variant = 'primary', loading, disabled, onPress, ...props }: ButtonProps) {
  const isPrimary = variant === 'primary';

  const handlePress: PressableProps['onPress'] = (e) => {
    if (process.env.EXPO_OS === 'ios') {
      Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    }
    onPress?.(e);
  };

  return (
    <Pressable
      onPress={handlePress}
      disabled={disabled || loading}
      style={({ pressed }) => ({
        backgroundColor: isPrimary
          ? pressed ? colors.emberAccent : colors.emberAccentStrong
          : pressed ? colors.borderSoft : colors.raisedSurface,
        borderRadius: radii.control,
        borderCurve: 'continuous',
        paddingVertical: 12,
        paddingHorizontal: 16,
        alignItems: 'center' as const,
        justifyContent: 'center' as const,
        flexDirection: 'row' as const,
        gap: spacing.xs,
        opacity: (disabled && !loading) ? 0.45 : 1,
        boxShadow: shadows.insetSheen,
        borderWidth: isPrimary ? 0 : 1,
        borderColor: colors.borderSoft,
        minHeight: 44,
      })}
      {...props}
    >
      {loading ? (
        <ActivityIndicator
          size="small"
          color={isPrimary ? colors.ivoryFog : colors.textInk}
        />
      ) : (
        <Text
          variant="label"
          style={{
            color: isPrimary ? colors.ivoryFog : colors.textInk,
            ...typography.label,
          }}
        >
          {label}
        </Text>
      )}
    </Pressable>
  );
}
