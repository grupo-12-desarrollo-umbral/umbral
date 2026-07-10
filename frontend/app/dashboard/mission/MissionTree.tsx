'use client'

import { useState } from 'react'
import type { MissionDto, MissionSubstageDto } from '@/app/lib/definitions'
import {
  AddStageControl,
  AddSubstageControl,
  AddClueControl,
  NodeRowControls,
  nextSequenceOrder,
} from './NodeControls'
import { SubstageEditor } from './SubstageEditor'
import { PLAY_MODE_LABELS, clueVisibilityLabel } from './labels'
import styles from '../dashboard.module.css'

// One-line summary shown under a collapsed substage: what content it holds without
// having to open it. TreasureHunt counts targets; Trivia reports whether a quiz is set.
function substageSummary(substage: MissionSubstageDto): string {
  const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`
  const lead =
    substage.playMode === 'Trivia'
      ? substage.triviaQuizSelection
        ? 'Quiz selected'
        : 'No quiz'
      : plural(substage.targets.length, 'target')
  return `${lead} · ${plural(substage.clues.length, 'clue')}`
}

export function MissionTree({
  mission,
  onMutated,
}: {
  mission: MissionDto
  onMutated: (updated: MissionDto) => void
}) {
  // Accordion: at most one substage's authoring body is expanded at a time. Newly
  // added substages auto-open so the author can configure them immediately.
  const [openSubstageId, setOpenSubstageId] = useState<number | null>(null)

  return (
    <div className={styles.missionTree} data-testid="mission-tree">
      {mission.stages.length === 0 ? (
        <p data-testid="mission-tree-empty" className={styles.treeEmpty}>
          No stages yet.
        </p>
      ) : (
        mission.stages.map((stage) => (
          <section
            key={stage.id}
            className={styles.treeStage}
            data-testid={`stage-node-${stage.id}`}
          >
            <div className={styles.treeStageHead}>
              <div>
                <span className={styles.treeEyebrow}>Stage {stage.sequenceOrder}</span>
                <h3 className={styles.treeNodeTitle}>{stage.title}</h3>
              </div>
              <NodeRowControls
                missionId={mission.id}
                nodeId={stage.id}
                nodeKind="Stage"
                initialTitle={stage.title}
                initialSequenceOrder={stage.sequenceOrder}
                onMutated={onMutated}
              />
            </div>

            {stage.substages.length > 0 && (
              <div className={styles.treeSubstageRail}>
                {stage.substages.map((substage) => {
                  const isOpen = openSubstageId === substage.id
                  return (
                  <div
                    key={substage.id}
                    className={styles.treeSubstage}
                    data-open={isOpen}
                    data-testid={`substage-node-${substage.id}`}
                  >
                    <div className={styles.treeSubstageHead}>
                      <button
                        type="button"
                        className={styles.substageToggle}
                        aria-expanded={isOpen}
                        aria-controls={`substage-body-${substage.id}`}
                        data-testid={`toggle-substage-${substage.id}`}
                        onClick={() => setOpenSubstageId(isOpen ? null : substage.id)}
                      >
                        <span className={styles.substageToggleCaret} aria-hidden="true">
                          {isOpen ? '▾' : '▸'}
                        </span>
                        <span className={styles.treeEyebrow}>Substage {substage.sequenceOrder}</span>
                        <span className={styles.treeNodeTitle}>{substage.title}</span>
                        <span
                          className={styles.chip}
                          data-tone={substage.playMode === 'Trivia' ? 'accent' : 'success'}
                          data-playmode={substage.playMode}
                          data-testid={`substage-playmode-${substage.id}`}
                        >
                          {PLAY_MODE_LABELS[substage.playMode]}
                        </span>
                      </button>
                      <NodeRowControls
                        missionId={mission.id}
                        nodeId={substage.id}
                        nodeKind="Substage"
                        initialTitle={substage.title}
                        initialSequenceOrder={substage.sequenceOrder}
                        onMutated={onMutated}
                      />
                    </div>

                    {isOpen ? (
                      <div id={`substage-body-${substage.id}`} className={styles.treeSubstageBody}>
                        {/* Play-mode + TreasureHunt/Trivia authoring (side-content) */}
                        <SubstageEditor
                          missionId={mission.id}
                          stageId={stage.id}
                          substage={substage}
                          difficulty={mission.difficulty}
                          onMutated={onMutated}
                        />

                        {/* Clue children */}
                        <div className={styles.treeSection}>
                          <span className={styles.treeSectionLabel}>Clues</span>
                          {substage.clues.length === 0 ? (
                            <p className={styles.treeEmpty}>No clues yet.</p>
                          ) : (
                            substage.clues.map((clue) => (
                              <div
                                key={clue.id}
                                className={styles.treeClue}
                                data-testid={`clue-node-${clue.id}`}
                              >
                                <div className={styles.treeClueInfo}>
                                  <span className={styles.treeClueTitle}>{clue.title}</span>
                                  {clue.text && (
                                    <span className={styles.treeClueText}>{clue.text}</span>
                                  )}
                                  <span className={styles.treeClueVisibility}>
                                    {clueVisibilityLabel(clue.visibilityPolicy)}
                                  </span>
                                </div>
                                <NodeRowControls
                                  missionId={mission.id}
                                  nodeId={clue.id}
                                  nodeKind="Clue"
                                  initialTitle={clue.title}
                                  initialSequenceOrder={clue.sequenceOrder}
                                  initialClueText={clue.text}
                                  initialVisibility={clue.visibilityPolicy}
                                  onMutated={onMutated}
                                />
                              </div>
                            ))
                          )}
                          <div className={styles.treeAddRow}>
                            <AddClueControl
                              missionId={mission.id}
                              stageId={stage.id}
                              substageId={substage.id}
                              nextOrder={nextSequenceOrder(substage.clues)}
                              onMutated={onMutated}
                            />
                          </div>
                        </div>
                      </div>
                    ) : (
                      <p className={styles.substageSummary}>{substageSummary(substage)}</p>
                    )}
                  </div>
                  )
                })}
              </div>
            )}

            <div className={styles.treeAddRow}>
              <AddSubstageControl
                missionId={mission.id}
                stageId={stage.id}
                nextOrder={nextSequenceOrder(stage.substages)}
                onMutated={(updated) => {
                  // Auto-open the substage just created so its editor is immediately usable.
                  const prevIds = new Set(stage.substages.map((s) => s.id))
                  const added = updated.stages
                    .find((s) => s.id === stage.id)
                    ?.substages.find((s) => !prevIds.has(s.id))
                  if (added) setOpenSubstageId(added.id)
                  onMutated(updated)
                }}
              />
            </div>
          </section>
        ))
      )}

      <div className={styles.treeAddRow}>
        <AddStageControl
          missionId={mission.id}
          nextOrder={nextSequenceOrder(mission.stages)}
          onMutated={onMutated}
        />
      </div>
    </div>
  )
}
