---
name: aspnet-backend-testing
description: Designs and implements testing strategy for ASP.NET backend projects using Clean Architecture, including unit, integration, and end-to-end tests, recommended .NET test libraries, and coverage enforcement by layer. Use when the user mentions ASP.NET testing, xUnit, Moq, NUnit, MSTest, integration tests, WebApplicationFactory, Testcontainers, Playwright, coverage, or wants to test a Clean Architecture backend.
---

# ASP.NET Backend Testing

Build the test strategy around the solution's existing layers first, then place each test in the layer that owns the behavior.

## Quick Start

1. Inspect the solution:
   - current layers, projects, and naming
   - existing test framework and assertion style
   - runtime dependencies: database, broker, cache, HTTP services
   - whether the system is API-only or includes a web UI
2. Default library choices:
   - unit tests: `xUnit` + `Moq`
   - API integration tests: `xUnit` + `Microsoft.AspNetCore.Mvc.Testing`
   - infrastructure integration tests: `xUnit` + `Testcontainers`
   - end-to-end tests: `xUnit` + `Microsoft.Playwright.Xunit` when a browser client exists
3. Map tests to Clean Architecture:
   - Domain: unit tests for invariants and pure business rules
   - Application: unit tests for handlers, validators, and orchestration
   - Infrastructure: integration tests with real technical dependencies
   - Presentation/API: integration tests through the ASP.NET host
   - End-to-end: only critical full-system workflows
4. Enforce a minimum of 95% line coverage for the project.

## Workflow

### 1. Choose the correct test type

- Unit tests:
  - use `xUnit` by default
  - use `Moq` only for outbound ports and collaborators that should stay outside the test
  - keep Domain and Application tests free of real infrastructure
- Integration tests:
  - use `Microsoft.AspNetCore.Mvc.Testing`, `WebApplicationFactory`, and `TestServer` for API-host testing
  - use `Testcontainers` for real databases, brokers, and other production-like dependencies
- End-to-end tests:
  - use `Microsoft.Playwright.Xunit` only when a web UI is part of the product
  - for backend-only systems, prefer black-box system tests against the running API instead of inventing browser coverage

### 2. Respect layer ownership

- Do not test domain invariants through controllers.
- Do not use unit tests to prove EF Core mappings, SQL, or middleware behavior.
- Do not mock what the test actually needs to verify at the framework or infrastructure boundary.

### 3. Keep the pyramid intact

- many Domain and Application unit tests
- fewer Infrastructure and API integration tests
- very few end-to-end tests

## Rules

- Never use end-to-end tests to cover business-rule permutations that belong in unit tests.
- Never rely on EF Core in-memory behavior as proof that the production database works.
- Never claim the 95% threshold is met without collecting and checking coverage output.

## References

- Detailed guidance: [REFERENCE.md](REFERENCE.md)
- Example library stacks and commands: [EXAMPLES.md](EXAMPLES.md)
- Coverage threshold helper: [scripts/check_cobertura_threshold.py](scripts/check_cobertura_threshold.py)
