# ASP.NET Backend Testing Reference

This reference is for ASP.NET backend projects that use Clean Architecture or a closely related layered design. It assumes the usual dependency direction:

- Presentation -> Application -> Domain
- Infrastructure -> Application/Domain abstractions

## Primary References Used

These sources were checked while drafting this skill:

- Microsoft Learn, Best practices for writing unit tests: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices
- Microsoft Learn, Integration tests in ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
- Microsoft Learn, `dotnet test`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
- Microsoft Learn, Microsoft.Testing.Platform code coverage: https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage
- Microsoft Learn, `dotnet-coverage`: https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-coverage
- Microsoft Learn, Unit testing C# with xUnit: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit
- xUnit.net, Shared context: https://xunit.net/docs/shared-context
- Playwright for .NET, Installation: https://playwright.dev/dotnet/docs/intro
- Playwright for .NET, Running and debugging tests: https://playwright.dev/dotnet/docs/running-tests
- Testcontainers, Getting started with Testcontainers for .NET: https://testcontainers.com/guides/getting-started-with-testcontainers-for-dotnet/

## Library Validation

These defaults are good choices for this workspace:

- Unit tests:
  - `xUnit` is a strong default in .NET and is used throughout Microsoft Learn examples.
  - `FluentAssertions` is the standard assertion library — use it across all test projects; never use raw `Assert.*` calls.
  - `Moq` is an acceptable mocking library for Application-layer unit tests.
  - Keep `Moq` focused on outbound ports and collaborators; most Domain tests should not need mocking.
- API integration tests:
  - `Microsoft.AspNetCore.Mvc.Testing` is the standard ASP.NET Core package for host-level integration testing.
  - `WebApplicationFactory` and `TestServer` are the correct tools when you need to boot the app and verify real HTTP behavior.
- Infrastructure integration tests:
  - `Testcontainers` is an appropriate default when production-like dependency behavior matters.
  - Prefer real engines such as PostgreSQL or RabbitMQ instead of relying only on in-memory substitutes.
- End-to-end tests:
  - `Microsoft.Playwright.Xunit` is a good choice when the product has a browser-facing UI.
  - For backend-only systems, use black-box API or system tests instead of browser automation.

## Layer-to-Test Matrix

### Domain layer

Preferred tests:

- unit tests only

Libraries:

- `xUnit`
- `FluentAssertions`

Verify:

- invariants
- calculations
- state transitions
- invalid states and edge cases

Avoid:

- `Moq`
- ASP.NET Core
- EF Core
- databases
- HTTP clients
- brokers

### Application layer

Preferred tests:

- mostly unit tests

Libraries:

- `xUnit`
- `FluentAssertions`
- `Moq`

Use `Moq` for:

- repository interfaces
- external service ports
- clock abstractions
- identity abstractions
- messaging ports

Do not mock real domain objects when they are cheap to construct.

### Infrastructure layer

Preferred tests:

- integration tests

Libraries:

- `xUnit`
- `FluentAssertions`
- `Testcontainers`

Use these to verify:

- EF Core mappings
- SQL behavior
- transaction behavior
- external adapter behavior

Do not treat EF Core in-memory behavior as proof that the production database works.

**Testcontainers fixture sharing** — never create a container inside a `[Fact]` body. Container startup takes 4–8 s each time. Instead use `IClassFixture<T>` to share one container across all tests in a class, or `ICollectionFixture<T>` to share across multiple test classes:

```csharp
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();
    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

public class MyTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;
    public MyTests(PostgreSqlFixture fixture) => _fixture = fixture;
}
```

**Handler dependencies** — Application-layer handlers must depend on repository interfaces (e.g., `IMissionRepository`), not on `DbContext` directly. Depending on `DbContext` makes the handler impossible to unit test and forces it into the integration test suite. The integration test then verifies that the real repository implements the interface correctly; the handler behavior is verified separately with a mocked interface.

### Presentation/API layer

Preferred tests:

- integration tests through the ASP.NET Core host

Libraries:

- `xUnit`
- `FluentAssertions`
- `Microsoft.AspNetCore.Mvc.Testing`
- `WebApplicationFactory`
- `TestServer`

Use these to verify:

- routes and status codes
- auth and authorization behavior
- request and response contracts
- validation responses
- middleware and exception handling

### End-to-end or system boundary

E2E tests are mandatory for every principal usage flow — the paths a real user must be able to complete for the feature to be considered working. Cover each one with at least one E2E test. Use judgment to avoid redundant permutations that belong in unit tests.

There are two scopes of E2E in a stack with a Next.js frontend:

**Backend E2E (lives in the backend test project)**

Calls real HTTP endpoints against the running ASP.NET Core service with a real database (Testcontainers). No browser, no frontend. Verifies that the backend flows work end to end in isolation — for example: bootstrap a user, assert the correct role is assigned, call a protected endpoint, assert a deactivated user is rejected.

Libraries:

- `xUnit`
- `FluentAssertions`
- `Testcontainers` for the real database
- `HttpClient` against `TestServer` or the running host

Do not substitute any infrastructure dependency in this suite.

**Full-stack E2E**

Not a backend concern. Owned by the frontend project.

## Coverage Policy

This skill assumes a minimum project coverage target of 93% for both line and branch coverage.

Enforcement guidance:

- collect coverage in CI on every mainline change
- fail the build when either line or branch coverage drops below 93%
- exclude generated code deliberately, not broadly
- review uncovered code for risk, not only for percentage

Coverage is a guardrail, not proof of correctness.

## Default Decision Tree

```text
What behavior am I proving?
|- Pure business rule or invariant                      -> Domain unit test with xUnit + FluentAssertions
|- Use-case orchestration or validation flow            -> Application unit test with xUnit + Moq + FluentAssertions
|- EF/query/repository/external adapter behavior        -> Infrastructure integration test with xUnit + Testcontainers + FluentAssertions
|- Route/auth/model-binding/middleware/API contract     -> API integration test with xUnit + Mvc.Testing + FluentAssertions
\- Principal backend flow across all layers             -> Backend E2E with xUnit + HttpClient + Testcontainers + FluentAssertions
```
