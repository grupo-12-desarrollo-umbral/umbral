import type { TargetScanRejectionReasonCode } from './target-scan-types';

// Participant-facing copy for a rejected scan. Mirrors trivia-answer-rejection-copy: a short title plus
// a message. For a retained rejection (422) the backend's `detail` IS the reason the participant should
// see (wrong/unknown target, target outside the active substage, or duplicate), so the message comes
// from the wire — `retainedMessage` here is only a fallback if `detail` is somehow blank. Every other
// code is a pre-intake/transport failure the client explains itself.

const TITLES: Record<TargetScanRejectionReasonCode, string> = {
  'retained-rejection': 'Scan not accepted',
  'session-not-accepting': 'Session paused',
  'not-a-participant': 'Not authorized',
  'unauthorized': 'Session expired',
  'session-not-found': 'Session unavailable',
  'invalid-scan': "Couldn't read code",
  'network': 'Connection issue',
  'unknown': 'Something went wrong',
};

const MESSAGES: Record<TargetScanRejectionReasonCode, string> = {
  'retained-rejection': "That scan didn't resolve a target.",
  'session-not-accepting': "This session isn't accepting scans right now.",
  'not-a-participant': "Your team isn't registered to scan in this session.",
  'unauthorized': 'Your session expired — sign in again to keep scanning.',
  'session-not-found': "This session's scanning isn't available.",
  'invalid-scan': 'That QR code was empty or unreadable. Try again.',
  'network': "Couldn't reach the server. Check your connection and try again.",
  'unknown': "Couldn't register that scan. Try again.",
};

export function targetScanRejectionTitle(reasonCode: TargetScanRejectionReasonCode): string {
  return TITLES[reasonCode];
}

// The message shown for a rejected scan. For a retained rejection the backend `detail` is the reason,
// so it wins; a blank detail (or any other code) falls back to the static copy above.
export function targetScanRejectionMessage(
  reasonCode: TargetScanRejectionReasonCode,
  detail?: string,
): string {
  if (reasonCode === 'retained-rejection') {
    const trimmed = detail?.trim();
    if (trimmed) return trimmed;
  }
  return MESSAGES[reasonCode];
}
