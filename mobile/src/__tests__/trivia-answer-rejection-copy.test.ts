import {
  triviaAnswerRejectionTitle,
  triviaAnswerRejectionMessage,
} from '@/lib/realtime/trivia-answer-rejection-copy';
import type { TriviaAnswerRejectionReasonCode } from '@/lib/realtime/trivia-types';

const ALL_CODES: (TriviaAnswerRejectionReasonCode | 'unknown')[] = [
  'late-trivia-answer',
  'duplicate-trivia-answer',
  'trivia-answer-requires-active-question',
  'trivia-answer-requires-active-session',
  'trivia-answer-requires-trivia-substage',
  'invalid-trivia-answer-option',
  'answer-submitter-is-not-session-participant',
  'unknown',
];

describe('triviaAnswerRejectionTitle', () => {
  test.each(ALL_CODES)('returns a non-empty title for %s', (code) => {
    const title = triviaAnswerRejectionTitle(code);
    expect(typeof title).toBe('string');
    expect(title.length).toBeGreaterThan(0);
  });
});

describe('triviaAnswerRejectionMessage', () => {
  test.each(ALL_CODES)('returns a non-empty message for %s', (code) => {
    const message = triviaAnswerRejectionMessage(code);
    expect(typeof message).toBe('string');
    expect(message.length).toBeGreaterThan(0);
  });
});
