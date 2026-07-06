'use client'

import { useCallback, useEffect, useRef, useState } from 'react'
import type {
  ActiveQuestionSnapshotDto,
  QuestionActivatedNotificationDto,
  QuestionClosedNotificationDto,
  SubstageAdvancedNotificationDto,
  TriviaRoundPhase,
} from '@/app/lib/definitions'

export type TriviaRoundState = {
  phase: TriviaRoundPhase
  pregameSecondsLeft: number | null
  activeQuestion: QuestionActivatedNotificationDto | null
  questionSecondsLeft: number | null // client-side approximation
  substageOrdinal: number            // 1-based; client-derived (session starts in substage 1)
  finalizing: boolean                // final substage done, session about to Finish
}

export type TriviaRoundHandlers = {
  handlePregameTimerTick: (remainingMs: number, totalMs: number) => void
  handleQuestionActivated: (n: QuestionActivatedNotificationDto) => void
  hydrateActiveQuestion: (n: ActiveQuestionSnapshotDto) => void
  handleQuestionClosed: (n: QuestionClosedNotificationDto) => void
  handleSubstageAdvanced: (n: SubstageAdvancedNotificationDto) => void
  complete: () => void
  reset: () => void
}

const initialState: TriviaRoundState = {
  phase: 'idle',
  pregameSecondsLeft: null,
  activeQuestion: null,
  questionSecondsLeft: null,
  substageOrdinal: 1,
  finalizing: false,
}

/**
 * Owns the automated trivia round state machine. The backend is authoritative for
 * actual question expiry; the per-second question countdown exposed here is a display
 * approximation for the operator. All transitions are driven by SignalR pushes.
 */
export function useTriviaRoundState(): TriviaRoundState & TriviaRoundHandlers {
  const [state, setState] = useState<TriviaRoundState>(initialState)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const clearCountdown = useCallback(() => {
    if (intervalRef.current !== null) {
      clearInterval(intervalRef.current)
      intervalRef.current = null
    }
  }, [])

  const reset = useCallback(() => {
    clearCountdown()
    setState(initialState)
  }, [clearCountdown])

  const handlePregameTimerTick = useCallback((remainingMs: number, totalMs: number) => {
    // The pre-game countdown is a fixed short window (≈5s). Translate the remaining
    // milliseconds into a whole-second numeral, never below zero.
    void totalMs
    const secondsLeft = Math.max(0, Math.ceil(remainingMs / 1000))
    clearCountdown()
    setState((current) => ({
      ...current,
      phase: 'pregame',
      pregameSecondsLeft: secondsLeft,
      activeQuestion: null,
      questionSecondsLeft: null,
    }))
  }, [clearCountdown])

  const startQuestionCountdown = useCallback(
    (n: QuestionActivatedNotificationDto, initialSecondsLeft: number) => {
      clearCountdown()
      setState((current) => ({
        phase: 'question-active',
        pregameSecondsLeft: null,
        activeQuestion: n,
        questionSecondsLeft: initialSecondsLeft,
        substageOrdinal: current.substageOrdinal,
        finalizing: false, // a new question means we're mid-substage, not finalizing
      }))

      intervalRef.current = setInterval(() => {
        setState((current) => {
          if (current.phase !== 'question-active' || current.questionSecondsLeft === null) {
            return current
          }
          const next = current.questionSecondsLeft - 1
          if (next <= 0) {
            clearCountdown()
            return { ...current, questionSecondsLeft: 0 }
          }
          return { ...current, questionSecondsLeft: next }
        })
      }, 1000)
    },
    [clearCountdown],
  )

  const handleQuestionActivated = useCallback(
    (n: QuestionActivatedNotificationDto) => {
      startQuestionCountdown(n, n.timeLimitSeconds)
    },
    [startQuestionCountdown],
  )

  const hydrateActiveQuestion = useCallback(
    (n: ActiveQuestionSnapshotDto) => {
      startQuestionCountdown(n, n.remainingSeconds)
    },
    [startQuestionCountdown],
  )

  // The notification payload is not needed: closing always moves to the between-questions
  // separator regardless of why the question closed.
  const handleQuestionClosed = useCallback(() => {
    clearCountdown()
    setState((current) => ({
      ...current,
      phase: 'between-questions',
      questionSecondsLeft: null,
    }))
  }, [clearCountdown])

  const handleSubstageAdvanced = useCallback(
    (n: SubstageAdvancedNotificationDto) => {
      clearCountdown()
      setState((current) => ({
        ...current,
        phase: 'substage-advancing',
        activeQuestion: null,
        questionSecondsLeft: null,
        // no next substage ⇒ final substage complete; otherwise advance the ordinal
        substageOrdinal: n.toSubstageId ? current.substageOrdinal + 1 : current.substageOrdinal,
        finalizing: n.toSubstageId == null,
      }))
    },
    [clearCountdown],
  )

  // SessionStateChanged -> Finished. Distinct from reset(): shows the completion state
  // only after the final substage rather than collapsing to idle.
  const complete = useCallback(() => {
    clearCountdown()
    setState((current) => ({
      ...current,
      phase: 'complete',
      activeQuestion: null,
      questionSecondsLeft: null,
    }))
  }, [clearCountdown])

  // Clear any running interval on unmount.
  useEffect(() => clearCountdown, [clearCountdown])

  return {
    ...state,
    handlePregameTimerTick,
    handleQuestionActivated,
    hydrateActiveQuestion,
    handleQuestionClosed,
    handleSubstageAdvanced,
    complete,
    reset,
  }
}
