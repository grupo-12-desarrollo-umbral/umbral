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
            Te damos la bienvenida, {profile?.displayName ?? 'Participante'}
          </Text>
          <Text variant="body" muted>
            Cuando tu sesión esté activa, ingresa tu código de sesión para ver
            sus equipos y unirte a tu espacio de equipo.
          </Text>
          {profile?.email ? (
            <Text variant="label" muted>
              {profile.email}
            </Text>
          ) : null}
        </View>
      </Panel>

      <Button
        label="Únete a tu sesión"
        variant="primary"
        onPress={() => router.push('/(app)/join' as Href)}
      />

      <Button label="Cerrar sesión" variant="secondary" onPress={signOut} />
    </Screen>
  );
}
