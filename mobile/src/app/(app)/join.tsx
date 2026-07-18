import { useState } from 'react';
import { Keyboard, View } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import * as Haptics from 'expo-haptics';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Panel } from '@/components/ui/panel';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { spacing } from '@/constants/theme';

const SESSION_CODE_RE = /^[A-Za-z0-9]{6}$/;

function normalizeSessionCode(value: string): string {
  return value.trim().toUpperCase();
}

function isSessionCode(value: string): boolean {
  return SESSION_CODE_RE.test(normalizeSessionCode(value));
}

function fireHaptic(type: 'success' | 'error') {
  if (process.env.EXPO_OS === 'ios') {
    Haptics.notificationAsync(
      type === 'success'
        ? Haptics.NotificationFeedbackType.Success
        : Haptics.NotificationFeedbackType.Error,
    );
  }
}

export default function JoinSessionScreen() {
  const router = useRouter();
  const [sessionCode, setSessionCode] = useState('');
  const [sessionError, setSessionError] = useState('');

  function handleSubmit() {
    Keyboard.dismiss();
    if (!sessionCode.trim()) {
      setSessionError('El código de sesión es obligatorio.');
      fireHaptic('error');
      return;
    }
    if (!isSessionCode(sessionCode)) {
      setSessionError('Ingresa un código de sesión válido de 6 caracteres.');
      fireHaptic('error');
      return;
    }
    setSessionError('');
    const normalizedSessionCode = normalizeSessionCode(sessionCode);
    router.push({
      pathname: '/(app)/team-lobby',
      params: { sessionCode: normalizedSessionCode },
    } as Href);
  }

  return (
    <Screen contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingVertical: spacing.lg }}>
        <BrandMark size="lg" />
      </View>

      <Panel>
        <View style={{ gap: spacing.xs }}>
          <Text variant="headline">Únete a tu sesión</Text>
          <Text variant="body" muted>
            Ingresa el código de sesión que te dio tu operador para ver la sala
            de equipos.
          </Text>
        </View>
      </Panel>

      <TextField
        label="Código de sesión"
        value={sessionCode}
        onChangeText={setSessionCode}
        placeholder="RSF231"
        autoCapitalize="characters"
        autoCorrect={false}
        returnKeyType="go"
        onSubmitEditing={handleSubmit}
        error={sessionError}
      />

      <Button label="Ver equipos" variant="primary" onPress={handleSubmit} />

      <Button
        label="Atrás"
        variant="secondary"
        onPress={() => router.back()}
      />
    </Screen>
  );
}
