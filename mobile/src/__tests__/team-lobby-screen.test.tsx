import React from 'react';
import { act, create } from 'react-test-renderer';
import TeamLobbyScreen from '@/app/(app)/team-lobby';
import { useTeamJoin } from '@/lib/membership/use-team-join';
import { useTeamLobby } from '@/lib/membership/use-team-lobby';
import { saveReconnectContext } from '@/lib/realtime/reconnect-context';

const mockReplace = jest.fn();
const mockBack = jest.fn();
const mockLoad = jest.fn();
const mockJoin = jest.fn();
const mockResetJoin = jest.fn();

jest.mock('expo-router', () => ({
  useLocalSearchParams: jest.fn(() => ({ sessionCode: 'SMOKE1' })),
  useRouter: jest.fn(() => ({ replace: mockReplace, back: mockBack })),
}));

jest.mock('expo-haptics', () => ({
  notificationAsync: jest.fn(),
  NotificationFeedbackType: { Success: 'success', Error: 'error' },
}));

jest.mock('@/lib/auth/use-auth', () => ({
  useAuth: jest.fn(() => ({
    profile: { displayName: 'Nova' },
    signOut: jest.fn(),
  })),
}));

jest.mock('@/lib/membership/use-team-join', () => ({
  useTeamJoin: jest.fn(),
}));

jest.mock('@/lib/membership/use-team-lobby', () => ({
  useTeamLobby: jest.fn(),
}));

jest.mock('@/lib/realtime/reconnect-context', () => ({
  saveReconnectContext: jest.fn(() => Promise.resolve()),
}));

const mockUseTeamJoin = useTeamJoin as jest.MockedFunction<typeof useTeamJoin>;
const mockUseTeamLobby = useTeamLobby as jest.MockedFunction<typeof useTeamLobby>;
const mockSaveReconnectContext = saveReconnectContext as jest.MockedFunction<typeof saveReconnectContext>;

const FOXES = {
  teamId: 'runtime-team-id',
  referenceTeamId: 'reference-team-id',
  displayName: 'Lantern Foxes',
  joinState: 'joinable' as const,
};
const OWLS = {
  teamId: 'runtime-team-id-b',
  referenceTeamId: 'reference-team-id-b',
  displayName: 'Ember Owls',
  joinState: 'joinable' as const,
};

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
}

// The team card's onPress handler, resolved by the visible team name.
function pressHandlerFor(renderer: ReturnType<typeof create>, name: string) {
  const teamName = renderer.root.findByProps({ children: name });
  let card = teamName.parent;
  while (card && typeof card.props.onPress !== 'function') {
    card = card.parent;
  }
  if (!card) throw new Error(`no pressable card for ${name}`);
  return card.props.onPress as () => void;
}

async function render() {
  let renderer: ReturnType<typeof create> | null = null;
  await act(async () => {
    renderer = create(<TeamLobbyScreen />);
  });
  return renderer!;
}

