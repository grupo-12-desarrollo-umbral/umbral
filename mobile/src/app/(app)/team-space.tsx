import { View } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Panel } from '@/components/ui/panel';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { colors, spacing } from '@/constants/theme';

function asParam(value: string | string[] | undefined): string {
  return Array.isArray(value) ? (value[0] ?? '') : (value ?? '');
}

/**
 * Placeholder team space the participant lands in once Identity confirms their
 * membership access fact. This is deliberately the end of the HU-07A slice:
 * Identity produced the gate, the client honoured it. The real-time hub
 * connection, group join, reconnection, and shared-state sync belong to
 * session-operations (HU-07B / HU-08) and are out of scope here.
 */
export default function TeamSpaceScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{
    liveSessionId?: string;
    teamId?: string;
    reason?: string;
  }>();

  const liveSessionId = asParam(params.liveSessionId);
  const teamId = asParam(params.teamId);
  const reason = asParam(params.reason);

  return (
    <Screen contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingVertical: spacing.lg }}>
        <BrandMark size="lg" />
      </View>

      <Panel>
        <View style={{ gap: spacing.sm }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
            <View
              style={{
                width: 10,
                height: 10,
                borderRadius: 5,
                backgroundColor: colors.signalSuccess,
              }}
            />
            <Text variant="headline">Access confirmed</Text>
          </View>
          <Text variant="body" muted>
            {reason || 'You may enter your team context.'}
          </Text>
        </View>
      </Panel>

      <Card>
        <View style={{ gap: spacing.sm }}>
          <Text variant="label" muted>
            TEAM
          </Text>
          <Text variant="mono" selectable>
            {teamId || '—'}
          </Text>
          <Text variant="label" muted>
            SESSION
          </Text>
          <Text variant="mono" selectable>
            {liveSessionId || '—'}
          </Text>
        </View>
      </Card>

      <Panel>
        <Text variant="body" muted>
          Connecting to your live team space is coming soon. This screen
          confirms the access fact only — it does not join you to the real-time
          session yet.
        </Text>
      </Panel>

      <Button label="Back" variant="secondary" onPress={() => router.back()} />
    </Screen>
  );
}
