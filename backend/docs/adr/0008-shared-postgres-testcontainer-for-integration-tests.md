# Share one Postgres Testcontainer across all integration test classes

Every integration test class in `identity-access-service` binds to a single shared Postgres container through xUnit's `ICollectionFixture<PostgreSqlFixture>` (the `[Collection(PostgreSqlCollection.Name)]` attribute), rather than each class declaring its own `IClassFixture<PostgreSqlFixture>`. xUnit treats each test class as its own collection and runs collections in parallel, so the per-class form booted **one Postgres container per class simultaneously** (5 at the time of writing). On a cold Docker daemon that simultaneous startup contention made Npgsql connections time out before the fixture's retry window elapsed, failing the whole suite non-deterministically. One shared container removes the race by construction. This is safe because isolation never depended on per-class containers: every test truncates the schema (`ResetDatabaseAsync`) before acting, and a single collection runs sequentially — which the per-test truncate already requires.

## Consequences

- Integration tests run **sequentially**, not in parallel per class. This is faster in practice, not slower: the suite went from ~20s (5 containers) to ~10s (1 container) and uses a fifth of the Docker resources, because container startup dominated the old wall-clock.
- A latent process-global race is also closed: `IdentityAccessApiWebApplicationFactory` sets the `ConnectionStrings__umbral_backendDb` environment variable for the whole process; under the old parallel execution two factories could stomp on it, but a serial collection makes that impossible.
- **Do not re-introduce per-class `IClassFixture<PostgreSqlFixture>` to "parallelize for speed."** It reintroduces the cold-start race and the env-var race for a wall-clock loss. The shared collection is the deliberate choice.

## Considered Options

- **Per-class `IClassFixture` + tuning the fixture's connection retry / wait strategy** — treats the symptom. The contention is at the Docker-daemon level (N containers accepting connections at once), so a longer retry just delays the failure; it does not remove the N-containers-at-once cause.
- **`[assembly: CollectionBehavior(DisableTestParallelization = true)]`** — serializes execution so the containers start one at a time, avoiding the simultaneous cold start, but still spins up N containers per run. Slower and wasteful versus a single shared container, with no isolation benefit.
