import { View, type ViewProps } from 'react-native';
import { colors, radii, shadows, spacing } from '@/constants/theme';

interface CardProps extends ViewProps {
  parchment?: boolean;
  padded?: boolean;
}

export function Card({ style, parchment, padded = true, children, ...props }: CardProps) {
  return (
    <View
      style={[
        {
          backgroundColor: parchment ? colors.parchment : colors.raisedSurface,
          borderRadius: radii.card,
          borderCurve: 'continuous',
          borderWidth: 1,
          borderColor: parchment ? colors.parchmentDeep : colors.borderSoft,
          boxShadow: parchment ? shadows.lanternHalo : shadows.insetSheen,
          padding: padded ? spacing.md : 0,
        },
        style,
      ]}
      {...props}
    >
      {children}
    </View>
  );
}
