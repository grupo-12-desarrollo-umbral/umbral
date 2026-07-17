import { useEffect, useRef, useState } from 'react';
import type { SessionsHubClient } from './sessions-hub';
import type {
  RevealSnapshotReconciliation,
  SubstageRankingRevealStartedNotificationDto,
} from './ranking-reveal-types';
import type { SubstageAdvancedNotificationDto } from './trivia-types';
import type { SessionStateChangedNotificationDto } from './sessions-hub-types';

export type UseRankingRevealResult = {
  // True while the substage ranking should hold the whole screen.
  isRevealing: boolean;
};

// The reveal on screen right now: its substage (to reject stale advances for another one) and whether
// it is terminal (the mission finishes on it rather than advancing off it).
type ActiveReveal = {
  substageSnapshotId: string;
  isTerminal: boolean;
};

// Equal-time precedence: a closing transition (a matching real advance, or a cancellation) wins over an
// opening one (a snapshot or a start push) when their server times tie. So a start and its advance that
// carry the same instant resolve deterministically to closed.
const OPENING = 0;
const CLOSING = 1;

// The last transition applied to the reveal, keyed by the server time and precedence that ordered it. A
// new transition wins only when it is strictly later, or equal in time but higher precedence.
type AppliedTransition = {
  state: ActiveReveal | null;
  serverTime: number;
  precedence: number;
};

const INITIAL_TRANSITION: AppliedTransition = {
  state: null,
  serverTime: Number.NEGATIVE_INFINITY,
  precedence: OPENING,
};

/**
 * Substage ranking reveal (D-3) as a single ordered transition model. The backend is the sole authority
 * for opening, closing and restoring the reveal; this hook never runs a local duration timer. Every
 * input becomes an ordered transition, stamped with an existing server timestamp, and the newest one
 * wins — so network response order can never reverse authoritative reveal state:
 *
 * - Session snapshot (active or null): ordered by `observedAt`. Seeds initial load and reconnect.
 * - `SubstageRankingRevealStarted`: ordered by `emittedAt`. Opens the reveal (the low-latency cue; the
 *   ranking itself is held live by `useRanking`).
 * - `SubstageAdvanced` into a real next substage: ordered by `advancedAt`. Closes the reveal, but only
 *   when it leaves the substage that is actually revealing. The terminal finish carries a null
 *   `toSubstageId` and is deliberately NOT a release — `Finished` keeps the same ranking as the final
 *   screen (a deadline finish emits no advance at all, so the state itself has to hold it).
 * - `Cancelled`: ordered by `changedAt`. Drops the reveal so the cancellation surface shows through.
 * - `Paused`/`Active` make no transition: the reveal is an absolute backend deadline, held across the
 *   whole pause and released only by the later advancement.
 *
 * Stale events (another session, an advance off a substage that is not revealing, or any transition
 * older than the one already applied) are ignored.
 */
