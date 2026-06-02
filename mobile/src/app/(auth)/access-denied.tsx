import { View } from 'react-native';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Panel } from '@/components/ui/panel';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { useAuth } from '@/lib/auth/use-auth';
import { spacing } from '@/constants/theme';
import type { RejectionReason } from '@/lib/auth/access-policy';

function getRejectionCopy(reason: RejectionReason | null): { title: string; body: string } {
  if (reason === 'deactivated') {
    return {
      title: 'Account Deactivated',
      body: 'Your account has been deactivated. Contact the event organiser for assistance.',
    };
  }
  if (reason === 'wrong-role') {
    return {
      title: 'Access Restricted',
      body: 'This app is for participants only. Administrator and operator accounts cannot sign in here.',
    };
  }
  return {
    title: 'Access Denied',
    body: 'You are not permitted to access this app at this time.',
  };
}

export default function AccessDeniedScreen() {
  const { rejectionReason, signOut } = useAuth();
  const { title, body } = getRejectionCopy(rejectionReason);

  return (
    <Screen centered contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
        <BrandMark size="lg" />
      </View>

      <Panel>
        <View style={{ gap: spacing.md, alignItems: 'center' }}>
          <Text variant="headline" style={{ textAlign: 'center' }}>
            {title}
          </Text>
          <Text variant="body" muted style={{ textAlign: 'center' }}>
            {body}
          </Text>
        </View>
      </Panel>

      <Button label="Sign out" variant="secondary" onPress={signOut} />
    </Screen>
  );
}
