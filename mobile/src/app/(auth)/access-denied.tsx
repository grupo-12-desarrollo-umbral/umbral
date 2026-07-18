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
      title: 'Cuenta desactivada',
      body: 'Tu cuenta ha sido desactivada. Comunícate con el organizador del evento para obtener ayuda.',
    };
  }
  if (reason === 'wrong-role') {
    return {
      title: 'Acceso restringido',
      body: 'Esta app es solo para participantes. Las cuentas de administrador y operador no pueden iniciar sesión aquí.',
    };
  }
  return {
    title: 'Acceso denegado',
    body: 'No tienes permiso para acceder a esta app en este momento.',
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

      <Button label="Cerrar sesión" variant="secondary" onPress={signOut} />
    </Screen>
  );
}
