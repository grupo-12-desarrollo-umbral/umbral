# Plan: operator QR-code preview in the live session view

Expose each treasure-hunt target's QR code to the operator in the live session view, so the
operator can print and place the codes from the surface that reflects what scans actually resolve
against. Reuses the existing `QrPreview` component; adds one operator-scoped read endpoint.

## Why the live session view is the correct surface (not the authoring UI)

The mission can drift from the session's snapshot. The only edit guard in mission-design is
`Mission.EnsureEditable()` (`Mission.cs:108`), which freezes a mission solely when it is
*deactivated* — an active mission's targets and QR codes stay editable, and mission-design has no
knowledge of live sessions (deliberate bounded-context separation). So a session created, then its
source mission's targets edited, leaves the authoring UI showing codes that the live session will
reject on every scan. The session's immutable snapshot is the only surface that shows the codes that
will actually be scanned.

## What was confirmed first (it changes the shape of the work)

The QR codes are **already in the session-operations snapshot** — `TargetSnapshot.QrCode`
(`TargetSnapshot.cs:62`), populated at `POST /api/sessions` from `MissionRuntimeTargetDto.QrCode`.
This is therefore a pure **exposure** change:

- No database migration.
- No mission-design change, and **no mission-design↔session-operations contract change** — the
  `QrCode` already flows across that boundary at session creation.
- The only new contract is one backend→frontend read endpoint, both sides owned on this branch.

Accessors already exist: `LiveSession.State`, `LiveSession.MissionRuntimeSnapshot` (→ stages →
substages with `PlayMode`/`Title`), `MissionRuntimeSnapshot.TargetSnapshots`, and
`LiveSession.ActiveSubstageId`. Precedent for filtering treasure-hunt substages by snapshot id is at
`MissionRuntimeSnapshot.cs:137`.

## Prerequisite (open at time of writing)

Cut `feat/operator-target-qr-codes` off `develop`, clean. Blocked until the unrelated uncommitted
WIP in the tree (trivia authoring in mission-design + frontend edits) is resolved.

## The contract

New endpoint, mirroring the existing operator reads:

```
GET /api/sessions/{liveSessionId}/target-codes   [Authorize: Operator]
```

```
OperatorTargetCodesDto(
    Guid LiveSessionId,
    IReadOnlyList<OperatorTargetSubstageDto> Substages)   // treasure-hunt substages, in sequence

OperatorTargetSubstageDto(
    Guid SubstageSnapshotId,
    string Title,
    int SequenceOrder,
    bool IsActive,                                          // == LiveSession.ActiveSubstageId
    IReadOnlyList<OperatorTargetCodeDto> Targets)

OperatorTargetCodeDto(
    Guid TargetSnapshotId,
    string Name,
    int SequenceOrder,
    string QrCode)
```

Fetched **once** on mount — not on the panel broadcast. The data is immutable for the session's
life, so it must not ride the `OperatorSessionPanelUpdated` push (which re-fires on every score
change / clue release).

## State gating (decided: all states except Finished/Cancelled), enforced in the domain

`Substages` is non-empty for **every state except `Finished` and `Cancelled`**, where it returns
empty. The gate lives in a domain projection (`LiveSession.ProjectOperatorTargetCodes()`) so the API
physically cannot leak codes for a terminal session; the frontend mirrors it so the panel does not
render there (no empty accordion). A pure-trivia mission also yields empty — nothing to print.

Rationale for including `Active`/`Paused`: the operator is already a trusted role (penalties, clue
release), and a damaged code needing a re-print mid-session is a real scenario. With no geofencing
(#156), a code on the operator's screen is functionally identical to the one at the landmark, so this
does not weaken a mechanic that placement already trusts the operator with.

## Backend changes (session-operations, ADR-0011 vertical slice)

- `Application/Sessions/Queries/GetOperatorTargetCodes/` — `GetOperatorTargetCodesQuery` + handler.
  Handler calls `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` (this is what maps a
  non-owning operator to 403, same as every other operator read), then
  `liveSession.ProjectOperatorTargetCodes()`.
- `Application/Dtos/Sessions/OperatorTargetCodesDto.cs` — the three records above (response DTOs live
  central under `Application/Dtos/<Area>/`, not co-located, per ADR-0011).
- `Domain/Entities/LiveSession.cs` — `ProjectOperatorTargetCodes()`: terminal state → empty;
  otherwise group `TargetSnapshots` by treasure-hunt substage, ordered by `SequenceOrder`, with the
  active substage flagged.
- `Api/Controllers/SessionsController.cs` — `[HttpGet("{liveSessionId:guid}/target-codes")]`,
  `[Authorize(Policy = AuthorizationPolicies.Operator)]`.
- Gateway: verify the `/api/sessions/{id}/**` route already proxies this prefix (almost certainly
  yes — confirm, don't assume).

## Frontend changes

- `app/lib/definitions.ts` — the DTO types.
- `app/lib/sessions.ts` — `getOperatorTargetCodes(liveSessionId)`, cloning
  `getOperatorSessionPanel`'s gateway path + auth.
- New `app/dashboard/OperatorTargetCodesPanel.tsx` — one collapsible group per treasure-hunt
  substage, each target reusing **`QrPreview` unchanged**, the active substage marked. Fetched once
  on mount.
- `app/dashboard/DashboardClient.tsx` — mount beside the other live panels (~line 1517); hide when
  `sessionState` is `Finished`/`Cancelled`. The component already tracks `sessionState` and has the
  exact exclusion filter at line 630.

## Tests

- **Backend unit** (`ProjectOperatorTargetCodes`): treasure-hunt substages only; grouped and ordered;
  active flag correct; empty on `Finished`/`Cancelled`; empty for a pure-trivia mission; mixed-mode
  returns only the treasure-hunt substage.
- **Backend integration**: 200 with the correct shape for the owning operator; 403 for a non-owning
  operator.
- **Frontend**: renders one group per treasure-hunt substage with a `QrPreview` per target; active
  substage marked; renders nothing for terminal states.

## Verification

`/verify` against the existing seed
(`frontend/tests/e2e/session-substage-progress-manual-seed.spec.ts`): the `Substage Progress E2E`
session sits in `Preparing`, so the panel should list `SP-E2E-QR-1` / `SP-E2E-QR-2` before Start —
the print-and-place moment. Then drive the device run scanning off that panel, closing handoff
Remaining #1 in the same pass.

## Notes

- One branch, backend + frontend together (paired contract — updating both sides in one branch is the
  explicit flag the root `AGENTS.md` asks for).
- Commit/PR: `/conventional-commits` before commit, `/safe-pr-creator` before PR; single squashed
  commit; merge-commit-only.
- Out of scope: reveal-restore across reconnect, and the operator-side reveal surface (both from the
  handoff). This is only the QR print surface.
