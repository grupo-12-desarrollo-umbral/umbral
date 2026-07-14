/**
 * HU-28 — unified clue surfacing on the TRIVIA participant view (B2).
 *
 * The trivia branch of `LiveTeamSpace` has no Clues tab, so a clue arriving in `board.visibleClues` —
 * an operator-authored operative clue, a substage-initial ("mission") clue, or a scheduled/target clue
 * — has nowhere to land. Every clue kind converges on the same treatment (toast + dropdown + dot).
 * This mounts two surfaces once at `LiveTeamSpace` level:
 *
 *   - `ChipInline`  — the durable store: a collapsed `CLUES · N` chip (ember-dot cue when unseen) that
 *                     expands the same parchment `ClueArtifact` cards used on the treasure-hunt board.
 *                     Sits in the normal scroll flow; the chip + its dot are the trivia "dropdown + dot".
 *   - `ClueToast`   — the arrival signal: a viewport-top-pinned transient toast, one truncated clue
 *                     line, ~220ms in / ~4s hold / ~180ms out, with an a11y announce. Lifted out of
 *                     the `Screen` ScrollView via `OperativeCluePortal` so it never scrolls away.
 *
 * Every clue kind (target/operative/mission) flows through the shared toast + chip; `clueKey` keeps the
 * both-null mission clues from colliding. Read-only off the existing board path (P0 delivers the clue
 * live) — never owns board state; the arrival trigger is pure prop-diff bookkeeping over `clueKey`.
 */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import {
  AccessibilityInfo,
  Animated,
  Platform,
  Pressable,
  StyleSheet,
  useAnimatedValue,
  View,
} from 'react-native';
import { Card } from '@/components/ui/card';
import { Text } from '@/components/ui/text';
import { colors, radii, shadows, spacing } from '@/constants/theme';
import { clueKey, type VisibleClueDto } from '@/lib/realtime/team-board-types';

// --- Viewport-pinned overlay portal (design doc decision 2) ---
//
// `LiveTeamSpace` renders inside the `Screen` ScrollView, so an in-flow toast would scroll away.
// The host wraps `Screen` and exposes a single overlay slot (a sibling of the ScrollView), into
// which `OperativeCluePortal` teleports the toast so it stays fixed to the viewport top. `box-none`
// keeps the live trivia question interactive behind the toast.

const OperativeCluePortalContext = createContext<((node: ReactNode) => void) | null>(null);

export function OperativeCluePortalHost({ children }: { children: ReactNode }) {
  const [node, setNode] = useState<ReactNode>(null);
  return (
    <OperativeCluePortalContext.Provider value={setNode}>
      <View style={{ flex: 1 }}>
        {children}
        {node != null ? (
          <View pointerEvents="box-none" style={StyleSheet.absoluteFill}>
            {node}
          </View>
        ) : null}
      </View>
    </OperativeCluePortalContext.Provider>
  );
}

function OperativeCluePortal({ children }: { children: ReactNode }) {
  const setNode = useContext(OperativeCluePortalContext);
  useEffect(() => {
    // No host (e.g. a unit test mounting the surface bare) → no-op; the in-flow chip still renders.
    setNode?.(children);
    return () => setNode?.(null);
  }, [children, setNode]);
  return null;
}

// --- The durable artifact: parchment Card + label + mono body (matches ClueCard) ---

function ClueArtifact({ clue }: { clue: VisibleClueDto }) {
  // A target clue heads with its target name; a target-less clue (operative or mission) just reads "CLUE"
  // — the surface is unified, so no per-kind "OPERATIVE"/"MISSION" wording.
  return (
    <Card parchment>
      <View accessibilityRole="text" style={{ gap: spacing.xs }}>
        <Text variant="label" muted>{clue.targetName ?? 'CLUE'}</Text>
        <Text variant="mono">{clue.clueText}</Text>
      </View>
    </Card>
  );
}

