import { ScrollView, type ScrollViewProps } from 'react-native';
import { colors, spacing } from '@/constants/theme';

interface ScreenProps extends ScrollViewProps {
  centered?: boolean;
}

export function Screen({ style, contentContainerStyle, centered, children, ...props }: ScreenProps) {
  return (
    <ScrollView
      contentInsetAdjustmentBehavior="automatic"
      keyboardShouldPersistTaps="handled"
      style={[{ flex: 1, backgroundColor: colors.ivoryFog }, style]}
      contentContainerStyle={[
        {
          padding: spacing.lg,
          gap: spacing.md,
          ...(centered && {
            flexGrow: 1,
            justifyContent: 'center' as const,
          }),
        },
        contentContainerStyle,
      ]}
      {...props}
    >
      {children}
    </ScrollView>
  );
}
