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
  actionsHeader = 'Acciones',
}: {
  questions: TriviaQuestionDto[]
  renderRowActions?: (question: TriviaQuestionDto) => ReactNode
  actionsHeader?: string
}) {
  if (questions.length === 0) {
    return <p className={styles.mutedText}>Aún no se han agregado preguntas.</p>
  }

  const showActions = renderRowActions !== undefined

  return (
    <table className={`${styles.table} ${styles.triviaQuestionTable}`}>
      <thead>
        <tr>
          <th>Enunciado</th>
          <th>Puntaje</th>
          <th>Temporizador (s)</th>
          <th>Explicación</th>
          <th>Estado</th>
          <th>Opciones</th>
          {showActions && <th>{actionsHeader}</th>}
        </tr>
      </thead>
      <tbody>
        {questions.map((q) => (
          <tr key={q.id} data-testid={`question-row-${q.id}`}>
            <td data-label="Enunciado">{q.prompt}</td>
            <td data-label="Puntaje">{q.scoreValue ?? '—'}</td>
            <td data-label="Temporizador (s)">{q.timeLimitSeconds ?? '—'}</td>
            <td data-label="Explicación">{q.explanation ?? '—'}</td>
            <td data-label="Estado">
              <span className={styles.chip} data-tone={q.isActive ? 'success' : 'muted'}>
                {q.isActive ? 'Activa' : 'Inactiva'}
              </span>
            </td>
            <td data-label="Opciones">
              <ul className={styles.triviaOptionList}>
                {[...q.options]
                  .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
                  .map((opt) => (
                    <li key={opt.id}>
                      {opt.optionText}
                      {opt.isCorrect && (
                        <span className={styles.triviaOptionCorrect} aria-label="Respuesta correcta">
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
