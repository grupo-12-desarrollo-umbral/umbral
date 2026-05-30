# Split Commits Git Flow Examples

## Example 1: Wrong branch, new feature, Git Flow available

```sh
# Add session replay query and tests
git checkout develop
git checkout -b feature/lin-248-session-replay-query
git add services/session-operations-service/src/Application/Queries/Replay \
  services/session-operations-service/tests/Application/Queries/Replay
git commit -m "implement (LIN-248): add session replay query"
```

## Example 2: Existing hotfix branch

```sh
# Guard null score publication
git checkout hotfix/lin-311-null-score-guard
git add services/scoring-monitoring-service/src/Application/Publishing/ScorePublisher.cs \
  services/scoring-monitoring-service/tests/Application/Publishing/ScorePublisherTests.cs
git commit -m "fix (LIN-311): guard null score publication"
```

## Example 3: Already on the correct branch

```sh
# Document mission aggregate invariants
git add docs/ddd_solution_model.md services/mission-design-service/CONTEXT.md
git commit -m "document (LIN-402): clarify mission aggregate invariants"
```

## Example 4: Repo does not expose Git Flow branches

State the constraint first, then keep the commit plan usable:

> The repository currently exposes `main` but no visible `develop`, `feature/*`, `release/*`, or `hotfix/*` branches. I cannot honestly prescribe Git Flow checkout commands without inventing branch topology, so I’m keeping the commit split and showing the branch command only if you want to create that structure.

```sh
# Extract score normalization policy
git add services/scoring-monitoring-service/src/Domain/Policies/ScoreNormalizationPolicy.cs \
  services/scoring-monitoring-service/tests/Domain/Policies/ScoreNormalizationPolicyTests.cs
git commit -m "refactor (LIN-515): extract score normalization policy"
```

Optional Git Flow bootstrap if the team confirms that `develop` should exist:

```sh
git checkout main
git checkout -b develop
git checkout -b feature/lin-515-score-normalization-policy
```
