import { describe, expect, it, vi } from 'vitest'

import { listActiveTeams, listAllTeams } from '@/app/lib/team-catalog'
import type { PagedResult, TeamDto } from '@/app/lib/definitions'

function createPageResult(
  items: TeamDto[],
  page: number,
  totalPages: number,
): PagedResult<TeamDto> {
  return {
    items,
    totalCount: totalPages * items.length,
    page,
    pageSize: items.length,
    totalPages,
    hasPreviousPage: page > 1,
    hasNextPage: page < totalPages,
  }
}

describe('team catalog pagination', () => {
  it('loads every page until pagination is exhausted', async () => {
    const firstPageItems: TeamDto[] = [
      {
        teamId: 'team-1',
        displayName: 'Gilded Owls',
        teamCode: 'OWLS',
        isActive: true,
        createdAt: '2026-06-04T00:00:00.000Z',
        updatedAt: '2026-06-04T00:00:00.000Z',
      },
    ]
    const secondPageItems: TeamDto[] = [
      {
        teamId: 'team-2',
        displayName: 'Maple Runners',
        teamCode: 'MAPLE',
        isActive: false,
        createdAt: '2026-06-04T00:00:00.000Z',
        updatedAt: '2026-06-04T00:00:00.000Z',
      },
    ]
    const loadPage = vi
      .fn<(page: number, pageSize: number) => Promise<PagedResult<TeamDto>>>()
      .mockResolvedValueOnce(createPageResult(firstPageItems, 1, 2))
      .mockResolvedValueOnce(createPageResult(secondPageItems, 2, 2))

    await expect(listAllTeams(loadPage)).resolves.toEqual([
      ...firstPageItems,
      ...secondPageItems,
    ])

    expect(loadPage).toHaveBeenNthCalledWith(1, 1, 100)
    expect(loadPage).toHaveBeenNthCalledWith(2, 2, 100)
  })

  it('returns only active teams after traversing all pages', async () => {
    const loadPage = vi
      .fn<(page: number, pageSize: number) => Promise<PagedResult<TeamDto>>>()
      .mockResolvedValueOnce(
        createPageResult(
          [
            {
              teamId: 'team-1',
              displayName: 'Gilded Owls',
              teamCode: 'OWLS',
              isActive: true,
              createdAt: '2026-06-04T00:00:00.000Z',
              updatedAt: '2026-06-04T00:00:00.000Z',
            },
            {
              teamId: 'team-2',
              displayName: 'Brass Lanterns',
              teamCode: 'BRASS',
              isActive: false,
              createdAt: '2026-06-04T00:00:00.000Z',
              updatedAt: '2026-06-04T00:00:00.000Z',
            },
          ],
          1,
          2,
        ),
      )
      .mockResolvedValueOnce(
        createPageResult(
          [
            {
              teamId: 'team-3',
              displayName: 'Iron Magnolias',
              teamCode: 'IRON',
              isActive: true,
              createdAt: '2026-06-04T00:00:00.000Z',
              updatedAt: '2026-06-04T00:00:00.000Z',
            },
          ],
          2,
          2,
        ),
      )

    await expect(listActiveTeams(loadPage)).resolves.toEqual([
      {
        teamId: 'team-1',
        displayName: 'Gilded Owls',
        teamCode: 'OWLS',
        isActive: true,
        createdAt: '2026-06-04T00:00:00.000Z',
        updatedAt: '2026-06-04T00:00:00.000Z',
      },
      {
        teamId: 'team-3',
        displayName: 'Iron Magnolias',
        teamCode: 'IRON',
        isActive: true,
        createdAt: '2026-06-04T00:00:00.000Z',
        updatedAt: '2026-06-04T00:00:00.000Z',
      },
    ])
  })
})
