import * as SecureStore from 'expo-secure-store';

const KEYS = {
  accessToken: 'umbral.access_token',
  refreshToken: 'umbral.refresh_token',
} as const;

export function getAccessToken(): Promise<string | null> {
  return SecureStore.getItemAsync(KEYS.accessToken);
}

export function getRefreshToken(): Promise<string | null> {
  return SecureStore.getItemAsync(KEYS.refreshToken);
}

export async function storeTokens(access: string, refresh: string): Promise<void> {
  await Promise.all([
    SecureStore.setItemAsync(KEYS.accessToken, access),
    SecureStore.setItemAsync(KEYS.refreshToken, refresh),
  ]);
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(KEYS.accessToken),
    SecureStore.deleteItemAsync(KEYS.refreshToken),
  ]);
}
