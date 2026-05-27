---
name: next
description: Output the exact ready-to-paste prompt for the next pending implementation phase. Checks for blockers before outputting. Use when you want to know what to run next without re-reading the plan.
---

# Next

Find the next pending phase and output the exact prompt to execute it.

## Process

1. Run the full detection logic from the `progress` skill — git log, file existence, dotnet build, Linear state — for each phase in order.
2. Check for blockers: if the most recently completed phase has a failing build or Linear drift (⚠️), stop and report the blocker instead of outputting a prompt.
3. If no blockers, find the first phase that is not complete and output the prompt.

## Blocker output

If the previous phase is broken, output:

```
⚠️ Phase X.Y for <service> is broken — fix it before starting X.Z.
Issue: <what check failed>
```

Output nothing else.

## Prompt format

Before outputting the prompt, query Linear for issues labelled `svc:<service-label>` in team **umbral-equipo-12** that are relevant to the current phase. Append the issue ID(s) as a `Ref:` so the commit message carries a direct Linear link.

```
Execute phase X.Y for <service-name> per plans/multi-phase-service-implementation.md. Touch only <LayerFolder>/. Ref: <DES-NNN>.
```

If multiple issues match, list them comma-separated: `Ref: DES-63, DES-64`.
If no issues match, omit the `Ref:` field.

Layer folder per phase:

| Phase | LayerFolder |
|---|---|
| X.1 | `Domain/` |
| X.2 | `Application/` |
| X.3 | `Infrastructure/` |
| X.4 | `Api/` |

## Example outputs

Normal:
```
Execute phase 1.2 for mission-design-service per plans/multi-phase-service-implementation.md. Touch only Application/. Ref: DES-63.
```

Blocked:
```
⚠️ Phase 1.1 for mission-design-service is broken — fix it before starting 1.2.
Issue: dotnet build on Domain/ exits non-zero.
```

All done:
```
All phases complete.
```

Output nothing else beyond the above.