describe('TeamLobbyScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUseTeamLobby.mockReturnValue({
      status: 'resolved',
      liveSessionId: 'live-session-id',
      teams: [FOXES],
      failure: null,
      errorMessage: null,
      load: mockLoad,
    });
    mockJoin.mockResolvedValue({
      kind: 'joined',
      response: { teamMembershipId: 'runtime-membership-id' },
    });
    mockUseTeamJoin.mockReturnValue({
      status: 'idle',
      outcome: null,
      join: mockJoin,
      reset: mockResetJoin,
    });
  });

  test('successful join persists once, fires one success path, and navigates once', async () => {
    const renderer = await render();

    await act(async () => {
      await pressHandlerFor(renderer, 'Lantern Foxes')();
    });

    expect(mockJoin).toHaveBeenCalledTimes(1);
    expect(mockJoin).toHaveBeenCalledWith('SMOKE1', 'runtime-team-id');
    expect(mockSaveReconnectContext).toHaveBeenCalledTimes(1);
    expect(mockSaveReconnectContext).toHaveBeenCalledWith({
      liveSessionId: 'live-session-id',
      teamId: 'reference-team-id',
      displayName: 'Nova',
      token: null,
    });
    expect(mockReplace).toHaveBeenCalledTimes(1);
    expect(mockReplace).toHaveBeenCalledWith({
      pathname: '/(app)/team-space',
      params: {
        liveSessionId: 'live-session-id',
        teamId: 'reference-team-id',
      },
    });
    // The lobby never issues a pre-join reset that could clear an active hook attempt.
    expect(mockResetJoin).not.toHaveBeenCalled();
  });

  test('the same team pressed twice before rerender sends one POST', async () => {
    mockJoin.mockReturnValue(deferred().promise); // held in-flight so ownership persists
    const renderer = await render();

    const press = pressHandlerFor(renderer, 'Lantern Foxes');
    act(() => {
      press();
      press();
    });

    expect(mockJoin).toHaveBeenCalledTimes(1);
    expect(mockJoin).toHaveBeenCalledWith('SMOKE1', 'runtime-team-id');
  });

  test('team A then team B before rerender sends only A, and B cannot persist its own context', async () => {
    mockUseTeamLobby.mockReturnValue({
      status: 'resolved',
      liveSessionId: 'live-session-id',
      teams: [FOXES, OWLS],
      failure: null,
      errorMessage: null,
      load: mockLoad,
    });
    const a = deferred<{ kind: 'joined'; response: { teamMembershipId: string } }>();
    mockJoin.mockReturnValue(a.promise);

    const renderer = await render();
    const pressA = pressHandlerFor(renderer, 'Lantern Foxes');
    const pressB = pressHandlerFor(renderer, 'Ember Owls');

    act(() => {
      pressA();
      pressB();
    });

    // Only A owns the selection: a single POST for A's runtime id, none for B.
    expect(mockJoin).toHaveBeenCalledTimes(1);
    expect(mockJoin).toHaveBeenCalledWith('SMOKE1', 'runtime-team-id');

    await act(async () => {
      a.resolve({ kind: 'joined', response: { teamMembershipId: 'membership-a' } });
      await a.promise;
    });

    // Reconnect context and navigation name A's reference id — B's tap wrote nothing.
    expect(mockSaveReconnectContext).toHaveBeenCalledTimes(1);
    expect(mockSaveReconnectContext).toHaveBeenCalledWith(
      expect.objectContaining({ teamId: 'reference-team-id' }),
    );
    expect(mockReplace).toHaveBeenCalledTimes(1);
    expect(mockReplace).toHaveBeenCalledWith(
      expect.objectContaining({
        params: expect.objectContaining({ teamId: 'reference-team-id' }),
      }),
    );
  });

  test('a failed join releases the lobby guard so a later tap sends a new request', async () => {
    mockJoin.mockResolvedValueOnce({ kind: 'failed', message: 'boom' });
    const renderer = await render();

    const press = pressHandlerFor(renderer, 'Lantern Foxes');
    await act(async () => {
      await press();
    });
    expect(mockJoin).toHaveBeenCalledTimes(1);
    expect(mockReplace).not.toHaveBeenCalled();

    // The guard was released on the failed terminal result: a later tap starts a fresh attempt.
    mockJoin.mockResolvedValueOnce({
      kind: 'joined',
      response: { teamMembershipId: 'membership-retry' },
    });
    await act(async () => {
      await press();
    });
    expect(mockJoin).toHaveBeenCalledTimes(2);
    expect(mockReplace).toHaveBeenCalledTimes(1);
  });

  test('a late completion from an unmounted screen neither persists nor navigates', async () => {
    const held = deferred<{ kind: 'joined'; response: { teamMembershipId: string } }>();
    mockJoin.mockReturnValue(held.promise);

    const renderer = await render();
    act(() => {
      pressHandlerFor(renderer, 'Lantern Foxes')();
    });
    expect(mockJoin).toHaveBeenCalledTimes(1);

    act(() => {
      renderer.unmount();
    });

    await act(async () => {
      held.resolve({ kind: 'joined', response: { teamMembershipId: 'membership-late' } });
      await held.promise;
    });

    expect(mockSaveReconnectContext).not.toHaveBeenCalled();
    expect(mockReplace).not.toHaveBeenCalled();
  });
});
