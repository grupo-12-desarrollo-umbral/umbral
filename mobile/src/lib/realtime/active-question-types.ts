import type {
  ActiveQuestionSnapshotDto,
  QuestionActivatedNotificationDto,
  TriviaTeamQuestionResultDto,
} from './trivia-types';

export type ActiveQuestion = {
  questionIndex: number;
  sequenceOrder: number;
  prompt: string;
  options: readonly string[];
  timeLimitSeconds: number;
  triviaSubstageSnapshotId: string;
};

export type ActiveQuestionView =
  | { kind: 'active'; question: ActiveQuestion }
  | {
      kind: 'reveal';
      question: ActiveQuestion;
      correctOptionSequenceOrder: number;
      explanation: string | null;
      teamResult: TriviaTeamQuestionResultDto | null;
    }
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
    triviaSubstageSnapshotId: dto.triviaSubstageSnapshotId,
  };
}

export function isTerminalSessionState(state: string): boolean {
  return state === 'Finished' || state === 'Cancelled';
}
