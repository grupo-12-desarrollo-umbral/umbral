'use client'

import { useEffect, useState, useTransition } from 'react'
import type { MissionDto, MissionSubstageDto, TriviaQuizSummaryDto } from '@/app/lib/definitions'
import {
  assignSubstagePlayMode,
  addTarget,
  updateTarget,
  removeTarget,
  associateClueWithTarget,
  unassociateClueFromTarget,
  setTriviaQuizSelection,
  updateTriviaQuizSelection,
} from '@/app/actions/mission-structure'
import { getTriviaQuizzes } from '@/app/actions/trivias'
import { nextSequenceOrder } from './NodeControls'
import { QrPreview } from './QrPreview'
import {
  PLAY_MODES,
  PLAY_MODE_LABELS,
  TARGET_QR_HELP,
  generateTargetQrCode,
  type PlayMode,
} from './labels'
import styles from '../dashboard.module.css'

type OnMutated = (updated: MissionDto) => void

export function SubstageEditor({
  missionId,
  stageId,
  substage,
  onMutated,
}: {
  missionId: number
  stageId: number
  substage: MissionSubstageDto
  onMutated: OnMutated
}) {
  return (
    <div className={styles.substageEditor}>
      <PlayModeControl
        missionId={missionId}
        stageId={stageId}
        substage={substage}
        onMutated={onMutated}
      />

      {substage.playMode === 'TreasureHunt' && (
        <div className={styles.treeSection}>
          <span className={styles.treeSectionLabel}>Targets</span>
          {substage.targets.length === 0 ? (
            <p className={styles.treeEmpty}>No targets yet.</p>
          ) : (
            substage.targets.map((target) => (
              <TargetRow
                key={target.id}
                missionId={missionId}
                stageId={stageId}
                substage={substage}
                target={target}
                onMutated={onMutated}
              />
            ))
          )}
          <div className={styles.treeAddRow}>
            <AddTargetControl
              missionId={missionId}
              stageId={stageId}
              substageId={substage.id}
              nextOrder={nextSequenceOrder(substage.targets)}
              onMutated={onMutated}
            />
          </div>
        </div>
      )}

      {substage.playMode === 'Trivia' && (
        <div className={styles.treeSection}>
          <span className={styles.treeSectionLabel}>Trivia quiz</span>
          <TriviaSelectionControl
            missionId={missionId}
            stageId={stageId}
            substage={substage}
            onMutated={onMutated}
          />
        </div>
      )}
    </div>
  )
}

// ---------------------------------------------------------------------------
// Trivia selection (Trivia play-mode only). A Trivia substage carries ONLY a
// published-quiz selection — winnerScore is a TreasureHunt concept (set via the
// target form), so it is intentionally absent here. The picker reuses the
// existing trivia client, filtered to Published quizzes.
// ---------------------------------------------------------------------------

function TriviaSelectionControl({
  missionId,
  stageId,
  substage,
  onMutated,
}: {
  missionId: number
  stageId: number
  substage: MissionSubstageDto
  onMutated: OnMutated
}) {
  const current = substage.triviaQuizSelection?.triviaQuizId ?? null
  const [quizzes, setQuizzes] = useState<TriviaQuizSummaryDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<string>(current === null ? '' : String(current))
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  useEffect(() => {
    let active = true
    getTriviaQuizzes()
      .then((all) => {
        if (active) setQuizzes(all.filter((q) => q.status === 'Published'))
      })
      .catch(() => {
        if (active) setLoadError('Could not load trivia quizzes.')
      })
    return () => {
      active = false
    }
  }, [])

  function save() {
    const quizId = Number(selected)
    if (selected === '' || Number.isNaN(quizId)) {
      setError('Select a published quiz.')
      return
    }
    startTransition(async () => {
      setError(null)
      try {
        const updated =
          current === null
            ? await setTriviaQuizSelection(missionId, stageId, substage.id, quizId)
            : await updateTriviaQuizSelection(missionId, stageId, substage.id, quizId)
        onMutated(updated)
      } catch (e) {
        // Selecting a non-published / missing quiz is API-enforced; surfaced verbatim.
        setError(e instanceof Error ? e.message : 'Could not save quiz selection.')
      }
    })
  }

  return (
    <div className={styles.playModeRow}>
      <label className={styles.nodeField}>
        <span className={styles.fieldLabel}>Quiz</span>
        <select
          className={styles.inlineInput}
          data-testid={`trivia-quiz-select-${substage.id}`}
          value={selected}
          disabled={isPending || quizzes === null}
          onChange={(e) => setSelected(e.target.value)}
        >
          <option value="">{quizzes === null ? 'Loading…' : 'Select quiz…'}</option>
          {(quizzes ?? []).map((quiz) => (
            <option key={quiz.id} value={quiz.id}>
              {quiz.title}
            </option>
          ))}
        </select>
      </label>

      <button
        className={styles.smallButton}
        disabled={isPending || selected === ''}
        onClick={save}
        type="button"
      >
        {current === null ? 'Select quiz' : 'Change quiz'}
      </button>

      {current !== null && (
        <span>
          Selected: {quizzes?.find((q) => q.id === current)?.title ?? `#${current}`}
        </span>
      )}

      {loadError && (
        <p className={styles.formError} role="alert" data-testid="node-error">
          {loadError}
        </p>
      )}
      {error && (
        <p className={styles.formError} role="alert" data-testid="node-error">
          {error}
        </p>
      )}
    </div>
  )
}

