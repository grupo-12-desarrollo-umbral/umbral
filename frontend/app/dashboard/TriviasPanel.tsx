'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getTriviaQuizzes,
  getTriviaQuiz,
  createTriviaQuiz,
  updateTriviaQuiz,
  addTriviaQuestion,
  updateTriviaQuestion,
  removeTriviaQuestion,
  publishTriviaQuiz,
  archiveTriviaQuiz,
  duplicateTriviaQuiz,
  retireTriviaQuiz,
} from '@/app/actions/trivias'
import type { TriviaQuizSummaryDto, TriviaQuizDto, TriviaQuestionDto, TriviaQuestionRequest } from '@/app/lib/definitions'
import { TriviaQuestionList } from './TriviaQuestionList'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type TriviaPanelView = 'list' | 'detail' | 'create' | 'edit' | 'add-question' | 'edit-question'

// Trivia questions are always worth this fixed score; the backend enforces the same value.
const FIXED_QUESTION_SCORE = 100

// Spanish display labels for the quiz lifecycle statuses. The enum values stay in English
// (they mirror the backend contract); this map is used wherever a status is shown to the operator.
const statusLabel: Record<string, string> = {
  Draft: 'Borrador',
  Published: 'Publicado',
  Archived: 'Archivado',
}

function displayStatus(status: string) {
  return statusLabel[status] ?? status
}

function computeReadiness(quiz: TriviaQuizDto): { isReady: boolean; reasons: string[] } {
  const reasons: string[] = []
  if (quiz.questions.length === 0) {
    reasons.push('Se requiere al menos una pregunta.')
  }
  for (const [index, q] of quiz.questions.entries()) {
    const questionLabel = `Pregunta ${index + 1}`
    if (q.scoreValue === null) {
      reasons.push(`${questionLabel}: se requiere el valor de puntaje.`)
    }
    if (q.timeLimitSeconds === null) {
      reasons.push(`${questionLabel}: se requiere el límite de tiempo.`)
    }
    if (q.options.length < 2 || q.options.length > 4) {
      reasons.push(`${questionLabel}: debe tener entre 2 y 4 opciones.`)
    }
    if (q.options.filter((o) => o.isCorrect).length !== 1) {
      reasons.push(`${questionLabel}: se requiere exactamente una opción correcta.`)
    }
  }
  return { isReady: reasons.length === 0, reasons }
}

