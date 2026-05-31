# Cover Script — `scripts/cover.sh`

Runs unit and/or integration tests with code coverage for a backend service, then merges results into an HTML report.

## Usage

```
./scripts/cover.sh <service-name> [options]
```

## Arguments

| Argument        | Description                                           |
| --------------- | ----------------------------------------------------- |
| `service-name`  | One of: `identity-access-service`, `mission-design-service`, `session-operations-service`, `scoring-monitoring-service` |

## Options

| Flag                       | Description                              |
| -------------------------- | ---------------------------------------- |
| `-u`, `--unit-only`        | Run only unit test projects              |
| `-i`, `--integration-only` | Run only integration test projects (requires Docker) |
| `-o`, `--open`             | Open the merged HTML report in the default browser |
| `-h`, `--help`             | Show usage help                          |

## Examples

```bash
# Run all tests with coverage for identity-access-service
./scripts/cover.sh identity-access-service

# Run only unit tests
./scripts/cover.sh identity-access-service --unit-only

# Run only integration tests (requires Docker for Testcontainers)
./scripts/cover.sh identity-access-service --integration-only

# Run all tests and open the HTML report
./scripts/cover.sh identity-access-service --open
```

## Prerequisites

- **reportgenerator** — `dotnet tool install -g dotnet-reportgenerator-globaltool`
- **Docker** — required for integration tests using Testcontainers (PostgreSQL)
- **coverlet** — each test `.csproj` must reference `coverlet.msbuild`

## How it works

1. Finds `*UnitTests*.csproj` and `*IntegrationTests*.csproj` within `<service>/tests/`.
2. Runs `dotnet test` with `CollectCoverage=true` and `CoverletOutputFormat=cobertura`.
3. Collects all generated `coverage.cobertura.xml` files.
4. Merges them with `reportgenerator` into `<service>/coverage/merged/`.
5. Outputs a text summary, CSV summary, and an `index.html` report.

## Output

```
<service>/coverage/merged/
  Summary.txt    — text summary of line/branch/method coverage
  Summary.csv    — CSV breakdown
  index.html     — interactive HTML report
```
