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
  // Display-only lock: the shown question was closed and is settling until the re-sync reconciles.
  isQuestionClosed: boolean;
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
  snapshotVersion = 0,
  requestResync,
  snapshotActiveQuestion,
  snapshotSessionState,
}: {
  client: SessionsHubClient;
  liveSessionId: string;
  isReconnected: boolean;
  reconnectNonce: number;
  // Fetch-completion tick from `useSessionTimer`. A close reconcile keys on this so it acts on the
  // landed re-fetch, not the stale snapshot present when the close triggered the re-sync.
  snapshotVersion?: number;
  // Triggers that re-fetch; called when a close for the displayed question locks it.
  requestResync?: () => void;
  snapshotActiveQuestion?: ActiveQuestionSnapshotDto | null;
  snapshotSessionState: string;
}): UseActiveQuestionResult {
  const [view, setView] = useState<ActiveQuestionView>(() =>
    viewFromSnapshot(snapshotActiveQuestion, snapshotSessionState),
  );
  const [sessionState, setSessionState] = useState(snapshotSessionState);
  const [isQuestionClosed, setIsQuestionClosed] = useState(false);
  const hasSeenQuestionRef = useRef(Boolean(snapshotActiveQuestion));
  const isTerminalRef = useRef(isTerminalSessionState(snapshotSessionState));
  const lastReconnectNonceRef = useRef(reconnectNonce);
  const lastSnapshotVersionRef = useRef(snapshotVersion);
  // Index of the active question currently on screen (null when no question is displayed).
  const displayedQuestionIndexRef = useRef<number | null>(
    snapshotActiveQuestion ? snapshotActiveQuestion.questionIndex : null,
  );
  // Index of the just-closed question while a close reconcile is pending (null otherwise).
  const closedQuestionIndexRef = useRef<number | null>(null);

  useEffect(() => {
    if (!isReconnected) return;

    // Snapshot re-seeding is driven by reconnect recovery / close reconcile, not by a user action.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSessionState(snapshotSessionState);
    const reconnectChanged = lastReconnectNonceRef.current !== reconnectNonce;
    const versionChanged = lastSnapshotVersionRef.current !== snapshotVersion;
    lastReconnectNonceRef.current = reconnectNonce;
    lastSnapshotVersionRef.current = snapshotVersion;
    const terminal = isTerminalSessionState(snapshotSessionState);
    // A close reconcile only resolves once the re-fetch has landed (a fresh snapshot version).
    const closeLanded = closedQuestionIndexRef.current !== null && versionChanged;

    if (terminal) {
      isTerminalRef.current = true;
      displayedQuestionIndexRef.current = null;
      closedQuestionIndexRef.current = null;
      setIsQuestionClosed(false);
      setView({ kind: 'closed' });
      return;
    }

    isTerminalRef.current = false;

    if (snapshotActiveQuestion) {
      // Echo guard: a landed re-fetch that still reports the just-closed question must not re-open
      // it — hold the close and fall to waiting rather than unlocking a dead question.
      if (closeLanded && closedQuestionIndexRef.current === snapshotActiveQuestion.questionIndex) {
        displayedQuestionIndexRef.current = null;
        closedQuestionIndexRef.current = null;
        setIsQuestionClosed(false);
        setView({ kind: 'waiting' });
        return;
      }
      hasSeenQuestionRef.current = true;
      displayedQuestionIndexRef.current = snapshotActiveQuestion.questionIndex;
      closedQuestionIndexRef.current = null;
      setIsQuestionClosed(false);
      setView({ kind: 'active', question: toActiveQuestion(snapshotActiveQuestion) });
      return;
    }

    // No active question in the snapshot. A landed close reconcile with no next question on a live
    // session settles to waiting; never strand on a stale interactive question.
    if (closeLanded) {
      displayedQuestionIndexRef.current = null;
      closedQuestionIndexRef.current = null;
      setIsQuestionClosed(false);
      setView({ kind: 'waiting' });
      return;
    }

    if (reconnectChanged) {
      displayedQuestionIndexRef.current = null;
      setView({ kind: 'none' });
    }
  }, [isReconnected, reconnectNonce, snapshotVersion, snapshotActiveQuestion, snapshotSessionState]);

  useEffect(() => {
    const unsubscribeQuestionActivated = client.onQuestionActivated(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (isTerminalRef.current) return;
      // Broadcast fast-path to the next question clears any prior close lock.
      hasSeenQuestionRef.current = true;
      displayedQuestionIndexRef.current = notification.questionIndex;
      closedQuestionIndexRef.current = null;
      setIsQuestionClosed(false);
      setView({ kind: 'active', question: toActiveQuestion(notification) });
    });

    const unsubscribeQuestionClosed = client.onQuestionClosed(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (isTerminalRef.current) return;
      const displayedIndex = displayedQuestionIndexRef.current;
      // Index-guarded: ignore a stale close for a superseded question, and any close with no
      // question on screen (keeps the existing none/waiting behavior).
      if (displayedIndex === null || notification.questionIndex !== displayedIndex) return;
      // Lock, don't blank: keep the question visible, flag it closed, and re-fetch to reconcile.
      closedQuestionIndexRef.current = displayedIndex;
      setIsQuestionClosed(true);
      requestResync?.();
    });

    const unsubscribeSubstageAdvanced = client.onSubstageAdvanced(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (notification.toSubstageId === null) {
        isTerminalRef.current = true;
        displayedQuestionIndexRef.current = null;
        closedQuestionIndexRef.current = null;
        setIsQuestionClosed(false);
        setView({ kind: 'closed' });
        return;
      }
      if (isTerminalRef.current) return;
      displayedQuestionIndexRef.current = null;
      closedQuestionIndexRef.current = null;
      setIsQuestionClosed(false);
      setView(hasSeenQuestionRef.current ? { kind: 'waiting' } : { kind: 'none' });
    });

    const unsubscribeStateChanged = client.onStateChanged(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      setSessionState(notification.currentState);
      if (isTerminalSessionState(notification.currentState)) {
        isTerminalRef.current = true;
        displayedQuestionIndexRef.current = null;
        closedQuestionIndexRef.current = null;
        setIsQuestionClosed(false);
        setView({ kind: 'closed' });
      }
    });

    return () => {
      unsubscribeQuestionActivated();
      unsubscribeQuestionClosed();
      unsubscribeSubstageAdvanced();
      unsubscribeStateChanged();
    };
  }, [client, liveSessionId, requestResync]);

  return { view, sessionState, isQuestionClosed };
}
