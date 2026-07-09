#!/usr/bin/env bash
#
# structure-guard.sh — enforces the ADR-0011 Application-layer vertical-slice layout.
#
# Fails (non-zero exit) if a service's Application/ tree violates the canonical
# convention. Run from backend/, or via `make structure-guard [SVC=<service>]`.
# This is the structural CI guard ADR-0011's Consequences section calls for.
#
# Rules enforced (ADR-0011 Decision §1/§2):
#   A. No `Handlers/`, `DTOs/`/`Dtos/`, or `Facades/` type-bucket directory nested under
#      an <Area> — EXCEPT the single central `Application/Dtos/` root, which is the
#      sanctioned home for all response DTOs (grouped by area; ADR-0011 §1/§2). A
#      single-consumer Facade is inlined into its handler (the handler realizes it); a
#      standalone `*Facade.cs` exists only when shared by ≥2 slices and then lives in
#      `<Area>/Common/` grouped by CONCERN. The rule keys on the directory NAME, so a
#      co-located `*Facade.cs`/`*Proxy.cs` file is never flagged — only the type-bucket
#      folder (or a per-area `DTOs/`/`Dtos/` bucket) is.
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
#   E. No slice-local `*Facade.cs`/`I*Facade.cs`. Option C (ADR-0013): a single-consumer
#      Facade is realized by its MediatR handler, so a standalone Facade type inside a
#      use-case slice is the ceremony that inlining removed. A Facade shared by >=2 slices
#      is legal and lives in `<Area>/Common/`, which never sits under Commands/ or
#      Queries/ — so the path decides, and the guard needs no consumer counting.
#
# Scope: only `services/*/src/Application`. Domain/Infrastructure/Api are untouched.
# Mandated PATTERNS are never flagged for being realized — the rules key on directory
# names, on *CommandHandler/*QueryHandler placement, on the un-mandated `Executor`
# suffix, and on a Facade's PLACEMENT (Rule E), never on whether a pattern exists. So
# co-located `*Proxy.cs` files (a Proxy is a cross-cutting decorator and stays a type —
# ADR-0012), and the plan-preserved `EventHandlers/` and `StateTransitions/` folders
# (mandated State machine + event dispatch — ADR-0011 §1, plan Phase 2), are left alone.
# A Facade is different: Rule E forbids it INSIDE a use-case slice because Option C
# realizes a single-consumer Facade in its handler; shared ones live in `<Area>/Common/`.
# The `Facades/` type-BUCKET folder is forbidden separately (Rule A) — never collect by
# type. Response DTOs live in the central `Application/Dtos/` root, never a per-area
# `DTOs/`/`Dtos/` bucket.
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

    # Rule A — Handlers/, DTOs/Dtos/, or Facades/ type-buckets. The central
    # Application/Dtos/ root is the sanctioned DTO home and is exempt; a per-area
    # DTOs/Dtos/ bucket is not.
    while IFS= read -r d; do
        echo "✗ [$svc] forbidden type-bucket directory: $d" >&2
        echo "      → response DTOs go in the central Application/Dtos/ root; inline a" >&2
        echo "        single-consumer Facade into its handler, or move a shared Facade" >&2
        echo "        into <Area>/Common/ grouped by concern (ADR-0011 §1/§2)." >&2
        violations=$((violations + 1))
    done < <(find "$app" -type d \( -name Handlers -o -name DTOs -o -name Dtos -o -name Facades \) ! -path "$app/Dtos" 2>/dev/null | sort)

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

    # Rule E — a slice-local Facade. Option C (ADR-0013): a single-consumer Facade is
    # realized by its handler, never a standalone type. Any `*Facade.cs`/`I*Facade.cs`
    # inside a use-case slice is therefore that inlined-away ceremony coming back. A
    # Facade shared by >=2 slices is legal and lives in `<Area>/Common/`, which is never
    # under Commands/ or Queries/ — so the path alone decides, no consumer counting.
    while IFS= read -r f; do
        echo "✗ [$svc] slice-local Facade (Option C inlines it into the handler): $f" >&2
        echo "      → move the orchestration into the sibling *CommandHandler.Handle and delete" >&2
        echo "        the Facade + interface; if >=2 slices consume it, move it to <Area>/Common/" >&2
        echo "        grouped by concern instead (ADR-0011 §1/§2, ADR-0013)." >&2
        violations=$((violations + 1))
    done < <(find "$app" -type f -name '*Facade.cs' \( -path '*/Commands/*' -o -path '*/Queries/*' \) 2>/dev/null | sort)
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
