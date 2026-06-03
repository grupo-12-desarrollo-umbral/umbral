import Constants from 'expo-constants';

// Derive the dev machine's LAN IP so the app can reach backend services on any
// network without a hardcoded address. Two paths cover both manifest formats:
//
//  1. expoConfig.hostUri — present in new Expo Go / dev-client ("192.168.x.x:8081")
//  2. experienceUrl      — always set in Expo Go ("exp://192.168.x.x:port"),
//                          used as fallback when path 1 is absent
function devHost(): string | null {
  const fromConfig = Constants.expoConfig?.hostUri;
  if (fromConfig) return fromConfig.split(':')[0] ?? null;

  try {
    const { hostname } = new URL(Constants.experienceUrl);
    // Only trust bare IPv4 LAN addresses, not tunnels or localhost
    if (hostname && /^\d+\.\d+\.\d+\.\d+$/.test(hostname) && hostname !== '127.0.0.1') {
      return hostname;
    }
  } catch { /* ignore */ }

  return null;
}

export function apiBaseUrl(): string {
  const host = devHost();
  if (host) return `http://${host}:8000`;
  return process.env.EXPO_PUBLIC_API_BASE_URL ?? '';
}

export function keycloakBaseUrl(): string {
  const host = devHost();
  if (host) return `http://${host}:8080`;
  return process.env.EXPO_PUBLIC_KEYCLOAK_URL ?? '';
}
