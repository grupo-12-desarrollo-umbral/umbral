# A/B Ticket Merge Execution Checklist (2026-07-09)

Derived from:
- `backend/docs/ab-ticket-merge-validation-handoff-2026-07-09.md`
- live Linear validation on 2026-07-09

Use this as the execution checklist. It separates items that are ready to apply from items blocked on product/canon decisions and from pure backlog-graph cleanup.

## Validated

- [x] Merge `DES-47` into `DES-46` and keep the merged ticket as plain `HU-34`. *(Applied 2026-07-09. `DES-46` retitled `HU-34 - Registro y rechazo de respuestas de equipo en trivia`, four ACs absorbed, `blocks DES-47` dropped, `relatedTo` added.)*
- [x] **Archive `DES-47` by hand.** *(Done 2026-07-09 — it no longer appears in any `Todo` query.)*
- [ ] Close `DES-16` as absorbed by `DES-15`. It is already archived, so this is documentation hygiene only.
- [ ] Keep these pairs split: `DES-32/33`, `DES-34/35`, `DES-56/57`.
- [ ] Do **not** merge `DES-49` and `DES-50`.
- [ ] Assign the "respondido/no respondido" visibility-state gate to `DES-49` instead of merging `DES-49/50`.
- [x] Treat `DES-29` as an orphaned `HU-21B`: rename it to plain `HU-21` after its blocker is fixed. *(Applied 2026-07-09.)*
- [x] Strip the query/read AC from `DES-29` (`historial de cambios puede consultarse posteriormente`) so it stops leaking into `DES-56`. *(Applied 2026-07-09.)*

## Needs Human Decision

- [ ] Reconcile `DES-85` before changing `HU-37/HU-39/HU-40` structure. The PRD still treats `HU-37A`, `HU-37B`, `HU-39A`, `HU-39B`, `HU-40A`, and `HU-40B` as distinct slices.
- [ ] Decide whether `DES-52` and `DES-55` should fold into `DES-54` after the PRD is updated, or whether the split remains canonical.
- [ ] If `DES-52` folds into `DES-54`, also remove the duplicated ranking-update AC from `DES-51`.
- [ ] Decide the direction of the duplicate between `DES-13` and `DES-59` (`HU` survives vs `ENABLER` survives).
- [ ] Decide whether `DES-40` and `DES-41` should merge at all after the blocker graph is repaired.

## Stale Graph

- [x] Re-point blockers off canceled `DES-28` onto `DES-76`: `DES-29`, `DES-32`, `DES-36`, `DES-37`, `DES-38`, `DES-39`, `DES-42`, `DES-53`. *(Applied 2026-07-09.)*
- [x] Re-point blockers off canceled `DES-30` onto `DES-77`: `DES-31`, `DES-36`, `DES-38`, `DES-39`, `DES-42`, `DES-59`. *(Applied 2026-07-09.)*
- [x] Re-point blockers off canceled `DES-44` onto `DES-78`: `DES-46`, `DES-49`. `DES-45` was already correct and was not touched. *(Applied 2026-07-09.)*
- [x] Fix `DES-29` so it is blocked by `DES-76`, not canceled `DES-28`. *(Applied 2026-07-09, as part of the `DES-28` sweep.)*
- [ ] Record `DES-23` -> `DES-75` in the supersession map. No edge work: `DES-23` blocks no live ticket.
- [ ] Fix `DES-41` so it is blocked by `DES-40`, not `DES-42`, before any `HU-30` merge. **Not applied** — dropping `DES-42` asserts that generic rejection does not need the QR form to exist first. Design call, not hygiene.
- [ ] Break the live dependency cycle: `DES-51 -> DES-42 -> DES-31 -> DES-51`. **Not applied** — the proposed break (drop `HU-37A` from `DES-31`) silently unblocks six downstream tickets. Needs sign-off.
- [x] Recompute readiness after the blocker cleanup. Startable now, zero live blockers: `DES-46` (merged `HU-34`), `DES-32`, `DES-53`, `DES-29`.

> **Residual, harmless:** `DES-28`, `DES-30`, `DES-44` and `DES-23` still carry `blocks` edges among
> *themselves*. Both ends are canceled, so no live ticket reads them. `DES-44.blocks` is now empty.

## Suggested Order

- [ ] 1. Repair stale blockers from `DES-28/30/44`.
- [ ] 2. Break the `DES-51 / DES-42 / DES-31` cycle.
- [ ] 3. Merge `DES-47 -> DES-46`.
- [ ] 4. Clean up `DES-16`.
- [ ] 5. Rename/fix `DES-29`.
- [ ] 6. Reconcile `DES-85`, then decide `DES-52/54/55`.
- [ ] 7. Resolve `DES-13` vs `DES-59`.
