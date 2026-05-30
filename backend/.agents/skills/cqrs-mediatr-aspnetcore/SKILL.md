---
name: cqrs-mediatr-aspnetcore
description: Designs and implements CQRS with MediatR in modern ASP.NET Core applications, including commands, queries, handlers, pipeline behaviors, validation, transactions, and endpoint wiring. Use when the user mentions CQRS, MediatR, mediator pattern, commands, queries, request handlers, pipeline behaviors, or wants ASP.NET Core API structure around request/response application flows.
---

# CQRS with MediatR for ASP.NET Core

Implement CQRS with MediatR in ASP.NET Core by fitting the existing solution structure first, then adding only the command/query boundaries, behaviors, and abstractions the codebase actually needs.

## Quick Start

1. Inspect the project first:
   - hosting style: controllers, Minimal APIs, mixed
   - current architecture: layered, Clean Architecture, modular monolith, feature folders
   - existing validation, persistence, transaction, and error-handling patterns
2. Decide whether CQRS is warranted:
   - use it for business workflows, distinct read/write needs, or non-trivial cross-cutting concerns
   - do not impose it on simple CRUD without clear payoff
3. Model the application contract:
   - commands change state and represent business intent
   - queries return read models and do not mutate state
   - notifications are for in-process fan-out, not command chaining
4. Implement the smallest vertical slice:
   - request type
   - handler
   - endpoint/controller action
   - validation and persistence
   - focused tests
5. Add cross-cutting behaviors deliberately:
   - validation
   - logging/tracing
   - transaction boundary for commands
   - authorization only when the application already centralizes it there

## Workflow

### 1. Shape the boundary

- Prefer task-based commands such as `ApproveInvoiceCommand`, not setter-style commands.
- Keep queries read-only and shaped to the caller's response needs.
- Return DTOs or read models from queries, not tracked domain entities.
- Keep one handler per request.

### 2. Keep handlers narrow

- Put orchestration in handlers.
- Put business invariants in the domain model or domain services, not in controllers/endpoints.
- Keep data access explicit.
- Pass `CancellationToken` all the way through.

### 3. Use pipeline behaviors for cross-cutting concerns

- Validation before the handler.
- Logging/metrics around the handler.
- Transaction behavior for state-changing commands only.
- Avoid putting core business decisions into behaviors.

### 4. Separate read and write concerns pragmatically

- It is enough to separate code paths first; separate databases only when the workload requires it.
- Queries may use a different persistence approach than commands.
- Commands should work against transactional consistency boundaries.

## Rules

- Never use MediatR as a replacement for basic method calls inside the same class or aggregate.
- Never let queries mutate state.
- Never publish domain events by recursively sending commands through MediatR.
- Never create generic `BaseCommandHandler` hierarchies unless the codebase already relies on them.
- Never add CQRS ceremony to simple endpoints without a concrete reason.

## References

- Implementation guidance: [REFERENCE.md](REFERENCE.md)
- Code templates and examples: [EXAMPLES.md](EXAMPLES.md)
