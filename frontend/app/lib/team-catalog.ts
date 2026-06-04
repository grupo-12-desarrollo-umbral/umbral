import type { PagedResult, TeamDto } from './definitions'

export async function listAllTeams(
  loadPage: (page: number, pageSize: number) => Promise<PagedResult<TeamDto>>,
  pageSize = 100,
): Promise<TeamDto[]> {
  const teams: TeamDto[] = []
  let page = 1

  while (true) {
    const result = await loadPage(page, pageSize)
    teams.push(...result.items)

    if (!result.hasNextPage) {
      return teams
    }

    page += 1
  }
}

export async function listActiveTeams(
  loadPage: (page: number, pageSize: number) => Promise<PagedResult<TeamDto>>,
  pageSize = 100,
): Promise<TeamDto[]> {
  const teams = await listAllTeams(loadPage, pageSize)
  return teams.filter((team) => team.isActive)
}
