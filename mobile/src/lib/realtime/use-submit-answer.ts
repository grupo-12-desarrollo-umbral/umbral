import { useCallback, useEffect, useRef, useState } from 'react';
import * as Haptics from 'expo-haptics';
import {
  submitTriviaAnswer,
  SubmitTriviaAnswerRejection,
} from '@/lib/api/sessions';
import {
  triviaAnswerRejectionTitle,
  triviaAnswerRejectionMessage,
} from './trivia-answer-rejection-copy';
import type { TriviaAnswerRejectionReasonCode } from './trivia-types';

export type TriviaAnswerRejectionDisplay = {
  reasonCode: TriviaAnswerRejectionReasonCode | 'unknown';
  title: string;
  message: string;
};

export type UseSubmitAnswerResult = {
  selectedOptionSequenceOrder: number | null;
  isSubmitting: boolean;
  isLocked: boolean;
  rejection: TriviaAnswerRejectionDisplay | null;
  selectOption: (sequenceOrder: number) => void;
  submit: () => void;
  clearRejection: () => void;
};

function fireHaptic(type: 'light' | 'success' | 'error') {
  if (process.env.EXPO_OS === 'ios') {
    if (type === 'light') {
      Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    } else {
      Haptics.notificationAsync(
        type === 'success'
          ? Haptics.NotificationFeedbackType.Success
          : Haptics.NotificationFeedbackType.Error,
      );
    }
  }
}

export function useSubmitAnswer({
  liveSessionId,
  teamId,
  triviaSubstageSnapshotId,
  questionSequenceOrder,
  token,
}: {
  liveSessionId: string;
  teamId: string;
  triviaSubstageSnapshotId: string;
  questionSequenceOrder: number;
  token?: string | null;
}): UseSubmitAnswerResult {
  const [selectedOptionSequenceOrder, setSelectedOptionSequenceOrder] =
    useState<number | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLocked, setIsLocked] = useState(false);
  const [rejection, setRejection] = useState<TriviaAnswerRejectionDisplay | null>(null);

  // Prevent double-submit via ref (state updates are async; a fast double-tap
  // could sneak past the isSubmitting guard).
  const submittingRef = useRef(false);
  const questionKeyRef = useRef<string | null>(null);

  const questionKey = `${triviaSubstageSnapshotId}:${questionSequenceOrder}`;

  useEffect(() => {
    if (questionKeyRef.current !== questionKey) {
      questionKeyRef.current = questionKey;
      setSelectedOptionSequenceOrder(null);
      setIsLocked(false);
      setRejection(null);
      setIsSubmitting(false);
      submittingRef.current = false;
    }
  }, [questionKey]);

  const selectOption = useCallback((sequenceOrder: number) => {
    setSelectedOptionSequenceOrder(sequenceOrder);
    setRejection(null);
    fireHaptic('light');
  }, []);

  const clearRejection = useCallback(() => {
    setRejection(null);
  }, []);

  const submit = useCallback(async () => {
    if (
      selectedOptionSequenceOrder === null ||
      isLocked ||
      submittingRef.current
    ) {
      return;
    }

    submittingRef.current = true;
    setIsSubmitting(true);

    try {
      await submitTriviaAnswer(liveSessionId, {
        teamId,
        triviaSubstageSnapshotId,
        questionSequenceOrder,
        selectedOptionSequenceOrder,
        token,
      });
      setIsLocked(true);
      fireHaptic('success');
    } catch (error) {
      const reasonCode =
        error instanceof SubmitTriviaAnswerRejection
          ? error.reasonCode
          : 'unknown';
      setRejection({
        reasonCode,
        title: triviaAnswerRejectionTitle(reasonCode),
        message: triviaAnswerRejectionMessage(reasonCode),
      });
      fireHaptic('error');
    } finally {
      setIsSubmitting(false);
      submittingRef.current = false;
    }
  }, [
    selectedOptionSequenceOrder,
    isLocked,
    liveSessionId,
    teamId,
    triviaSubstageSnapshotId,
    questionSequenceOrder,
    token,
  ]);

  return {
    selectedOptionSequenceOrder,
    isSubmitting,
    isLocked,
    rejection,
    selectOption,
    submit,
    clearRejection,
  };
}
