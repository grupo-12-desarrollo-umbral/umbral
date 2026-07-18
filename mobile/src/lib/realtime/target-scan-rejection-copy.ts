import type { TargetScanRejectionReasonCode } from './target-scan-types';

// Participant-facing copy for a rejected scan. Mirrors trivia-answer-rejection-copy: a short title plus
// a message. For a retained rejection (422) the backend's `detail` IS the reason the participant should
// see (wrong/unknown target, target outside the active substage, or duplicate), so the message comes
// from the wire — `retainedMessage` here is only a fallback if `detail` is somehow blank. Every other
// code is a pre-intake/transport failure the client explains itself.

const TITLES: Record<TargetScanRejectionReasonCode, string> = {
  'retained-rejection': 'Escaneo no aceptado',
  'session-not-accepting': 'Sesión en pausa',
  'concurrent-modification': 'Reintentar',
  'not-a-participant': 'No autorizado',
  'unauthorized': 'Sesión expirada',
  'session-not-found': 'Sesión no disponible',
  'invalid-scan': 'No se pudo leer el código',
  'network': 'Problema de conexión',
  'unknown': 'Algo salió mal',
};

const MESSAGES: Record<TargetScanRejectionReasonCode, string> = {
  'retained-rejection': 'Ese escaneo no resolvió un target.',
  'session-not-accepting': 'Esta sesión no está aceptando escaneos en este momento.',
  'concurrent-modification': 'La sesión se estaba actualizando en ese momento. Escanea de nuevo.',
  'not-a-participant': 'Tu equipo no está registrado para escanear en esta sesión.',
  'unauthorized': 'Tu sesión expiró — inicia sesión de nuevo para seguir escaneando.',
  'session-not-found': 'El escaneo de esta sesión no está disponible.',
  'invalid-scan': 'Ese código QR estaba vacío o ilegible. Reintenta.',
  'network': 'No pudimos conectar con el servidor. Revisa tu conexión y reintenta.',
  'unknown': 'No se pudo registrar ese escaneo. Reintenta.',
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
