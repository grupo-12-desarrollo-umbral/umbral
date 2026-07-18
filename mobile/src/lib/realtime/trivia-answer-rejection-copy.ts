import type { TriviaAnswerRejectionReasonCode } from './trivia-types';

const TITLES: Record<TriviaAnswerRejectionReasonCode | 'unknown', string> = {
  'late-trivia-answer': 'Demasiado tarde',
  'duplicate-trivia-answer': 'Ya respondida',
  'trivia-answer-requires-active-question': 'No hay pregunta activa',
  'trivia-answer-requires-active-session': 'Sesión no activa',
  'trivia-answer-requires-trivia-substage': 'No es una ronda de trivia',
  'invalid-trivia-answer-option': 'Selección inválida',
  'answer-submitter-is-not-session-participant': 'No autorizado',
  'unknown': 'Problema de conexión',
};

const MESSAGES: Record<TriviaAnswerRejectionReasonCode | 'unknown', string> = {
  'late-trivia-answer': 'La ventana de tiempo para responder se cerró antes de que llegara tu respuesta.',
  'duplicate-trivia-answer': 'Tu equipo ya envió una respuesta para esta pregunta.',
  'trivia-answer-requires-active-question': 'Esta pregunta ya no está abierta.',
  'trivia-answer-requires-active-session': 'Responder no está disponible en este momento.',
  'trivia-answer-requires-trivia-substage': 'La etapa actual no acepta respuestas.',
  'invalid-trivia-answer-option': 'La opción seleccionada no es válida para esta pregunta.',
  'answer-submitter-is-not-session-participant': 'Tu equipo no está registrado para responder en esta sesión.',
  'unknown': 'No pudimos conectar con el servidor. Revisa tu conexión y reintenta.',
};

export function triviaAnswerRejectionTitle(
  reasonCode: TriviaAnswerRejectionReasonCode | 'unknown',
): string {
  return TITLES[reasonCode];
}

export function triviaAnswerRejectionMessage(
  reasonCode: TriviaAnswerRejectionReasonCode | 'unknown',
): string {
  return MESSAGES[reasonCode];
}
