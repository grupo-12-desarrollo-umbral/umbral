'use client'

import type { MissionDto } from '@/app/lib/definitions'
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

export function MissionTree({
  mission,
  onMutated,
}: {
  mission: MissionDto
  onMutated: (updated: MissionDto) => void
}) {
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
                {stage.substages.map((substage) => (
                  <div
                    key={substage.id}
                    className={styles.treeSubstage}
                    data-testid={`substage-node-${substage.id}`}
                  >
                    <div className={styles.treeSubstageHead}>
                      <div className={styles.treeSubstageTitleGroup}>
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
                        {substage.playMode === 'Trivia' && substage.winnerScore !== null && (
                          <span className={styles.treeClueText}>
                            Winner score: {substage.winnerScore}
                          </span>
                        )}
                      </div>
                      <NodeRowControls
                        missionId={mission.id}
                        nodeId={substage.id}
                        nodeKind="Substage"
                        initialTitle={substage.title}
                        initialSequenceOrder={substage.sequenceOrder}
                        onMutated={onMutated}
                      />
                    </div>

                    {/* Play-mode + TreasureHunt/Trivia authoring (side-content) */}
                    <SubstageEditor
                      missionId={mission.id}
                      stageId={stage.id}
                      substage={substage}
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
                            <span className={styles.treeClueTitle}>{clue.title}</span>
                            <span className={styles.treeClueText}>{clue.text}</span>
                            <span className={styles.chip} data-tone="muted">
                              {clueVisibilityLabel(clue.visibilityPolicy)}
                            </span>
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
                ))}
              </div>
            )}

            <div className={styles.treeAddRow}>
              <AddSubstageControl
                missionId={mission.id}
                stageId={stage.id}
                nextOrder={nextSequenceOrder(stage.substages)}
                onMutated={onMutated}
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