export function TriviasPanel({ role }: { role: DashboardRole }) {
  const [view, setView] = useState<TriviaPanelView>('list')
  const [selectedQuiz, setSelectedQuiz] = useState<TriviaQuizDto | null>(null)
  const [selectedQuestion, setSelectedQuestion] = useState<TriviaQuestionDto | null>(null)
  const [confirmRemoveId, setConfirmRemoveId] = useState<number | null>(null)
  const [listData, setListData] = useState<TriviaQuizSummaryDto[] | null>(null)
  const [isPending, startTransition] = useTransition()
  const [listError, setListError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [questionError, setQuestionError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const [confirmPublish, setConfirmPublish] = useState(false)
  const [confirmArchive, setConfirmArchive] = useState(false)
  const [confirmDuplicate, setConfirmDuplicate] = useState(false)
  const [confirmRetire, setConfirmRetire] = useState(false)
  const [lifecycleError, setLifecycleError] = useState<string | null>(null)

  const allConfirmsClosed = !confirmPublish && !confirmArchive && !confirmDuplicate && !confirmRetire

  useEffect(() => {
    startTransition(async () => {
      setListError(null)
      try {
        const result = await getTriviaQuizzes()
        setListData(result)
      } catch {
        setListError('No se pudieron cargar los cuestionarios de trivia.')
      }
    })
  }, [refreshKey])

  async function handleOpenDetail(id: number) {
    startTransition(async () => {
      try {
        const quiz = await getTriviaQuiz(id)
        setSelectedQuiz(quiz)
        setView('detail')
      } catch {
        setListError('No se pudieron cargar los detalles del cuestionario de trivia.')
      }
    })
  }

  async function handleCreate(title: string, description: string) {
    startTransition(async () => {
      setFormError(null)
      try {
        const created = await createTriviaQuiz(title, description)
        setSelectedQuiz(created)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'invalid_fields') {
          setFormError('El título y la descripción son obligatorios (máx. 200 y 2000 caracteres).')
        } else {
          setFormError('No se pudo crear el cuestionario de trivia. Inténtalo de nuevo.')
        }
      }
    })
  }

  async function handleUpdate(title: string, description: string) {
    if (!selectedQuiz) return
    startTransition(async () => {
      setFormError(null)
      try {
        const updated = await updateTriviaQuiz(selectedQuiz.id, title, description)
        setSelectedQuiz(updated)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'invalid_fields') {
          setFormError('El título y la descripción son obligatorios (máx. 200 y 2000 caracteres).')
        } else if (msg === 'trivia_not_found') {
          setFormError('El cuestionario de trivia ya no existe.')
        } else if (msg === 'trivia_not_editable') {
          setFormError('Este cuestionario de trivia ya no se puede editar (ya no está en estado Borrador).')
        } else {
          setFormError('No se pudo actualizar el cuestionario de trivia. Inténtalo de nuevo.')
        }
      }
    })
  }

  async function handleAddQuestion(question: TriviaQuestionRequest) {
    if (!selectedQuiz) return
    startTransition(async () => {
      setQuestionError(null)
      try {
        const updated = await addTriviaQuestion(selectedQuiz.id, question)
        setSelectedQuiz(updated)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'invalid_question') {
          setQuestionError('Pregunta no válida. Revisa todos los campos y asegúrate de marcar exactamente una opción correcta.')
        } else if (msg === 'trivia_not_found') {
          setQuestionError('El cuestionario de trivia ya no existe.')
        } else {
          setQuestionError('No se pudo agregar la pregunta. Inténtalo de nuevo.')
        }
      }
    })
  }

  async function handleUpdateQuestion(questionId: number, question: TriviaQuestionRequest) {
    if (!selectedQuiz) return
    startTransition(async () => {
      setQuestionError(null)
      try {
        const updated = await updateTriviaQuestion(selectedQuiz.id, questionId, question)
        setSelectedQuiz(updated)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'invalid_question') {
          setQuestionError('Pregunta no válida. Revisa todos los campos y asegúrate de marcar exactamente una opción correcta.')
        } else if (msg === 'trivia_not_found') {
          setQuestionError('El cuestionario de trivia ya no existe.')
        } else {
          setQuestionError('No se pudo actualizar la pregunta. Inténtalo de nuevo.')
        }
      }
    })
  }

  async function handleRemoveQuestion(questionId: number) {
    if (!selectedQuiz) return
    startTransition(async () => {
      setQuestionError(null)
      try {
        const updated = await removeTriviaQuestion(selectedQuiz.id, questionId)
        setSelectedQuiz(updated)
        setConfirmRemoveId(null)
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'trivia_not_found') {
          setQuestionError('Esa pregunta ya no existe. Actualiza el cuestionario e inténtalo de nuevo.')
        } else if (msg === 'trivia_not_editable') {
          setQuestionError('Este cuestionario ya no se puede editar (ya no está en estado Borrador).')
        } else {
          setQuestionError('No se pudo eliminar la pregunta. Inténtalo de nuevo.')
        }
        setConfirmRemoveId(null)
      }
    })
  }

  async function handlePublish() {
    if (!selectedQuiz) return
    startTransition(async () => {
      setLifecycleError(null)
      try {
        const updated = await publishTriviaQuiz(selectedQuiz.id)
        setSelectedQuiz(updated)
        setConfirmPublish(false)
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'trivia_publish_conflict') {
          setLifecycleError(
            'Falló la publicación. Asegúrate de que el cuestionario esté en estado Borrador y de que todas las preguntas tengan valor de puntaje, límite de tiempo y opciones válidas.',
          )
        } else if (msg === 'trivia_not_found') {
          setLifecycleError('El cuestionario de trivia ya no existe.')
        } else {
          setLifecycleError('No se pudo publicar el cuestionario. Inténtalo de nuevo.')
        }
        setConfirmPublish(false)
      }
    })
  }

  async function handleArchive() {
    if (!selectedQuiz) return
    startTransition(async () => {
      setLifecycleError(null)
      try {
        const updated = await archiveTriviaQuiz(selectedQuiz.id)
        setSelectedQuiz(updated)
        setConfirmArchive(false)
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'trivia_archive_conflict') {
          setLifecycleError('Este cuestionario no se puede archivar en su estado actual.')
        } else if (msg === 'trivia_not_found') {
          setLifecycleError('El cuestionario de trivia ya no existe.')
        } else {
          setLifecycleError('No se pudo archivar el cuestionario. Inténtalo de nuevo.')
        }
        setConfirmArchive(false)
      }
    })
  }

  async function handleDuplicate() {
    if (!selectedQuiz) return
    startTransition(async () => {
      setLifecycleError(null)
      try {
        const duplicated = await duplicateTriviaQuiz(selectedQuiz.id)
        setSelectedQuiz(duplicated)
        setConfirmDuplicate(false)
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'trivia_duplicate_conflict') {
          setLifecycleError('No se puede duplicar un cuestionario archivado.')
        } else if (msg === 'trivia_not_found') {
          setLifecycleError('El cuestionario de trivia ya no existe.')
        } else {
          setLifecycleError('No se pudo duplicar el cuestionario. Inténtalo de nuevo.')
        }
        setConfirmDuplicate(false)
      }
    })
  }

  async function handleRetire() {
    if (!selectedQuiz) return
    startTransition(async () => {
      setLifecycleError(null)
      const result = await retireTriviaQuiz(selectedQuiz.id)
      if ('data' in result) {
        setSelectedQuiz(result.data)
        setConfirmRetire(false)
        setRefreshKey((k) => k + 1)
        return
      }
      if (result.error === 'trivia_retire_conflict') {
        // `detail` names the actual blocker — an active mission still referencing the quiz, or its
        // current lifecycle state. Prefer it; the generic line is only for an unreadable body.
        setLifecycleError(
          result.detail ?? 'Este cuestionario no se puede retirar en su estado actual.',
        )
      } else if (result.error === 'trivia_not_found') {
        setLifecycleError('El cuestionario de trivia ya no existe.')
      } else {
        setLifecycleError('No se pudo retirar el cuestionario. Inténtalo de nuevo.')
      }
      setConfirmRetire(false)
    })
  }

  function statusTone(status: string): 'success' | 'warning' | 'muted' {
    if (status === 'Published') return 'success'
    if (status === 'Draft') return 'warning'
    return 'muted'
  }

  function renderQuestionsSection(
    questions: TriviaQuizDto['questions'],
    quiz: TriviaQuizDto,
  ) {
    const isDraft = quiz.status === 'Draft'
    const canEdit = role === 'operator' && isDraft

    return (
      <div data-testid="trivia-questions-section">
        <div className={styles.subsectionHeader}>
          <h3>Preguntas</h3>
          {canEdit && (
            <button
              className={styles.primaryButton}
              data-testid="add-question-btn"
              disabled={isPending}
              onClick={() => { setQuestionError(null); setConfirmRemoveId(null); setSelectedQuestion(null); setView('add-question') }}
              type="button"
            >
              Agregar pregunta
            </button>
          )}
        </div>

        {questionError && (
          <p className={styles.formError} role="alert" data-testid="question-error">
            {questionError}
          </p>
        )}

        <TriviaQuestionList
          questions={questions}
          renderRowActions={
            canEdit
              ? (q) =>
                  confirmRemoveId === q.id ? (
                    <span className={styles.confirmRow}>
                      <button
                        className={styles.smallButton}
                        data-testid={`confirm-remove-question-btn-${q.id}`}
                        disabled={isPending}
                        onClick={() => handleRemoveQuestion(q.id)}
                        type="button"
                      >
                        Confirmar eliminación
                      </button>
                      <button
                        className={styles.inlineButton}
                        disabled={isPending}
                        onClick={() => setConfirmRemoveId(null)}
                        type="button"
                      >
                        Cancelar
                      </button>
                    </span>
                  ) : (
                    <span className={styles.confirmRow}>
                      <button
                        className={styles.inlineButton}
                        data-testid={`edit-question-btn-${q.id}`}
                        disabled={isPending}
                        onClick={() => {
                          setQuestionError(null)
                          setConfirmRemoveId(null)
                          setSelectedQuestion(q)
                          setView('edit-question')
                        }}
                        type="button"
                      >
                        Editar
                      </button>
                      <button
                        className={styles.inlineButton}
                        data-testid={`remove-question-btn-${q.id}`}
                        disabled={isPending}
                        onClick={() => { setQuestionError(null); setConfirmRemoveId(q.id) }}
                        type="button"
                      >
                        Eliminar
                      </button>
                    </span>
                  )
              : undefined
          }
        />
      </div>
    )
  }

  if (view === 'add-question' && selectedQuiz !== null) {
    return (
      <section className={styles.panel} data-testid="trivias-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setQuestionError(null); setView('detail') }}
          type="button"
        >
          ← {selectedQuiz.title}
        </button>

        <h2 className={styles.missionDetailTitle}>Agregar pregunta</h2>

        <TriviaQuestionForm
          initial={null}
          isPending={isPending}
          error={questionError}
          onSubmit={(q) => handleAddQuestion(q)}
          onCancel={() => { setQuestionError(null); setView('detail') }}
        />
      </section>
    )
  }

  if (view === 'edit-question' && selectedQuiz !== null && selectedQuestion !== null) {
    return (
      <section className={styles.panel} data-testid="trivias-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setQuestionError(null); setView('detail') }}
          type="button"
        >
          ← {selectedQuiz.title}
        </button>

        <h2 className={styles.missionDetailTitle}>Editar pregunta</h2>

        <TriviaQuestionForm
          initial={selectedQuestion}
          isPending={isPending}
          error={questionError}
          onSubmit={(q) => handleUpdateQuestion(selectedQuestion.id, q)}
          onCancel={() => { setQuestionError(null); setView('detail') }}
        />
      </section>
    )
  }

  if (view === 'detail' && selectedQuiz !== null) {
    return (
      <section className={styles.panel} data-testid="trivia-detail">
        <button
          className={styles.inlineButton}
          onClick={() => { setView('list'); setConfirmPublish(false); setConfirmArchive(false); setConfirmDuplicate(false); setConfirmRetire(false); setLifecycleError(null); setConfirmRemoveId(null); setQuestionError(null) }}
          type="button"
        >
          ← Volver a los cuestionarios de trivia
        </button>

        <h2 className={styles.missionDetailTitle} data-testid="trivia-detail-title">
          {selectedQuiz.title}
        </h2>

        <div className={styles.missionDetailDescCard}>
          <span className={styles.missionDetailDescLabel}>Descripción</span>
          <p data-testid="trivia-detail-description">{selectedQuiz.description}</p>
        </div>

        <div className={styles.missionDetailInlineMeta}>
          <span>
            Estado:
            <span
              className={styles.chip}
              data-tone={statusTone(selectedQuiz.status)}
              data-testid="trivia-detail-status"
            >
              {displayStatus(selectedQuiz.status)}
            </span>
          </span>
          <span>
            Fuente lista:
            <span
              className={styles.chip}
              data-tone={selectedQuiz.isSourceReady ? 'success' : 'muted'}
              data-testid="trivia-source-ready"
            >
              {selectedQuiz.isSourceReady ? 'Sí' : 'No'}
            </span>
          </span>
          {selectedQuiz.isDuplicate && selectedQuiz.sourceTriviaQuizId !== null && (
            <span>
              Copiado de:
              <span
                className={styles.chip}
                data-tone="muted"
                data-testid="trivia-source-quiz-id"
              >
                Cuestionario n.º {selectedQuiz.sourceTriviaQuizId}
              </span>
            </span>
          )}
          {selectedQuiz.hasUsageHistory && (
            <span>
              <span
                className={styles.chip}
                data-tone="warning"
                data-testid="trivia-has-usage-history"
              >
                Tiene historial de uso
              </span>
            </span>
          )}
        </div>

        {selectedQuiz.status === 'Draft' && (() => {
          const { isReady, reasons } = computeReadiness(selectedQuiz)
          return !isReady ? (
            <div className={styles.missionDetailDescCard} data-testid="trivia-readiness-indicator">
              <span className={styles.missionDetailDescLabel}>Preparación para publicar</span>
              <ul style={{ margin: 0, paddingLeft: '1.2rem', color: 'var(--text-secondary)' }}>
                {reasons.map((r, i) => <li key={i}>{r}</li>)}
              </ul>
            </div>
          ) : null
        })()}

        {lifecycleError && (
          <p className={styles.formError} role="alert" data-testid="lifecycle-error">
            {lifecycleError}
          </p>
        )}

        <div className={styles.missionDetailActions}>
          {/* Edit — unchanged */}
          <button
            className={styles.inlineButton}
            data-testid="edit-trivia-btn"
            disabled={isPending || selectedQuiz.status !== 'Draft'}
            onClick={() => { setFormError(null); setView('edit') }}
            type="button"
          >
            Editar
          </button>

          {/* Publish trigger — only Draft, operator, no confirmations open */}
          {role === 'operator' && selectedQuiz.status === 'Draft' && allConfirmsClosed && (() => {
            const { isReady, reasons } = computeReadiness(selectedQuiz)
            return (
              <button
                className={styles.primaryButton}
                data-testid="publish-trivia-btn"
                disabled={isPending || !isReady}
                title={!isReady ? reasons.join(' ') : undefined}
                onClick={() => { setLifecycleError(null); setConfirmPublish(true) }}
                type="button"
              >
                Publicar
              </button>
            )
          })()}

          {/* Publish confirmation row — unchanged */}
          {confirmPublish && (
            <span className={styles.confirmRow}>
              <button
                className={styles.smallButton}
                data-testid="confirm-publish-btn"
                disabled={isPending}
                onClick={handlePublish}
                type="button"
              >
                Confirmar publicación
              </button>
              <button
                className={styles.inlineButton}
                disabled={isPending}
                onClick={() => { setConfirmPublish(false); setLifecycleError(null) }}
                type="button"
              >
                Cancelar
              </button>
            </span>
          )}

          {/* Archive trigger — unused quizzes only; used quizzes get Retire instead */}
          {role === 'operator' && selectedQuiz.status !== 'Archived' && !selectedQuiz.hasUsageHistory && allConfirmsClosed && (
            <button
              className={styles.inlineButton}
              data-testid="archive-trivia-btn"
              disabled={isPending}
              onClick={() => { setLifecycleError(null); setConfirmArchive(true) }}
              type="button"
            >
              Archivar
            </button>
          )}

          {/* Archive confirmation row — unchanged */}
          {confirmArchive && (
            <span className={styles.confirmRow}>
              <button
                className={styles.smallButton}
                data-testid="confirm-archive-btn"
                disabled={isPending}
                onClick={handleArchive}
                type="button"
              >
                Confirmar archivado
              </button>
              <button
                className={styles.inlineButton}
                disabled={isPending}
                onClick={() => { setConfirmArchive(false); setLifecycleError(null) }}
                type="button"
              >
                Cancelar
              </button>
            </span>
          )}

          {/* Duplicate trigger — non-Archived, operator only, no confirmations open */}
          {role === 'operator' && selectedQuiz.status !== 'Archived' && allConfirmsClosed && (
            <button
              className={styles.inlineButton}
              data-testid="duplicate-trivia-btn"
              disabled={isPending}
              onClick={() => { setLifecycleError(null); setConfirmDuplicate(true) }}
              type="button"
            >
              Duplicar
            </button>
          )}

          {/* Duplicate confirmation row */}
          {confirmDuplicate && (
            <span className={styles.confirmRow}>
              <button
                className={styles.smallButton}
                data-testid="confirm-duplicate-btn"
                disabled={isPending}
                onClick={handleDuplicate}
                type="button"
              >
                Confirmar duplicación
              </button>
              <button
                className={styles.inlineButton}
                disabled={isPending}
                onClick={() => { setConfirmDuplicate(false); setLifecycleError(null) }}
                type="button"
              >
                Cancelar
              </button>
            </span>
          )}

          {/* Retire trigger — used quizzes only, non-Archived, operator only, no confirmations open */}
          {role === 'operator' && selectedQuiz.status !== 'Archived' && selectedQuiz.hasUsageHistory && allConfirmsClosed && (
            <button
              className={styles.inlineButton}
              data-testid="retire-trivia-btn"
              disabled={isPending}
              title="Retirar de uso futuro: se conserva el historial de sesiones."
              onClick={() => { setLifecycleError(null); setConfirmRetire(true) }}
              type="button"
            >
              Retirar
            </button>
          )}

          {/* Retire confirmation row */}
          {confirmRetire && (
            <span className={styles.confirmRow}>
              <button
                className={styles.smallButton}
                data-testid="confirm-retire-btn"
                disabled={isPending}
                onClick={handleRetire}
                type="button"
              >
                Confirmar retiro
              </button>
              <button
                className={styles.inlineButton}
                disabled={isPending}
                onClick={() => { setConfirmRetire(false); setLifecycleError(null) }}
                type="button"
              >
                Cancelar
              </button>
            </span>
          )}
        </div>

        {renderQuestionsSection(selectedQuiz.questions, selectedQuiz)}
      </section>
    )
  }

  if (view === 'create') {
    return (
      <section className={styles.panel} data-testid="trivias-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setFormError(null); setView('list') }}
          type="button"
        >
          ← Volver a los cuestionarios de trivia
        </button>

        <h2 className={styles.missionDetailTitle}>Crear cuestionario de trivia</h2>

        <TriviaQuizForm
          initial={{ title: '', description: '' }}
          isPending={isPending}
          error={formError}
          onSubmit={handleCreate}
          onCancel={() => { setFormError(null); setView('list') }}
        />
      </section>
    )
  }

  if (view === 'edit' && selectedQuiz !== null) {
    return (
      <section className={styles.panel} data-testid="trivias-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setFormError(null); setView('detail') }}
          type="button"
        >
          ← {selectedQuiz.title}
        </button>

        <h2 className={styles.missionDetailTitle}>Editar cuestionario de trivia</h2>

        <TriviaQuizForm
          initial={{ title: selectedQuiz.title, description: selectedQuiz.description }}
          isPending={isPending}
          error={formError}
          onSubmit={handleUpdate}
          onCancel={() => { setFormError(null); setView('detail') }}
        />

        {renderQuestionsSection(selectedQuiz.questions, selectedQuiz)}
      </section>
    )
  }

  // Default list view
  return (
    <section className={styles.panel} data-testid="trivias-panel">
      <div className={styles.panelHeader}>
        <div>
          <h2>Cuestionarios de trivia</h2>
        </div>
        {role === 'operator' && (
          <button
            className={styles.primaryButton}
            data-testid="create-trivia-btn"
            disabled={isPending}
            onClick={() => { setFormError(null); setView('create') }}
            type="button"
          >
            Crear cuestionario de trivia
          </button>
        )}
      </div>

      {listError && <p className={styles.formError}>{listError}</p>}

      {listData && (
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Título</th>
                <th>Descripción</th>
                <th>Estado</th>
                <th>Fuente lista</th>
                <th>Procedencia</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {listData.map((quiz) => (
                <tr key={quiz.id} data-testid={`trivia-row-${quiz.id}`}>
                  <td data-label="Title">{quiz.title}</td>
                  <td data-label="Description">{quiz.description}</td>
                  <td data-label="Status">
                    <span
                      className={styles.chip}
                      data-tone={statusTone(quiz.status)}
                      data-testid={`trivia-status-${quiz.id}`}
                    >
                      {displayStatus(quiz.status)}
                    </span>
                  </td>
                  <td data-label="Source ready">
                    <span
                      className={styles.chip}
                      data-tone={quiz.isSourceReady ? 'success' : 'muted'}
                      data-testid={`trivia-source-ready-${quiz.id}`}
                    >
                      {quiz.isSourceReady ? 'Sí' : 'No'}
                    </span>
                  </td>
                  <td data-label="Provenance">
                    {quiz.isDuplicate ? (
                      <span
                        className={styles.chip}
                        data-tone="muted"
                        data-testid={`trivia-copy-chip-${quiz.id}`}
                      >
                        Copia
                      </span>
                    ) : quiz.hasUsageHistory ? (
                      <span
                        className={styles.chip}
                        data-tone="warning"
                        data-testid={`trivia-usage-chip-${quiz.id}`}
                      >
                        Usado
                      </span>
                    ) : (
                      <span style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>—</span>
                    )}
                  </td>
                  <td data-label="Actions">
                    <button
                      className={styles.inlineButton}
                      data-testid={`view-trivia-btn-${quiz.id}`}
                      disabled={isPending}
                      onClick={() => handleOpenDetail(quiz.id)}
                      type="button"
                    >
                      Ver detalles
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
      )}
    </section>
  )
}

function TriviaQuizForm({
  initial,
  isPending,
  error,
  onSubmit,
  onCancel,
}: {
  initial: { title: string; description: string }
  isPending: boolean
  error: string | null
  onSubmit: (title: string, description: string) => void
  onCancel: () => void
}) {
  const [title, setTitle] = useState(initial.title)
  const [description, setDescription] = useState(initial.description)

  return (
    <form
      className={styles.missionForm}
      data-testid="trivia-form"
      onSubmit={(e) => {
        e.preventDefault()
        onSubmit(title.trim(), description.trim())
      }}
    >
      {error && <p className={styles.formError} role="alert">{error}</p>}

      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Título del cuestionario</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="trivia-title-input"
          disabled={isPending}
          maxLength={200}
          placeholder="Ingresa el título del cuestionario"
          required
          value={title}
          onChange={(e) => setTitle(e.target.value)}
        />
      </div>

      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Descripción</span>
        <textarea
          className={styles.missionFormDescTextarea}
          data-testid="trivia-description-input"
          disabled={isPending}
          maxLength={2000}
          placeholder="Describe el cuestionario"
          required
          rows={4}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>

      <div className={styles.missionDetailActions}>
        <button
          className={styles.primaryButton}
          data-testid="trivia-submit-btn"
          disabled={isPending}
          type="submit"
        >
          Guardar
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          type="button"
          onClick={onCancel}
        >
          Cancelar
        </button>
      </div>
    </form>
  )
}

type OptionDraft = {
  optionText: string
  sequenceOrder: number
  isCorrect: boolean
}

function TriviaQuestionForm({
  initial,
  isPending,
  error,
  onSubmit,
  onCancel,
}: {
  initial: TriviaQuestionDto | null
  isPending: boolean
  error: string | null
  onSubmit: (question: TriviaQuestionRequest) => void
  onCancel: () => void
}) {
  const [prompt, setPrompt] = useState(initial?.prompt ?? '')
  // Every trivia question is worth a fixed score of 100; operators do not set it.
  const scoreValue = FIXED_QUESTION_SCORE
  const [timeLimitSeconds, setTimeLimitSeconds] = useState<number | null>(
    initial?.timeLimitSeconds ?? 15,
  )
  const [explanation, setExplanation] = useState(initial?.explanation ?? '')
  const [isActive, setIsActive] = useState(initial?.isActive ?? true)
  const [options, setOptions] = useState<OptionDraft[]>(
    initial?.options && initial.options.length >= 2
      ? [...initial.options]
          .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
          .map((opt) => ({
            optionText: opt.optionText,
            sequenceOrder: opt.sequenceOrder,
            isCorrect: opt.isCorrect,
          }))
      : [
          { optionText: '', sequenceOrder: 1, isCorrect: false },
          { optionText: '', sequenceOrder: 2, isCorrect: false },
        ],
  )
  const [formError, setFormError] = useState<string | null>(null)

  function addOption() {
    if (options.length >= 4) return
    const nextSeq = Math.max(...options.map((o) => o.sequenceOrder)) + 1
    setOptions([...options, { optionText: '', sequenceOrder: nextSeq, isCorrect: false }])
  }

  function removeOption(index: number) {
    if (options.length <= 2) return
    setOptions(options.filter((_, i) => i !== index))
  }

  function updateOption(index: number, patch: Partial<OptionDraft>) {
    setOptions(options.map((opt, i) => (i === index ? { ...opt, ...patch } : opt)))
  }

  function markCorrect(index: number) {
    setOptions(options.map((opt, i) => ({ ...opt, isCorrect: i === index })))
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    if (timeLimitSeconds === null || timeLimitSeconds < 15 || timeLimitSeconds > 30) {
      setFormError('El límite de tiempo es obligatorio y debe estar entre 15 y 30 segundos.')
      return
    }

    const correctCount = options.filter((o) => o.isCorrect).length
    if (correctCount !== 1) {
      setFormError('Se debe marcar exactamente una opción como correcta.')
      return
    }
    if (options.some((o) => o.optionText.trim() === '')) {
      setFormError('Todos los textos de las opciones son obligatorios.')
      return
    }
    if (options.length < 2 || options.length > 4) {
      setFormError('Una pregunta debe tener entre 2 y 4 opciones.')
      return
    }

    onSubmit({
      prompt: prompt.trim(),
      scoreValue,
      timeLimitSeconds,
      explanation: explanation.trim() !== '' ? explanation.trim() : null,
      isActive,
      options: options.map((opt) => ({
        optionText: opt.optionText.trim(),
        sequenceOrder: opt.sequenceOrder,
        isCorrect: opt.isCorrect,
      })),
    })
  }

  const displayedError = error ?? formError

  return (
    <form
      className={styles.missionForm}
      data-testid="question-form"
      onSubmit={handleSubmit}
    >
      {/* Prompt */}
      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Enunciado</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="question-prompt-input"
          disabled={isPending}
          maxLength={2000}
          placeholder="Ingresa el enunciado de la pregunta"
          required
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
        />
      </div>

      {/* Score — fixed at 100 for every question */}
      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Valor de puntaje</span>
        <p data-testid="question-score-value">{scoreValue} puntos (fijo)</p>
      </div>

      {/* Timer */}
      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Límite de tiempo (15–30 segundos)</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="question-timer-input"
          disabled={isPending}
          min={15}
          max={30}
          required
          type="number"
          value={timeLimitSeconds ?? ''}
          onChange={(e) =>
            setTimeLimitSeconds(e.target.value === '' ? null : Number(e.target.value))
          }
        />
      </div>

      {/* Explanation */}
      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Explicación (opcional)</span>
        <textarea
          className={styles.missionFormDescTextarea}
          data-testid="question-explanation-input"
          disabled={isPending}
          maxLength={4000}
          placeholder="Explica por qué la respuesta correcta es la correcta"
          rows={3}
          value={explanation}
          onChange={(e) => setExplanation(e.target.value)}
        />
      </div>

      {/* Active toggle */}
      <div className={styles.missionFormNameCard}>
        <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <input
            checked={isActive}
            data-testid="question-active-checkbox"
            disabled={isPending}
            type="checkbox"
            onChange={(e) => setIsActive(e.target.checked)}
          />
          <span className={styles.missionDetailDescLabel}>Activa</span>
        </label>
      </div>

      {/* Options */}
      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Opciones (2–4)</span>
        {options.map((opt, index) => (
          <div
            key={index}
            className={styles.confirmRow}
            data-testid={`question-option-${index}`}
            style={{ marginBottom: '0.5rem' }}
          >
            <input
              className={styles.missionFormNameInput}
              data-testid={`question-option-text-${index}`}
              disabled={isPending}
              maxLength={1000}
              placeholder={`Opción ${index + 1}`}
              required
              value={opt.optionText}
              onChange={(e) => updateOption(index, { optionText: e.target.value })}
            />
            <input
              checked={opt.isCorrect}
              data-testid={`question-option-correct-${index}`}
              disabled={isPending}
              name="correct-option"
              style={{ marginLeft: '0.75rem' }}
              title="Marcar como correcta"
              type="radio"
              onChange={() => markCorrect(index)}
            />
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginLeft: '0.35rem' }}>Correcta</span>
            {options.length > 2 && (
              <button
                className={styles.inlineButton}
                data-testid={`question-remove-option-btn-${index}`}
                disabled={isPending}
                onClick={() => removeOption(index)}
                style={{ marginLeft: '1rem' }}
                type="button"
              >
                ✕
              </button>
            )}
          </div>
        ))}
        {options.length < 4 && (
          <div style={{ width: '100%', marginTop: '1rem', paddingLeft: '0.25rem' }}>
            <button
              className={styles.inlineButton}
              data-testid="question-add-option-btn"
              disabled={isPending}
              onClick={addOption}
              type="button"
            >
              + Agregar opción
            </button>
          </div>
        )}
      </div>

      {displayedError && (
        <p className={styles.formError} role="alert">{displayedError}</p>
      )}

      {/* Actions */}
      <div className={styles.missionDetailActions}>
        <button
          className={styles.primaryButton}
          data-testid="question-submit-btn"
          disabled={isPending}
          type="submit"
        >
          Guardar
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          type="button"
          onClick={onCancel}
        >
          Cancelar
        </button>
      </div>
    </form>
  )
}
