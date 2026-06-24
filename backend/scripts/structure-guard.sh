#!/usr/bin/env bash
#
# structure-guard.sh — enforces the ADR-0011 Application-layer vertical-slice layout.
#
# Fails (non-zero exit) if a service's Application/ tree violates the canonical
# convention. Run from backend/, or via `make structure-guard [SVC=<service>]`.
# This is the structural CI guard ADR-0011's Consequences section calls for.
#
# Rules enforced (ADR-0011 Decision §1/§2):
#   A. No `Handlers/`, `DTOs/`, or `Facades/` type-bucket directory anywhere under
#      Application/. The handler and its owned DTO live in the use-case folder; a
#      mandated Facade lives co-located in the slice it orchestrates, or — if shared by
#      ≥2 slices — in `<Area>/Common/` grouped by CONCERN (not a `Facades/` bucket).
#      The rule keys on the directory NAME, so a co-located `*Facade.cs` file is never
#      flagged — only the type-bucket folder is.
#   B. No generic `UseCases/` wrapper directory under Application/ — the path is
#      <Area>/{Commands|Queries}/<UseCase>/, not a flat bag of use cases.
#   C. The Commands/Queries level is MANDATORY. Every `<X>CommandHandler.cs` must
#      sit at  <Area>/Commands/<UseCase>/  and every `<X>QueryHandler.cs` at
#      <Area>/Queries/<UseCase>/  — i.e. the handler's grandparent folder is named
#      `Commands`/`Queries`. This catches a flattened area
#      (<Area>/<UseCase>/...Handler.cs) and a missing use-case folder
#      (<Area>/Commands/...Handler.cs) alike.
#   D. No un-mandated forwarding `*Executor` type. The hand-rolled
#      Proxy → Service → Executor chain is a mediator inside MediatR; `Executor` is
#      NOT one of the ADR-0004 mandated patterns (Composite, Template Method, Facade,
#      State, Chain of Responsibility, Proxy, Strategy — none is ever named
#      "Executor"), so any `*Executor` under Application/ is the un-mandated bottom of
#      that triplet and must be collapsed into its handler (ADR-0011 §3). Exempt only
#      via EXECUTOR_ALLOWLIST below.
#
# Scope: only `services/*/src/Application`. Domain/Infrastructure/Api are untouched.
# Mandated PATTERNS are never flagged — the rules key on type-bucket directory names,
# on *CommandHandler/*QueryHandler placement, and on the un-mandated `Executor` suffix,
# never on whether a pattern is realized. So co-located `*Proxy.cs`/`*Facade.cs` files,
# and the plan-preserved `EventHandlers/` and `StateTransitions/` folders (mandated
# State machine + event dispatch — ADR-0011 §1, plan Phase 2), are left alone. Only the
# `Facades/` type-BUCKET folder is forbidden (Rule A): a Facade belongs in its slice or
# in `<Area>/Common/`, not collected by type.
set -euo pipefail

# Allowlist for Rule D: backend-relative paths of `*Executor` files that are
# genuinely mandated. The patterns matrix
# (docs/trivia_sprint_required_patterns_matrix.md) names NO Executor for any HU, so
# this is empty. If it ever does, add the file's path here to exempt it — keep the
# matrix HU in the comment so the exemption is auditable.
EXECUTOR_ALLOWLIST=(
    # e.g. "services/foo-service/src/Application/Bar/Commands/Baz/IBazExecutor.cs"  # HU-NN
)

cd "$(dirname "$0")/.."   # → backend/

# Default: scan every service. `SVC=<service>` (env) narrows to one — the Makefile
# target forwards SVC only when it was set on the command line.
if [[ -n "${SVC:-}" ]]; then
    APP_DIRS=("services/${SVC}/src/Application")
else
    APP_DIRS=()
    while IFS= read -r d; do APP_DIRS+=("$d"); done \
        < <(find services -maxdepth 3 -type d -path '*/src/Application' 2>/dev/null | sort)
fi

violations=0
checked=0

for app in "${APP_DIRS[@]}"; do
    [[ -d "$app" ]] || continue
    checked=$((checked + 1))
    svc=$(echo "$app" | cut -d/ -f2)

    # Rule A — Handlers/, DTOs/, or Facades/ type-buckets.
    while IFS= read -r d; do
        echo "✗ [$svc] forbidden type-bucket directory: $d" >&2
        echo "      → co-locate the handler/DTO/Facade in its use-case slice, or move a" >&2
        echo "        shared Facade into <Area>/Common/ grouped by concern (ADR-0011 §2)." >&2
        violations=$((violations + 1))
    done < <(find "$app" -type d \( -name Handlers -o -name DTOs -o -name Facades \) 2>/dev/null | sort)

    # Rule B — generic UseCases/ wrapper segment.
    while IFS= read -r d; do
        echo "✗ [$svc] forbidden 'UseCases/' wrapper directory: $d" >&2
        echo "      → use <Area>/{Commands|Queries}/<UseCase>/ directly (ADR-0011 §1)." >&2
        violations=$((violations + 1))
    done < <(find "$app" -type d -name UseCases 2>/dev/null | sort)

    # Rule C — the Commands/ level: every command handler one level under Commands/.
    while IFS= read -r f; do
        grandparent=$(basename "$(dirname "$(dirname "$f")")")
        if [[ "$grandparent" != "Commands" ]]; then
            echo "✗ [$svc] command handler not under <Area>/Commands/<UseCase>/: $f" >&2
            echo "      (grandparent folder is '$grandparent'; the Commands/Queries level is mandatory — ADR-0011 §1)." >&2
            violations=$((violations + 1))
        fi
    done < <(find "$app" -type f -name '*CommandHandler.cs' 2>/dev/null | sort)

    # Rule C — the Queries/ level: every query handler one level under Queries/.
    while IFS= read -r f; do
        grandparent=$(basename "$(dirname "$(dirname "$f")")")
        if [[ "$grandparent" != "Queries" ]]; then
            echo "✗ [$svc] query handler not under <Area>/Queries/<UseCase>/: $f" >&2
            echo "      (grandparent folder is '$grandparent'; the Commands/Queries level is mandatory — ADR-0011 §1)." >&2
            violations=$((violations + 1))
        fi
    done < <(find "$app" -type f -name '*QueryHandler.cs' 2>/dev/null | sort)

    # Rule D — un-mandated forwarding *Executor types (allowlist-exempt).
    while IFS= read -r f; do
        allowed=0
        for a in "${EXECUTOR_ALLOWLIST[@]}"; do
            if [[ "$f" == "$a" ]]; then allowed=1; break; fi
        done
        if [[ $allowed -eq 1 ]]; then
            echo "ℹ [$svc] allowlisted Executor (matrix-named, exempt): $f"
            continue
        fi
        echo "✗ [$svc] un-mandated forwarding '*Executor' type: $f" >&2
        echo "      → collapse the IService/IExecutor pass-through into the handler; 'Executor' is not an ADR-0004 pattern (ADR-0011 §3)." >&2
        violations=$((violations + 1))
    done < <(find "$app" -type f -name '*Executor*.cs' 2>/dev/null | sort)
done

if [[ $checked -eq 0 ]]; then
    echo "structure-guard: no Application/ tree found${SVC:+ for $SVC} — nothing to check." >&2
    exit 1
fi

if [[ $violations -gt 0 ]]; then
    echo "" >&2
    echo "structure-guard: $violations violation(s) of the ADR-0011 vertical-slice layout." >&2
    exit 1
fi

echo "structure-guard: OK — Application/ layout conforms to ADR-0011 (${checked} service(s) checked${SVC:+: $SVC})."
