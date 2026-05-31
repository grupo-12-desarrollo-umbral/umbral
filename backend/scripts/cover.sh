#!/usr/bin/env bash
set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

usage() {
    cat <<EOF
${BOLD}Usage:${NC}  cover.sh <service-name> [options]

${BOLD}Arguments:${NC}
  service-name    One of: identity-access-service, mission-design-service,
                  session-operations-service, scoring-monitoring-service

${BOLD}Options:${NC}
  -u, --unit-only        Run only unit tests
  -i, --integration-only Run only integration tests (requires Docker)
  -o, --open             Open the HTML report in the default browser
  -h, --help             Show this help

${BOLD}Requirements:${NC}
  - reportgenerator (dotnet tool install -g dotnet-reportgenerator-globaltool)
  - Docker (for integration tests with Testcontainers)
  - coverlet is expected in each test .csproj (coverlet.msbuild)
EOF
    exit 0
}

# ── Parse args ──────────────────────────────────────────────
SERVICE=""
UNIT_ONLY=false
INTEGRATION_ONLY=false
OPEN_REPORT=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        -u|--unit-only)        UNIT_ONLY=true; shift ;;
        -i|--integration-only) INTEGRATION_ONLY=true; shift ;;
        -o|--open)             OPEN_REPORT=true; shift ;;
        -h|--help)             usage ;;
        -*) echo -e "${RED}Unknown option: $1${NC}"; usage ;;
        *)  SERVICE="$1"; shift ;;
    esac
done

if [[ -z "$SERVICE" ]]; then
    echo -e "${RED}Error: service name is required${NC}"
    usage
fi

# ── Resolve paths ───────────────────────────────────────────
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SERVICE_DIR="$REPO_ROOT/services/$SERVICE"

if [[ ! -d "$SERVICE_DIR" ]]; then
    echo -e "${RED}Error: service '$SERVICE' not found at $SERVICE_DIR${NC}"
    echo -e "Available: $(ls -d "$REPO_ROOT"/services/*/ 2>/dev/null | xargs -I{} basename {} | tr '\n' ' ')"
    exit 1
fi

TESTS_DIR="$SERVICE_DIR/tests"
COVERAGE_DIR="$SERVICE_DIR/coverage"
MERGED_DIR="$COVERAGE_DIR/merged"

# ── Find test projects ──────────────────────────────────────
UNIT_PROJS=($(find "$TESTS_DIR" -maxdepth 3 -name "*UnitTests*.csproj" 2>/dev/null || true))
INTEGRATION_PROJS=($(find "$TESTS_DIR" -maxdepth 3 -name "*IntegrationTests*.csproj" 2>/dev/null || true))

