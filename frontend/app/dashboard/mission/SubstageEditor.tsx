'use client'

import { useEffect, useState, useTransition } from 'react'
import type {
  MissionDto,
  MissionSubstageDto,
  TriviaQuizDto,
  TriviaQuizSummaryDto,
} from '@/app/lib/definitions'
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
import { getTriviaQuizzes, getTriviaQuiz } from '@/app/actions/trivias'
import { TriviaQuestionList } from '../TriviaQuestionList'
import { nextSequenceOrder } from './NodeControls'
import { QrPreview } from './QrPreview'
import { TargetMap } from './TargetMap'
import { isPlacedCoordinate, type TargetMapMarker } from './target-map-html'
import {
  PLAY_MODES,
  PLAY_MODE_LABELS,
  TARGET_QR_HELP,
  generateTargetQrCode,
  type PlayMode,
} from './labels'
import styles from '../dashboard.module.css'

// Display labels for backend enums (values stay English on the wire).
const QUIZ_STATUS_LABELS: Record<string, string> = {
  Draft: 'Borrador',
  Published: 'Publicado',
  Archived: 'Archivado',
}
function quizStatusLabel(status: string) {
  return QUIZ_STATUS_LABELS[status] ?? status
}
const DIFFICULTY_DISPLAY: Record<string, string> = {
  Beginner: 'Principiante',
  Intermediate: 'Intermedio',
  Advanced: 'Avanzado',
}
function difficultyLabel(value: string) {
  return DIFFICULTY_DISPLAY[value] ?? value
}

// Parses a coordinate <input> value to a finite number, or null when blank/invalid. null means
// "unplaced": the request then sends 0 (the backend has no null coordinate — an unplaced target reads
// as 0,0), and the picker shows no draft pin.
function parseCoord(value: string): number | null {
  if (value.trim() === '') return null
  const n = Number(value)
  return Number.isFinite(n) ? n : null
}

// The map pins for a substage's already-placed targets, excluding the one being edited (passed as
// `excludeId`) since that target's live draft is shown separately as the draft pin. Targets with no
// location (stored as 0,0) are dropped, so they neither pin the ocean nor anchor the initial view away
// from the operator's own location.
function contextMarkers(
  targets: MissionSubstageDto['targets'],
  excludeId?: number,
): TargetMapMarker[] {
  return targets
    .filter((t) => t.id !== excludeId && isPlacedCoordinate(t.latitude, t.longitude))
    .map((t) => ({
      id: String(t.id),
      name: t.name,
      latitude: t.latitude,
      longitude: t.longitude,
    }))
}

type OnMutated = (updated: MissionDto) => void

export function SubstageEditor({
  missionId,
  stageId,
  substage,
  difficulty,
  onMutated,
  readOnly = false,
}: {
  missionId: number
  stageId: number
  substage: MissionSubstageDto
  difficulty: string
  onMutated: OnMutated
  // On a deactivated mission the editor is inspect-only: content (targets, quiz, map) still
  // renders, but every authoring affordance is withheld to match the HU-09 backend guard.
  readOnly?: boolean
}) {
  return (
    <div className={styles.substageEditor}>
      {!readOnly && (
        <PlayModeControl
          missionId={missionId}
          stageId={stageId}
          substage={substage}
          onMutated={onMutated}
        />
      )}

      {substage.playMode === 'TreasureHunt' && (
        <div className={styles.treeSection}>
          <span className={styles.treeSectionLabel}>Targets</span>
          {substage.targets.length === 0 ? (
            <p className={styles.treeEmpty}>Aún no hay targets.</p>
          ) : (
            substage.targets.map((target) => (
              <TargetRow
                key={target.id}
                missionId={missionId}
                stageId={stageId}
                substage={substage}
                target={target}
                difficulty={difficulty}
                onMutated={onMutated}
                readOnly={readOnly}
              />
            ))
          )}
          {!readOnly && (
            <div className={styles.treeAddRow}>
              <AddTargetControl
                missionId={missionId}
                stageId={stageId}
                substageId={substage.id}
                nextOrder={nextSequenceOrder(substage.targets)}
                difficulty={difficulty}
                siblings={substage.targets}
                onMutated={onMutated}
              />
            </div>
          )}

          {substage.targets.length > 0 && (
            <div className={styles.targetOverview}>
              <span className={styles.treeSectionLabel}>Vista general del mapa</span>
              <TargetMap
                markers={contextMarkers(substage.targets)}
                testId={`target-overview-map-${substage.id}`}
                label="Todos los targets en el mapa"
              />
            </div>
          )}
        </div>
      )}

      {substage.playMode === 'Trivia' && (
        <div className={styles.treeSection}>
          <span className={styles.treeSectionLabel}>Cuestionario de trivia</span>
          <TriviaSelectionControl
            missionId={missionId}
            stageId={stageId}
            substage={substage}
            onMutated={onMutated}
            readOnly={readOnly}
          />
        </div>
      )}
    </div>
  )
}

