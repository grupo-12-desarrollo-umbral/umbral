import { View, type ViewProps } from 'react-native';
import { colors, radii, shadows, spacing } from '@/constants/theme';

interface PanelProps extends ViewProps {
  padded?: boolean;
}

export function Panel({ style, padded = true, children, ...props }: PanelProps) {
  return (
    <View
      style={[
        {
          backgroundColor: colors.panelSurface,
          borderRadius: radii.panel,
          borderCurve: 'continuous',
          borderWidth: 1,
          borderColor: colors.borderSoft,
          boxShadow: shadows.insetSheen,
          padding: padded ? spacing.lg : 0,
        },
        style,
      ]}
      {...props}
    >
      {children}
    </View>
  );
}
