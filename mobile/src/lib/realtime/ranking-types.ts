// HU-25B ranking contract (mirrors RankingSnapshotDto.cs / RankingRowDto.cs in
// scoring-monitoring-service). Wire fields are camelCase (ASP.NET System.Text.Json default).

export type RankingRowDto = {
  teamId: string;
  teamDisplayName: string;
  position: number;
  totalScore: number;
  // TimeSpan? serialised as "HH:mm:ss" or null when no resolution-time data.
  resolutionTime: string | null;
};

export type RankingSnapshotDto = {
  liveSessionId: string;
  generatedAt: string;
  calculationVersion: number;
  rows: readonly RankingRowDto[];
};

// Backend sends a well-known empty snapshot when no ranking has been computed yet:
//   RankingSnapshotDto.Empty → rows: [], generatedAt: DateTimeOffset.MinValue ("0001-01-01T00:00:00+00:00")
export function isEmptySnapshot(snapshot: RankingSnapshotDto): boolean {
  return snapshot.rows.length === 0;
}
