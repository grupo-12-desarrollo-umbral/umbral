/**
 * The real Map tab surface for the treasure-hunt board (#156): a Leaflet map inside a WebView,
 * centred on the active target with a pin per target. Replaces the earlier labelled MapStub now that
 * target coordinates flow through the participant board contract (#154).
 *
 * A target list with no usable coordinates degrades to a clear empty state — no WebView, no crash.
 */
import { StyleSheet, View } from 'react-native';
import { WebView } from 'react-native-webview';
import { Text } from '@/components/ui/text';
import { colors, radii, spacing } from '@/constants/theme';
import { buildTargetMapHtml, type TargetMapMarker } from './target-map-html';

export type TargetMapTarget = {
  targetSnapshotId: string;
  name: string;
  latitude: number;
  longitude: number;
};

// A wire payload always carries numeric coordinates, but guard anyway: a target missing/`NaN`
// coordinates is treated as unplaceable so the map never renders a pin at a bogus point.
//
// 0,0 is unplaceable for a different reason: the backend has no null coordinate, so a target the
// operator saved without a location reads back as 0,0. Honouring it literally would drop a pin in the
// Atlantic (Null Island) instead of showing the empty state. The real point at 0,0 is open ocean and
// can't be authored in the operator editor either, so nothing reachable is lost.
function isPlaceable(target: TargetMapTarget): boolean {
  if (!Number.isFinite(target.latitude) || !Number.isFinite(target.longitude)) return false;
  return target.latitude !== 0 || target.longitude !== 0;
}

export function TargetMap({
  targets,
  style,
}: {
  targets: readonly TargetMapTarget[];
  style?: object;
}) {
  const placeable = targets.filter(isPlaceable);

  if (placeable.length === 0) {
    return (
      <View
        accessibilityRole="image"
        accessibilityLabel="Aún no hay ubicación de target en el mapa"
        style={[styles.frame, styles.empty, style]}
      >
        <Text variant="label" muted>AÚN SIN UBICACIÓN EN EL MAPA</Text>
        <Text variant="body" muted style={{ textAlign: 'center' }}>
          Este target no tiene lugar en el mapa.
        </Text>
      </View>
    );
  }

  const markers: TargetMapMarker[] = placeable.map((target) => ({
    id: target.targetSnapshotId,
    name: target.name,
    latitude: target.latitude,
    longitude: target.longitude,
  }));

  return (
    <View accessibilityLabel="Mapa de targets" style={[styles.frame, style]}>
      <WebView
        testID="target-map-webview"
        originWhitelist={['*']}
        source={{ html: buildTargetMapHtml(markers) }}
        style={styles.web}
        // A display surface, not an input: no page scroll, and keep it passive.
        scrollEnabled={false}
        javaScriptEnabled
        domStorageEnabled
      />
    </View>
  );
}

const styles = StyleSheet.create({
  frame: {
    borderRadius: radii.card,
    borderCurve: 'continuous',
    borderWidth: 1,
    borderColor: colors.borderSoft,
    overflow: 'hidden',
    backgroundColor: colors.warmMist,
  },
  web: { flex: 1, backgroundColor: 'transparent' },
  empty: { alignItems: 'center', justifyContent: 'center', gap: spacing.xs, padding: spacing.lg },
});
