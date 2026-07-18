import { useEffect, useRef, useState } from 'react';
import { Keyboard, Pressable, View } from 'react-native';
import * as Haptics from 'expo-haptics';
import { useRouter, type Href } from 'expo-router';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useAuth } from '@/lib/auth/use-auth';
import { colors, spacing } from '@/constants/theme';

const NETWORK_ERROR_PREFIX = 'Error de red';

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
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

export default function LoginScreen() {
  const { signIn, status, errorMessage } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [emailError, setEmailError] = useState('');
  const [passwordError, setPasswordError] = useState('');

  const loading = status === 'authenticating';
  const isNetworkError = errorMessage?.startsWith(NETWORK_ERROR_PREFIX) ?? false;

  // Haptic on auth error (errorMessage appearing/changing).
  const prevErrorRef = useRef<string | null>(null);
  useEffect(() => {
    if (errorMessage && errorMessage !== prevErrorRef.current) {
      fireHaptic('error');
    }
    prevErrorRef.current = errorMessage;
  }, [errorMessage]);

  // Haptic on successful auth (fires before the guard redirects away).
  useEffect(() => {
    if (status === 'authenticated') {
      fireHaptic('success');
    }
  }, [status]);

  async function handleSubmit() {
    Keyboard.dismiss();

    let valid = true;
    if (!email.trim()) {
      setEmailError('Correo electrónico es obligatorio.');
      valid = false;
    } else if (!isValidEmail(email.trim())) {
      setEmailError('Ingresa un correo electrónico válido.');
      valid = false;
    } else {
      setEmailError('');
    }

    if (!password) {
      setPasswordError('La contraseña es obligatoria.');
      valid = false;
    } else {
      setPasswordError('');
    }

    if (!valid) {
      fireHaptic('error');
      return;
    }

    await signIn(email.trim(), password);
  }

  function handleForgotPassword() {
    Keyboard.dismiss();
    // Native form (ADR-0016 §1) — no Keycloak hosted page, no browser redirect.
    router.push('/(auth)/forgot-password' as Href);
  }

  function handleCreateAccount() {
    Keyboard.dismiss();
    // Native form (ADR-0016 §1) — no Keycloak hosted page, no browser redirect.
    router.push('/(auth)/register' as Href);
  }

  return (
    <Screen centered contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
        <BrandMark size="lg" />
      </View>

      <TextField
        label="Correo electrónico"
        value={email}
        onChangeText={setEmail}
        placeholder="you@example.com"
        keyboardType="email-address"
        autoCapitalize="none"
        autoCorrect={false}
        returnKeyType="next"
        error={emailError}
      />

      <TextField
        label="Contraseña"
        value={password}
        onChangeText={setPassword}
        placeholder="••••••••"
        secure
        returnKeyType="go"
        onSubmitEditing={handleSubmit}
        error={passwordError}
      />

      {errorMessage ? (
        <Text
          variant="body"
          selectable
          style={{ color: colors.signalCritical, textAlign: 'center' }}
        >
          {errorMessage}
        </Text>
      ) : null}

      <Button
        label={isNetworkError ? 'Reintentar' : 'Iniciar sesión'}
        variant="primary"
        onPress={handleSubmit}
        disabled={loading}
        loading={loading}
      />

      <Pressable
        accessibilityRole="link"
        onPress={handleForgotPassword}
        disabled={loading}
        style={{ alignItems: 'center', paddingVertical: spacing.xs }}
      >
        <Text
          variant="label"
          style={{
            color: loading ? colors.textMuted : colors.emberAccentStrong,
            textDecorationLine: 'underline',
          }}
        >
          ¿Olvidaste tu contraseña?
        </Text>
      </Pressable>

      <Pressable
        accessibilityRole="link"
        onPress={handleCreateAccount}
        disabled={loading}
        style={{ alignItems: 'center', paddingVertical: spacing.xs }}
      >
        <Text
          variant="label"
          style={{
            color: loading ? colors.textMuted : colors.emberAccentStrong,
            textDecorationLine: 'underline',
          }}
        >
          ¿Primera vez? Crea una cuenta
        </Text>
      </Pressable>
    </Screen>
  );
}
