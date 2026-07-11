import type { ActiveQuestionSnapshotDto, QuestionActivatedNotificationDto } from './trivia-types';

export type ActiveQuestion = {
  questionIndex: number;
  sequenceOrder: number;
  prompt: string;
  options: readonly string[];
  timeLimitSeconds: number;
};

export type ActiveQuestionView =
  | { kind: 'active'; question: ActiveQuestion }
  | { kind: 'waiting' }
  | { kind: 'none' }
  | { kind: 'closed' };

export function toActiveQuestion(
  dto: QuestionActivatedNotificationDto | ActiveQuestionSnapshotDto,
): ActiveQuestion {
  return {
    questionIndex: dto.questionIndex,
    sequenceOrder: dto.sequenceOrder,
    prompt: dto.prompt,
    options: dto.options,
    timeLimitSeconds: dto.timeLimitSeconds,
  };
}

export function isTerminalSessionState(state: string): boolean {
  return state === 'Finished' || state === 'Cancelled';
}