// --- Collapsed signal chip → inline expansion (the reviewable home) ---

function ChipInline({
  clues,
  unseen,
  expanded,
  onToggle,
}: {
  clues: readonly VisibleClueDto[];
  unseen: number;
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <View testID="operative-clue-list" style={{ gap: spacing.sm }}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Clues, ${clues.length}${unseen > 0 ? `, ${unseen} new` : ''}`}
        onPress={onToggle}
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          backgroundColor: colors.raisedSurface,
          borderWidth: 1,
          borderColor: colors.borderSoft,
          borderRadius: radii.control,
          borderCurve: 'continuous',
          boxShadow: shadows.insetSheen,
          paddingHorizontal: spacing.md,
          paddingVertical: spacing.sm,
        }}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
          {unseen > 0 ? (
            <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.emberAccentStrong }} />
          ) : null}
          <Text variant="label" muted>{`CLUES · ${clues.length}`}</Text>
        </View>
        <Text variant="label" muted>{expanded ? '▲' : '▼'}</Text>
      </Pressable>

      {expanded ? (
        <View style={{ gap: spacing.sm }}>
          {clues.map((c) => (
            <ClueArtifact key={clueKey(c)} clue={c} />
          ))}
        </View>
      ) : null}
    </View>
  );
}

// --- Arrival toast: transient, viewport-top-pinned, one truncated line (not parchment/mono) ---

function ClueToast({
  clue,
  onOpen,
  onDismiss,
}: {
  clue: VisibleClueDto;
  onOpen: () => void;
  onDismiss: () => void;
}) {
  const translateY = useAnimatedValue(-24);
  const opacity = useAnimatedValue(0);

  const dismiss = useCallback(() => {
    Animated.parallel([
      Animated.timing(translateY, { toValue: -24, duration: 180, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 0, duration: 180, useNativeDriver: true }),
    ]).start(({ finished }) => {
      if (finished) onDismiss();
    });
  }, [translateY, opacity, onDismiss]);

  useEffect(() => {
    // A purely visual auto-dismiss is inaccessible — speak the arrival (design doc decision 3).
    if (Platform.OS !== 'web') {
      AccessibilityInfo.announceForAccessibility(`New clue. ${clue.clueText}`);
    }
    Animated.parallel([
      Animated.timing(translateY, { toValue: 0, duration: 220, useNativeDriver: true }),
      Animated.timing(opacity, { toValue: 1, duration: 220, useNativeDriver: true }),
    ]).start();
    const t = setTimeout(dismiss, 4000);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Animated.View
      accessibilityLiveRegion="polite"
      pointerEvents="box-none"
      style={{
        position: 'absolute',
        top: 56,
        left: spacing.lg,
        right: spacing.lg,
        opacity,
        transform: [{ translateY }],
      }}
    >
      <Pressable
        testID="operative-clue-toast"
        accessibilityRole="button"
        accessibilityLabel="Open clues"
        onPress={() => {
          onOpen();
          dismiss();
        }}
        style={{
          backgroundColor: colors.panelSurface,
          borderColor: colors.borderSoft,
          borderWidth: 1,
          borderRadius: radii.card,
          borderCurve: 'continuous',
          boxShadow: shadows.card,
          padding: spacing.md,
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.sm,
        }}
      >
        <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.emberAccentStrong }} />
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="label" muted>NEW CLUE</Text>
          <Text variant="body" numberOfLines={1}>{clue.clueText}</Text>
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Dismiss"
          onPress={dismiss}
          hitSlop={spacing.xs}
          style={{ width: 24, height: 24, alignItems: 'center', justifyContent: 'center' }}
        >
          <Text variant="label" muted>✕</Text>
        </Pressable>
      </Pressable>
    </Animated.View>
  );
}

// --- The surface: owns the chip + toast, driven entirely by `visibleClues` prop diffs ---

/**
 * `home` is for hosts that already have a durable home for clues — the treasure-hunt board's Clues
 * tab. Passing it drops the chip (which would duplicate that tab) and renders the arrival toast
 * only: `listVisible` suppresses the toast while that home is on screen, and `onOpen` hands the
 * reveal back to the host (the TH board switches to its Clues tab). Omit it on trivia, which has no
 * such home and needs the chip as the durable store.
 */
export function OperativeClueSurface({
  visibleClues,
  home,
  suppressToast = false,
}: {
  visibleClues: readonly VisibleClueDto[];
  home?: { listVisible: boolean; onOpen: () => void };
  /** Suppress the arrival toast (e.g. during pre-game countdown). New clues still accumulate unseen
   *  in the chip and will toast once suppression lifts. */
  suppressToast?: boolean;
}) {
  // Every clue kind converges here (operative, substage-initial/mission, and target/scheduled clues):
  // one chip + one toast for all of them. On trivia the chip is the durable store; on the TH board it
  // is dropped (the Clues tab is the store) and only the toast + the board's own dot remain.
  const clues = visibleClues;

  const [expanded, setExpanded] = useState(false);
  const [toastClue, setToastClue] = useState<VisibleClueDto | null>(null);
  // Chip "unseen" bookkeeping: how many clues had been received last time the list was open.
  const [seenCount, setSeenCount] = useState(() => clues.length);

  // Toast-arrival dedup: a seen-set over clueKey. Baseline = the clues present at mount (already-seen)
  // so a reconnect/seed snapshot never toasts. Prop-diff bookkeeping only — never owns board state.
  const toastedIds = useRef<Set<string>>(new Set(clues.map(clueKey)));

  // Whichever list is the durable home for these clues: the chip's expansion (trivia) or the
  // host's own list (the TH board's Clues tab).
  const listVisible = home ? home.listVisible : expanded;

  // A genuinely new clue of any kind (not yet toasted) arrived → raise the toast once. A board
  // re-projection that adds no clue (e.g. a score-only update) leaves `fresh` empty.
  // While suppressed (e.g. during pre-game countdown) clues stay fresh and will toast once lifted.
  useEffect(() => {
    const fresh = clues.filter((c) => !toastedIds.current.has(clueKey(c)));
    if (fresh.length === 0) return;
    // While suppressed don't mark as toasted — clues remain "fresh" so they toast once lifted.
    if (suppressToast) return;
    for (const c of fresh) toastedIds.current.add(clueKey(c));
    // The list already shows arrivals while open, so only signal when it isn't on screen. A batch
    // (e.g. a reconnect delivering several at once) toasts the newest — the projection orders each
    // group newest-first, so the head is the freshest.
    if (!listVisible) setToastClue(fresh[0]);
  }, [clues, listVisible, suppressToast]);

  // While open every clue is visible, so unseen is 0 by definition; the handlers below acknowledge
  // (advance seenCount to the current length) on open/collapse — no setState-from-effect needed.
  const unseen = listVisible ? 0 : Math.max(0, clues.length - seenCount);

  const openList = useCallback(() => {
    setSeenCount(clues.length);
    setExpanded(true);
  }, [clues.length]);
  const toggleList = useCallback(() => {
    // Collapsing acknowledges everything that was on screen (including clues that arrived while open).
    setSeenCount(clues.length);
    setExpanded((prev) => !prev);
  }, [clues.length]);

  if (clues.length === 0) return null;

  return (
    <>
      {home ? null : (
        <ChipInline clues={clues} unseen={unseen} expanded={expanded} onToggle={toggleList} />
      )}

      {toastClue ? (
        <OperativeCluePortal>
          <ClueToast
            key={clueKey(toastClue)}
            clue={toastClue}
            onOpen={home ? home.onOpen : openList}
            onDismiss={() => setToastClue(null)}
          />
        </OperativeCluePortal>
      ) : null}
    </>
  );
}
