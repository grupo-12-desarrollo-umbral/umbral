import { View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Panel } from '@/components/ui/panel';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { useAuth } from '@/lib/auth/use-auth';
import { spacing } from '@/constants/theme';

export default function ParticipantHome() {
  const { profile, signOut } = useAuth();
  const router = useRouter();

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
            When your session is live, enter your session code to see its teams
            and join your team space.
          </Text>
          {profile?.email ? (
            <Text variant="label" muted>
              {profile.email}
            </Text>
          ) : null}
        </View>
      </Panel>

      <Button
        label="Join your session"
        variant="primary"
        onPress={() => router.push('/(app)/join' as Href)}
      />

      <Button label="Sign out" variant="secondary" onPress={signOut} />
    </Screen>
  );
}
