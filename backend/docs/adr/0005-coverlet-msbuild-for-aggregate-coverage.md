# Use coverlet.msbuild with MergeWith chaining for aggregate coverage enforcement

All test projects use `coverlet.msbuild` instead of `coverlet.collector`. Coverage is collected by running each test project in order with `/p:CollectCoverage=true /p:CoverletOutputFormat=json`, passing the previous run's output via `/p:MergeWith`; the final project adds `/p:Threshold=95 /p:ThresholdType=line /p:ThresholdStat=total` so `dotnet test` itself fails if the aggregate falls short. This replaces a custom Python script that accepted only a single pre-merged Cobertura file and required the agent to improvise a merge step that was never reliable in practice.

## Considered Options

- **coverlet.collector + custom Python script** — the previous approach: `--collect:"XPlat Code Coverage;Format=cobertura"` per project, manual Cobertura merge, then `check_cobertura_threshold.py`. Rejected because `dotnet-coverage` (the standard merge tool) is not installed in the development environment, forcing the agent to write ad-hoc merge logic inline and preventing use of the threshold script.
- **ReportGenerator CLI tool** — can merge multiple Cobertura reports but does not enforce a threshold on its own; still requires an external check step.
