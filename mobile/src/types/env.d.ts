declare global {
  namespace NodeJS {
    interface ProcessEnv {
      readonly EXPO_PUBLIC_API_BASE_URL: string;
      readonly EXPO_PUBLIC_KEYCLOAK_URL: string;
      readonly EXPO_PUBLIC_KEYCLOAK_REALM: string;
      readonly EXPO_PUBLIC_KEYCLOAK_CLIENT_ID: string;
    }
  }
}

export {};