// ---------------------------------------------------------------------------
// Trivia selection (Trivia play-mode only). A Trivia substage carries ONLY a
// published-quiz selection — per-target score is a TreasureHunt concept (set via
// the target form), so it is intentionally absent here. The picker reuses the
// existing trivia client, filtered to Published quizzes.
// ---------------------------------------------------------------------------

function TriviaSelectionControl({
  missionId,
  stageId,
  substage,
  onMutated,
  readOnly = false,
}: {
  missionId: number
  stageId: number
  substage: MissionSubstageDto
  onMutated: OnMutated
  readOnly?: boolean
}) {
  const current = substage.triviaQuizSelection?.triviaQuizId ?? null
  const [quizzes, setQuizzes] = useState<TriviaQuizSummaryDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<string>(current === null ? '' : String(current))
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  // Quiz preview (issue #146): the full quiz — questions, options, correct answer —
  // fetched lazily on first expand and cached until the selection changes. `preview`
  // is only trusted when `preview.id === current`.
  const [preview, setPreview] = useState<TriviaQuizDto | null>(null)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [previewLoading, setPreviewLoading] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    getTriviaQuizzes()
      .then((all) => {
        if (active) setQuizzes(all.filter((q) => q.status === 'Published'))
      })
      .catch(() => {
        if (active) setLoadError('No se pudieron cargar los cuestionarios de trivia.')
      })
    return () => {
      active = false
    }
  }, [])

  function loadPreview(quizId: number) {
    setPreviewLoading(true)
    setPreviewError(null)
    getTriviaQuiz(quizId)
      .then((quiz) => setPreview(quiz))
      .catch(() => setPreviewError('No se pudo cargar la vista previa del cuestionario.'))
      .finally(() => setPreviewLoading(false))
  }

  function togglePreview() {
    if (current === null) return
    const willOpen = !previewOpen
    setPreviewOpen(willOpen)
    if (willOpen && (preview === null || preview.id !== current)) {
      loadPreview(current)
    }
  }

  function save() {
    const quizId = Number(selected)
    if (selected === '' || Number.isNaN(quizId)) {
      setError('Selecciona un cuestionario publicado.')
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
        // Selection changed: drop the stale preview and refresh it if it's open.
        setPreview(null)
        setPreviewError(null)
        if (previewOpen) loadPreview(quizId)
      } catch (e) {
        // Selecting a non-published / missing quiz is API-enforced; surfaced verbatim.
        setError(e instanceof Error ? e.message : 'No se pudo guardar la selección del cuestionario.')
      }
    })
  }

  return (
    <div className={styles.triviaSelect}>
      <div className={styles.playModeRow}>
        {!readOnly && (
          <>
            <label className={styles.nodeField}>
              <span className={styles.fieldLabel}>Cuestionario</span>
              <select
                className={styles.inlineInput}
                data-testid={`trivia-quiz-select-${substage.id}`}
                value={selected}
                disabled={isPending || quizzes === null}
                onChange={(e) => setSelected(e.target.value)}
              >
                <option value="">{quizzes === null ? 'Cargando…' : 'Selecciona un cuestionario…'}</option>
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
              {current === null ? 'Seleccionar cuestionario' : 'Cambiar cuestionario'}
            </button>
          </>
        )}

        {current !== null ? (
          <span>
            Seleccionado: {quizzes?.find((q) => q.id === current)?.title ?? `#${current}`}
          </span>
        ) : (
          readOnly && <span className={styles.treeClueText}>Ningún cuestionario seleccionado.</span>
        )}
      </div>

      {current !== null && (
        <div className={styles.triviaPreview} data-testid={`trivia-quiz-preview-${substage.id}`}>
          <button
            type="button"
            className={styles.qrPreviewToggle}
            aria-expanded={previewOpen}
            data-testid={`trivia-quiz-preview-toggle-${substage.id}`}
            onClick={togglePreview}
          >
            {previewOpen ? '▾ Ocultar preguntas' : '▸ Vista previa de preguntas'}
          </button>

          {previewOpen && (
            <div className={styles.triviaPreviewBody}>
              {previewLoading && <p className={styles.treeEmpty}>Cargando cuestionario…</p>}
              {previewError && (
                <p className={styles.formError} role="alert">
                  {previewError}
                </p>
              )}
              {preview && preview.id === current && !previewLoading && (
                <>
                  <div className={styles.triviaPreviewMeta}>
                    <span className={styles.treeClueTitle}>{preview.title}</span>
                    <span
                      className={styles.chip}
                      data-tone={preview.status === 'Published' ? 'success' : 'muted'}
                    >
                      {quizStatusLabel(preview.status)}
                    </span>
                  </div>
                  <TriviaQuestionList questions={preview.questions} />
                </>
              )}
            </div>
          )}
        </div>
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
        setError(e instanceof Error ? e.message : 'No se pudo cambiar el modo de juego.')
      }
    })
  }

  return (
    <div className={styles.playModeControl}>
      <div className={styles.playModeRow}>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Modo de juego</span>
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
      </div>

      {pending !== null && (
        <div className={styles.playModeWarning} role="alert" data-testid="playmode-switch-warning">
          <span>
            Cambiar a {PLAY_MODE_LABELS[pending]} descarta el contenido del otro modo
            (targets, asociaciones de pistas y selección de trivia).
          </span>
          <div className={styles.playModeWarningActions}>
            <button
              className={styles.dangerButton}
              disabled={isPending}
              onClick={confirm}
              type="button"
            >
              Confirmar cambio
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
              Cancelar
            </button>
          </div>
        </div>
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

// Coordinate authoring shared by the add and edit forms: two numeric inputs plus a Leaflet picker
// (clicking the map fills the inputs). Coordinates are display/context for the participant map (#156) —
// resolution stays QR-based — so placement is optional; an unplaced target sends 0,0.
function LocationField({
  latitude,
  longitude,
  onChange,
  markers,
  testIdPrefix,
}: {
  latitude: string
  longitude: string
  onChange: (latitude: string, longitude: string) => void
  markers: TargetMapMarker[]
  testIdPrefix: string
}) {
  const lat = parseCoord(latitude)
  const lng = parseCoord(longitude)
  // A target saved without a location reads back as "0"/"0", which parses as a valid coordinate. Treat
  // it as unplaced so the map falls through to the operator's own location instead of Null Island.
  const draft =
    lat !== null && lng !== null && isPlacedCoordinate(lat, lng) ? { latitude: lat, longitude: lng } : null

  return (
    <div className={styles.locationField}>
      <span className={styles.fieldLabel}>Ubicación en el mapa</span>
      <div className={styles.coordRow}>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Latitud</span>
          <input
            className={styles.inlineInput}
            data-testid={`${testIdPrefix}-latitude-input`}
            type="number"
            step="any"
            inputMode="decimal"
            value={latitude}
            onChange={(e) => onChange(e.target.value, longitude)}
            placeholder="-90 a 90"
          />
        </label>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Longitud</span>
          <input
            className={styles.inlineInput}
            data-testid={`${testIdPrefix}-longitude-input`}
            type="number"
            step="any"
            inputMode="decimal"
            value={longitude}
            onChange={(e) => onChange(latitude, e.target.value)}
            placeholder="-180 a 180"
          />
        </label>
      </div>
      <TargetMap
        interactive
        markers={markers}
        draft={draft}
        onPick={(pickedLat, pickedLng) =>
          onChange(pickedLat.toFixed(6), pickedLng.toFixed(6))
        }
        testId={`${testIdPrefix}-map`}
        label="Elegir la ubicación del target"
      />
      <p className={styles.qrHelp}>
        Haz clic en el mapa para colocar el pin, o escribe las coordenadas. Déjalo en blanco si este target no tiene ubicación.
      </p>
    </div>
  )
}

// A target's score is not authored: the backend derives it from the mission's
// difficulty as BASE_TARGET_SCORE * factor, where the factor is the 1-based tier
// (Beginner=1, Intermediate=2, Advanced=3). Mirrored here only to preview the value
// the server will assign; the server remains the source of truth.
const BASE_TARGET_SCORE = 50
const DIFFICULTY_TIERS = ['Beginner', 'Intermediate', 'Advanced']

function deriveTargetScore(difficulty: string): number | null {
  const tier = DIFFICULTY_TIERS.findIndex((d) => d.toLowerCase() === difficulty.toLowerCase())
  return tier === -1 ? null : BASE_TARGET_SCORE * (tier + 1)
}

function AddTargetControl({
  missionId,
  stageId,
  substageId,
  nextOrder,
  difficulty,
  siblings,
  onMutated,
}: {
  missionId: number
  stageId: number
  substageId: number
  nextOrder: number
  difficulty: string
  siblings: MissionSubstageDto['targets']
  onMutated: OnMutated
}) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [qrCode, setQrCode] = useState('')
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  const derivedScore = deriveTargetScore(difficulty)

  function reset() {
    setName('')
    setQrCode('')
    setLatitude('')
    setLongitude('')
    setIsActive(true)
    setError(null)
    setOpen(false)
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await addTarget(missionId, stageId, substageId, {
          name: name.trim(),
          qrCode: qrCode.trim(),
          sequenceOrder: nextOrder,
          latitude: parseCoord(latitude) ?? 0,
          longitude: parseCoord(longitude) ?? 0,
          isActive,
        })
        onMutated(updated)
        reset()
      } catch (e) {
        setError(e instanceof Error ? e.message : 'No se pudo agregar el target.')
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
        + Agregar target
      </button>
    )
  }

  return (
    <div className={`${styles.nodeForm} ${styles.nodeFormWide}`}>
      <div className={styles.nodeFormGrid}>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Nombre del target</span>
          <input
            className={styles.inlineInput}
            data-testid="target-name-input"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Nombre del target"
          />
        </label>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Puntaje</span>
          <span className={styles.inlineInput} data-testid="target-score-derived" aria-readonly="true">
            {derivedScore ?? '—'} <small>(definido por la dificultad {difficultyLabel(difficulty)})</small>
          </span>
        </label>
        <label className={styles.inlineCheck}>
          <input
            data-testid="target-active-input"
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
          />{' '}
          Activo
        </label>
        <div className={styles.qrField}>
          <span className={styles.fieldLabel}>Código QR</span>
          <div className={styles.qrInputRow}>
            <input
              className={styles.inlineInput}
              data-testid="target-qrcode-input"
              value={qrCode}
              onChange={(e) => setQrCode(e.target.value)}
              placeholder="Código QR"
            />
            <button
              className={styles.smallButton}
              data-testid="generate-qr-btn-new"
              disabled={isPending}
              onClick={() => setQrCode(generateTargetQrCode())}
              type="button"
            >
              Generar
            </button>
          </div>
          <p className={styles.qrHelp}>{TARGET_QR_HELP}</p>
          <QrPreview code={qrCode} testId="qr-preview-new" />
        </div>
      </div>
      <LocationField
        latitude={latitude}
        longitude={longitude}
        onChange={(lat, lng) => {
          setLatitude(lat)
          setLongitude(lng)
        }}
        markers={contextMarkers(siblings)}
        testIdPrefix="add-target"
      />
      <div className={styles.nodeFormActions}>
        <button
          className={styles.smallButton}
          disabled={isPending || name.trim() === '' || qrCode.trim() === ''}
          onClick={submit}
          type="button"
        >
          Guardar
        </button>
        <button className={styles.inlineButton} disabled={isPending} onClick={reset} type="button">
          Cancelar
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

// The edit form is a separate component so it mounts when the row enters edit
// mode: every field seeds from the current `target` on that mount. Holding this
// state in the always-mounted TargetRow would freeze it at first render and
// re-open the form with values that `onMutated` has since replaced.
function TargetEditForm({
  missionId,
  stageId,
  substageId,
  clues,
  siblings,
  target,
  onMutated,
  onDone,
}: {
  missionId: number
  stageId: number
  substageId: number
  clues: MissionSubstageDto['clues']
  siblings: MissionSubstageDto['targets']
  target: MissionSubstageDto['targets'][number]
  onMutated: OnMutated
  onDone: () => void
}) {
  const [name, setName] = useState(target.name)
  const [qrCode, setQrCode] = useState(target.qrCode)
  const [sequenceOrder, setSequenceOrder] = useState(String(target.sequenceOrder))
  const [latitude, setLatitude] = useState(String(target.latitude))
  const [longitude, setLongitude] = useState(String(target.longitude))
  const [isActive, setIsActive] = useState(target.isActive)
  // '' = no clue. Seeded from the current association so a save can change or clear it.
  const [clueId, setClueId] = useState(target.clueId === null ? '' : String(target.clueId))
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  function saveEdit() {
    const seq = parseInt(sequenceOrder, 10)
    if (Number.isNaN(seq)) {
      setError('El orden de secuencia debe ser un número.')
      return
    }
    startTransition(async () => {
      setError(null)
      try {
        let updated = await updateTarget(missionId, stageId, substageId, target.id, {
          name: name.trim(),
          qrCode: qrCode.trim(),
          sequenceOrder: seq,
          latitude: parseCoord(latitude) ?? 0,
          longitude: parseCoord(longitude) ?? 0,
          isActive,
        })
        // Reconcile the clue association against the picker. Max one clue per target,
        // so a change means clearing the old link before adding the new one.
        const desired = clueId === '' ? null : Number(clueId)
        if (desired !== target.clueId) {
          if (target.clueId !== null) {
            updated = await unassociateClueFromTarget(missionId, stageId, substageId, target.id)
          }
          if (desired !== null) {
            updated = await associateClueWithTarget(
              missionId,
              stageId,
              substageId,
              target.id,
              desired,
            )
          }
        }
        onMutated(updated)
        onDone()
      } catch (e) {
        setError(e instanceof Error ? e.message : 'No se pudo actualizar el target.')
      }
    })
  }

  return (
    <div className={styles.nodeForm} data-testid={`target-node-${target.id}`}>
      <div className={styles.nodeFormGrid}>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Nombre del target</span>
          <input
            className={styles.inlineInput}
            data-testid="target-name-input"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Nombre del target"
          />
        </label>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Orden</span>
          <input
            className={styles.inlineInput}
            data-testid="target-sequence-input"
            type="number"
            value={sequenceOrder}
            onChange={(e) => setSequenceOrder(e.target.value)}
          />
        </label>
        <label className={styles.nodeField}>
          <span className={styles.fieldLabel}>Pista asociada</span>
          <select
            className={styles.inlineInput}
            data-testid={`clue-select-${target.id}`}
            value={clueId}
            disabled={isPending || clues.length === 0}
            onChange={(e) => setClueId(e.target.value)}
          >
            <option value="">{clues.length === 0 ? 'Aún no hay pistas' : 'Sin pista'}</option>
            {clues.map((clue) => (
              <option key={clue.id} value={clue.id}>
                {clue.title}
              </option>
            ))}
          </select>
        </label>
        <label className={styles.inlineCheck}>
          <input
            data-testid="target-active-input"
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
          />{' '}
          Activo
        </label>
        <div className={styles.qrField}>
          <span className={styles.fieldLabel}>Código QR</span>
          <div className={styles.qrInputRow}>
            <input
              className={styles.inlineInput}
              data-testid="target-qrcode-input"
              value={qrCode}
              onChange={(e) => setQrCode(e.target.value)}
              placeholder="Código QR"
            />
            <button
              className={styles.smallButton}
              data-testid={`generate-qr-btn-${target.id}`}
              disabled={isPending}
              onClick={() => setQrCode(generateTargetQrCode())}
              type="button"
            >
              Generar
            </button>
          </div>
          <p className={styles.qrHelp}>{TARGET_QR_HELP}</p>
          <QrPreview code={qrCode} testId={`qr-preview-${target.id}`} />
        </div>
      </div>
      <LocationField
        latitude={latitude}
        longitude={longitude}
        onChange={(lat, lng) => {
          setLatitude(lat)
          setLongitude(lng)
        }}
        markers={contextMarkers(siblings, target.id)}
        testIdPrefix={`edit-target-${target.id}`}
      />
      <div className={styles.nodeFormActions}>
        <button
          className={styles.smallButton}
          disabled={isPending || name.trim() === '' || qrCode.trim() === ''}
          onClick={saveEdit}
          type="button"
        >
          Guardar
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          onClick={onDone}
          type="button"
        >
          Cancelar
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
  difficulty,
  onMutated,
  readOnly = false,
}: {
  missionId: number
  stageId: number
  substage: MissionSubstageDto
  target: MissionSubstageDto['targets'][number]
  difficulty: string
  onMutated: OnMutated
  readOnly?: boolean
}) {
  const [editing, setEditing] = useState(false)
  const [confirmRemove, setConfirmRemove] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  function remove() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await removeTarget(missionId, stageId, substage.id, target.id)
        onMutated(updated)
        setConfirmRemove(false)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'No se pudo eliminar el target.')
      }
    })
  }

  if (editing) {
    return (
      <TargetEditForm
        missionId={missionId}
        stageId={stageId}
        substageId={substage.id}
        clues={substage.clues}
        siblings={substage.targets}
        target={target}
        onMutated={onMutated}
        onDone={() => setEditing(false)}
      />
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
          <span className={styles.treeClueText} data-testid={`target-score-${target.id}`}>
            Puntaje: {target.score} ({difficultyLabel(difficulty)})
          </span>
          <span className={styles.treeClueText} data-testid={`target-location-${target.id}`}>
            {isPlacedCoordinate(target.latitude, target.longitude)
              ? `📍 ${target.latitude.toFixed(5)}, ${target.longitude.toFixed(5)}`
              : 'Sin ubicación definida'}
          </span>
          {!target.isActive && (
            <span className={styles.chip} data-tone="muted">
              Inactivo
            </span>
          )}
          {target.clueId !== null && (
            <span className={styles.treeClueText}>
              pista #{target.clueId}
              {associatedClueTitle ? ` — ${associatedClueTitle}` : ''}
            </span>
          )}
        </span>

        {!readOnly && (
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
          Editar
        </button>

        {!confirmRemove && (
          <button
            className={styles.inlineButton}
            data-testid={`remove-target-btn-${target.id}`}
            disabled={isPending}
            onClick={() => setConfirmRemove(true)}
            type="button"
          >
            Eliminar
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
              Confirmar eliminación
            </button>
            <button
              className={styles.inlineButton}
              disabled={isPending}
              onClick={() => setConfirmRemove(false)}
              type="button"
            >
              Cancelar
            </button>
          </>
        )}
        </span>
        )}
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
