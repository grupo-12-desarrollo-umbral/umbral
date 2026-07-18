import { View } from 'react-native';
import { colors, spacing } from '@/constants/theme';
import { Panel } from '@/components/ui/panel';
import { Text } from '@/components/ui/text';
import type { ActiveQuestionView } from '@/lib/realtime/active-question-types';

type EmptyKind = Exclude<ActiveQuestionView['kind'], 'active'>;

const EMPTY_COPY: Record<EmptyKind, { title: string; body: string }> = {
  waiting: {
    title: 'Esperando la siguiente pregunta...',
    body: 'El operador aún no ha activado la siguiente.',
  },
  none: {
    title: 'Aún no hay pregunta activa.',
    body: 'Espera un momento - el anfitrión iniciará la ronda.',
  },
  reveal: {
    title: 'Pregunta cerrada.',
    body: 'Se están revelando los resultados.',
  },
  closed: {
    title: 'Sesión cerrada.',
    body: 'Esta sesión ha terminado. Gracias por jugar.',
  },
};

function closedBody(sessionState: string): string {
  if (sessionState === 'Cancelled') return 'Esta sesión fue cancelada por el anfitrión.';
  return 'Esta sesión ha terminado. Gracias por jugar.';
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
