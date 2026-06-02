import { View } from 'react-native';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Panel } from '@/components/ui/panel';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { useAuth } from '@/lib/auth/use-auth';
import { spacing } from '@/constants/theme';

export default function ParticipantHome() {
  const { profile, signOut } = useAuth();

  return (
    <Screen centered contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
        <BrandMark size="lg" />
      </View>

      <Panel>
        <View style={{ gap: spacing.sm }}>
          <Text variant="headline">
            Welcome, {profile?.displayName ?? 'Participant'}
          </Text>
          <Text variant="body" muted>
            You're signed in. More features are coming soon.
          </Text>
          {profile?.email ? (
            <Text variant="label" muted>
              {profile.email}
            </Text>
          ) : null}
        </View>
      </Panel>

      <Button label="Sign out" variant="secondary" onPress={signOut} />
    </Screen>
  );
}
