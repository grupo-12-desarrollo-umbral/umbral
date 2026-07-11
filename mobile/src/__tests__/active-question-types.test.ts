import {
  isTerminalSessionState,
  toActiveQuestion,
  type ActiveQuestion,
} from '@/lib/realtime/active-question-types';
import type { ActiveQuestionSnapshotDto, QuestionActivatedNotificationDto } from '@/lib/realtime/trivia-types';

const ACTIVATED: QuestionActivatedNotificationDto = {
  liveSessionId: 'sess-1',
  questionIndex: 2,
  sequenceOrder: 3,
  prompt: 'Which lantern is lit?',
  options: ['North', 'South', 'East', 'West'],
  timeLimitSeconds: 45,
  activatedAt: '2026-07-11T10:00:00Z',
  triviaSubstageSnapshotId: 'substage-abc',
};

const SNAPSHOT: ActiveQuestionSnapshotDto = {
  ...ACTIVATED,
  remainingSeconds: 30,
};

const EXPECTED: ActiveQuestion = {
  questionIndex: 2,
  sequenceOrder: 3,
  prompt: 'Which lantern is lit?',
  options: ['North', 'South', 'East', 'West'],
  timeLimitSeconds: 45,
  triviaSubstageSnapshotId: 'substage-abc',
};

describe('active question mapping', () => {
  test('projects a QuestionActivated broadcast into the display view', () => {
    expect(toActiveQuestion(ACTIVATED)).toEqual(EXPECTED);
  });

  test('projects a snapshot active question without retaining remainingSeconds', () => {
    expect(toActiveQuestion(SNAPSHOT)).toEqual(EXPECTED);
    expect(toActiveQuestion(SNAPSHOT)).not.toHaveProperty('remainingSeconds');
  });

  test('recognizes only Finished and Cancelled as terminal session states', () => {
    expect(['Scheduled', 'Preparing', 'Active', 'Paused'].map(isTerminalSessionState)).toEqual([
      false,
      false,
      false,
      false,
    ]);
    expect(['Finished', 'Cancelled'].map(isTerminalSessionState)).toEqual([true, true]);
  });
});
