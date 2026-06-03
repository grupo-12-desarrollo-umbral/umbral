import {
  clearReconnectContext,
  loadReconnectContext,
  saveReconnectContext,
} from '@/lib/realtime/reconnect-context';

const mockSecureStore = new Map<string, string>();

// `jest.mock` is hoisted above the import by babel-jest; the `mock`-prefixed
// store is allowed inside the factory under jest's hoisting rules.
jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async (key: string) => mockSecureStore.get(key) ?? null),
  setItemAsync: jest.fn(async (key: string, value: string) => {
    mockSecureStore.set(key, value);
  }),
  deleteItemAsync: jest.fn(async (key: string) => {
    mockSecureStore.delete(key);
  }),
}));

describe('reconnect context store', () => {
  beforeEach(() => {
    mockSecureStore.clear();
  });

  test('save -> load -> clear round-trip', async () => {
    const context = {
      liveSessionId: 'session-1',
      teamId: 'team-1',
      displayName: 'Nova',
      token: null,
    };

    await saveReconnectContext(context);
    await expect(loadReconnectContext()).resolves.toEqual(context);

    await clearReconnectContext();
    await expect(loadReconnectContext()).resolves.toBeNull();
  });
});
