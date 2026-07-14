import { useEffect, useRef, useState } from 'react';
import { Keyboard, Pressable, View } from 'react-native';
import * as Haptics from 'expo-haptics';
import * as Linking from 'expo-linking';
import { BrandMark } from '@/components/ui/brand-mark';
import { Button } from '@/components/ui/button';
import { Screen } from '@/components/ui/screen';
import { Text } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { buildRegistrationUrl, buildResetCredentialsUrl } from '@/lib/auth/keycloak';
import { useAuth } from '@/lib/auth/use-auth';
import { colors, spacing } from '@/constants/theme';

const NETWORK_ERROR_PREFIX = 'Network error';

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
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [emailError, setEmailError] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [resetError, setResetError] = useState('');
  const [registerError, setRegisterError] = useState('');

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
    setResetError('');
    setRegisterError('');

    let valid = true;
    if (!email.trim()) {
      setEmailError('Email is required.');
      valid = false;
    } else if (!isValidEmail(email.trim())) {
      setEmailError('Enter a valid email address.');
      valid = false;
    } else {
      setEmailError('');
    }

    if (!password) {
      setPasswordError('Password is required.');
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

  async function handleForgotPassword() {
    Keyboard.dismiss();
    setResetError('');

    try {
      await Linking.openURL(buildResetCredentialsUrl());
    } catch {
      setResetError('Could not open the password reset page.');
      fireHaptic('error');
    }
  }

  async function handleCreateAccount() {
    Keyboard.dismiss();
    setRegisterError('');

    try {
      await Linking.openURL(buildRegistrationUrl());
    } catch {
      setRegisterError('Could not open the sign-up page.');
      fireHaptic('error');
    }
  }

  return (
    <Screen centered contentContainerStyle={{ gap: spacing.md }}>
      <View style={{ alignItems: 'center', paddingBottom: spacing.xl }}>
        <BrandMark size="lg" />
      </View>

      <TextField
        label="Email"
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
        label="Password"
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

      {resetError ? (
        <Text
          variant="body"
          selectable
          style={{ color: colors.signalCritical, textAlign: 'center' }}
        >
          {resetError}
        </Text>
      ) : null}

      {registerError ? (
        <Text
          variant="body"
          selectable
          style={{ color: colors.signalCritical, textAlign: 'center' }}
        >
          {registerError}
        </Text>
      ) : null}

      <Button
        label={isNetworkError ? 'Try again' : 'Sign in'}
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
          Forgot your password?
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
          New here? Create an account
        </Text>
      </Pressable>
    </Screen>
  );
}
