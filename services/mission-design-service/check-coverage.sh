#!/usr/bin/env bash
# Runs unit + integration tests with coverage, merges the cobertura reports
# (union of covered lines), and checks the >=95% line-coverage gate.
# Run with the VPN OFF so Testcontainers host->container networking works.
set -euo pipefail
cd "$(dirname "$0")"

# The Ryuk resource-reaper's host->container port connection is flaky on this
# Docker host and intermittently times out ("Initialization has been
# cancelled"). Disable it; Testcontainers still tears down its own containers
# at the end of the run.
export TESTCONTAINERS_RYUK_DISABLED=true

rm -rf ./TestResults
dotnet test tests/UnitTests/Application.UnitTests.csproj \
  --collect:"XPlat Code Coverage" --results-directory ./TestResults/unit -v quiet
dotnet test tests/IntegrationTests/Infrastructure.IntegrationTests.csproj \
  --collect:"XPlat Code Coverage" --results-directory ./TestResults/integration -v quiet

python3 - "$@" <<'PY'
import glob, xml.etree.ElementTree as ET

covered, valid = {}, {}   # keyed by (package, filename, line) -> hits>0 / exists
pkg_lines = {}            # package -> set of (filename,line)
pkg_hit   = {}            # package -> set of covered (filename,line)

for f in glob.glob('./TestResults/**/coverage.cobertura.xml', recursive=True):
    root = ET.parse(f).getroot()
    for p in root.iter('package'):
        name = p.attrib['name']
        pkg_lines.setdefault(name, set()); pkg_hit.setdefault(name, set())
        for cls in p.iter('class'):
            fn = cls.attrib.get('filename','')
            for ln in cls.iter('line'):
                num = ln.attrib['number']; hits = int(ln.attrib['hits'])
                key = (fn, num)
                pkg_lines[name].add(key)
                if hits > 0: pkg_hit[name].add(key)

tot_v = tot_c = 0
print("Per-package line coverage (unit + integration merged):")
for name in sorted(pkg_lines):
    v = len(pkg_lines[name]); c = len(pkg_hit[name])
    tot_v += v; tot_c += c
    print("  %-42s %6.2f%%  (%d/%d)" % (name, 100*c/v if v else 0, c, v))

overall = 100*tot_c/tot_v if tot_v else 0
print("\nOVERALL line coverage: %.2f%%  (%d/%d)" % (overall, tot_c, tot_v))
print("GATE >= 95%%: %s" % ("PASS" if overall >= 95 else "FAIL"))
PY
