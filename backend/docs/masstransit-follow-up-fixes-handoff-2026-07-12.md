# HANDOFF — MassTransit architecture-record and false-green test fixes

**Date:** 2026-07-12  
**Next-session focus:** Apply the two follow-up fixes found while reviewing merged PR [#189](https://github.com/grupo-12-desarrollo-umbral/umbral/pull/189) against the repository standards and the RabbitMQ audit-scope handoff.

## TL;DR — how it is now

- PR #189 correctly implements the deliberately vanilla MassTransit/RabbitMQ production path requested by issue #164: `AddMassTransit(...).UsingRabbitMq(...)`, direct `IPublishEndpoint.Publish(...)`, `[EntityName]`, native topology, configuration binding, Generic Host lifecycle, and no custom publisher wrapper, retry policy, formatter, or outbox.
- The publish runs post-commit and carries the required linked 5-second timeout. Broker-down publication is therefore **bounded-blocking best effort**, not structurally non-blocking. Preserve this behavior exactly; see [`gh-164-masstransit-integration-test-hang-postmortem-2026-07-12.md`](./gh-164-masstransit-integration-test-hang-postmortem-2026-07-12.md).
- Two cleanup defects remain: an older architectural rule still mandates a project-owned publisher abstraction, and RabbitMQ Testcontainers tests can report success after catching arbitrary broker-startup failures.

## Fix 1 — reconcile the architecture records

### Why, TL;DR

The current code and issue #164 intentionally allow an Application event handler to depend on MassTransit's transport-neutral `IPublishEndpoint`. However, [`docs/adr/0011-application-layer-vertical-slice-organization.md`](./adr/0011-application-layer-vertical-slice-organization.md) and [`plans/application-layer-cqrs-refactor.md`](../plans/application-layer-cqrs-refactor.md) still say handlers must use a project-owned abstraction such as `IIntegrationEventPublisher`. Leaving both rules active will cause #165, DES-92, and later consumers to receive contradictory implementation guidance.

### Required outcome

- Record the newer decision explicitly, preferably in a small superseding ADR rather than silently rewriting ADR-0011's history.
- Add a supersession/reference note to ADR-0011 and update the referenced CQRS plan so there is one active rule.
- State the boundary precisely:
  - Application may reference `MassTransit.Abstractions`, integration contracts, `[EntityName]`, `IPublishEndpoint`, and—where the service architecture calls for inbound adapters there—MassTransit messaging abstractions.
  - RabbitMQ transport packages/APIs, `UsingRabbitMq`, broker credentials, connections, and endpoint/topology configuration stay in Infrastructure.
  - Do not restore `IIntegrationEventPublisher`; a forwarding wrapper with no policy is unwanted ceremony.
  - Publishing remains post-commit through the existing domain-event dispatch and must retain the D-3 linked 5-second timeout plus logged/swallowed failure behavior.
- Keep MassTransit/RabbitMQ registration in `Infrastructure/Messaging`, invoked through Infrastructure DI; Api remains the composition root through that registration.
- For future consumers, keep transport adapters thin: map/validate/deduplicate as required, then dispatch an Application command/query. Do not put scoring or audit business rules in the consumer body.

This is a documentation/convention fix. Do **not** restructure PR #189's production publisher while applying it.

## Fix 2 — stop false-green RabbitMQ integration tests

### Why, TL;DR

`MassTransitQuestionClosedPublishTests` catches every exception from `rabbit.StartAsync()` and returns. Image-pull, authentication, configuration, port, or RabbitMQ startup regressions can therefore produce a green test without exercising MassTransit. Similar broad-catch/return behavior exists in the legacy `RabbitMqIntegrationEventPublisherTests` and should be corrected consistently.

### Required outcome

- A positively identified “Docker/Testcontainers unavailable” condition may produce an explicit xUnit **skip** with a visible reason.
- All other container or broker startup exceptions must escape and fail the test.
- Never use `catch (Exception) { return; }` as skip behavior.
- Centralize the availability/skip helper if more than one messaging test needs identical logic; avoid duplicating fragile exception classification.
- Keep the dedicated real-broker MassTransit test. Keep `IPublishEndpoint` stubbed and the MassTransit hosted service disabled in the general `WebApplicationFactory`, per the postmortem; ordinary API integration tests must not contact a broker.
- Audit at least:
  - `services/session-operations-service/tests/IntegrationTests/Messaging/MassTransitQuestionClosedPublishTests.cs`
  - `services/session-operations-service/tests/IntegrationTests/Messaging/RabbitMqIntegrationEventPublisherTests.cs`

## Non-goals

- No transactional outbox or delivery-semantics change.
- No custom MassTransit retry policy, endpoint-name formatter, or topology layer.
- No return to the hand-rolled publisher abstraction for migrated/new events.
- No change to the audit-history scope: clue releases/evidence/penalties use RabbitMQ; general pause/resume/cancel history does not. See [`rabbitmq-clue-release-audit-scope-handoff-2026-07-11.md`](./rabbitmq-clue-release-audit-scope-handoff-2026-07-11.md).
- Do not remove the per-publish 5-second guard or the test-host bus isolation introduced after the #164 hang.

## Verification

After implementing both tracks:

1. Confirm the active architecture documents no longer contradict direct use of `IPublishEndpoint` in the post-commit Application event handler.
2. Confirm a deliberately invalid RabbitMQ image/configuration fails the messaging test rather than passing or skipping; restore the valid configuration afterward.
3. Confirm Docker-unavailable behavior is reported as skipped, not passed, if the test stack supports that scenario.
4. Run the sandbox-hardened service gate required by `backend/AGENTS.md`:
   `make -C backend gate SVC=session-operations-service`.
5. Confirm the dedicated MassTransit RabbitMQ test actually ran when Docker is available, and preserve the postmortem's broker-down timeout coverage.

## Canonical references

- Merged implementation: PR [#189](https://github.com/grupo-12-desarrollo-umbral/umbral/pull/189), issue [#164](https://github.com/grupo-12-desarrollo-umbral/umbral/issues/164).
- Hang cause and mandatory forward guardrails: [`gh-164-masstransit-integration-test-hang-postmortem-2026-07-12.md`](./gh-164-masstransit-integration-test-hang-postmortem-2026-07-12.md).
- Migration/audit scope and D-3 amendment: [`rabbitmq-clue-release-audit-scope-handoff-2026-07-11.md`](./rabbitmq-clue-release-audit-scope-handoff-2026-07-11.md).
- Stale architecture sources to reconcile: [`docs/adr/0011-application-layer-vertical-slice-organization.md`](./adr/0011-application-layer-vertical-slice-organization.md) and [`plans/application-layer-cqrs-refactor.md`](../plans/application-layer-cqrs-refactor.md).

## Suggested skills

- `diagnose` — implement and prove the false-green test correction through a narrow reproduce/fix/regression loop.
- `tdd` — make the skip-versus-fail classification explicit before changing the Testcontainers setup.
- `cqrs-mediatr-aspnetcore` — preserve the post-commit domain-event-to-integration-event boundary while reconciling the architecture record.
- `aspnet-backend-testing` — if available, for Testcontainers and `WebApplicationFactory` isolation conventions.
- `rabbitmq-events-dotnet` — if available, but treat any hand-rolled-publisher guidance as stale until #166 refreshes it for MassTransit.