// ---------------------------------------------------------------------------
// Play-mode selector. Switching modes discards the other mode's content, so a
// change is staged behind an explicit warning the admin must confirm.
// ---------------------------------------------------------------------------

function PlayModeControl({
  missionId,
  stageId,
  substage,
  onMutated,
}: {
  missionId: number
  stageId: number
  substage: MissionSubstageDto
  onMutated: OnMutated
}) {
  const [pending, setPending] = useState<PlayMode | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  function onSelect(value: string) {
    const next = value as PlayMode
    setError(null)
    setPending(next === substage.playMode ? null : next)
  }

  function confirm() {
    if (pending === null) return
    const target = pending
    startTransition(async () => {
      setError(null)
      try {
        const updated = await assignSubstagePlayMode(missionId, stageId, substage.id, {
          playMode: target,
        })
        onMutated(updated)
        setPending(null)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not change play mode.')
      }
    })
  }

  return (
    <div className={styles.playModeRow}>
      <label className={styles.nodeField}>
        <span className={styles.fieldLabel}>Play mode</span>
        <select
          className={styles.inlineInput}
          data-testid={`playmode-select-${substage.id}`}
          value={pending ?? substage.playMode}
          disabled={isPending}
          onChange={(e) => onSelect(e.target.value)}
        >
          {PLAY_MODES.map((m) => (
            <option key={m} value={m}>
              {PLAY_MODE_LABELS[m]}
            </option>
          ))}
        </select>
      </label>

      {pending !== null && (
        <span className={styles.confirmRow}>
          <span role="alert" data-testid="playmode-switch-warning">
            Switching to {PLAY_MODE_LABELS[pending]} discards the other mode&rsquo;s content
            (targets, clue associations, trivia selection, and winner score).
          </span>
          <button
            className={styles.dangerButton}
            disabled={isPending}
            onClick={confirm}
            type="button"
          >
            Confirm switch
          </button>
          <button
            className={styles.inlineButton}
            disabled={isPending}
            onClick={() => {
              setPending(null)
              setError(null)
            }}
            type="button"
          >
            Cancel
          </button>
        </span>
      )}

      {error && (
        <p className={styles.formError} role="alert" data-testid="node-error">
          {error}
        </p>
      )}
    </div>
  )
}

// ---------------------------------------------------------------------------
// Target authoring (TreasureHunt only).
// ---------------------------------------------------------------------------

