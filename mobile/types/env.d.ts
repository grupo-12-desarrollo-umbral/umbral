declare namespace NodeJS {
  interface ProcessEnv {
    EXPO_PUBLIC_API_BASE_URL: string;
    EXPO_PUBLIC_KEYCLOAK_URL: string;
    EXPO_PUBLIC_KEYCLOAK_REALM: string;
    EXPO_PUBLIC_KEYCLOAK_CLIENT_ID: string;
  }
}
