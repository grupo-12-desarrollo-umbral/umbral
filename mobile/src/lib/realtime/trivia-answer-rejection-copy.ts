import type { TriviaAnswerRejectionReasonCode } from './trivia-types';

const TITLES: Record<TriviaAnswerRejectionReasonCode | 'unknown', string> = {
  'late-trivia-answer': 'Too late',
  'duplicate-trivia-answer': 'Already answered',
  'trivia-answer-requires-active-question': 'No active question',
  'trivia-answer-requires-active-session': 'Session not active',
  'trivia-answer-requires-trivia-substage': 'Not a trivia round',
  'invalid-trivia-answer-option': 'Invalid selection',
  'answer-submitter-is-not-session-participant': 'Not authorized',
  'unknown': 'Connection issue',
};

const MESSAGES: Record<TriviaAnswerRejectionReasonCode | 'unknown', string> = {
  'late-trivia-answer': 'The time window to answer closed before your submission arrived.',
  'duplicate-trivia-answer': 'Your team has already submitted an answer for this question.',
  'trivia-answer-requires-active-question': 'This question is no longer open.',
  'trivia-answer-requires-active-session': "Answering isn't available right now.",
  'trivia-answer-requires-trivia-substage': "The current stage doesn't accept answers.",
  'invalid-trivia-answer-option': "The selected option isn't valid for this question.",
  'answer-submitter-is-not-session-participant': "Your team isn't registered to answer in this session.",
  'unknown': "Couldn't reach the server. Check your connection and try again.",
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
