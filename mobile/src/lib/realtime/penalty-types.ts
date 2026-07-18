// Payload of the ScoringHub "PenaltyApplied" push. Mirrors the backend PenaltyAppliedNotificationDto
// (camelCase over the wire via System.Text.Json defaults). `teamId` is the cross-context ReferenceTeamId
// — the same id ranking rows carry — so the client matches it against the participant's referenceTeamId.
export type PenaltyAppliedDto = {
  liveSessionId: string;
  teamId: string;
  penaltyId: string;
  scoreEntryId: string;
  // The true, unclamped deduction (a positive number), shown verbatim as "Score dropped by N pts".
  deductionMagnitude: number;
  reason: string;
  appliedAt: string;
};
