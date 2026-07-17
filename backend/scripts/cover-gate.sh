#!/usr/bin/env bash
#
# cover-gate.sh — canonical Phase X.4 coverage gate (per ADR-0005).
#
# Chains coverlet across all supplied test projects, merges the results, and
# enforces the coverage threshold on BOTH line and branch coverage. Exit code
# is the gate:
#   0        = green (both line and branch thresholds met)
#   non-zero = gate FAILS (build error, test failure, or coverage below bar)
#
# This script is the single source of truth for BOTH the CI/CD pass/fail AND
# the coverage number you demonstrate. It does two things on a green run:
#   1. enforces /p:Threshold on the merged result (the gate), and
#   2. persists the merged Cobertura file and renders a human-readable report
#      from THAT EXACT file — so the demonstrated number can never diverge from
#      the gated number.
#
# It is committed to the repo so it works on any machine and without Claude —
# any human or agent runs the same thing. Do NOT render the demonstration
# report from cover.sh: that script uses a different project set and different
# merge/filter settings, so its number is not the gated number.
#
# Sandbox note: the env vars below are exported in the SAME shell as the
# `dotnet test` calls. A sandbox blocks MSBuild's named-pipe node-reuse
# workers; disabling node reuse here avoids that without per-call flags. If a
# run still fails on a loopback socket (vstest testhost <-> console), the
# sandbox is blocking 127.0.0.1 itself — that is an environment problem, not a
# gate problem; surface it rather than editing this script.
set -euo pipefail

export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# Gate threshold (% total), applied to BOTH line and branch coverage. Coverlet
# takes a single Threshold value for every listed ThresholdType, so the same bar
# gates line and branch. Override with THRESHOLD=NN if a consumer requires a
# different bar; the project minimum is the value below.
THRESHOLD="${THRESHOLD:-93}"

usage() {
    cat <<EOF
Usage:  cover-gate.sh <test.csproj> [<test.csproj> ...]

Arguments:
  test.csproj   One or more test projects. Coverage is chained across all of
                them (each merged into the next); the LAST project enforces the
                threshold and emits the merged Cobertura report.

All paths may be absolute or relative to the current directory. Run from the
service directory so relative paths like tests/UnitTests/<Proj>.csproj resolve.

Environment:
  THRESHOLD     Coverage gate, % total, applied to BOTH line and branch
                coverage (default: 93).
  COVERAGE_DIR  Report output directory (default: coverage/gate under the
                current service directory).

Outputs (on the merged result, written under COVERAGE_DIR):
  merged.cobertura.xml   the gated coverage file
  Summary.txt            text summary (if reportgenerator present)
  index.html             HTML report   (if reportgenerator present)
EOF
    exit "${1:-0}"
}

case "${1:-}" in -h|--help) usage 0 ;; esac

if [[ $# -lt 1 ]]; then
    echo "Error: at least one test project is required." >&2
    usage 1
fi

PROJECTS=("$@")
for proj in "${PROJECTS[@]}"; do
    if [[ ! -f "$proj" ]]; then
        echo "Error: test project not found: $proj" >&2
        exit 1
    fi
done

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Auto-generated source is excluded from coverage: it is emitted by source
# generators (e.g. OpenApiXmlCommentSupport.generated.cs), not hand-written, so
# counting its hundreds of untested lines would understate the real coverage of
# code we actually author. This is NOT a Domain/Application exclusion — those
# stay fully measured. Glob is matched by coverlet against the source path.
EXCLUDE_BY_FILE='**/*.generated.cs'

# Durable output for the demonstration report — rendered from the gate's own
# merged file so the number shown equals the number gated. Absolute path:
# CoverletOutput resolves relative to each test .csproj, not this cwd, so a
# relative path would scatter the file under whichever project ran last.
GATE_DIR="${COVERAGE_DIR:-$PWD/coverage/gate}"
rm -rf "$GATE_DIR"
mkdir -p "$GATE_DIR"
MERGED_XML="$GATE_DIR/merged.cobertura.xml"

LAST_INDEX=$(( ${#PROJECTS[@]} - 1 ))
MERGED_JSON=""
GATE_RC=0

for i in "${!PROJECTS[@]}"; do
    proj="${PROJECTS[$i]}"
    STEP_JSON="$TMP/step-$i.json"

    # MergeWith the accumulated json from previous steps (empty on the first).
    merge_args=()
    if [[ -n "$MERGED_JSON" ]]; then
        merge_args+=(/p:MergeWith="$MERGED_JSON")
    fi

    if [[ $i -eq $LAST_INDEX ]]; then
        # Final project → emit merged Cobertura + enforce threshold on line AND
        # branch. The escaped quotes around "line,branch" are load-bearing: the
        # comma must reach MSBuild inside a quoted value, else MSBuild splits it
        # and dies with MSB1006 "Property is not valid".
        # A threshold miss makes `dotnet test` exit non-zero = GATE FAILS, but
        # we capture it (rather than letting set -e abort) so the report still
        # renders from the merged file — useful for finding the gaps.
        if ! dotnet test "$proj" \
            /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura \
            /p:CoverletOutput="$MERGED_XML" \
            /p:ExcludeByFile="$EXCLUDE_BY_FILE" \
            "${merge_args[@]}" \
            /p:Threshold="$THRESHOLD" /p:ThresholdType=\"line,branch\" \
            /p:ThresholdStat=total; then
            GATE_RC=1
        fi
    else
        # Intermediate project → accumulate into json for chaining.
        dotnet test "$proj" \
            /p:CollectCoverage=true /p:CoverletOutputFormat=json \
            /p:CoverletOutput="$STEP_JSON" \
            /p:ExcludeByFile="$EXCLUDE_BY_FILE" \
            "${merge_args[@]}"
        MERGED_JSON="$STEP_JSON"
    fi
done

# Render the report from the EXACT gated file (whether or not the gate passed,
# so a red run still shows where the gaps are). The gate verdict is GATE_RC.
if [[ -f "$MERGED_XML" ]] && command -v reportgenerator >/dev/null 2>&1; then
    reportgenerator \
        -reports:"$MERGED_XML" \
        -targetdir:"$GATE_DIR" \
        -reporttypes:TextSummary\;Html\;CsvSummary \
        -classfilters:"+*" >/dev/null
    echo
    echo "Coverage report (rendered from the gated file):"
    echo "  $GATE_DIR/Summary.txt"
    echo "  $GATE_DIR/index.html"
fi

if [[ $GATE_RC -eq 0 ]]; then
    echo "Gate GREEN — line and branch coverage >= ${THRESHOLD}%."
else
    echo "Gate FAILED — line or branch coverage below ${THRESHOLD}% (or a test failed)." >&2
fi
exit "$GATE_RC"
