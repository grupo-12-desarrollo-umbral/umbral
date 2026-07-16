import { useEffect, useRef, useState } from 'react';
import { getTriviaTeamQuestionResult } from '@/lib/api/sessions';
import {
  isTerminalSessionState,
  toActiveQuestion,
  type ActiveQuestion,
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
  // Derived from the view kind so it is always consistent (reveal ≡ closed).
  const isQuestionClosed = view.kind === 'reveal';

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
  // Track the current active question so we can carry it into reveal without a stale closure.
  const currentQuestionRef = useRef<ActiveQuestion | null>(
    snapshotActiveQuestion ? toActiveQuestion(snapshotActiveQuestion) : null,
  );
  // Track the current view kind so the reconcile effect can read it without a stale closure.
  const viewKindRef = useRef<ActiveQuestionView['kind']>(view.kind);

  // Sync refs whenever the view changes.
  useEffect(() => {
    viewKindRef.current = view.kind;
    if (view.kind === 'active') {
      currentQuestionRef.current = view.question;
    }
  }, [view]);

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
      setView({ kind: 'closed' });
      return;
    }

    isTerminalRef.current = false;

    if (snapshotActiveQuestion) {
      // If we're currently revealing this exact question, the snapshot echo is expected — stay in reveal.
      const isRevealingThisQuestion =
        viewKindRef.current === 'reveal' &&
        currentQuestionRef.current?.questionIndex === snapshotActiveQuestion.questionIndex;
      if (
        closeLanded &&
        closedQuestionIndexRef.current === snapshotActiveQuestion.questionIndex &&
        isRevealingThisQuestion
      ) {
        closedQuestionIndexRef.current = null;
        return;
      }
      // Echo guard: a landed re-fetch that still reports the just-closed question must not re-open
      // it — hold the close and fall to waiting rather than unlocking a dead question.
      if (
        closeLanded &&
        closedQuestionIndexRef.current === snapshotActiveQuestion.questionIndex
      ) {
        displayedQuestionIndexRef.current = null;
        closedQuestionIndexRef.current = null;
        setView({ kind: 'waiting' });
        return;
      }
      hasSeenQuestionRef.current = true;
      displayedQuestionIndexRef.current = snapshotActiveQuestion.questionIndex;
      closedQuestionIndexRef.current = null;
      setView({ kind: 'active', question: toActiveQuestion(snapshotActiveQuestion) });
      return;
    }

    // Reveal window (HU-35): while a just-closed question is showing its result, the backend holds
    // the next activation for the reveal duration, so a snapshot re-fetch reports no active question.
    // Stay in reveal — the transition out is driven by the delayed `QuestionActivated` /
    // `SubstageAdvanced` push, not by this snapshot. (A terminal state was already handled above.)
    if (viewKindRef.current === 'reveal') {
      return;
    }

    // No active question in the snapshot. A landed close reconcile with no next question on a live
    // session settles to waiting; never strand on a stale interactive question.
    if (closeLanded) {
      displayedQuestionIndexRef.current = null;
      closedQuestionIndexRef.current = null;
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
      setView({ kind: 'active', question: toActiveQuestion(notification) });
    });

    const unsubscribeQuestionClosed = client.onQuestionClosed(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (isTerminalRef.current) return;
      const displayedIndex = displayedQuestionIndexRef.current;
      // Index-guarded: ignore a stale close for a superseded question, and any close with no
      // question on screen (keeps the existing none/waiting behavior).
      if (displayedIndex === null || notification.questionIndex !== displayedIndex) return;
      // Lock into reveal: keep the question visible, show the correct option + explanation from the
      // push, and fire the team-result read (A-2). Controls stay locked until the next reconcile.
      const question = currentQuestionRef.current;
      if (!question) return;
      closedQuestionIndexRef.current = displayedIndex;
      viewKindRef.current = 'reveal';
      setView({
        kind: 'reveal',
        question,
        correctOptionSequenceOrder: notification.correctOptionSequenceOrder,
        explanation: notification.explanation,
        teamResult: null,
      });
      // Fire the my-result GET (A-2). Silently ignored if it resolves after we've left reveal.
      getTriviaTeamQuestionResult(liveSessionId, question.sequenceOrder)
        .then(result => {
          setView(prev => {
            if (prev.kind !== 'reveal') return prev;
            return { ...prev, teamResult: result };
          });
        })
        .catch(() => {
          // Silently fail — the question-level reveal (correct option + explanation) is already
          // showing from the push; the team result is additive.
        });
      requestResync?.();
    });

    const unsubscribeSubstageAdvanced = client.onSubstageAdvanced(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      if (notification.toSubstageId === null) {
        isTerminalRef.current = true;
        displayedQuestionIndexRef.current = null;
        closedQuestionIndexRef.current = null;
        setView({ kind: 'closed' });
        return;
      }
      if (isTerminalRef.current) return;
      displayedQuestionIndexRef.current = null;
      closedQuestionIndexRef.current = null;
      setView(hasSeenQuestionRef.current ? { kind: 'waiting' } : { kind: 'none' });
    });

    const unsubscribeStateChanged = client.onStateChanged(notification => {
      if (notification.liveSessionId !== liveSessionId) return;
      setSessionState(notification.currentState);
      if (isTerminalSessionState(notification.currentState)) {
        isTerminalRef.current = true;
        displayedQuestionIndexRef.current = null;
        closedQuestionIndexRef.current = null;
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
