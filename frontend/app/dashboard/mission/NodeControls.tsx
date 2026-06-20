'use client'

import { useState, useTransition } from 'react'
import type { MissionDto } from '@/app/lib/definitions'
import {
  addMissionNode,
  updateMissionNode,
  removeMissionNode,
} from '@/app/actions/mission-structure'
import styles from '../dashboard.module.css'

// Backend enum (Domain/Enums/ClueVisibilityPolicy.cs) — sent as the string name and
// parsed with Enum.Parse on the server. The UI never invents values outside this set.
const CLUE_VISIBILITY_POLICIES = ['VisibleWhenSubstageStarts', 'HiddenUntilOperatorRelease'] as const
type ClueVisibility = (typeof CLUE_VISIBILITY_POLICIES)[number]

type OnMutated = (updated: MissionDto) => void

export function nextSequenceOrder(siblings: { sequenceOrder: number }[]): number {
  return siblings.length === 0 ? 0 : Math.max(...siblings.map((s) => s.sequenceOrder)) + 1
}

// ---------------------------------------------------------------------------
// Add controls — one generic body; the public wrappers fix node type + testids.
// ---------------------------------------------------------------------------

function AddNodeControl({
  missionId,
  nextOrder,
  nodeType,
  stageId,
  substageId,
  triggerTestId,
  confirmTestId,
  triggerLabel,
  onMutated,
}: {
  missionId: number
  nextOrder: number
  nodeType: 'Stage' | 'Substage' | 'Clue'
  stageId?: number
  substageId?: number
  triggerTestId: string
  confirmTestId: string
  triggerLabel: string
  onMutated: OnMutated
}) {
  const isClue = nodeType === 'Clue'
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [clueText, setClueText] = useState('')
  const [visibility, setVisibility] = useState<ClueVisibility>('VisibleWhenSubstageStarts')
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  function reset() {
    setTitle('')
    setClueText('')
    setVisibility('VisibleWhenSubstageStarts')
    setError(null)
    setOpen(false)
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await addMissionNode(missionId, {
          nodeType,
          title: title.trim(),
          sequenceOrder: nextOrder,
          // A Substage carries its parent stageId; a Clue carries BOTH stageId and
          // substageId (the backend handler dereferences both — verified against source).
          ...(nodeType === 'Substage' ? { stageId } : {}),
          ...(nodeType === 'Clue'
            ? { stageId, substageId, clueText, clueVisibilityPolicy: visibility }
            : {}),
        })
        onMutated(updated) // full MissionResponse replaces panel state
        reset()
      } catch (e) {
        // Backend rejection (400/409 containment) surfaced verbatim; the UI never pre-empts it.
        setError(e instanceof Error ? e.message : 'Could not add node.')
      }
    })
  }

  if (!open) {
    return (
      <button
        className={styles.inlineButton}
        data-testid={triggerTestId}
        disabled={isPending}
        onClick={() => setOpen(true)}
        type="button"
      >
        {triggerLabel}
      </button>
    )
  }

  return (
    <div className={styles.confirmRow}>
      <input
        className={styles.inlineInput}
        data-testid="node-title-input"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        placeholder={`${nodeType} title`}
      />
      {isClue && (
        <>
          <input
            className={styles.inlineInput}
            data-testid="clue-text-input"
            value={clueText}
            onChange={(e) => setClueText(e.target.value)}
            placeholder="Clue text"
          />
          <select
            className={styles.inlineInput}
            data-testid="clue-visibility-input"
            value={visibility}
            onChange={(e) => setVisibility(e.target.value as ClueVisibility)}
          >
            {CLUE_VISIBILITY_POLICIES.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </>
      )}
      <button
        className={styles.smallButton}
        data-testid={confirmTestId}
        disabled={isPending || title.trim() === ''}
        onClick={submit}
        type="button"
      >
        Save
      </button>
      <button className={styles.inlineButton} disabled={isPending} onClick={reset} type="button">
        Cancel
      </button>
      {error && (
        <p className={styles.formError} role="alert" data-testid="node-error">
          {error}
        </p>
      )}
    </div>
  )
}

export function AddStageControl({
  missionId,
  nextOrder,
  onMutated,
}: {
  missionId: number
  nextOrder: number
  onMutated: OnMutated
}) {
  return (
    <AddNodeControl
      missionId={missionId}
      nextOrder={nextOrder}
      nodeType="Stage"
      triggerTestId="add-stage-btn"
      confirmTestId="confirm-add-stage-btn"
      triggerLabel="+ Add stage"
      onMutated={onMutated}
    />
  )
}

