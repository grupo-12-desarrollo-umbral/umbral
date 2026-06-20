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
        <p data-testid="mission-tree-empty">No stages yet.</p>
      ) : (
        mission.stages.map((stage) => (
          <section key={stage.id} data-testid={`stage-node-${stage.id}`}>
            <h3>
              {stage.title}{' '}
              <NodeRowControls
                missionId={mission.id}
                nodeId={stage.id}
                nodeKind="Stage"
                initialTitle={stage.title}
                initialSequenceOrder={stage.sequenceOrder}
                onMutated={onMutated}
              />
            </h3>

            {stage.substages.map((substage) => (
              <div key={substage.id} data-testid={`substage-node-${substage.id}`}>
                <h4>
                  {substage.title}{' '}
                  <span className={styles.chip} data-testid={`substage-playmode-${substage.id}`}>
                    {substage.playMode}
                  </span>
                  {substage.playMode === 'Trivia' && substage.winnerScore !== null && (
                    <span> · winnerScore {substage.winnerScore}</span>
                  )}{' '}
                  <NodeRowControls
                    missionId={mission.id}
                    nodeId={substage.id}
                    nodeKind="Substage"
                    initialTitle={substage.title}
                    initialSequenceOrder={substage.sequenceOrder}
                    onMutated={onMutated}
                  />
                </h4>

                {/* Play-mode + TreasureHunt target authoring (side-content) */}
                <SubstageEditor
                  missionId={mission.id}
                  stageId={stage.id}
                  substage={substage}
                  onMutated={onMutated}
                />

                {/* Clue children */}
                <ul>
                  {substage.clues.map((clue) => (
                    <li key={clue.id} data-testid={`clue-node-${clue.id}`}>
                      {clue.title} — {clue.text} ({clue.visibilityPolicy}){' '}
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
                    </li>
                  ))}
                </ul>

                <AddClueControl
                  missionId={mission.id}
                  stageId={stage.id}
                  substageId={substage.id}
                  nextOrder={nextSequenceOrder(substage.clues)}
                  onMutated={onMutated}
                />
              </div>
            ))}

            <AddSubstageControl
              missionId={mission.id}
              stageId={stage.id}
              nextOrder={nextSequenceOrder(stage.substages)}
              onMutated={onMutated}
            />
          </section>
        ))
      )}

      <AddStageControl
        missionId={mission.id}
        nextOrder={nextSequenceOrder(mission.stages)}
        onMutated={onMutated}
      />
    </div>
  )
}
