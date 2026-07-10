import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('child_process', () => ({
  execSync: vi.fn(),
}))

import { execSync } from 'child_process'
import globalSetup, { runSql } from '../../setup/global-setup'

describe('runSql', () => {
  beforeEach(() => {
    vi.mocked(execSync).mockReset()
  })

  it('runs psql with ON_ERROR_STOP so seed failures abort setup', () => {
    runSql('mission_design', 'select 1;', 'seeded')

    expect(execSync).toHaveBeenCalledWith(
      'docker exec -i backend-postgres-1 psql -v ON_ERROR_STOP=1 -U postgres -d mission_design',
      {
        input: 'select 1;',
        stdio: ['pipe', 'pipe', 'pipe'],
        timeout: 20000,
      },
    )
  })

  it('seeds identity_access against the registered team tables', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('skip keycloak')))

    await globalSetup()

    expect(execSync).toHaveBeenCalled()
    expect(vi.mocked(execSync).mock.calls[0]?.[1]).toMatchObject({
      input: expect.stringContaining('DELETE FROM registered_team_memberships;'),
    })
    expect(vi.mocked(execSync).mock.calls[0]?.[1]).toMatchObject({
      input: expect.stringContaining('INSERT INTO registered_teams'),
    })

    vi.unstubAllGlobals()
  })
})