if $INTEGRATION_ONLY && [[ ${#INTEGRATION_PROJS[@]} -eq 0 ]]; then
    echo -e "${YELLOW}No integration test projects found in $TESTS_DIR${NC}"
    exit 0
fi

if $UNIT_ONLY && [[ ${#UNIT_PROJS[@]} -eq 0 ]]; then
    echo -e "${YELLOW}No unit test projects found in $TESTS_DIR${NC}"
    exit 0
fi

if ! $INTEGRATION_ONLY && ! $UNIT_ONLY; then
    if [[ ${#UNIT_PROJS[@]} -eq 0 ]] && [[ ${#INTEGRATION_PROJS[@]} -eq 0 ]]; then
        echo -e "${YELLOW}No test projects found in $TESTS_DIR${NC}"
        exit 0
    fi
fi

# ── Prerequisites ───────────────────────────────────────────
if ! command -v reportgenerator &>/dev/null; then
    echo -e "${RED}reportgenerator not found. Install it with:${NC}"
    echo "  dotnet tool install -g dotnet-reportgenerator-globaltool"
    exit 1
fi

# ── Prepare output dirs ─────────────────────────────────────
rm -rf "$MERGED_DIR"
mkdir -p "$MERGED_DIR"

COVERAGE_FILES=()

# ── Run unit tests ──────────────────────────────────────────
run_unit_tests() {
    if ! $INTEGRATION_ONLY && [[ ${#UNIT_PROJS[@]} -gt 0 ]]; then
        echo -e "\n${BOLD}${CYAN}━━━ Unit Tests ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        for proj in "${UNIT_PROJS[@]}"; do
            local proj_name="$(basename "$proj" .csproj)"
            local out_dir="$(dirname "$proj")/TestResults/coverage"
            mkdir -p "$out_dir"
            echo -e "  ${BOLD}$proj_name${NC}"

            dotnet test "$proj" \
                -c Release \
                -l "console;verbosity=normal" \
                /p:CollectCoverage=true \
                /p:CoverletOutputFormat=cobertura \
                /p:CoverletOutput="$out_dir/" \
                /p:ExcludeByFile="**/OpenApiXmlCommentSupport.generated.cs" \
                2>&1 | grep -E '(Passed|Failed|Total tests|Line|Branch|Method|Module|Total|Average)' || true

            local xml="$out_dir/coverage.cobertura.xml"
            if [[ -f "$xml" ]]; then
                COVERAGE_FILES+=("$xml")
            fi
        done
    fi
}

# ── Run integration tests ───────────────────────────────────
run_integration_tests() {
    if ! $UNIT_ONLY && [[ ${#INTEGRATION_PROJS[@]} -gt 0 ]]; then
        echo -e "\n${BOLD}${CYAN}━━━ Integration Tests ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        for proj in "${INTEGRATION_PROJS[@]}"; do
            local proj_name="$(basename "$proj" .csproj)"
            local out_dir="$(dirname "$proj")/TestResults/coverage"
            mkdir -p "$out_dir"
            echo -e "  ${BOLD}$proj_name${NC}"

            dotnet test "$proj" \
                -c Release \
                -l "console;verbosity=normal" \
                /p:CollectCoverage=true \
                /p:CoverletOutputFormat=cobertura \
                /p:CoverletOutput="$out_dir/" \
                /p:ExcludeByFile="**/OpenApiXmlCommentSupport.generated.cs" \
                2>&1 | grep -E '(Passed|Failed|Total tests|Line|Branch|Method|Module|Total|Average)' || true

            local xml="$out_dir/coverage.cobertura.xml"
            if [[ -f "$xml" ]]; then
                COVERAGE_FILES+=("$xml")
            fi
        done
    fi
}

run_unit_tests
run_integration_tests

# ── Merge & report ──────────────────────────────────────────
if [[ ${#COVERAGE_FILES[@]} -eq 0 ]]; then
    echo -e "${YELLOW}No coverage files generated.${NC}"
    exit 0
fi

echo -e "\n${BOLD}${CYAN}━━━ Merging & Reporting ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

REPORTS=$(printf ";%s" "${COVERAGE_FILES[@]}")
REPORTS="${REPORTS:1}"

reportgenerator \
    -reports:"$REPORTS" \
    -targetdir:"$MERGED_DIR" \
    -reporttypes:TextSummary\;Html\;CsvSummary \
    -classfilters:"+*" \
    2>&1

# ── Print summary ───────────────────────────────────────────
echo -e "\n${BOLD}${CYAN}━━━ Coverage Summary ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
cat "$MERGED_DIR/Summary.txt"

echo -e "\n${BOLD}Reports written to:${NC}"
echo -e "  ${GREEN}$MERGED_DIR/Summary.txt${NC}"
echo -e "  ${GREEN}$MERGED_DIR/Summary.csv${NC}"
echo -e "  ${GREEN}$MERGED_DIR/index.html${NC}"

if $OPEN_REPORT; then
    xdg-open "$MERGED_DIR/index.html" 2>/dev/null || open "$MERGED_DIR/index.html" 2>/dev/null || true
fi
