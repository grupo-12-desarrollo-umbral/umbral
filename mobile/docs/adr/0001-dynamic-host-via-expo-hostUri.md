# Dynamic backend host derived from Expo's hostUri

The app needs to reach the backend API and Keycloak from a physical device on arbitrary networks (dev laptops at home, university WiFi during demos). Hardcoding a LAN IP in `.env` breaks on every network change. We derive the host at runtime from `Constants.expoConfig.hostUri` — the address Metro already broadcasts — stripping its port and substituting the backend ports (8000 / 8080). This makes the app network-agnostic in development with zero config. In standalone/production builds `hostUri` is absent and the env vars act as the fallback, so the two modes don't interfere.

## Considered Options

- **Hardcoded IP in `.env`** — requires updating `.env` on every network change; breaks silently on devices.
- **Custom dev-server script that patches `.env`** — fragile, adds tooling overhead.
- **`Constants.expoConfig.hostUri` (chosen)** — available in Expo Go and dev-client builds; zero config; same mechanism Expo itself uses internally for asset loading.

## Consequences

Keycloak must be reachable on the same host as Metro. If backend services run on a different machine than the dev laptop, the fallback env vars must be set explicitly.
