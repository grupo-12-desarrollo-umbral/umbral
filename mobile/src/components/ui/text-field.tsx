import { useState } from 'react';
import { Pressable, TextInput, View, type TextInputProps } from 'react-native';
import { Text } from './text';
import { colors, radii, shadows, spacing, typography } from '@/constants/theme';

interface TextFieldProps extends Omit<TextInputProps, 'style'> {
  label: string;
  error?: string;
  secure?: boolean;
}

export function TextField({ label, error, secure, ...props }: TextFieldProps) {
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);

  const borderColor = error
    ? colors.signalCritical
    : focused
      ? colors.emberAccentStrong
      : colors.borderSoft;

  return (
    <View style={{ gap: spacing.xs - 2 }}>
      <Text variant="label" style={{ color: colors.textMuted }}>
        {label.toUpperCase()}
      </Text>
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          backgroundColor: colors.raisedSurface,
          borderRadius: radii.control,
          borderCurve: 'continuous',
          borderWidth: 1.5,
          borderColor,
          boxShadow: shadows.insetSheen,
          paddingHorizontal: spacing.md,
          minHeight: 48,
        }}
      >
        <TextInput
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          secureTextEntry={secure && !revealed}
          placeholderTextColor={colors.textMuted}
          style={{
            flex: 1,
            ...typography.body,
            color: colors.textInk,
            paddingVertical: spacing.sm,
          }}
          {...props}
        />
        {secure && (
          <Pressable
            onPress={() => setRevealed((v) => !v)}
            hitSlop={8}
            style={{ paddingLeft: spacing.xs }}
          >
            <Text variant="label" style={{ color: colors.textMuted }}>
              {revealed ? 'HIDE' : 'SHOW'}
            </Text>
          </Pressable>
        )}
      </View>
      {error ? (
        <Text variant="label" selectable style={{ color: colors.signalCritical }}>
          {error}
        </Text>
      ) : null}
    </View>
  );
}
