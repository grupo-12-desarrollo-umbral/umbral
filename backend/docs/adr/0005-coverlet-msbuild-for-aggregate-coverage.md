# Use coverlet.msbuild with MergeWith chaining for aggregate coverage enforcement

All test projects use `coverlet.msbuild` instead of `coverlet.collector`. Coverage is collected by running each test project in order with `/p:CollectCoverage=true /p:CoverletOutputFormat=json`, passing the previous run's output via `/p:MergeWith`; the final project adds `/p:Threshold=93 /p:ThresholdType="line,branch" /p:ThresholdStat=total` so `dotnet test` itself fails if the aggregate **line or branch** coverage falls short. This replaces a custom Python script that accepted only a single pre-merged Cobertura file and required the agent to improvise a merge step that was never reliable in practice.

The gate enforces **both line and branch** coverage against the same threshold. coverlet 6.0.4 applies a single `/p:Threshold` value to every type listed in `/p:ThresholdType`, so one bar (93% by default) gates both dimensions; it does not support per-type threshold values. The comma-separated `ThresholdType` value must be quoted (`\"line,branch\"`) so the comma reaches MSBuild inside the property value rather than being parsed as a separate switch (which fails with `MSB1006 Property is not valid`).

## Gate scope and single source of truth

`backend/scripts/cover-gate.sh` is the **only** authority for both CI/CD pass/fail and the coverage number we demonstrate. It is variadic: the chain spans **all test projects** for the service — `Application.UnitTests`, `Api.UnitTests`, and `Infrastructure.IntegrationTests` — with the last project enforcing the threshold. `Api.UnitTests` is included so the gate measures the whole tested surface (Api endpoints included), not just Application + Infrastructure.

On a green run the gate persists the merged Cobertura file to `coverage/gate/merged.cobertura.xml` and renders `Summary.txt` / `index.html` **from that exact file**. The demonstrated number is therefore identical to the gated number by construction.

`backend/scripts/cover.sh` is a dev-only convenience for whole-solution exploration. It uses a different project discovery and different ReportGenerator filters, so its number is **not** the gated number and must not be used to demonstrate that the gate passed.

The threshold defaults to 93% (the project minimum, applied to both line and branch) and is overridable per-run with the `THRESHOLD` env var when a consumer requires a different bar (e.g. `THRESHOLD=95`).

## Gated services and scope

`make gate-all` auto-discovers gated services from `services/*/tests/IntegrationTests/*.csproj`: `identity-access-service`, `mission-design-service`, and `session-operations-service`. As of issue #149 all three clear the 93% line **and** branch bar (identity 96.2% branch, mission 95.3% branch, session 95.1% branch).

Two backend services are intentionally **out of scope** for the coverage gate:

- **`api-gateway`** — exercised only through end-to-end tests, so coverlet observes ~0% and a per-service threshold would be meaningless.
- **`scoring-monitoring-service`** — has no test or source projects to measure yet.

## Considered Options

- **coverlet.collector + custom Python script** — the previous approach: `--collect:"XPlat Code Coverage;Format=cobertura"` per project, manual Cobertura merge, then `check_cobertura_threshold.py`. Rejected because `dotnet-coverage` (the standard merge tool) is not installed in the development environment, forcing the agent to write ad-hoc merge logic inline and preventing use of the threshold script.
- **ReportGenerator CLI tool** — can merge multiple Cobertura reports but does not enforce a threshold on its own; still requires an external check step.
