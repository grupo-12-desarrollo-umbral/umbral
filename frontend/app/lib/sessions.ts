import 'server-only'
import {
  IdentityError,
  type CreateSessionRequest,
  type SessionCreatedDto,
  type SessionAssignmentSummaryDto,
  type SessionAssociatedTeamsDto,
  type AssociateTeamToSessionResultDto,
  type AssignSessionOperatorResultDto,
  type SessionLifecycleState,
  type TransitionSessionStateResultDto,
  type SessionTimerSnapshotDto,
  type TriviaAnsweredMonitorDto,
  type TriviaAnswerReviewDto,
  type OperatorSessionPanelDto,
  type ReleaseClueRequest,
  type ReleaseClueResultDto,
  type ReleasableCluesDto,
  type AddOperativeClueRequest,
  type AddOperativeClueResultDto,
  type ApplyPenaltyRequest,
  type AppliedPenaltyDto,
  type RankingSnapshotDto,
  type EvidenceTraceDto,
  type SessionHistoryDto,
} from './definitions'
import { verifySession } from './dal'
import { API_GATEWAY_URL, getGatewayHeaders } from './gateway'

export async function createSession(
  req: CreateSessionRequest,
): Promise<SessionCreatedDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
    method: 'POST',
    headers: await getGatewayHeaders({
      'Content-Type': 'application/json',
    }),
    body: JSON.stringify(req),
  })

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (response.status === 404) throw new Error('mission_not_found')
  if (response.status === 409 || response.status === 422) {
    // Mission is the only session source now, so the eligibility/readiness rejection can arrive
    // as either 409 or 422 (mission inactive / not runtime-ready). Both map to one message.
    throw new Error('mission_not_eligible')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createSession failed with status ${response.status}`)
  }

  return response.json() as Promise<SessionCreatedDto>
}

export async function listAssignableSessions(): Promise<SessionAssignmentSummaryDto[]> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (!response.ok) {
    throw new IdentityError('unknown', `listAssignableSessions failed with status ${response.status}`)
  }
  return response.json() as Promise<SessionAssignmentSummaryDto[]>
}

export async function listOperatorSessions(): Promise<SessionAssignmentSummaryDto[]> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator session listing unavailable.')
  if (!response.ok) {
    throw new IdentityError('unknown', `listOperatorSessions failed with status ${response.status}`)
  }
  return response.json() as Promise<SessionAssignmentSummaryDto[]>
}

export async function assignSessionOperator(
  liveSessionId: string,
  operatorUserId: number,
): Promise<AssignSessionOperatorResultDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/operator-assignment`,
    {
      method: 'PATCH',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ operatorUserId }),
    },
  )

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 422) throw new Error('ineligible_operator')
  if (!response.ok) {
    throw new IdentityError('unknown', `assignSessionOperator failed with status ${response.status}`)
  }
  return response.json() as Promise<AssignSessionOperatorResultDto>
}

export async function transitionSessionState(
  liveSessionId: string,
  targetState: SessionLifecycleState,
  reason?: string,
): Promise<TransitionSessionStateResultDto> {
  await verifySession()
  const body = reason?.trim()
    ? { targetState, reason: reason.trim() }
    : { targetState }
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions/${liveSessionId}/state`, {
    method: 'PATCH',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(body),
  })

  if (response.status === 400) throw new Error('invalid_payload')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new Error('not_assigned_operator')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 409) {
    // The backend tags each transition conflict with a stable ProblemDetails `type` so the UI can
    // show a specific cause (no teams, no assigned operator) rather than one vague message. For the
    // generic invalid-transition case, `detail` also carries the specific rejected from->to edge
    // (InvalidSessionStateTransitionException.PublicDetail) — passed through via Error.cause so the
    // caller can render it instead of a generic "not allowed" message.
    const problem = (await response.json().catch(() => null)) as { type?: string; detail?: string } | null
    switch (problem?.type) {
      case 'session-no-teams':
        throw new Error('no_teams')
      case 'session-operator-unassigned':
        throw new Error('session_unassigned')
      default:
        throw new Error('invalid_transition', { cause: problem?.detail })
    }
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `transitionSessionState failed with status ${response.status}`)
  }

  return response.json() as Promise<TransitionSessionStateResultDto>
}

export async function getOperatorSessionTimerSnapshot(
  liveSessionId: string,
): Promise<SessionTimerSnapshotDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/timer`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Timer read: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Timer read: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (!response.ok) throw new IdentityError('unknown', `Timer read failed with status ${response.status}`)

  return response.json() as Promise<SessionTimerSnapshotDto>
}

