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
SVCDIR := services/$(SVC)

# Services that have converged onto the ADR-0011 vertical-slice layout. `build`
# runs the structural guard (scripts/structure-guard.sh) ONLY for these — a service
# still mid-migration (Phase 2) is skipped so its build isn't blocked before its
# slice-collapse lands. Add a service here the moment that collapse merges; that one
# edit flips its build from "skipped" to "enforcing". The standalone
# `make structure-guard` still checks every service regardless of this list.
CONVERGED_SERVICES := mission-design-service session-operations-service identity-access-service

# Verbose dotnet output (restore/build/per-test spew) goes to a log file; the
# recipes echo only a compact summary. An agent driving the toolchain reads the
# exit code + summary and opens the log only on failure, so its *resident*
# context stays tiny on any CLI — the verbose stream is the single largest token
# sink in a verification phase, and amplification (the context is re-sent every
# turn) multiplies it. Absolute path, because `gate` cd's into the service dir.
# Gitignored.
LOGDIR := $(CURDIR)/.make-logs

# Project discovery — service-agnostic, mirrors cover.sh. There is no .sln, so
# Api (the composition root) plus the test projects span the whole build graph.
API_PROJECT          := $(wildcard $(SVCDIR)/src/Api/*.csproj)
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

.PHONY: help build test gate gate-all clean clean-all clean-artifacts guard-artifacts structure-guard structure-guard-converged layer-guard ef

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
# reflection laundering (scripts/layer-guard.sh). Unlike the structural guard this
# is UNCONDITIONAL — the prohibition has no per-service migration, so every build
# runs it (scoped to $(SVC) when set). Extra prerequisite; build recipe unchanged.
build: layer-guard

help:
	@echo "Targets (pass SVC=<service>):"
	@echo "  build   compile Api + all test projects"
	@echo "  test    run unit + integration test projects"
	@echo "  gate    ADR-0005 coverage gate (cover-gate.sh, integration project last)"
	@echo "  gate-all  run gate for every gateable service (one report each)"
	@echo "  clean   remove bin/obj for SVC (resets stale build artifacts)"
	@echo "  clean-all remove bin/obj across every service"
	@echo "  clean-artifacts reclaim stale + foreign-owned (Codex nobody:nogroup) bin/obj incl. api-gateway; uses sudo if needed"
	@echo "  ef      dotnet ef passthrough — also pass ARGS=\"migrations add ...\""
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
# paths and writes coverage/gate/ under the service (unchanged from the prior
# hand-invocation). The integration project is passed last — it enforces the
# threshold, per ADR-0005.
# Full cover-gate.sh output → log; echo only the per-project pass counts, the
# Gate GREEN/FAILED verdict, and the line-coverage number. On a red gate, print
# a bounded failing-test excerpt the driver can hand straight to the subagent
# (Step E). The gate verdict is still the EXIT CODE (preserved via $$rc) — the
# summary is a convenience, never the source of truth.
gate:
	@mkdir -p $(LOGDIR); log="$(LOGDIR)/gate-$(SVC).log"; \
	cd $(SVCDIR) && \
	  units=$$(find tests -maxdepth 3 -name '*UnitTests*.csproj'        2>/dev/null | sort) && \
	  integ=$$(find tests -maxdepth 3 -name '*IntegrationTests*.csproj' 2>/dev/null | sort) && \
	  ../../scripts/cover-gate.sh $$units $$integ >"$$log" 2>&1; rc=$$?; \
	  grep -hE 'Passed!|Failed!|Gate (GREEN|FAILED)' "$$log" || true; \
	  [ -f coverage/gate/Summary.txt ] && grep -E 'Line coverage' coverage/gate/Summary.txt | head -n1 || true; \
	  if [ $$rc -ne 0 ]; then \
	    echo "── failing tests (excerpt) ──"; \
	    grep -hE 'Failed |Error Message|Expected:|Actual:|Assert\.' "$$log" | tail -n 60 || true; \
	    echo "gate FAILED ($(SVC)) — full log: $$log"; \
	  else \
	    echo "gate GREEN ($(SVC)) — report: $(SVCDIR)/coverage/gate/index.html, full log: $$log"; \
	  fi; \
	  exit $$rc

# Automates what the driver runs by hand after each Phase X.4: the same
# per-service gate (ADR-0005) for every gateable service, one merged report each
# under <svc>/coverage/gate/. Reuses the `gate` target verbatim — no second copy
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

# Build-time gate: run the structural guard for $(SVC) only when it has converged
# (is in CONVERGED_SERVICES). Mid-migration services are skipped with a hint, so
# their build is never blocked before Phase 2 collapses them.
structure-guard-converged:
	@if echo " $(CONVERGED_SERVICES) " | grep -q " $(SVC) "; then \
	  SVC="$(SVC)" scripts/structure-guard.sh; \
	else \
	  echo "structure-guard: $(SVC) not yet converged (ADR-0011) — skipped in build; preview with 'make structure-guard SVC=$(SVC)'."; \
	fi
