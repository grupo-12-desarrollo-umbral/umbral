'use client'

import type { MissionDto } from '@/app/lib/definitions'
import styles from '../dashboard.module.css'

export function MissionTree({
  mission,
  onMutated,
}: {
  mission: MissionDto
  onMutated: (updated: MissionDto) => void
}) {
  void onMutated // consumed by NodeControls in 2.1

  return (
    <div className={styles.missionTree} data-testid="mission-tree">
      {mission.stages.length === 0 ? (
        <p data-testid="mission-tree-empty">No stages yet.</p>
      ) : (
        mission.stages.map((stage) => (
          <section key={stage.id} data-testid={`stage-node-${stage.id}`}>
            <h3>{stage.title}</h3>
            {stage.substages.map((substage) => (
              <div key={substage.id} data-testid={`substage-node-${substage.id}`}>
                <h4>
                  {substage.title}{' '}
                  <span className={styles.chip} data-testid={`substage-playmode-${substage.id}`}>
                    {substage.playMode}
                  </span>
                  {substage.playMode === 'Trivia' && substage.winnerScore !== null && (
                    <span> · winnerScore {substage.winnerScore}</span>
                  )}
                </h4>

                {/* TreasureHunt side-content */}
                <ul>
                  {substage.targets.map((target) => (
                    <li key={target.id} data-testid={`target-node-${target.id}`}>
                      {target.name} · {target.qrCode}
                      {target.clueId !== null && <span> · clue #{target.clueId}</span>}
                    </li>
                  ))}
                </ul>

                {/* Clue children */}
                <ul>
                  {substage.clues.map((clue) => (
                    <li key={clue.id} data-testid={`clue-node-${clue.id}`}>
                      {clue.title} — {clue.text} ({clue.visibilityPolicy})
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </section>
        ))
      )}
    </div>
  )
}
