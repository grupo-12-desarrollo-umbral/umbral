# Use coverlet.msbuild with MergeWith chaining for aggregate coverage enforcement

All test projects use `coverlet.msbuild` instead of `coverlet.collector`. Coverage is collected by running each test project in order with `/p:CollectCoverage=true /p:CoverletOutputFormat=json`, passing the previous run's output via `/p:MergeWith`; the final project adds `/p:Threshold=95 /p:ThresholdType=branch /p:ThresholdStat=total` so `dotnet test` itself fails if aggregate branch coverage falls short. Line coverage remains visible in the generated report but does not determine the gate. This replaces a custom Python script that accepted only a single pre-merged Cobertura file and required the agent to improvise a merge step that was never reliable in practice.

The gate enforces **aggregate branch coverage of at least 95%**. Coverlet 6.0.4 receives a single threshold type, `branch`, so line coverage cannot make the build pass or fail.

## Gate scope and single source of truth

`backend/scripts/cover-gate.sh` is the **only** authority for both CI/CD pass/fail and the coverage number we demonstrate. It is variadic: the chain spans **all test projects** for the service — `Application.UnitTests`, `Api.UnitTests`, and `Infrastructure.IntegrationTests` — with the last project enforcing the threshold. `Api.UnitTests` is included so the gate measures the whole tested surface (Api endpoints included), not just Application + Infrastructure.

On a green run the gate persists the merged Cobertura file to `coverage/gate/merged.cobertura.xml` and renders `Summary.txt` / `index.html` **from that exact file**. The demonstrated number is therefore identical to the gated number by construction.

`backend/scripts/cover.sh` is a dev-only convenience for whole-solution exploration. It uses a different project discovery and different ReportGenerator filters, so its number is **not** the gated number and must not be used to demonstrate that the gate passed.

The threshold defaults to 95%, the project minimum for aggregate branch coverage. The `THRESHOLD` environment variable remains available for consumers that require a stricter bar.

## Gated services and scope

`make gate-all` auto-discovers gated services from `services/*/tests/IntegrationTests/*.csproj`. Every discovered service must clear the 95% aggregate branch-coverage bar.

Two backend services are intentionally **out of scope** for the coverage gate:

- **`api-gateway`** — exercised only through end-to-end tests, so coverlet observes ~0% and a per-service threshold would be meaningless.
- **`scoring-monitoring-service`** — has no test or source projects to measure yet.

## Considered Options

- **coverlet.collector + custom Python script** — the previous approach: `--collect:"XPlat Code Coverage;Format=cobertura"` per project, manual Cobertura merge, then `check_cobertura_threshold.py`. Rejected because `dotnet-coverage` (the standard merge tool) is not installed in the development environment, forcing the agent to write ad-hoc merge logic inline and preventing use of the threshold script.
- **ReportGenerator CLI tool** — can merge multiple Cobertura reports but does not enforce a threshold on its own; still requires an external check step.
