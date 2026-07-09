# Handoff — Participant team lobby + self-join (2026-07-08)

**Session outcome:** investigation only. No code written (deliberately — see below). Filed one issue, annotated three.

## The problem we chased

The mobile app takes a **6-char session code**, then loads a team lobby:
`GET /api/sessions/{sessionCode}/teams` → expects `SessionTeamLobbyDto` with per-team `joinState: mine | joinable | locked` (`mobile/src/lib/api/teams.ts`).

**That endpoint 404s.** It was designed, DB-scaffolded, and documented in identity-access, then **deliberately removed** in the Users↔SessionOperations realignment (commit `acd47af` "remove session team association + participant lobby"). The mobile client still calls the removed design.

## What we confirmed (traces)

1. **Lobby GET does not exist.** Only Operator-scoped `.../teams` endpoints exist in session-ops `SessionsController`, returning `SessionAssociatedTeamsDto` (`Open|Locked|Closed`), not the participant `joinState` shape. Gateway routes `/api/sessions/**` → session-ops.

2. **`joinState` semantics are a projection over policy, not new rules** (from issues #88/#89 + `users-realignment-decisions-2026-07-06.md`):
   - `mine` — participant's **current pick** (`TeamMember`/`SessionParticipant`, session-ops-local). Not derivable from Users eligibility.
   - `joinable` — team in the participant's selectable set (their whitelist, **or all attached teams if they hold no membership → Open Team Selection**) **and** session is `Scheduled`/`Preparing` **and** capacity free **and** not frozen.
   - `locked` — everything else.
   - **Trap:** an empty whitelist for an *eligible* participant = "unassigned → open selection", which is distinct from a *deactivated/denied* user. Don't collapse them.

3. **The whitelist input already exists** — `GET /api/permissions/participant-eligible-teams` (`GetParticipantEligibleTeams`, identity-access), merged to develop via **#105/#107**. Returns `ParticipantEligibleTeamsDto(IsEligible, ReasonCode, Teams[])`, session-independent, gate reason-coded. **No new identity-access work needed.** (We nearly rebuilt this before finding it — it was absent only on `feat/issue-90`, which was branched before #107 merged.)

4. **The self-join call is also broken and mis-routed.**
   - Mobile calls `POST /api/teams/{teamId}/participants/self` (body `{ liveSessionId }`). **No such route exists** in either service — 404.
   - `/api/teams/**` → **identity-access**, whose only nearby write creates a `RegisteredTeamMembership` (a *may-join whitelist* row). The realignment (decisions doc lines 72-73) says a session pick **never** writes a `RegisteredTeamMembership` — it must create a `SessionParticipant`/`TeamMember` in **session-ops**.
   - The `teamId` in the lobby is the **runtime team id** (`Team.TeamId`, a per-session `Guid.NewGuid()` bound to one `LiveSessionId`) → session context is **implicit** in it. The catalog `ReferenceTeamId` is the shared one.
   - session-ops already has the domain model (`Team.AssignParticipant` → `TeamMember.Assign`); it just has no HTTP endpoint.
   - **Verdict:** the self-join must **move to session-ops** under `/api/sessions/...`, targeting the runtime `Team.TeamId`.

## Why no code

The lobby GET and self-join are **read/write projections over policy that isn't built yet** (#88, #89). Building either now = guessing the rules. The one piece that *was* buildable (the whitelist query) already exists (#105/#107).

## Issues

| # | What | Status |
|---|---|---|
| **#108** | **Filed this session** — participant lobby GET (read side), session-ops. Blocked by #88, #89. Whitelist dependency = #105/#107 (done). | open |
| #88 | Open Team Selection policy (the write behavior). **Owns two endpoints: lobby GET (#108) + self-join POST.** Commented with the #105/#107 note. | open, unbuilt |
| #89 | Pre-Start assignment freeze / capacity / switching. Commented with the #105/#107 note. | open, unbuilt |
| #105/#107 | Eligible-teams set-query (the whitelist input). | ✅ merged to develop |
| **#110** | **Self-join POST** in session-ops — write sibling of #108, blocked by #88/#89. Eligibility gate baked into acceptance. | open |

## Implementation order

1. **#88** — Open Team Selection (defines the selectable set + pre-start gate).
2. **#89** — freeze / capacity / switching (defines when `joinable` closes).
3. **#108** (lobby GET) + **the self-join POST** — thin session-ops endpoints that read the whitelist (#107) and merge with local session state.

identity-access is done. Everything remaining is **session-ops**.

## Open items for next session

- ~~File the self-join POST issue~~ → filed as **#110** (write side, session-ops, blocked by #88/#89; eligibility gate in acceptance).
- Decide the self-join route shape (e.g. `POST /api/sessions/{code}/teams/{runtimeTeamId}/join`) and whether it carries `liveSessionId` explicitly or relies on the implicit runtime id. *(open question on #110)*
- Mobile will need to repoint both calls once the session-ops endpoints land (path + DTO already match on the lobby side).
