'use client'

// Read-only questions table shared by the Trivias panel and the mission editor's
// trivia-substage preview (issue #146). Presentational: the caller owns the header
// and any affordances. Passing `renderRowActions` adds an Actions column (the Trivias
// panel's Edit control); omitting it — the mission editor's preview — renders the
// table read-only, with no edit or delete controls.

import type { ReactNode } from 'react'
import type { TriviaQuestionDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

export function TriviaQuestionList({
  questions,
  renderRowActions,
  actionsHeader = 'Actions',
}: {
  questions: TriviaQuestionDto[]
  renderRowActions?: (question: TriviaQuestionDto) => ReactNode
  actionsHeader?: string
}) {
  if (questions.length === 0) {
    return <p className={styles.mutedText}>No questions added yet.</p>
  }

  const showActions = renderRowActions !== undefined

  return (
    <table className={`${styles.table} ${styles.triviaQuestionTable}`}>
      <thead>
        <tr>
          <th>Order</th>
          <th>Prompt</th>
          <th>Score</th>
          <th>Timer (s)</th>
          <th>Explanation</th>
          <th>Status</th>
          <th>Options</th>
          {showActions && <th>{actionsHeader}</th>}
        </tr>
      </thead>
      <tbody>
        {[...questions]
          .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
          .map((q) => (
            <tr key={q.id} data-testid={`question-row-${q.id}`}>
              <td data-label="Order">{q.sequenceOrder}</td>
              <td data-label="Prompt">{q.prompt}</td>
              <td data-label="Score">{q.scoreValue ?? '—'}</td>
              <td data-label="Timer (s)">{q.timeLimitSeconds ?? '—'}</td>
              <td data-label="Explanation">{q.explanation ?? '—'}</td>
              <td data-label="Status">
                <span className={styles.chip} data-tone={q.isActive ? 'success' : 'muted'}>
                  {q.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td data-label="Options">
                <ul className={styles.triviaOptionList}>
                  {[...q.options]
                    .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
                    .map((opt) => (
                      <li key={opt.id}>
                        {opt.optionText}
                        {opt.isCorrect && (
                          <span className={styles.triviaOptionCorrect} aria-label="Correct answer">
                            ✓
                          </span>
                        )}
                      </li>
                    ))}
                </ul>
              </td>
              {showActions && <td data-label={actionsHeader}>{renderRowActions(q)}</td>}
            </tr>
          ))}
      </tbody>
    </table>
  )
}