// HU-36A operator answered/not-answered board snapshot. Mirrors getOperatorSessionTimerSnapshot's
// gateway path + auth/status mapping. Distinct case: 409 = no trivia question is currently active
// (the backend throws Conflict, so there is no body) — surfaced as a typed error the action turns
// into the board's empty state rather than an error state.
export async function getOperatorTriviaAnsweredMonitor(
  liveSessionId: string,
): Promise<TriviaAnsweredMonitorDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/answered-monitor`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Answered monitor: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Answered monitor: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (response.status === 409) throw new Error('no_active_question')
  if (!response.ok) throw new IdentityError('unknown', `Answered monitor read failed with status ${response.status}`)

  return response.json() as Promise<TriviaAnsweredMonitorDto>
}

// HU-24A operator live session panel snapshot. Mirrors getOperatorSessionTimerSnapshot's gateway
// path + auth/status mapping. 403 = a non-owning operator: the ownership Proxy denies the read.
export async function getOperatorSessionPanel(
  liveSessionId: string,
): Promise<OperatorSessionPanelDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/operator-panel`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Operator panel: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator panel: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (!response.ok) throw new IdentityError('unknown', `Operator panel read failed with status ${response.status}`)

  return response.json() as Promise<OperatorSessionPanelDto>
}

// HU-36B operator post-close answer review. Mirrors getOperatorSessionPanel's gateway path + auth/
// status mapping. 403 = a non-owning operator: the ownership Proxy denies the read.
// 409 = question not yet closed or unavailable (backend throws Conflict).
export async function getTriviaAnswerReview(
  liveSessionId: string,
  questionSequenceOrder: number,
): Promise<TriviaAnswerReviewDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/trivia/questions/${questionSequenceOrder}/answer-review`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Answer review: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Answer review: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (response.status === 409) throw new Error('question_not_closed')
  if (!response.ok) throw new IdentityError('unknown', `Answer review read failed with status ${response.status}`)

  return response.json() as Promise<TriviaAnswerReviewDto>
}

// HU-28 operator release-clue picker source. Mirrors getOperatorSessionPanel's gateway path + auth/
// status mapping. 403 = a non-owning operator: the ownership Proxy denies the read.
export async function getReleasableClues(
  liveSessionId: string,
): Promise<ReleasableCluesDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/clues/releasable`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Releasable clues: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Releasable clues: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (!response.ok) throw new IdentityError('unknown', `Releasable clues read failed with status ${response.status}`)

  return response.json() as Promise<ReleasableCluesDto>
}

export async function getSessionAssociatedTeams(
  liveSessionId: string,
): Promise<SessionAssociatedTeamsDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions/${liveSessionId}/teams`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator role required.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `getSessionAssociatedTeams failed with status ${response.status}`)
  }

  return response.json() as Promise<SessionAssociatedTeamsDto>
}

export async function associateTeamToSession(
  liveSessionId: string,
  referenceTeamId: string,
): Promise<AssociateTeamToSessionResultDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions/${liveSessionId}/teams`, {
    method: 'POST',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ referenceTeamId }),
  })

  if (response.status === 400) throw new Error('inactive_team')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator role required.')
  if (response.status === 404) throw new Error('not_found')
  if (response.status === 409) {
    // Branch on the stable ProblemDetails `type` (the ErrorCode slug), not `detail`: these domain
    // exceptions declare no PublicDetail, so `detail` is a generic per-category sentence, never the
    // domain message. Mirrors transitionSessionState / releaseClue.
    const problem = (await response.json().catch(() => null)) as { type?: string } | null
    switch (problem?.type) {
      case 'duplicate-team-association-in-session':
        throw new Error('duplicate_association')
      case 'team-association-requires-scheduled-session':
        throw new Error('session_not_scheduled')
      default:
        throw new Error('association_conflict')
    }
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `associateTeamToSession failed with status ${response.status}`)
  }

  return response.json() as Promise<AssociateTeamToSessionResultDto>
}

