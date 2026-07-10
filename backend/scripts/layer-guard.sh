#!/usr/bin/env bash
#
# layer-guard.sh — forbids cross-layer reflection laundering (Clean Architecture
# dependency rule). Backs ADR-0014 (docs/adr/0014-no-cross-layer-reflection.md); the
# unconditional CI gate its Consequences section calls for.
#
# NOTE: the scan root is resolved from THIS SCRIPT'S path (`cd "$(dirname "$0")/.."` →
# backend/), NOT from $PWD. Invoking it by absolute path from another worktree silently
# scans the script's own tree, not the caller's.
#
# The dependency rule points INWARD: Domain ← Application ← Infrastructure, with
# Api (Presentation) at the outer edge. An inner layer must never depend on an
# outer layer. The compiler and the .csproj ProjectReferences enforce the HONEST
# version of this — and a type-reference architecture test (NetArchTest) would
# too. But ALL of those are blind to a dependency expressed as a *string* and
# resolved by reflection, e.g.
#
#     var hub = AppDomain.CurrentDomain.GetAssemblies()
#         .Select(a => a.GetType("umbral_backend.Api.Hubs.SessionsHub"))
#         .First(t => t is not null);
#     var ctxType = typeof(IHubContext<>).MakeGenericType(hub);   // IHubContext<SessionsHub>
#
# That launders an Infrastructure→Api dependency past every compile-time check:
# the .csproj has no reference, so structure/type-graph guards see nothing. This
# guard reads SOURCE TEXT — the only place the violation is visible — and fails
# the build if an inner layer reflectively names or resolves an outer-layer type.
#
# Run from backend/, or via `make layer-guard [SVC=<service>]`. No dotnet — pure
# text, so it runs on any machine and without Claude (like structure-guard.sh).
#
# Rules (fail = non-zero exit). Scanned in services/*/src/{Domain,Application,
# Infrastructure} only — Api owns the hubs and legitimately injects IHubContext:
#   1. MakeGenericType(...)  — building a generic host type (IHubContext<TApiHub>)
#                              at runtime, the tell-tale of laundering IHubContext.
#   2. .GetType("...")       — reflection type lookup by string literal. The no-arg
#                              instance `x.GetType()` (e.g. serialization) is fine;
#                              only the string-argument form is flagged.
#   3. AppDomain             — assembly/type scanning to resolve a type by name.
#   4. "<Ns>.Api."           — a string literal naming the Api (Presentation)
#                              namespace from inside an inner layer.
#
# The fix is NEVER a cleverer lookup — it is to MOVE the adapter into the layer
# that owns the type. Put the SignalR broadcaster in Api and inject
# IHubContext<THub> by constructor, implementing an Application-defined port
# (interface). Inner layers depend on the port, never the outer type. See
# backend-agent.md (constraints) for the authoring rule.
#
# Full-line comments are stripped before matching, so a doc comment that mentions
# a forbidden token is not flagged — only real code is. A genuinely-legitimate hit
# can be exempted by adding its backend-relative FILE PATH to ALLOWLIST below (with
# a reason), mirroring structure-guard.sh's EXECUTOR_ALLOWLIST.
set -euo pipefail

# Allowlist: backend-relative paths exempt from this guard. Empty by design — the
# reflection-laundering pattern has no legitimate use in an inner layer. If one is
# ever justified, add the file path here WITH a reason so the exemption is auditable.
ALLOWLIST=(
    # e.g. "services/foo-service/src/Infrastructure/Legit/GenericFactory.cs"  # why
)

# The Presentation-layer namespace segment inner layers must not name as a string.
# Keyed on the ".Api." segment of the solution's namespaces (umbral_backend.Api.*).
API_NS_SEGMENT='"[A-Za-z_][A-Za-z0-9_.]*\.Api\.'

# Combined forbidden-token regex (rules 1–4 above).
PATTERNS="MakeGenericType|\\.GetType\\(\"|AppDomain|${API_NS_SEGMENT}"

# Inner layers (relative to Api). Api itself is intentionally NOT scanned.
INNER=(Domain Application Infrastructure)

cd "$(dirname "$0")/.."   # → backend/

# Default: scan every service. `SVC=<service>` (env) narrows to one — the Makefile
# target forwards SVC only when set on the command line.
TARGET_DIRS=()
while IFS= read -r src; do
    svc=$(echo "$src" | cut -d/ -f2)
    if [[ -n "${SVC:-}" && "$svc" != "${SVC}" ]]; then continue; fi
    for layer in "${INNER[@]}"; do
        [[ -d "$src/$layer" ]] && TARGET_DIRS+=("$src/$layer")
    done
done < <(find services -maxdepth 3 -type d -path '*/src' 2>/dev/null | sort)

violations=0
checked=0

for dir in "${TARGET_DIRS[@]}"; do
    checked=$((checked + 1))
    # grep -> file:line:content ; drop full-line comments (//, ///, *) to avoid
    # flagging doc-comment prose, then apply the file-path allowlist.
    while IFS= read -r hit; do
        [[ -z "$hit" ]] && continue
        file="${hit%%:*}"
        skip=0
        for a in "${ALLOWLIST[@]}"; do
            if [[ "$file" == "$a" ]]; then skip=1; break; fi
        done
        [[ $skip -eq 1 ]] && continue
        echo "✗ cross-layer reflection / type-name string: $hit" >&2
        violations=$((violations + 1))
    done < <(grep -rnEI "$PATTERNS" "$dir" --include='*.cs' 2>/dev/null \
                | grep -vE ':[0-9]+:[[:space:]]*(//|///|\*)' || true)
done

if [[ $checked -eq 0 ]]; then
    echo "layer-guard: no inner-layer tree found${SVC:+ for $SVC} — nothing to check." >&2
    exit 1
fi

if [[ $violations -gt 0 ]]; then
    echo "" >&2
    echo "layer-guard: $violations cross-layer reflection violation(s)." >&2
    echo "  An inner layer (Domain/Application/Infrastructure) is naming or resolving an" >&2
    echo "  Api-layer type by string/reflection. Move the adapter into the layer that owns" >&2
    echo "  the type and inject it through an Application port — do not launder the" >&2
    echo "  dependency through reflection. (See scripts/layer-guard.sh header.)" >&2
    exit 1
fi

echo "layer-guard: OK — no cross-layer reflection in ${INNER[*]} (${checked} tree(s) checked${SVC:+, service: $SVC})."
