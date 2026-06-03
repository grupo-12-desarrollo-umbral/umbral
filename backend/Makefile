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

# Project discovery — service-agnostic, mirrors cover.sh. There is no .sln, so
# Api (the composition root) plus the test projects span the whole build graph.
API_PROJECT          := $(wildcard $(SVCDIR)/src/Api/*.csproj)
UNIT_PROJECTS        := $(shell find $(SVCDIR)/tests -maxdepth 3 -name '*UnitTests*.csproj'        2>/dev/null | sort)
INTEGRATION_PROJECTS := $(shell find $(SVCDIR)/tests -maxdepth 3 -name '*IntegrationTests*.csproj' 2>/dev/null | sort)

.PHONY: help build test gate ef

help:
	@echo "Targets (pass SVC=<service>):"
	@echo "  build   compile Api + all test projects"
	@echo "  test    run unit + integration test projects"
	@echo "  gate    ADR-0005 coverage gate (cover-gate.sh, integration project last)"
	@echo "  ef      dotnet ef passthrough — also pass ARGS=\"migrations add ...\""

build:
	@for p in $(API_PROJECT) $(UNIT_PROJECTS) $(INTEGRATION_PROJECTS); do \
	  echo ">> dotnet build $$p"; dotnet build "$$p" || exit 1; \
	done

test:
	@for p in $(UNIT_PROJECTS) $(INTEGRATION_PROJECTS); do \
	  echo ">> dotnet test $$p"; dotnet test "$$p" || exit 1; \
	done

# Run from the service directory so cover-gate.sh resolves its relative project
# paths and writes coverage/gate/ under the service (unchanged from the prior
# hand-invocation). The integration project is passed last — it enforces the
# threshold, per ADR-0005.
gate:
	@cd $(SVCDIR) && \
	  units=$$(find tests -maxdepth 3 -name '*UnitTests*.csproj'        2>/dev/null | sort) && \
	  integ=$$(find tests -maxdepth 3 -name '*IntegrationTests*.csproj' 2>/dev/null | sort) && \
	  ../../scripts/cover-gate.sh $$units $$integ

ef:
	dotnet ef $(ARGS) \
	  --startup-project $(SVCDIR)/src/Api \
	  --project $(SVCDIR)/src/Infrastructure
