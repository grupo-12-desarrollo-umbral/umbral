import { Text as RNText, type TextProps } from 'react-native';
import { colors, typography } from '@/constants/theme';

type Variant = 'display' | 'headline' | 'title' | 'body' | 'label' | 'mono';

interface UmbralTextProps extends TextProps {
  variant?: Variant;
  muted?: boolean;
  accent?: boolean;
}

export function Text({ variant = 'body', muted, accent, style, ...props }: UmbralTextProps) {
  const color = accent
    ? colors.emberAccent
    : muted
      ? colors.textMuted
      : colors.textInk;

  return (
    <RNText
      style={[typography[variant], { color }, style]}
      {...props}
    />
  );
}
