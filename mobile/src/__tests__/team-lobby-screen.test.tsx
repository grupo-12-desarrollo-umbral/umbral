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

describe('TeamLobbyScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUseTeamLobby.mockReturnValue({
      status: 'resolved',
      liveSessionId: 'live-session-id',
      teams: [
        {
          teamId: 'runtime-team-id',
          referenceTeamId: 'reference-team-id',
          displayName: 'Lantern Foxes',
          joinState: 'joinable',
        },
      ],
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

  test('successful join persists and navigates with the reference team id', async () => {
    let renderer: ReturnType<typeof create> | null = null;
    await act(async () => {
      renderer = create(<TeamLobbyScreen />);
    });

    const teamName = renderer!.root.findByProps({ children: 'Lantern Foxes' });
    let teamCard = teamName.parent;
    while (teamCard && typeof teamCard.props.onPress !== 'function') {
      teamCard = teamCard.parent;
    }
    expect(teamCard).not.toBeNull();
    await act(async () => {
      await teamCard!.props.onPress();
    });

    expect(mockJoin).toHaveBeenCalledWith('SMOKE1', 'runtime-team-id');
    expect(mockSaveReconnectContext).toHaveBeenCalledWith({
      liveSessionId: 'live-session-id',
      teamId: 'reference-team-id',
      displayName: 'Nova',
      token: null,
    });
    expect(mockReplace).toHaveBeenCalledWith({
      pathname: '/(app)/team-space',
      params: {
        liveSessionId: 'live-session-id',
        teamId: 'reference-team-id',
      },
    });
  });
});