// HU-26 operator clue release. Mirrors transitionSessionState's POST + 409-ProblemDetails-`type` parse.
// 403 = non-Operator or a non-owning operator (the ownership Proxy denies). The single 409 carries three
// distinct causes distinguished by the ProblemDetails `type` slug, surfaced as distinct typed errors so
// the control can render a specific, non-crashing message for each. Omit teamId to release to all teams.
export async function releaseClue(
  liveSessionId: string,
  body: ReleaseClueRequest,
): Promise<ReleaseClueResultDto> {
  await verifySession()
  const payload: Record<string, string> = {}
  if (body.targetId) payload.targetId = body.targetId
  if (body.clueId) payload.clueId = body.clueId
  if (body.teamId) payload.teamId = body.teamId
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/clues/release`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(payload),
    },
  )

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 409) {
    // The backend tags each release conflict with a stable ProblemDetails `type` (its ErrorCode slug);
    // the `detail` is a generic per-category sentence, never the domain message — so branch on `type`,
    // mirroring transitionSessionState (the domain exceptions declare no PublicDetail).
    const problem = (await response.json().catch(() => null)) as { type?: string } | null
    switch (problem?.type) {
      case 'clue-already-released-to-team':
        throw new Error('already_released')
      case 'clue-not-releasable':
        throw new Error('not_releasable')
      case 'session-not-active-for-clue-release':
        throw new Error('not_active')
      default:
        throw new Error('release_conflict')
    }
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `releaseClue failed with status ${response.status}`)
  }

  return response.json() as Promise<ReleaseClueResultDto>
}

// HU-28 operator operative-clue authoring. Mirrors releaseClue's POST + 409-ProblemDetails-detail parse.
// 403 = non-Operator or a non-owning operator (ownership denies). The single 409 (session not Active/
// Paused) is surfaced as a distinct typed error the control renders non-crashingly. teamIds is sent
// as-is (already non-empty; "all teams" is the full id list assembled by the caller).
export async function addOperativeClue(
  liveSessionId: string,
  body: AddOperativeClueRequest,
): Promise<AddOperativeClueResultDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/operative-clues`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ clueText: body.clueText, teamIds: body.teamIds }),
    },
  )

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 409) {
    // Single 409 cause here (session not Active/Paused). The backend's ProblemDetails carries the
    // domain message in `detail`; match the not-live substring, mirroring releaseClue's detail parse.
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null
    const detail = problem?.detail?.toLowerCase() ?? ''
    if (detail.includes('requires an active or paused')) throw new Error('not_live')
    throw new Error('clue_conflict')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `addOperativeClue failed with status ${response.status}`)
  }

  return response.json() as Promise<AddOperativeClueResultDto>
}

// HU-38 operator justified penalty. Mirrors addOperativeClue's gateway POST + auth/status mapping.
// 403 = a non-owning operator: the scoring ownership Proxy denies (Administrator is unrestricted
// backend-side, but the action layer keeps this Operator-only). 400 = blank reason (the backend
// validator is authoritative even though the control disables submit). Success is 200 OK.
export async function applyPenalty(
  liveSessionId: string,
  body: ApplyPenaltyRequest,
): Promise<AppliedPenaltyDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/penalties`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ teamId: body.teamId, reason: body.reason }),
    },
  )

  if (response.status === 400) throw new Error('invalid_reason')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `applyPenalty failed with status ${response.status}`)
  }

  return response.json() as Promise<AppliedPenaltyDto>
}

// HU-24B operator ranking snapshot. The REST fallback behind the RankingChanged push: fetched on
// connect/reconnect so the panel is correct even while the hub is down. 403 = an operator not assigned
// to this session (the scoring ownership Proxy denies). A session with no score entries yet is NOT an
// error — the backend returns the well-known empty snapshot (rows: []).
export async function getOperatorRanking(liveSessionId: string): Promise<RankingSnapshotDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/ranking/operator`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `getOperatorRanking failed with status ${response.status}`)
  }

  return response.json() as Promise<RankingSnapshotDto>
}

// RF-15 session audit history from scoring-monitoring. Administrator OR Operator — unlike the
// operator-only reads in this module, so a 403 here is a role failure, not an assignment failure.
// Optional teamId narrows to one team's events. A session with no recorded events is NOT an error:
// the backend returns events: [].
export async function getSessionHistory(
  liveSessionId: string,
  teamId?: string,
): Promise<SessionHistoryDto> {
  await verifySession()
  const query = teamId ? `?teamId=${encodeURIComponent(teamId)}` : ''
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/history${query}`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator or operator role required.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `getSessionHistory failed with status ${response.status}`)
  }

  return response.json() as Promise<SessionHistoryDto>
}

// HU-24B operator evidence trace snapshot. The REST fallback behind the EvidenceSubmission* pushes:
// fetched on select/reconnect so the panel is correct even while the hub is down. Deliberately sends no
// teamId — the operator is entitled to the whole session's trace, and access is proven by session
// assignment (403 = an operator not assigned to this session). A session with no submissions yet is NOT
// an error: the backend returns items: [].
export async function getOperatorEvidenceTrace(liveSessionId: string): Promise<EvidenceTraceDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/evidence-submissions`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `getOperatorEvidenceTrace failed with status ${response.status}`)
  }

  return response.json() as Promise<EvidenceTraceDto>
}
