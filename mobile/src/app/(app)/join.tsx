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
      setSessionError('Session code is required.');
      fireHaptic('error');
      return;
    }
    if (!isSessionCode(sessionCode)) {
      setSessionError('Enter a valid 6-character session code.');
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
          <Text variant="headline">Join your session</Text>
          <Text variant="body" muted>
            Enter the session code your operator gave you to see the team lobby.
          </Text>
        </View>
      </Panel>

      <TextField
        label="Session Code"
        value={sessionCode}
        onChangeText={setSessionCode}
        placeholder="RSF231"
        autoCapitalize="characters"
        autoCorrect={false}
        returnKeyType="go"
        onSubmitEditing={handleSubmit}
        error={sessionError}
      />

      <Button label="View teams" variant="primary" onPress={handleSubmit} />

      <Button
        label="Back"
        variant="secondary"
        onPress={() => router.back()}
      />
    </Screen>
  );
}
