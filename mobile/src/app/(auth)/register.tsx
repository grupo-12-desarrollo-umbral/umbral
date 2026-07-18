import { useRef, useState } from 'react';
import { Keyboard, Pressable, View } from 'react-native';
import * as Haptics from 'expo-haptics';
import { useRouter, type Href } from 'expo-router';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { ApiError } from '@/lib/api/client';
import { registerParticipant } from '@/lib/api/identity';
import { colors, spacing } from '@/constants/theme';

// Minimum password length; a floor only. Keycloak's realm policy is the source of truth and rejects a
// weak password on the backend, surfaced here as a generic error (ADR-0016 §1).
const MIN_PASSWORD_LENGTH = 8;

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

// Maps a failed register call to a user-facing message. The backend returns RFC 7807 problem+json
// (no `message` field the client can read), and the rate limiter returns a bodiless 429, so map by
// HTTP status — the same approach the rest of the app uses.
function messageForError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 0) {
      return 'Error de red. Revisa tu conexión e inténtalo de nuevo.';
    }
    if (error.status === 409) {
      return 'Ya existe una cuenta con este correo electrónico.';
    }
    if (error.status === 429) {
      return 'Demasiados intentos. Inténtalo de nuevo en unos minutos.';
    }
    if (error.status === 400) {
      return 'Revisa tus datos e inténtalo de nuevo.';
    }
  }
  return 'Algo salió mal. Inténtalo de nuevo.';
}

export default function RegisterScreen() {
  const router = useRouter();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayNameError, setDisplayNameError] = useState('');
  const [emailError, setEmailError] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [submitError, setSubmitError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [registered, setRegistered] = useState(false);

  const prevErrorRef = useRef<string | null>(null);

  function fireErrorHaptic(next: string) {
    if (next && next !== prevErrorRef.current) {
      fireHaptic('error');
    }
    prevErrorRef.current = next;
  }

  async function handleSubmit() {
    Keyboard.dismiss();
    setSubmitError('');

    const trimmedName = displayName.trim();
    const trimmedEmail = email.trim();

    let valid = true;
    if (!trimmedName) {
      setDisplayNameError('El nombre para mostrar es obligatorio.');
      valid = false;
    } else {
      setDisplayNameError('');
    }

    if (!trimmedEmail) {
      setEmailError('Correo electrónico es obligatorio.');
      valid = false;
    } else if (!isValidEmail(trimmedEmail)) {
      setEmailError('Ingresa un correo electrónico válido.');
      valid = false;
    } else {
      setEmailError('');
    }

    if (!password) {
      setPasswordError('La contraseña es obligatoria.');
      valid = false;
    } else if (password.length < MIN_PASSWORD_LENGTH) {
      setPasswordError(`La contraseña debe tener al menos ${MIN_PASSWORD_LENGTH} caracteres.`);
      valid = false;
    } else {
      setPasswordError('');
    }

    if (!valid) {
      fireHaptic('error');
      return;
    }

    setSubmitting(true);
    try {
      await registerParticipant(trimmedName, trimmedEmail, password);
      fireHaptic('success');
      setRegistered(true);
    } catch (err) {
      const message = messageForError(err);
      setSubmitError(message);
      fireErrorHaptic(message);
    } finally {
      setSubmitting(false);
    }
  }

  function handleBackToLogin() {
    router.replace('/(auth)/login' as Href);
  }

  if (registered) {
    return (
      <Screen centered contentContainerStyle={{ gap: spacing.md }}>
        <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
          <BrandMark size="lg" />
        </View>

        <Text variant="title" style={{ textAlign: 'center' }}>
          Revisa tu correo
        </Text>
        <Text variant="body" style={{ color: colors.textMuted, textAlign: 'center' }}>
          Enviamos un enlace de verificación a {email.trim()}. Verifica tu correo y luego inicia sesión.
        </Text>

        <Button label="Volver a iniciar sesión" variant="primary" onPress={handleBackToLogin} />
      </Screen>
    );
  }

  return (
    <Screen centered contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
        <BrandMark size="lg" />
      </View>

      <TextField
        label="Nombre para mostrar"
        value={displayName}
        onChangeText={setDisplayName}
        placeholder="Tu nombre"
        autoCapitalize="words"
        returnKeyType="next"
        error={displayNameError}
      />

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

      {submitError ? (
        <Text
          variant="body"
          selectable
          style={{ color: colors.signalCritical, textAlign: 'center' }}
        >
          {submitError}
        </Text>
      ) : null}

      <Button
        label="Crear cuenta"
        variant="primary"
        onPress={handleSubmit}
        disabled={submitting}
        loading={submitting}
      />

      <Pressable
        accessibilityRole="link"
        onPress={handleBackToLogin}
        disabled={submitting}
        style={{ alignItems: 'center', paddingVertical: spacing.xs }}
      >
        <Text
          variant="label"
          style={{
            color: submitting ? colors.textMuted : colors.emberAccentStrong,
            textDecorationLine: 'underline',
          }}
        >
          ¿Ya tienes una cuenta? Inicia sesión
        </Text>
      </Pressable>
    </Screen>
  );
}
