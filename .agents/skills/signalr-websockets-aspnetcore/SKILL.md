---
name: signalr-websockets-aspnetcore
description: Designs and implements production-grade real-time communication with ASP.NET Core SignalR, including hub contracts, connection lifecycle, authentication, groups, background publishing, reconnect behavior, and scale-out. Use when the user mentions SignalR, WebSockets, hubs, real-time updates, live notifications, streaming, groups, presence, reconnection, IHubContext, or ASP.NET Core real-time features.
---

# SignalR WebSockets for ASP.NET Core

Implement SignalR in modern ASP.NET Core applications while following the host application's existing architecture, DI, auth, configuration, and delivery semantics.

## Quick Start

1. Inspect the project first:
   - hosting model, auth stack, and endpoint conventions
   - existing DTO, serialization, and result patterns
   - current background services, domain events, and observability setup
2. Choose the real-time contract:
   - hub endpoint path
   - client method names or strongly typed hub interface
   - user, group, and connection targeting model
   - reconnect and offline behavior
3. Implement the smallest working slice:
   - `AddSignalR`
   - `MapHub`
   - hub class plus contract
   - one publish path from app code through `IHubContext`
4. Add lifecycle controls:
   - auth and authorization
   - `OnConnectedAsync` and `OnDisconnectedAsync`
   - group membership and presence rules
   - reconnect behavior and client recovery
5. Verify with focused tests and operational checks.

## Workflow

### 1. Shape the contract

- Prefer explicit DTOs over loose anonymous payloads.
- Prefer strongly typed hubs when the project values compile-time safety.
- Keep hub methods thin; route business work into application services.
- Decide whether messages are commands, notifications, streams, or presence updates.

### 2. Model the lifecycle

- Treat connection, authentication, authorization, group join, publish, reconnect, and disconnect as separate concerns.
- Use `OnConnectedAsync` only for connection-scoped initialization.
- Use `OnDisconnectedAsync` for cleanup and telemetry, not durable business compensation.
- Assume reconnect can create a new connection ID and require state rejoin.

### 3. Publish safely

- Do not inject hub instances directly; use `IHubContext`.
- Publish from controllers, handlers, workers, or domain-event adapters through application boundaries.
- Use cancellation tokens and await hub sends.
- Keep transport concerns out of domain entities.

### 4. Operate in production

- Secure origins, tokens, and hub methods.
- Tune transports and buffer limits only when measurements justify it.
- Plan scale-out before building presence or group-heavy features.
- Add logs, metrics, and correlation around connect, disconnect, send, and failure paths.

## Rules

- Never store mutable shared state in hub instance fields.
- Never use SignalR as the source of truth for durable workflow state.
- Never trust client-supplied group, user, or tenant identifiers without authorization checks.
- Never expose sensitive identifiers or tokens in logs or payloads.
- Never assume delivery, ordering, or reconnect behavior is stronger than what the app explicitly implements.

## References

- Implementation guidance: [REFERENCE.md](REFERENCE.md)
- Code templates and examples: [EXAMPLES.md](EXAMPLES.md)
