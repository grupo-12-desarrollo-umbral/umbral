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
import { requestPasswordReset } from '@/lib/api/identity';
import { colors, spacing } from '@/constants/theme';

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

// Maps a failed reset request to a user-facing message. The backend never reveals whether the email
// exists (it always 202s), so only transport-level failures reach here: map by HTTP status, the same
// approach the rest of the app uses.
function messageForError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 0) {
      return 'Error de red. Revisa tu conexión e inténtalo de nuevo.';
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

export default function ForgotPasswordScreen() {
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [emailError, setEmailError] = useState('');
  const [submitError, setSubmitError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

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

    const trimmedEmail = email.trim();

    if (!trimmedEmail) {
      setEmailError('Correo electrónico es obligatorio.');
      fireHaptic('error');
      return;
    }
    if (!isValidEmail(trimmedEmail)) {
      setEmailError('Ingresa un correo electrónico válido.');
      fireHaptic('error');
      return;
    }
    setEmailError('');

    setSubmitting(true);
    try {
      await requestPasswordReset(trimmedEmail);
      fireHaptic('success');
      setSubmitted(true);
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

  // Neutral confirmation: identical whether or not the email is registered, so the screen never
  // discloses account existence (ADR-0016 §1).
  if (submitted) {
    return (
      <Screen centered contentContainerStyle={{ gap: spacing.md }}>
        <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
          <BrandMark size="lg" />
        </View>

        <Text variant="title" style={{ textAlign: 'center' }}>
          Revisa tu correo
        </Text>
        <Text variant="body" style={{ color: colors.textMuted, textAlign: 'center' }}>
          Si existe una cuenta para ese correo, te enviamos las instrucciones para restablecer tu contraseña.
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

      <Text variant="title" style={{ textAlign: 'center' }}>
        Restablece tu contraseña
      </Text>
      <Text variant="body" style={{ color: colors.textMuted, textAlign: 'center' }}>
        Ingresa tu correo y te enviaremos un enlace para restablecer tu contraseña.
      </Text>

      <TextField
        label="Correo electrónico"
        value={email}
        onChangeText={setEmail}
        placeholder="you@example.com"
        keyboardType="email-address"
        autoCapitalize="none"
        autoCorrect={false}
        returnKeyType="go"
        onSubmitEditing={handleSubmit}
        error={emailError}
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
        label="Enviar enlace"
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
          Volver a iniciar sesión
        </Text>
      </Pressable>
    </Screen>
  );
}
