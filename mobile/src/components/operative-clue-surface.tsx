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

// --- Toast-arrival memory that survives the play surface remounting across the reveal ---
//
// The surface bases its "is this clue new?" decision on a seen-set plus the substage it last saw
// (operative-clue-surface `useEffect` below). That works while the component stays mounted — but the
// substage ranking reveal (D-3) replaces the WHOLE play surface (`team-space.tsx` early-returns
// `<RankingReveal>` above both play-mode branches) for the ~10s of the reveal, and it closes on the
// very `SubstageAdvanced` push that also swaps `board` to the next substage and its
// `VisibleWhenSubstageStarts` clues. So the surface unmounts during S1's reveal and remounts already
// on S2 — a fresh instance that, left to its own mount baseline, would treat S2's start-visible clues
// as an already-seen snapshot and never toast them (the reported bug, in both trivia and TH).
//
// This memory is owned by `OperativeCluePortalHost`, which wraps `LiveTeamSpace` ABOVE that reveal
// early-return and so outlives the remount. Persisting the seen-set + last substage here lets the
// remounted surface still observe the S1→S2 boundary and toast, exactly as an in-place transition would.
type ClueToastMemory = {
  toastedIds: Set<string>;
  lastSubstageId: string | null | undefined;
  // False until the first surface to mount under this host seeds the baseline from its own clues.
  initialized: boolean;
};

// Exposed as a stable accessor rather than the raw ref: the surface mutates the object this returns
// (the host's own mutable ref), never a context value directly — which the immutability lint forbids.
type GetClueToastMemory = () => ClueToastMemory;

const OperativeClueToastMemoryContext = createContext<GetClueToastMemory | null>(null);

export function OperativeCluePortalHost({ children }: { children: ReactNode }) {
  const [node, setNode] = useState<ReactNode>(null);
  // Outlives the reveal remount (see ClueToastMemory above), so a post-reveal surface can tell a real
  // substage boundary from a genuine late-join/reconnect snapshot.
  const memory = useRef<ClueToastMemory>({
    toastedIds: new Set(),
    lastSubstageId: undefined,
    initialized: false,
  });
  const getMemory = useCallback<GetClueToastMemory>(() => memory.current, []);
  return (
    <OperativeClueToastMemoryContext.Provider value={getMemory}>
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
    </OperativeClueToastMemoryContext.Provider>
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
        <Text variant="label" muted>{clue.targetName ?? 'PISTA'}</Text>
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
        accessibilityLabel={`Pistas, ${clues.length}${unseen > 0 ? `, ${unseen} nuevas` : ''}`}
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
          <Text variant="label" muted>{`PISTAS · ${clues.length}`}</Text>
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
      AccessibilityInfo.announceForAccessibility(`Nueva pista. ${clue.clueText}`);
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
        accessibilityLabel="Abrir pistas"
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
          <Text variant="label" muted>NUEVA PISTA</Text>
          <Text variant="body" numberOfLines={1}>{clue.clueText}</Text>
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Descartar"
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
  substageId,
}: {
  visibleClues: readonly VisibleClueDto[];
  home?: { listVisible: boolean; onOpen: () => void };
  /** Suppress the arrival toast (e.g. during pre-game countdown). New clues still accumulate unseen
   *  in the chip and will toast once suppression lifts. */
  suppressToast?: boolean;
  /** The active substage's snapshot id. When it changes, the toast baseline re-arms so the new
   *  substage's visible-on-start clues toast even though they ride the same board push that swaps
   *  the substage. Omit and every clue present at mount stays baselined as already-seen. */
  substageId?: string | null;
}) {
  // Every clue kind converges here (operative, substage-initial/mission, and target/scheduled clues):
  // one chip + one toast for all of them. On trivia the chip is the durable store; on the TH board it
  // is dropped (the Clues tab is the store) and only the toast + the board's own dot remain.
  const clues = visibleClues;

  const [expanded, setExpanded] = useState(false);
  const [toastClue, setToastClue] = useState<VisibleClueDto | null>(null);
  // Chip "unseen" bookkeeping: how many clues had been received last time the list was open.
  const [seenCount, setSeenCount] = useState(() => clues.length);

  // Toast-arrival bookkeeping (seen-set over clueKey + the last substage seen) lives in memory that,
  // when a portal host provides one, OUTLIVES this surface remounting across the reveal (see
  // ClueToastMemory). A bare mount (unit test / no host) falls back to a local ref, keeping the
  // single-mount behavior unchanged. Both are read only inside the effect below — never during render
  // — so the first-mount baseline is seeded there rather than in render.
  const getSharedMemory = useContext(OperativeClueToastMemoryContext);
  const localMemory = useRef<ClueToastMemory>({
    toastedIds: new Set(),
    lastSubstageId: undefined,
    initialized: false,
  });

  // Whichever list is the durable home for these clues: the chip's expansion (trivia) or the
  // host's own list (the TH board's Clues tab).
  const listVisible = home ? home.listVisible : expanded;

  // A genuinely new clue of any kind (not yet toasted) arrived → raise the toast once. A board
  // re-projection that adds no clue (e.g. a score-only update) leaves `fresh` empty.
  // While suppressed (e.g. during pre-game countdown) clues stay fresh and will toast once lifted.
  useEffect(() => {
    // Prefer the host-provided memory (survives the reveal remount); fall back to the local ref.
    const mem = getSharedMemory ? getSharedMemory() : localMemory.current;
    // First surface to mount under this memory: everything present is the reconnect / late-join
    // snapshot → baseline as already-seen (no toast) and record its substage so the first mount does
    // not spuriously re-arm. A later (post-reveal) remount finds `initialized` already true and keeps
    // the PRE-reveal substage + seen-set, so the boundary check below still sees the substage change.
    if (!mem.initialized) {
      mem.initialized = true;
      mem.toastedIds = new Set(clues.map(clueKey));
      mem.lastSubstageId = substageId;
      return;
    }
    // A new substage started (in place, or across the reveal remount) → its visible-on-start clues are
    // genuinely new. Clear the seen-set (the prior substage's clues left `visibleClues` on the
    // boundary, so nothing stale survives) before the freshness diff so they toast rather than inherit
    // the mount baseline, and re-zero the chip's seen count so the same clues flag as unseen too.
    if (substageId !== mem.lastSubstageId) {
      mem.lastSubstageId = substageId;
      mem.toastedIds = new Set();
      setSeenCount(0);
    }
    const fresh = clues.filter((c) => !mem.toastedIds.has(clueKey(c)));
    if (fresh.length === 0) return;
    // While suppressed don't mark as toasted — clues remain "fresh" so they toast once lifted.
    if (suppressToast) return;
    for (const c of fresh) mem.toastedIds.add(clueKey(c));
    // The list already shows arrivals while open, so only signal when it isn't on screen. A batch
    // (e.g. a reconnect delivering several at once) toasts the newest — the projection orders each
    // group newest-first, so the head is the freshest.
    if (!listVisible) setToastClue(fresh[0]);
    // `localMemory` is a ref (stable, exhaustive-deps ignores it); `getSharedMemory` is a stable
    // useCallback fixed for a host's lifetime, so neither re-runs the effect on its own.
  }, [clues, listVisible, suppressToast, substageId, getSharedMemory]);

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
