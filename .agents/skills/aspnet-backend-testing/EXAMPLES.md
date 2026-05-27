# ASP.NET Backend Testing Examples

## Example Triggers

Use this skill when the user asks things like:

- "Add unit tests with xUnit and Moq"
- "Set up integration tests for this ASP.NET Core API"
- "Where should these tests live in Clean Architecture?"
- "Use Testcontainers for PostgreSQL in tests"
- "Set up Playwright for end-to-end tests"
- "Fail CI when coverage goes below 95%"

## Example Test Project Layout

```text
tests/
  Domain.UnitTests/
  Application.UnitTests/
  Infrastructure.IntegrationTests/
  Api.IntegrationTests/
  System.EndToEndTests/
```

## Example Package Choices

Domain or Application unit tests:

```bash
dotnet add tests/Application.UnitTests package xunit
dotnet add tests/Application.UnitTests package xunit.runner.visualstudio
dotnet add tests/Application.UnitTests package Microsoft.NET.Test.Sdk
dotnet add tests/Application.UnitTests package Moq
```

API integration tests:

```bash
dotnet add tests/Api.IntegrationTests package xunit
dotnet add tests/Api.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
```

Infrastructure integration tests:

```bash
dotnet add tests/Infrastructure.IntegrationTests package xunit
dotnet add tests/Infrastructure.IntegrationTests package Testcontainers.PostgreSql
```

Browser-based end-to-end tests:

```bash
dotnet add tests/System.EndToEndTests package Microsoft.Playwright.Xunit
dotnet build tests/System.EndToEndTests
pwsh tests/System.EndToEndTests/bin/Debug/net8.0/playwright.ps1 install
```

## Example Coverage Commands

Using Microsoft.Testing.Platform coverage support:

```bash
dotnet test --coverage --coverage-output coverage.cobertura.xml --coverage-output-format cobertura
python3 .agents/skills/aspnet-backend-testing/scripts/check_cobertura_threshold.py coverage.cobertura.xml 95
```

Using Coverlet where the repo already standardizes on it:

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
python3 .agents/skills/aspnet-backend-testing/scripts/check_cobertura_threshold.py path/to/coverage.cobertura.xml 95
```

## Example Placement Calls

If the code under test is:

- a value object enforcing email format: write a Domain unit test with `xUnit`
- a command handler coordinating repository and clock: write an Application unit test with `xUnit` and `Moq`
- an EF Core repository query: write an Infrastructure integration test with `xUnit` and `Testcontainers`
- an endpoint returning `401`, `400`, or `200`: write an API integration test with `xUnit` and `Microsoft.AspNetCore.Mvc.Testing`
- a sign-in to purchase flow across the whole product: write an end-to-end test with `Microsoft.Playwright.Xunit`
