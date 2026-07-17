# backend/Makefile
# ─────────────────────────────────────────────────────────────────────────────
# Sandbox-robust entrypoints for the .NET toolchain. Every caller — Claude Code,
# Codex, CI, or a human — goes through these targets, so the environment that
# keeps `dotnet` working under a filesystem/network sandbox lives here ONCE.
# Callers never need to remember per-command prefixes.
#
#   make build SVC=identity-access-service
#   make test  SVC=identity-access-service
#   make gate  SVC=identity-access-service
#   make ef    SVC=identity-access-service ARGS="migrations add AddFoo"
#
# Run from backend/, or use `make -C backend <target> SVC=<service>`.
# ─────────────────────────────────────────────────────────────────────────────

# A sandbox blocks MSBuild's named-pipe node-reuse workers, which crashes dotnet
# before it does any work; disabling reuse here means no caller needs the
# MSBUILDDISABLENODEREUSE prefix. Telemetry opt-out avoids a stray first-run
# network call that would otherwise trip the network sandbox. Exported, so they
# reach every recipe shell and every child process (including cover-gate.sh).
export MSBUILDDISABLENODEREUSE := 1
export DOTNET_CLI_TELEMETRY_OPTOUT := 1
export DOTNET_NOLOGO := 1

SVC ?= identity-access-service

# Services live under services/; the api-gateway does not — it is a YARP reverse
# proxy with a single flat src/ project, not a Clean Architecture service. Resolving
# its root here is what lets `SVC=api-gateway` flow through build/test unchanged.
SVCDIR := $(if $(filter api-gateway,$(SVC)),api-gateway,services/$(SVC))

# Services that have converged onto the ADR-0011 vertical-slice layout. `build`
# runs the structural guard (scripts/structure-guard.sh) ONLY for these — a service
# still mid-migration (Phase 2) is skipped so its build isn't blocked before its
# slice-collapse lands. Add a service here the moment that collapse merges; that one
# edit flips its build from "skipped" to "enforcing". The standalone
# `make structure-guard` still checks every service regardless of this list.
CONVERGED_SERVICES := mission-design-service session-operations-service identity-access-service scoring-monitoring-service

# Verbose dotnet output (restore/build/per-test spew) goes to a log file; the
# recipes echo only a compact summary. An agent driving the toolchain reads the
# exit code + summary and opens the log only on failure, so its *resident*
# context stays tiny on any CLI — the verbose stream is the single largest token
# sink in a verification phase, and amplification (the context is re-sent every
# turn) multiplies it. Absolute path, because `gate` cd's into the service dir.
# Gitignored.
LOGDIR := $(CURDIR)/.make-logs
COVERAGE_ROOT := $(CURDIR)/coverage

