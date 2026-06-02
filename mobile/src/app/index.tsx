import { ActivityIndicator, View } from 'react-native';
import { colors } from '@/constants/theme';

// Shown briefly during session restore; AuthGuard in _layout redirects once status resolves.
export default function LoadingIndex() {
  return (
    <View style={{ flex: 1, backgroundColor: colors.ivoryFog, alignItems: 'center', justifyContent: 'center' }}>
      <ActivityIndicator size="large" color={colors.emberAccentStrong} />
    </View>
  );
}