export function AddSubstageControl({
  missionId,
  stageId,
  nextOrder,
  onMutated,
}: {
  missionId: number
  stageId: number
  nextOrder: number
  onMutated: OnMutated
}) {
  return (
    <AddNodeControl
      missionId={missionId}
      nextOrder={nextOrder}
      nodeType="Substage"
      stageId={stageId}
      triggerTestId={`add-substage-btn-${stageId}`}
      confirmTestId={`confirm-add-substage-btn-${stageId}`}
      triggerLabel="+ Add substage"
      onMutated={onMutated}
    />
  )
}

export function AddClueControl({
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
  return (
    <AddNodeControl
      missionId={missionId}
      nextOrder={nextOrder}
      nodeType="Clue"
      stageId={stageId}
      substageId={substageId}
      triggerTestId={`add-clue-btn-${substageId}`}
      confirmTestId={`confirm-add-clue-btn-${substageId}`}
      triggerLabel="+ Add clue"
      onMutated={onMutated}
    />
  )
}

// ---------------------------------------------------------------------------
// Per-node edit + remove. Remove mirrors the panel's deactivate confirm pattern.
// ---------------------------------------------------------------------------

export function NodeRowControls({
  missionId,
  nodeId,
  nodeKind,
  initialTitle,
  initialSequenceOrder,
  initialClueText,
  initialVisibility,
  onMutated,
}: {
  missionId: number
  nodeId: number
  nodeKind: 'Stage' | 'Substage' | 'Clue'
  initialTitle: string
  initialSequenceOrder: number
  initialClueText?: string
  initialVisibility?: string
  onMutated: OnMutated
}) {
  const isClue = nodeKind === 'Clue'
  const [editing, setEditing] = useState(false)
  const [confirmRemove, setConfirmRemove] = useState(false)
  const [title, setTitle] = useState(initialTitle)
  const [sequenceOrder, setSequenceOrder] = useState(String(initialSequenceOrder))
  const [clueText, setClueText] = useState(initialClueText ?? '')
  const [visibility, setVisibility] = useState<ClueVisibility>(
    initialVisibility === 'HiddenUntilOperatorRelease'
      ? 'HiddenUntilOperatorRelease'
      : 'VisibleWhenSubstageStarts',
  )
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
        const updated = await updateMissionNode(missionId, nodeId, {
          title: title.trim(),
          sequenceOrder: seq,
          ...(isClue ? { clueText, clueVisibilityPolicy: visibility } : {}),
        })
        onMutated(updated)
        setEditing(false)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not update node.')
      }
    })
  }

  function remove() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await removeMissionNode(missionId, nodeId)
        onMutated(updated)
        setConfirmRemove(false)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not remove node.')
      }
    })
  }

  if (editing) {
    return (
      <span className={styles.confirmRow}>
        <input
          className={styles.inlineInput}
          data-testid="node-title-input"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Title"
        />
        <input
          className={styles.inlineInput}
          data-testid="node-sequence-input"
          type="number"
          value={sequenceOrder}
          onChange={(e) => setSequenceOrder(e.target.value)}
        />
        {isClue && (
          <>
            <input
              className={styles.inlineInput}
              data-testid="clue-text-input"
              value={clueText}
              onChange={(e) => setClueText(e.target.value)}
              placeholder="Clue text"
            />
            <select
              className={styles.inlineInput}
              data-testid="clue-visibility-input"
              value={visibility}
              onChange={(e) => setVisibility(e.target.value as ClueVisibility)}
            >
              {CLUE_VISIBILITY_POLICIES.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
          </>
        )}
        <button
          className={styles.smallButton}
          disabled={isPending || title.trim() === ''}
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
        {error && (
          <p className={styles.formError} role="alert" data-testid="node-error">
            {error}
          </p>
        )}
      </span>
    )
  }

  return (
    <span className={styles.nodeControls}>
      <button
        className={styles.inlineButton}
        data-testid={`edit-node-btn-${nodeId}`}
        disabled={isPending}
        onClick={() => {
          setError(null)
          setEditing(true)
        }}
        type="button"
      >
        Edit
      </button>
      {!confirmRemove && (
        <button
          className={styles.inlineButton}
          data-testid={`remove-node-btn-${nodeId}`}
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
            data-testid={`confirm-remove-node-btn-${nodeId}`}
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
      {error && (
        <p className={styles.formError} role="alert" data-testid="node-error">
          {error}
        </p>
      )}
    </span>
  )
}
