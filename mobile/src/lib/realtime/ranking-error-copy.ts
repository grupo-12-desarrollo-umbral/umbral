import type { TimerSnapshotError } from '@/lib/api/sessions';

/**
 * Participant-facing copy for a failed ranking-snapshot fetch (HU-25B). Shared by
 * every surface that renders live standings — the treasure-hunt board's TEAMS tab
 * and the trivia surface's ALL TEAMS panel — so the wording stays in one place.
 */
export function rankingErrorCopy(error: TimerSnapshotError): string {
  switch (error) {
    case 'network-error':
      return 'No pudimos acceder a la clasificación — revisa tu conexión.';
    case 'unauthorized':
      return 'Tu sesión expiró — no se pudo cargar la clasificación.';
    case 'forbidden':
      return 'No tienes acceso a la clasificación de esta sesión.';
    case 'not-found':
      return 'La clasificación de esta sesión no está disponible.';
    case 'timer-unavailable':
      return 'La clasificación aún no está lista — espera un momento.';
    default:
      return 'No se pudo cargar la clasificación.';
  }
}