export function useRankingReveal({
  client,
  liveSessionId,
  reconciliation,
}: {
  client: SessionsHubClient;
  liveSessionId: string;
  // The reveal + server time carried by the latest successful snapshot, versioned so each is applied
  // once. Null/undefined until the first snapshot lands.
  reconciliation?: RevealSnapshotReconciliation | null;
}): UseRankingRevealResult {
  const [activeReveal, setActiveReveal] = useState<ActiveReveal | null>(null);
  const lastAppliedRef = useRef<AppliedTransition>(INITIAL_TRANSITION);

  // Apply a transition only when it is newer than the last applied one (equal time breaks toward the
  // higher precedence). Refs, not state, own the ordering baseline so it is read synchronously in event
  // callbacks without a render-time stale closure.
  function applyTransition(next: {
    state: ActiveReveal | null;
    serverTime: number;
    precedence: number;
  }): void {
    const last = lastAppliedRef.current;
    const usable = !Number.isNaN(next.serverTime);
    const isNewer = usable
      ? next.serverTime > last.serverTime ||
        (next.serverTime === last.serverTime && next.precedence > last.precedence)
      : // An unparseable timestamp cannot be ordered; fall back to applying it (liveness over strict
        // ordering) without advancing the baseline, so later well-formed events still order correctly.
        true;
    if (!isNewer) return;
    lastAppliedRef.current = {
      state: next.state,
      serverTime: usable ? next.serverTime : last.serverTime,
      precedence: usable ? next.precedence : last.precedence,
    };
    setActiveReveal(next.state);
  }

  const lastAppliedVersionRef = useRef<number | null>(null);
  const sessionRef = useRef(liveSessionId);

  // A new live session cannot inherit the previous one's reconciliation — reset the baseline so a reveal
  // from the prior session can never leak into the next one. Declared before the seed effect so, on the
  // commit that changes `liveSessionId`, the reset runs before a same-commit snapshot is applied. Guarded
  // to a genuine change so the initial mount does no redundant clear.
  useEffect(() => {
    if (sessionRef.current === liveSessionId) return;
    sessionRef.current = liveSessionId;
    lastAppliedRef.current = INITIAL_TRANSITION;
    lastAppliedVersionRef.current = null;
    setActiveReveal(null);
  }, [liveSessionId]);

  // Apply each successful snapshot once (keyed by version), ordered by its `observedAt`. A failed fetch
  // never bumps the version, so it can neither reopen nor reseed a reveal the live events have moved past.
  useEffect(() => {
    if (!reconciliation) return;
    if (lastAppliedVersionRef.current === reconciliation.version) return;
    lastAppliedVersionRef.current = reconciliation.version;
    applyTransition({
      state: reconciliation.reveal
        ? {
            substageSnapshotId: reconciliation.reveal.substageSnapshotId,
            isTerminal: reconciliation.reveal.isTerminal,
          }
        : null,
      serverTime: Date.parse(reconciliation.observedAt),
      precedence: OPENING,
    });
  }, [reconciliation]);

  useEffect(() => {
    const unsubscribeStarted = client.onSubstageRankingRevealStarted(
      (notification: SubstageRankingRevealStartedNotificationDto) => {
        if (notification.liveSessionId !== liveSessionId) return;
        applyTransition({
          state: {
            substageSnapshotId: notification.substageSnapshotId,
            isTerminal: notification.isTerminal,
          },
          serverTime: Date.parse(notification.emittedAt),
          precedence: OPENING,
        });
      },
    );

    const unsubscribeAdvanced = client.onSubstageAdvanced(
      (notification: SubstageAdvancedNotificationDto) => {
        if (notification.liveSessionId !== liveSessionId) return;
        // The terminal finish carries a null toSubstageId and must keep holding (Finished renders the
        // ranking as the final screen); only a real advance into a next substage releases.
        if (notification.toSubstageId === null) return;
        // When a reveal is on screen, only the advance that leaves *that* substage may close it, so a
        // stale/duplicate advance off some other substage never drops a fresh reveal. When no reveal is
        // applied yet — its snapshot response may still be in flight — there is no revealing substage to
        // match against; record the closing transition anyway (ordered by `advancedAt`) so a later, older
        // snapshot cannot reopen a reveal this advance has already moved the mission past.
        const current = lastAppliedRef.current.state;
        if (current !== null && notification.fromSubstageId !== current.substageSnapshotId) return;
        applyTransition({
          state: null,
          serverTime: Date.parse(notification.advancedAt),
          precedence: CLOSING,
        });
      },
    );

    const unsubscribeStateChanged = client.onStateChanged(
      (notification: SessionStateChangedNotificationDto) => {
        if (notification.liveSessionId !== liveSessionId) return;
        // Cancelled aborts the mission: drop the reveal so the host's cancellation notice shows through.
        // Finished keeps it (final screen); Paused/Active make no transition.
        if (notification.currentState === 'Cancelled') {
          applyTransition({
            state: null,
            serverTime: Date.parse(notification.changedAt),
            precedence: CLOSING,
          });
        }
      },
    );

    return () => {
      unsubscribeStarted();
      unsubscribeAdvanced();
      unsubscribeStateChanged();
    };
  }, [client, liveSessionId]);

  return { isRevealing: activeReveal !== null };
}