# Project discovery — service-agnostic, mirrors cover.sh. There is no .sln, so
# Api (the composition root) plus the test projects span the whole build graph.
# The second wildcard is the api-gateway's flat src/ApiGateway.csproj; no service
# keeps a csproj directly under src/ (theirs sit in src/Api, src/Domain, …), so
# the two patterns never both match and this stays a single-project variable.
API_PROJECT          := $(wildcard $(SVCDIR)/src/Api/*.csproj) $(wildcard $(SVCDIR)/src/*.csproj)
UNIT_PROJECTS        := $(shell find $(SVCDIR)/tests -maxdepth 3 -name '*UnitTests*.csproj'        2>/dev/null | sort)
INTEGRATION_PROJECTS := $(shell find $(SVCDIR)/tests -maxdepth 3 -name '*IntegrationTests*.csproj' 2>/dev/null | sort)

# Every domain service coverlet can actually measure: one with an in-process
# IntegrationTests project (the gate runs it last to enforce the threshold).
# Discovered, not hardcoded, so a new service is gated the day it grows tests —
# and a scaffold without tests (scoring-monitoring-service) is skipped. The
# api-gateway is intentionally absent: its only test is a full-stack E2E that
# runs the gateway in a separate container, so coverlet sees ~0% of it (it is a
# behavioural gate, not a coverlet report).
GATEABLE_SERVICES := $(sort $(foreach p,$(wildcard services/*/tests/IntegrationTests/*.csproj),$(word 2,$(subst /, ,$(p)))))

.PHONY: help build test gate gate-all clean clean-all clean-artifacts guard-artifacts structure-guard structure-guard-converged layer-guard layer-guard-layered ef rewire doctor

# Compose service names of the hot-reload dev containers (docker-compose.override.yml):
# every service/ plus the gateway, which lives outside services/.
DEV_SERVICES := api-gateway $(notdir $(wildcard services/*))

# Fail fast (before any dotnet call) when this service's bin/obj are owned by
# someone other than you — the #1 cause of the confusing NuGet "Access to the
# path '…/obj/<guid>.tmp' is denied / Permission denied" failure. A
# containerized `dotnet watch` dev stack (docker compose) rebuilds obj as
# root/nobody on every file change, and a sandboxed agent (Codex bwrap) writes
# them as nobody too; either leaves the host build unable to write. Surface the
# exact remedy instead of a stack trace. CI is unaffected (it builds fresh as a
# single user, so nothing is foreign-owned). See docs/codex-sandbox-setup.md.
guard-artifacts:
	@foreign=$$(find $(SVCDIR) -type d \( -name bin -o -name obj \) ! -user "$$(id -un)" 2>/dev/null); \
	  if [ -n "$$foreign" ]; then \
	    echo "✗ pre-flight: bin/obj under $(SVC) are owned by another user (not $$(id -un)):"; \
	    printf '    %s\n' $$foreign | head -5; \
	    echo "  Likely a containerized 'dotnet watch' dev stack or a sandboxed agent wrote them."; \
	    echo "  Fix:  docker compose down              # if the dev stack is up (check: docker ps)"; \
	    echo "        make -C backend clean-artifacts   # reclaim the foreign-owned trees"; \
	    echo "  Details: backend/docs/codex-sandbox-setup.md"; \
	    exit 1; \
	  fi

build test gate: guard-artifacts

# Phase 3 wiring: `build` enforces the ADR-0011 structural layout, but only for
# services on CONVERGED_SERVICES (see above). Accumulates as an extra prerequisite
# of `build` — the build recipe itself is unchanged.
build: structure-guard-converged

# `build` also enforces the Clean Architecture dependency rule against cross-layer
# reflection laundering (scripts/layer-guard.sh). The prohibition has no per-service
# migration, so every LAYERED service's build runs it (scoped to $(SVC) when set) —
# see layer-guard-layered for the one exemption. Extra prerequisite; build recipe
# unchanged.
build: layer-guard-layered

help:
	@echo "Targets (pass SVC=<service>, or SVC=api-gateway):"
	@echo "  build   compile Api + all test projects"
	@echo "  test    run unit + integration test projects"
	@echo "  (SVC=api-gateway builds/tests the gateway; its guards and coverage gate do not apply,"
	@echo "   and its AuthPath E2E is excluded — run that via api-gateway/tests/docker-compose.auth-tests.yml)"
	@echo "  gate    ADR-0005 coverage gate (cover-gate.sh, integration project last)"
	@echo "  gate-all  run gate for every gateable service (one report each)"
	@echo "  clean   remove bin/obj for SVC (resets stale build artifacts)"
	@echo "  clean-all remove bin/obj across every service"
	@echo "  clean-artifacts reclaim stale + foreign-owned (Codex nobody:nogroup) bin/obj incl. api-gateway; uses sudo if needed"
	@echo "  ef      dotnet ef passthrough — also pass ARGS=\"migrations add ...\""
	@echo "  rewire  restart SVC's dev container + wait until it serves (after a DI/wiring/startup edit hot reload can't apply)"
	@echo "  doctor  check the dev stack for disarmed watchers / in-tree binaries — the failures /health cannot see (all services, or SVC=<svc>)"
	@echo "  structure-guard  enforce the ADR-0011 Application-layer vertical-slice layout (all services, or SVC=<svc>)"
	@echo "  layer-guard      forbid cross-layer reflection laundering (inner layer naming an Api type by string; all services, or SVC=<svc>)"
	@echo "  (build/test/gate pre-flight-check bin/obj ownership; foreign-owned → fail fast with the fix)"
	@echo "  (build/test/gate echo a compact summary; full dotnet output → $(LOGDIR)/<target>-<svc>.log)"

build:
	@mkdir -p $(LOGDIR); log="$(LOGDIR)/build-$(SVC).log"; : > "$$log"; \
	for p in $(API_PROJECT) $(UNIT_PROJECTS) $(INTEGRATION_PROJECTS); do \
	  echo ">> dotnet build $$p"; \
	  if ! dotnet build "$$p" >>"$$log" 2>&1; then \
	    echo "BUILD FAILED: $$p"; tail -n 40 "$$log"; \
	    echo "(full log: $$log)"; exit 1; \
	  fi; \
	done; \
	echo "build OK ($(SVC)) — full log: $$log"

test:
	@mkdir -p $(LOGDIR); log="$(LOGDIR)/test-$(SVC).log"; : > "$$log"; \
	for p in $(UNIT_PROJECTS) $(INTEGRATION_PROJECTS); do \
	  echo ">> dotnet test $$p"; \
	  if ! dotnet test "$$p" >>"$$log" 2>&1; then \
	    echo "TEST FAILED: $$p"; \
	    grep -hE 'Failed |Error Message|Expected:|Actual:|Assert\.' "$$log" | tail -n 60 || true; \
	    echo "(full log: $$log)"; exit 1; \
	  fi; \
	done; \
	grep -hE 'Passed!' "$$log" || true; \
	echo "test OK ($(SVC)) — full log: $$log"

# Run from the service directory so cover-gate.sh resolves its relative project
# paths, while COVERAGE_DIR centralizes reports under backend/coverage/<service>/.
# The integration project is passed last — it enforces the threshold, per ADR-0005.
# Full cover-gate.sh output → log; echo only the per-project pass counts, the
# Gate GREEN/FAILED verdict, and the line-coverage number. On a red gate, print
# a bounded failing-test excerpt the driver can hand straight to the subagent
# (Step E). The gate verdict is still the EXIT CODE (preserved via $$rc) — the
# summary is a convenience, never the source of truth.
gate:
	@mkdir -p $(LOGDIR); log="$(LOGDIR)/gate-$(SVC).log"; \
	report_dir="$(COVERAGE_ROOT)/$(SVC)"; \
	cd $(SVCDIR) && \
	  units=$$(find tests -maxdepth 3 -name '*UnitTests*.csproj'        2>/dev/null | sort) && \
	  integ=$$(find tests -maxdepth 3 -name '*IntegrationTests*.csproj' 2>/dev/null | sort) && \
	  COVERAGE_DIR="$$report_dir" ../../scripts/cover-gate.sh $$units $$integ >"$$log" 2>&1; rc=$$?; \
	  grep -hE 'Passed!|Failed!|Gate (GREEN|FAILED)' "$$log" || true; \
	  [ -f "$$report_dir/Summary.txt" ] && grep -E 'Line coverage' "$$report_dir/Summary.txt" | head -n1 || true; \
	  if [ $$rc -ne 0 ]; then \
	    echo "── failing tests (excerpt) ──"; \
	    grep -hE 'Failed |Error Message|Expected:|Actual:|Assert\.' "$$log" | tail -n 60 || true; \
	    echo "gate FAILED ($(SVC)) — full log: $$log"; \
	  else \
	    echo "gate GREEN ($(SVC)) — report: $$report_dir/index.html, full log: $$log"; \
	  fi; \
	  exit $$rc

# Automates what the driver runs by hand after each Phase X.4: the same
# per-service gate (ADR-0005) for every gateable service, one merged report each
# under backend/coverage/<service>/. Reuses the `gate` target verbatim — no second copy
# of the coverlet chain. Does NOT stop at the first red: every service runs so
# you get the full picture, then it exits non-zero if any gate failed (CI uses a
# per-service matrix instead, so a failure isolates to one job).
gate-all:
	@failed=""; \
	for svc in $(GATEABLE_SERVICES); do \
	  echo "═══ gate $$svc ═══"; \
	  $(MAKE) --no-print-directory gate SVC=$$svc || failed="$$failed $$svc"; \
	done; \
	echo; echo "─── coverage gate summary ───"; \
	for svc in $(GATEABLE_SERVICES); do \
	  case " $$failed " in *" $$svc "*) echo "  ✗ $$svc";; *) echo "  ✓ $$svc";; esac; \
	done; \
	if [ -n "$$failed" ]; then echo "Gate FAILED for:$$failed" >&2; exit 1; fi; \
	echo "All gates GREEN."

# Reload a service's COMPOSITION ROOT after a DI / wiring / middleware / startup
# edit. `dotnet watch` Hot Reload patches method bodies in the RUNNING process and
# never re-runs startup, so DI registrations, MassTransit `AddConsumer<T>`, YARP
# routes, and pipeline changes are silently ignored until the process restarts
# (full rationale in the header of docker-compose.override.yml). A container
# restart is the reliable, graceful way to do it: the old process fully exits and
# frees :8080 before the new one binds, so it avoids the port race that dead-ends
# the watcher on an ordinary restart (also documented there — `make doctor` detects
# that state). Pure code/logic edits do NOT need this — they hot-reload instantly.
#
# Restarts SVC's container, then polls it via the dynamically-resolved host port
# (so it is port-map-agnostic) until it serves HTTP again — a definite "back up"
# signal instead of a guess. Ready = any non-5xx response (services answer /health
# 200; the gateway has no health route and 404s, which still proves it is serving).
# SVC is the compose service name, e.g. session-operations-service or api-gateway.
#   make rewire SVC=session-operations-service
rewire:
	@echo "rewire $(SVC): docker compose restart + wait for it to serve …"; \
	docker compose restart $(SVC) || { echo "  restart failed — is the dev stack up? (docker compose up -d)"; exit 1; }; \
	hostport=$$(docker compose port $(SVC) 8080 2>/dev/null); \
	if [ -z "$$hostport" ]; then \
	  echo "  could not resolve $(SVC)'s host port for container port 8080 (is it running/published?)."; exit 1; \
	fi; \
	url="http://$$hostport/health"; \
	for i in $$(seq 1 60); do \
	  code=$$(curl -s -o /dev/null -w '%{http_code}' "$$url" 2>/dev/null); \
	  if [ "$$code" != "000" ] && [ "$$code" -lt 500 ]; then \
	    echo "  $(SVC) serving again ($$url -> $$code) after ~$$((i*3))s."; exit 0; \
	  fi; \
	  sleep 3; \
	done; \
	echo "  TIMED OUT after 180s waiting for $(SVC) to serve. Inspect: docker compose logs --tail=60 $(SVC)"; exit 1

# Dev-loop health check. Detects the two ways this stack lies about being healthy —
# both invisible to /health, which is why they cost whole debugging sessions:
#   1. Watcher disarmed. A restart lost the race for :8080, watch dead-ended, and
#      the PREVIOUS app process is still serving. Health 200, code stale (see the
#      KNOWN TRAP header in docker-compose.override.yml). Signature: an app process
#      is alive AND watch's last word is "Waiting for a file to change".
#   2. In-tree binary. The process runs from src/**/bin/ instead of /tmp/umbral-*/,
#      so it started before the Directory.Build.props redirect and still shares
#      bin/obj with host builds — the collision that fakes CS1061 "does not contain
#      a definition for …" errors a restart cannot clear (override header, line ~49).
# Read-only: reports and names the fix, never restarts (that is `rewire`, which the
# caller should decide to run). Checks every dev service, or SVC=<svc> for one.
# Non-zero exit when anything needs attention, so a script can gate on it.
#   make doctor
doctor:
	@svcs="$(DEV_SERVICES)"; \
	if [ "$(origin SVC)" = "command line" ]; then svcs="$(SVC)"; fi; \
	bad=""; \
	for svc in $$svcs; do \
	  if [ -z "$$(docker compose ps -q $$svc 2>/dev/null)" ]; then \
	    printf '  ? %-30s not running (dev stack down? docker compose up -d)\n' "$$svc"; continue; \
	  fi; \
	  app=$$(docker compose top $$svc 2>/dev/null | awk 'NR>1 && $$NF ~ /bin\/Debug/ {print $$NF}' | head -1); \
	  watch=$$(docker compose logs $$svc 2>/dev/null | grep 'dotnet watch' | tail -1); \
	  case "$$watch" in *"Waiting for a file to change"*) waiting=1;; *) waiting=0;; esac; \
	  if [ -n "$$app" ] && [ $$waiting -eq 1 ]; then \
	    printf '  ✗ %-30s WATCHER DISARMED — old process still serving, edits will NOT apply\n' "$$svc"; \
	    bad="$$bad $$svc"; \
	  elif [ -z "$$app" ] && [ $$waiting -eq 1 ]; then \
	    printf '  ✗ %-30s app process DOWN — watcher waiting for a change (build broken?)\n' "$$svc"; \
	    printf '      %s\n' "docker compose logs --tail=60 $$svc"; \
	    bad="$$bad $$svc"; \
	  elif [ -z "$$app" ]; then \
	    printf '  ? %-30s no app process yet (still building — re-run in ~30s)\n' "$$svc"; \
	  else \
	    case "$$app" in \
	      /tmp/*) printf '  ✓ %-30s watcher armed\n' "$$svc";; \
	      *) printf '  ✗ %-30s IN-TREE binary — shares bin/obj with host builds\n' "$$svc"; \
	         printf '      %s\n' "$$app"; \
	         printf '      started before the Directory.Build.props /tmp redirect\n'; \
	         bad="$$bad $$svc";; \
	    esac; \
	  fi; \
	done; \
	if [ -n "$$bad" ]; then \
	  echo; echo "  Fix — restart the affected services:"; \
	  for svc in $$bad; do echo "        make -C backend rewire SVC=$$svc"; done; \
	  exit 1; \
	fi; \
	echo "  dev stack healthy — every watcher armed, every binary out-of-tree."

# Remove build artifacts so the next build/gate starts from a clean tree. This
# is the fast recovery path when a sandboxed toolchain run leaves `bin/` or
# `obj/` with bad ownership metadata (for example `nobody:nogroup` from a bwrap
# user namespace) and the next host-side run refuses to rewrite them. It also
# clears the normal host-owned trees.
clean:
	@find $(SVCDIR) -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null; \
	  echo "Cleaned bin/obj under $(SVCDIR)."

clean-all:
	@find services -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null; \
	  echo "Cleaned bin/obj across all services."

# Cross-service recovery for poisoned/stale build artifacts. Unlike clean-all,
# this also covers api-gateway (outside services/) AND reclaims FOREIGN-OWNED
# trees — the nobody:nogroup bin/obj that Codex's bwrap sandbox leaves behind
# (see docs/codex-sandbox-setup.md). Those dirs lack the write bit for the
# current user, so a plain rm can't empty them; sudo is required, and only works
# from a real host shell (a sandbox can't escalate — "no new privileges"). The
# recipe escalates ONLY when such trees survive the ordinary pass, and FAILS
# LOUDLY with the exact manual command rather than echoing a false success.
clean-artifacts:
	@find services api-gateway -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null; \
	  foreign=$$(find services api-gateway -type d \( -name bin -o -name obj \) ! -user "$$(id -un)" 2>/dev/null); \
	  if [ -n "$$foreign" ]; then \
	    echo "Foreign-owned artifacts remain (e.g. Codex nobody:nogroup) — escalating with sudo…"; \
	    sudo find services api-gateway -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true; \
	    foreign=$$(find services api-gateway -type d \( -name bin -o -name obj \) ! -user "$$(id -un)" 2>/dev/null); \
	  fi; \
	  if [ -n "$$foreign" ]; then \
	    echo "clean-artifacts FAILED — could not remove these (run from a host shell with sudo):"; \
	    printf '  %s\n' "$$foreign"; \
	    echo "  sudo find backend -type d \\( -name bin -o -name obj \\) -prune -exec rm -rf {} +"; \
	    exit 1; \
	  fi; \
	  echo "Cleaned bin/obj across all services (and api-gateway)."

ef:
	dotnet ef $(ARGS) \
	  --startup-project $(SVCDIR)/src/Api \
	  --project $(SVCDIR)/src/Infrastructure

# Structural CI guard for the ADR-0011 vertical-slice layout (no dotnet, so no
# bin/obj pre-flight needed). Scans every service's Application/ tree by default;
# pass SVC=<service> to scope to one. SVC has a makefile default (?=), so it is
# forwarded to the script ONLY when set on the command line — otherwise the guard
# runs across all services. See scripts/structure-guard.sh for the rules.
structure-guard:
	@if [ "$(origin SVC)" = "command line" ]; then \
	  SVC="$(SVC)" scripts/structure-guard.sh; \
	else \
	  SVC="" scripts/structure-guard.sh; \
	fi

# Clean Architecture dependency-rule guard: forbids an inner layer (Domain/
# Application/Infrastructure) from naming or reflectively resolving an Api-layer
# type — the reflection/type-name-string laundering that .csproj refs and
# NetArchTest cannot see (no dotnet, so no bin/obj pre-flight needed). Scans every
# service by default; SVC=<service> narrows to one, forwarded only when set on the
# command line. See scripts/layer-guard.sh for the rules.
layer-guard:
	@if [ "$(origin SVC)" = "command line" ]; then \
	  SVC="$(SVC)" scripts/layer-guard.sh; \
	else \
	  SVC="" scripts/layer-guard.sh; \
	fi

# Build-time gate: the dependency rule applies to every service, but NOT to the
# api-gateway — a reverse proxy with no Domain/Application/Infrastructure trees,
# where layer-guard.sh exits non-zero precisely because it finds nothing to check.
# Exempting it here is what keeps `build SVC=api-gateway` from failing on a rule it
# cannot violate. The standalone `make layer-guard` still scans every service.
# The origin check mirrors the `layer-guard` target: SVC has a makefile default, so
# it narrows the scan only when set on the command line.
layer-guard-layered:
	@if [ "$(SVCDIR)" = "api-gateway" ]; then \
	  echo "layer-guard: api-gateway is a reverse proxy with no inner layers — not applicable, skipped."; \
	elif [ "$(origin SVC)" = "command line" ]; then \
	  SVC="$(SVC)" scripts/layer-guard.sh; \
	else \
	  SVC="" scripts/layer-guard.sh; \
	fi

# Build-time gate: run the structural guard for $(SVC) only when it has converged
# (is in CONVERGED_SERVICES). Mid-migration services are skipped with a hint, so
# their build is never blocked before Phase 2 collapses them. The api-gateway is
# exempt for the same reason as layer-guard-layered — it has no Application/ tree.
structure-guard-converged:
	@if [ "$(SVCDIR)" = "api-gateway" ]; then \
	  echo "structure-guard: api-gateway is a reverse proxy with no Application/ layer — not applicable, skipped."; \
	elif echo " $(CONVERGED_SERVICES) " | grep -q " $(SVC) "; then \
	  SVC="$(SVC)" scripts/structure-guard.sh; \
	else \
	  echo "structure-guard: $(SVC) not yet converged (ADR-0011) — skipped in build; preview with 'make structure-guard SVC=$(SVC)'."; \
	fi
