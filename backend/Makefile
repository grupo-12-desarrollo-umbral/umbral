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

.PHONY: help build test gate gate-all clean clean-all ef

help:
	@echo "Targets (pass SVC=<service>):"
	@echo "  build   compile Api + all test projects"
	@echo "  test    run unit + integration test projects"
	@echo "  gate    ADR-0005 coverage gate (cover-gate.sh, integration project last)"
	@echo "  gate-all  run gate for every gateable service (one report each)"
	@echo "  clean   remove bin/obj for SVC (resets stale build artifacts)"
	@echo "  clean-all remove bin/obj across every service"
	@echo "  ef      dotnet ef passthrough — also pass ARGS=\"migrations add ...\""
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

# Remove build artifacts so the next build/gate starts from a clean tree. The
# hot-reload override runs `dotnet watch` as root over the bind-mounted source,
# so its bin/obj land back here owned by root and block a later host-run gate
# (UnauthorizedAccessException on *.deps.json). This clears the host-owned ones;
# a root-owned remnant from the containers still needs a one-time
# `sudo find services -type d \( -name bin -o -name obj \) -exec rm -rf {} +`.
clean:
	@find $(SVCDIR) -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null; \
	  echo "Cleaned bin/obj under $(SVCDIR)."

clean-all:
	@find services -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null; \
	  echo "Cleaned bin/obj across all services."

ef:
	dotnet ef $(ARGS) \
	  --startup-project $(SVCDIR)/src/Api \
	  --project $(SVCDIR)/src/Infrastructure
