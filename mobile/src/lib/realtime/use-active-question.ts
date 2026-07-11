import { useEffect, useRef, useState } from 'react';
import {
  isTerminalSessionState,
  toActiveQuestion,
  type ActiveQuestionView,
} from './active-question-types';
import type { SessionsHubClient } from './sessions-hub';
import type { ActiveQuestionSnapshotDto } from './trivia-types';

export type UseActiveQuestionResult = {
  view: ActiveQuestionView;
  sessionState: string;
};

function viewFromSnapshot(
  activeQuestion: ActiveQuestionSnapshotDto | null | undefined,
  sessionState: string,
): ActiveQuestionView {
  if (isTerminalSessionState(sessionState)) return { kind: 'closed' };
  if (activeQuestion) return { kind: 'active', question: toActiveQuestion(activeQuestion) };
  return { kind: 'none' };
}

export function useActiveQuestion({
  client,
  liveSessionId,
  isReconnected,
  reconnectNonce,
  snapshotActiveQuestion,
  snapshotSessionState,
}: {
  client: SessionsHubClient;
  liveSessionId: string;
  isReconnected: boolean;
  reconnectNonce: number;
  snapshotActiveQuestion?: ActiveQuestionSnapshotDto | null;
  snapshotSessionState: string;
}): UseActiveQuestionResult {
  const [view, setView] = useState<ActiveQuestionView>(() =>
    viewFromSnapshot(snapshotActiveQuestion, snapshotSessionState),
  );
  const [sessionState, setSessionState] = useState(snapshotSessionState);
  const hasSeenQuestionRef = useRef(Boolean(snapshotActiveQuestion));
  const isTerminalRef = useRef(isTerminalSessionState(snapshotSessionState));
  const lastSeedNonceRef = useRef<number | null>(null);

  useEffect(() => {
    if (!isReconnected) return;

    // Snapshot re-seeding is intentionally driven by reconnect recovery, not by a user action.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSessionState(snapshotSessionState);
    const nonceChanged = lastSeedNonceRef.current !== reconnectNonce;
    const terminal = isTerminalSessionState(snapshotSessionState);
    lastSeedNonceRef.current = reconnectNonce;

    if (terminal) {
      isTerminalRef.current = true;
      setView({ kind: 'closed' });
      return;
    }

    isTerminalRef.current = false;

    if (snapshotActiveQuestion) {
      hasSeenQuestionRef.current = true;
      setView({ kind: 'active', question: toActiveQuestion(snapshotActiveQuestion) });
      return;
    }

    if (nonceChanged) {
      setView({ kind: 'none' });
    }
  }, [isReconnected, reconnectNonce, snapshotActiveQuestion, snapshotSessionState]);

  useEffect(() => {
    const unsubscribeQuestionActivated = client.onQuestionActivated(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (isTerminalRef.current) return;
      hasSeenQuestionRef.current = true;
      setView({ kind: 'active', question: toActiveQuestion(notification) });
    });

    const unsubscribeQuestionClosed = client.onQuestionClosed(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (isTerminalRef.current) return;
      setView(hasSeenQuestionRef.current ? { kind: 'waiting' } : { kind: 'none' });
    });

    const unsubscribeSubstageAdvanced = client.onSubstageAdvanced(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (notification.toSubstageId === null) {
        isTerminalRef.current = true;
        setView({ kind: 'closed' });
        return;
      }
      if (isTerminalRef.current) return;
      setView(hasSeenQuestionRef.current ? { kind: 'waiting' } : { kind: 'none' });
    });

    const unsubscribeStateChanged = client.onStateChanged(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      setSessionState(notification.currentState);
      if (isTerminalSessionState(notification.currentState)) {
        isTerminalRef.current = true;
        setView({ kind: 'closed' });
      }
    });

    return () => {
      unsubscribeQuestionActivated();
      unsubscribeQuestionClosed();
      unsubscribeSubstageAdvanced();
      unsubscribeStateChanged();
    };
  }, [client, liveSessionId]);

  return { view, sessionState };
}
