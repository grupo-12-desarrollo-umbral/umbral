using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.ValueObjects;

// The substage ranking reveal currently on screen (D-3), read back from persisted aggregate state so a
// reconnecting client can restore it — the low-latency SubstageRankingRevealStarted push opens the
// reveal, this is the recovery source. Mirrors SubstageRevealStartedEvent's fields; EmittedAt is
// reconstructed as RevealUntil - SubstageRankingRevealDuration (the window is always that fixed length),
// so both instants ride the snapshot and duration stays immune to client clock skew.
public sealed record ActiveSubstageRankingReveal(
    Guid SubstageSnapshotId,
    SubstagePlayMode PlayMode,
    DateTimeOffset RevealUntil,
    bool IsTerminal,
    DateTimeOffset EmittedAt);
