// HU-31 participant target-scan contract (mobile side of #223). Every shape mirrors a verified
// `session-operations-service` DTO/endpoint. Guids serialize as strings, DateTimeOffset as ISO-8601.

// Request body for `POST /sessions/{liveSessionId}/participants/target-scans` (Participant policy).
// `scannedValue` is the raw QR payload captured by the camera; `token` is the optional
// runtime-participation credential the backend guard re-checks.
// Source: Api/Controllers/SessionsController.cs (record RegisterTargetScanRequest).
export type RegisterTargetScanRequest = {
  teamId: string;
  scannedValue: string;
  token?: string | null;
};

// 200 response. Acceptance metadata ONLY — deliberately omits the score, which travels solely on the
// RabbitMQ TargetResolved fact for downstream scoring, never on this participant path. `isResolved` is
// always true on a 200 (a non-resolving scan surfaces as a 422 rejection instead).
// Source: Application/Dtos/Sessions/RegisterTargetScanResultDto.cs
export type RegisterTargetScanResultDto = {
  liveSessionId: string;
  teamId: string;
  activeSubstageId: string;
  targetSnapshotId: string | null;
  isResolved: boolean;
  rejectionReason: string | null;
  submittedAt: string;
};

// Why a scan was not accepted, keyed off the HTTP status of the failure. A retained rejection (422,
// `type: "target-scan-rejected"`) is the treasure-hunt reject path — wrong/unknown QR, target outside
// the active substage, or a duplicate/already-resolved target — with the consistent reason carried in
// ProblemDetails `detail`. The remaining codes are pre-intake blocks the backend raises the same way it
// does for a trivia answer: a non-admitting session (409), a denied/unattributable participant (403),
// an expired credential (401), an unknown session (404), a blank scan (400), or a network failure (0).
export type TargetScanRejectionReasonCode =
  | 'retained-rejection' // 422 — registered for audit but not resolved; reason in `detail`
  | 'session-not-accepting' // 409 — session is Paused/Preparing/Finished/Cancelled
  | 'not-a-participant' // 403 — participation denied or caller is not an admitted participant
  | 'unauthorized' // 401 — missing/expired credential
  | 'session-not-found' // 404 — no such live session
  | 'invalid-scan' // 400 — blank/invalid scanned value
  | 'network' // 0 — request never reached the server
  | 'unknown'; // any other status
