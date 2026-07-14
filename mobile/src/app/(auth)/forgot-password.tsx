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
      return 'Network error. Check your connection and try again.';
    }
    if (error.status === 429) {
      return 'Too many attempts. Please try again in a few minutes.';
    }
    if (error.status === 400) {
      return 'Please check your details and try again.';
    }
  }
  return 'Something went wrong. Please try again.';
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
      setEmailError('Email is required.');
      fireHaptic('error');
      return;
    }
    if (!isValidEmail(trimmedEmail)) {
      setEmailError('Enter a valid email address.');
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
          Check your email
        </Text>
        <Text variant="body" style={{ color: colors.textMuted, textAlign: 'center' }}>
          If an account exists for that email, we&apos;ve sent password reset instructions.
        </Text>

        <Button label="Back to sign in" variant="primary" onPress={handleBackToLogin} />
      </Screen>
    );
  }

  return (
    <Screen centered contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
        <BrandMark size="lg" />
      </View>

      <Text variant="title" style={{ textAlign: 'center' }}>
        Reset your password
      </Text>
      <Text variant="body" style={{ color: colors.textMuted, textAlign: 'center' }}>
        Enter your email and we&apos;ll send you a link to reset your password.
      </Text>

      <TextField
        label="Email"
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
        label="Send reset link"
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
          Back to sign in
        </Text>
      </Pressable>
    </Screen>
  );
}