function AddTargetControl({
  missionId,
  stageId,
  substageId,
  nextOrder,
  onMutated,
}: {
  missionId: number
  stageId: number
  substageId: number
  nextOrder: number
  onMutated: OnMutated
}) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [qrCode, setQrCode] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [winnerScore, setWinnerScore] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  function reset() {
    setName('')
    setQrCode('')
    setIsActive(true)
    setWinnerScore('')
    setError(null)
    setOpen(false)
  }

  function submit() {
    const parsedScore = winnerScore.trim() === '' ? undefined : Number(winnerScore)
    if (parsedScore !== undefined && Number.isNaN(parsedScore)) {
      setError('Winner score must be a number.')
      return
    }
    startTransition(async () => {
      setError(null)
      try {
        const updated = await addTarget(missionId, stageId, substageId, {
          name: name.trim(),
          qrCode: qrCode.trim(),
          sequenceOrder: nextOrder,
          isActive,
          ...(parsedScore !== undefined ? { winnerScore: parsedScore } : {}),
        })
        onMutated(updated)
        reset()
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not add target.')
      }
    })
  }

  if (!open) {
    return (
      <button
        className={styles.inlineButton}
        data-testid={`add-target-btn-${substageId}`}
        disabled={isPending}
        onClick={() => setOpen(true)}
        type="button"
      >
        + Add target
      </button>
    )
  }

  return (
    <div className={styles.nodeForm}>
      <div className={styles.nodeFormGrid}>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Target name</span>
          <input
            className={styles.inlineInput}
            data-testid="target-name-input"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Target name"
          />
        </label>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Winner score (optional)</span>
          <input
            className={styles.inlineInput}
            data-testid="target-winnerscore-input"
            type="number"
            value={winnerScore}
            onChange={(e) => setWinnerScore(e.target.value)}
            placeholder="Winner score (optional)"
          />
        </label>
        <label className={styles.inlineCheck}>
          <input
            data-testid="target-active-input"
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
          />{' '}
          Active
        </label>
        <div className={styles.qrField}>
          <span className={styles.fieldLabel}>QR code</span>
          <div className={styles.qrInputRow}>
            <input
              className={styles.inlineInput}
              data-testid="target-qrcode-input"
              value={qrCode}
              onChange={(e) => setQrCode(e.target.value)}
              placeholder="QR code"
            />
            <button
              className={styles.smallButton}
              data-testid="generate-qr-btn-new"
              disabled={isPending}
              onClick={() => setQrCode(generateTargetQrCode())}
              type="button"
            >
              Generate
            </button>
          </div>
          <p className={styles.qrHelp}>{TARGET_QR_HELP}</p>
          <QrPreview code={qrCode} testId="qr-preview-new" />
        </div>
      </div>
      <div className={styles.nodeFormActions}>
        <button
          className={styles.smallButton}
          disabled={isPending || name.trim() === '' || qrCode.trim() === ''}
          onClick={submit}
          type="button"
        >
          Save
        </button>
        <button className={styles.inlineButton} disabled={isPending} onClick={reset} type="button">
          Cancel
        </button>
      </div>
      {error && (
        <p className={styles.formError} role="alert" data-testid="node-error">
          {error}
        </p>
      )}
    </div>
  )
}

