namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Lightweight identity of an active mission that references a given trivia quiz, returned by
/// the quiz→missions inverse query. Carries only what archive-time enforcement needs to reject
/// the operation with an actionable message.
/// </summary>
public sealed record ActiveMissionReference(int MissionId, string Name);
