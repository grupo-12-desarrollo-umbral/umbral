# ASP.NET Backend Testing Examples

## Example Triggers

Use this skill when the user asks things like:

- "Add unit tests with xUnit and Moq"
- "Set up integration tests for this ASP.NET Core API"
- "Where should these tests live in Clean Architecture?"
- "Use Testcontainers for PostgreSQL in tests"
- "Set up Playwright for end-to-end tests"
- "Fail CI when coverage goes below 93%"

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
dotnet add tests/Application.UnitTests package FluentAssertions
```

API integration tests:

```bash
dotnet add tests/Api.IntegrationTests package xunit
dotnet add tests/Api.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/Api.IntegrationTests package FluentAssertions
```

Infrastructure integration tests:

```bash
dotnet add tests/Infrastructure.IntegrationTests package xunit
dotnet add tests/Infrastructure.IntegrationTests package Testcontainers.PostgreSql
dotnet add tests/Infrastructure.IntegrationTests package FluentAssertions
```

Browser-based end-to-end tests:

```bash
dotnet add tests/System.EndToEndTests package Microsoft.Playwright.Xunit
dotnet add tests/System.EndToEndTests package FluentAssertions
dotnet build tests/System.EndToEndTests
pwsh tests/System.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install
```

## Example Coverage Commands

Use `coverlet.msbuild` (already present in test projects). Run test projects in
order, chaining `/p:MergeWith` between runs. All runs except the last emit JSON;
the final run enforces the 93% threshold — `dotnet test` exits non-zero on failure.

Two-project service (Application.UnitTests + Infrastructure.IntegrationTests):

```bash
TMP=/tmp/cov-$$
mkdir -p $TMP

dotnet test tests/UnitTests/Application.UnitTests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=json \
  /p:CoverletOutput=$TMP/step1.json

dotnet test tests/IntegrationTests/Infrastructure.IntegrationTests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=$TMP/merged.xml \
  /p:MergeWith=$TMP/step1.json \
  /p:Threshold=93 \
  /p:ThresholdType=line \
  /p:ThresholdStat=total
```

Three-project service (add a middle step emitting JSON before the final run):

```bash
dotnet test tests/UnitTests/Application.UnitTests.csproj \
  /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=$TMP/step1.json

dotnet test tests/Api.UnitTests/Api.UnitTests.csproj \
  /p:CollectCoverage=true /p:CoverletOutputFormat=json \
  /p:CoverletOutput=$TMP/step2.json /p:MergeWith=$TMP/step1.json

dotnet test tests/IntegrationTests/Infrastructure.IntegrationTests.csproj \
  /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=$TMP/merged.xml /p:MergeWith=$TMP/step2.json \
  /p:Threshold=93 /p:ThresholdType=line /p:ThresholdStat=total
```

## Example Placement Calls

If the code under test is:

- a value object enforcing email format: write a Domain unit test with `xUnit` and `FluentAssertions`
- a command handler coordinating repository and clock: write an Application unit test with `xUnit`, `Moq`, and `FluentAssertions`
- an EF Core repository query: write an Infrastructure integration test with `xUnit`, `Testcontainers`, and `FluentAssertions`
- an endpoint returning `401`, `400`, or `200`: write an API integration test with `xUnit`, `Microsoft.AspNetCore.Mvc.Testing`, and `FluentAssertions`
- a sign-in to purchase flow across the whole product: write an end-to-end test with `Microsoft.Playwright.Xunit` and `FluentAssertions`