function TargetRow({
  missionId,
  stageId,
  substage,
  target,
  onMutated,
}: {
  missionId: number
  stageId: number
  substage: MissionSubstageDto
  target: MissionSubstageDto['targets'][number]
  onMutated: OnMutated
}) {
  const [editing, setEditing] = useState(false)
  const [confirmRemove, setConfirmRemove] = useState(false)
  const [name, setName] = useState(target.name)
  const [qrCode, setQrCode] = useState(target.qrCode)
  const [sequenceOrder, setSequenceOrder] = useState(String(target.sequenceOrder))
  const [isActive, setIsActive] = useState(target.isActive)
  const [selectedClueId, setSelectedClueId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  function saveEdit() {
    const seq = parseInt(sequenceOrder, 10)
    if (Number.isNaN(seq)) {
      setError('Sequence order must be a number.')
      return
    }
    startTransition(async () => {
      setError(null)
      try {
        const updated = await updateTarget(missionId, stageId, substage.id, target.id, {
          name: name.trim(),
          qrCode: qrCode.trim(),
          sequenceOrder: seq,
          isActive,
        })
        onMutated(updated)
        setEditing(false)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not update target.')
      }
    })
  }

  function remove() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await removeTarget(missionId, stageId, substage.id, target.id)
        onMutated(updated)
        setConfirmRemove(false)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not remove target.')
      }
    })
  }

  function associate() {
    const clueId = Number(selectedClueId)
    if (selectedClueId === '' || Number.isNaN(clueId)) {
      setError('Select a clue to associate.')
      return
    }
    startTransition(async () => {
      setError(null)
      try {
        const updated = await associateClueWithTarget(
          missionId,
          stageId,
          substage.id,
          target.id,
          clueId,
        )
        onMutated(updated)
        setSelectedClueId('')
      } catch (e) {
        // Max one clue per target is API-enforced; its rejection is surfaced verbatim.
        setError(e instanceof Error ? e.message : 'Could not associate clue.')
      }
    })
  }

  function unassociate() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await unassociateClueFromTarget(missionId, stageId, substage.id, target.id)
        onMutated(updated)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not remove clue association.')
      }
    })
  }

  if (editing) {
    return (
      <div className={styles.nodeForm} data-testid={`target-node-${target.id}`}>
        <div className={styles.nodeFormGrid}>
          <label className={styles.nodeField}>
            <span className={styles.fieldLabel}>Target name</span>
            <input
              className={styles.inlineInput}
              data-testid="target-name-input"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Target name"
            />
          </label>
          <label className={styles.nodeField}>
            <span className={styles.fieldLabel}>Order</span>
            <input
              className={styles.inlineInput}
              data-testid="target-sequence-input"
              type="number"
              value={sequenceOrder}
              onChange={(e) => setSequenceOrder(e.target.value)}
            />
          </label>
          <label className={styles.inlineCheck}>
            <input
              data-testid="target-active-input"
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
            />{' '}
            Active
          </label>
          <div className={styles.qrField}>
            <span className={styles.fieldLabel}>QR code</span>
            <div className={styles.qrInputRow}>
              <input
                className={styles.inlineInput}
                data-testid="target-qrcode-input"
                value={qrCode}
                onChange={(e) => setQrCode(e.target.value)}
                placeholder="QR code"
              />
              <button
                className={styles.smallButton}
                data-testid={`generate-qr-btn-${target.id}`}
                disabled={isPending}
                onClick={() => setQrCode(generateTargetQrCode())}
                type="button"
              >
                Generate
              </button>
            </div>
            <p className={styles.qrHelp}>{TARGET_QR_HELP}</p>
            <QrPreview code={qrCode} testId={`qr-preview-${target.id}`} />
          </div>
        </div>
        <div className={styles.nodeFormActions}>
          <button
            className={styles.smallButton}
            disabled={isPending || name.trim() === '' || qrCode.trim() === ''}
            onClick={saveEdit}
            type="button"
          >
            Save
          </button>
          <button
            className={styles.inlineButton}
            disabled={isPending}
            onClick={() => {
              setEditing(false)
              setError(null)
            }}
            type="button"
          >
            Cancel
          </button>
        </div>
        {error && (
          <p className={styles.formError} role="alert" data-testid="node-error">
            {error}
          </p>
        )}
      </div>
    )
  }

  const associatedClueTitle =
    target.clueId !== null
      ? substage.clues.find((c) => c.id === target.clueId)?.title ?? null
      : null

  return (
    <div className={styles.treeTarget} data-testid={`target-node-${target.id}`}>
      <div className={styles.treeTargetMain}>
        <span className={styles.treeTargetInfo}>
          <span className={styles.treeClueTitle}>{target.name}</span>
          <span className={styles.qrPreviewCode}>{target.qrCode}</span>
          {!target.isActive && (
            <span className={styles.chip} data-tone="muted">
              Inactive
            </span>
          )}
          {target.clueId !== null && (
            <span className={styles.treeClueText}>
              clue #{target.clueId}
              {associatedClueTitle ? ` — ${associatedClueTitle}` : ''}
            </span>
          )}
        </span>

        <span className={styles.nodeControls}>
        <button
          className={styles.inlineButton}
          data-testid={`edit-target-btn-${target.id}`}
          disabled={isPending}
          onClick={() => {
            setError(null)
            setEditing(true)
          }}
          type="button"
        >
          Edit
        </button>

        {target.clueId === null ? (
          <>
            <select
              className={styles.inlineInput}
              data-testid={`clue-select-${target.id}`}
              value={selectedClueId}
              disabled={isPending || substage.clues.length === 0}
              onChange={(e) => setSelectedClueId(e.target.value)}
            >
              <option value="">Select clue…</option>
              {substage.clues.map((clue) => (
                <option key={clue.id} value={clue.id}>
                  {clue.title}
                </option>
              ))}
            </select>
            <button
              className={styles.inlineButton}
              data-testid={`associate-clue-btn-${target.id}`}
              disabled={isPending || substage.clues.length === 0}
              onClick={associate}
              type="button"
            >
              Associate clue
            </button>
          </>
        ) : (
          <button
            className={styles.inlineButton}
            data-testid={`unassociate-clue-btn-${target.id}`}
            disabled={isPending}
            onClick={unassociate}
            type="button"
          >
            Remove clue
          </button>
        )}

        {!confirmRemove && (
          <button
            className={styles.inlineButton}
            data-testid={`remove-target-btn-${target.id}`}
            disabled={isPending}
            onClick={() => setConfirmRemove(true)}
            type="button"
          >
            Remove
          </button>
        )}
        {confirmRemove && (
          <>
            <button
              className={styles.dangerButton}
              data-testid={`confirm-remove-target-btn-${target.id}`}
              disabled={isPending}
              onClick={remove}
              type="button"
            >
              Confirm remove
            </button>
            <button
              className={styles.inlineButton}
              disabled={isPending}
              onClick={() => setConfirmRemove(false)}
              type="button"
            >
              Cancel
            </button>
          </>
        )}
        </span>
      </div>

      <QrPreview code={target.qrCode} testId={`qr-preview-${target.id}`} />

      {error && (
        <p className={styles.formError} role="alert" data-testid="node-error">
          {error}
        </p>
      )}
    </div>
  )
}
