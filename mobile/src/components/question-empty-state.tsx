import { View } from 'react-native';
import { colors, spacing } from '@/constants/theme';
import { Panel } from '@/components/ui/panel';
import { Text } from '@/components/ui/text';
import type { ActiveQuestionView } from '@/lib/realtime/active-question-types';

type EmptyKind = Exclude<ActiveQuestionView['kind'], 'active'>;

const EMPTY_COPY: Record<EmptyKind, { title: string; body: string }> = {
  waiting: {
    title: 'Waiting for the next question...',
    body: "The operator hasn't activated the next one.",
  },
  none: {
    title: 'No active question yet.',
    body: 'Sit tight - the host will start the round.',
  },
  reveal: {
    title: 'Question closed.',
    body: 'Results are being revealed.',
  },
  closed: {
    title: 'Session closed.',
    body: 'This session has ended. Thanks for playing.',
  },
};

function closedBody(sessionState: string): string {
  if (sessionState === 'Cancelled') return 'This session was cancelled by the host.';
  return 'This session has ended. Thanks for playing.';
}

export function QuestionEmptyState({ kind, sessionState }: { kind: EmptyKind; sessionState: string }) {
  const copy = kind === 'closed'
    ? { ...EMPTY_COPY.closed, body: closedBody(sessionState) }
    : EMPTY_COPY[kind];

  return (
    <View style={{ padding: spacing.lg, backgroundColor: colors.ivoryFog }}>
      <Panel style={{ alignItems: 'center', gap: spacing.sm }}>
        <Text
          variant="headline"
          style={{ color: kind === 'closed' ? colors.signalCritical : colors.textInk, textAlign: 'center' }}
        >
          {copy.title}
        </Text>
        <Text muted style={{ textAlign: 'center' }}>
          {copy.body}
        </Text>
      </Panel>
    </View>
  );
}
