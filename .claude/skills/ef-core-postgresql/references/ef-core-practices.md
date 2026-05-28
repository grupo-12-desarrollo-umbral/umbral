# EF Core Practices

## Table of Contents

1. Model-authoring workflow
2. Entity design rules
3. Relationship design
4. Keys, constraints, and indexes
5. Context lifetime and DI
6. Base provider configuration
7. Transactions and concurrency
8. Value conversions and comparers
9. Query-shape rules
10. Tracking defaults
11. Loading related data
12. Streaming, hot paths, and cache shape
13. Migrations and deployment
14. Connection and pool tuning
15. Diagnostics
16. Common failure patterns
17. Official sources

## Model-authoring workflow

Use this sequence when creating a new EF Core model from scratch:

1. Define the aggregate boundary or independent table boundary.
2. Decide the primary key shape and any alternate keys or unique indexes.
3. Separate true entities from value objects and owned data.
4. Decide which collections are real relations and which are better modeled as arrays or JSON.
5. Write entity classes with invariants and navigation properties that reflect the domain, not the database naming trivia.
6. Move table names, column types, lengths, indexes, delete behavior, conversions, and provider-specific details into Fluent API configuration.
7. Generate the migration and inspect the resulting schema before trusting it.
8. Check that the expected query patterns are efficient against the chosen model.

## Entity design rules

Prefer simple entity classes and explicit configuration classes.

Practical defaults:

- Give each aggregate root a stable key.
- Prefer immutable value objects where possible.
- Keep navigation properties intentional; do not expose every possible back-reference just because EF can map it.
- Prefer separate configuration classes implementing `IEntityTypeConfiguration<T>` once the model stops being trivial.
- Keep database-specific rules out of attributes when Fluent API is clearer or more expressive.

Good default split:

- entity class: domain state and behavior
- configuration class: table, column, relationship, conversion, index, and provider-specific mapping rules

## Relationship design

Model relationships explicitly and challenge defaults.

Rules:

- Use owned types for data that has no identity outside the owner.
- Use one-to-many or many-to-many tables for data with its own lifecycle or query identity.
- Choose delete behavior explicitly; do not rely on conventions for destructive relationships.
- Be skeptical of optional relationships that are only optional because creation ordering is inconvenient.
- Keep many-to-many skip navigations only when the join has no business meaning; if the join carries attributes or lifecycle, model it as an explicit entity.

Review heuristics:

- If the child cannot exist meaningfully without the parent, consider owned types or required dependents.
- If the related item must be queried independently, it probably wants its own table.
- If the join needs timestamps, status, ordering, or metadata, it is not a pure many-to-many.

## Keys, constraints, and indexes

Treat these as part of model design, not polish.

Rules:

- Pick key generation intentionally: database-generated, client-generated, or domain-assigned.
- Add unique constraints for business invariants that the database should enforce.
- Define indexes from real query shapes, not generic fear.
- Name critical indexes and constraints consistently when the project has a convention.

PostgreSQL-specific selection is covered in `postgresql-features.md`, but the design decision starts here:

- equality/range/sort lookups usually mean B-tree
- containment or full-text workloads often mean GIN
- large append-heavy time-ordered data may justify BRIN after validation

## Context lifetime and DI

Use `DbContext` as a short-lived unit of work. Microsoft documents three hard constraints that should drive reviews:

- Dispose the context after use.
- Do not share it across threads.
- Treat `InvalidOperationException` from EF internals as unrecoverable for that context instance.

Default choices:

- Use `AddDbContext<TContext>()` for typical ASP.NET Core request-scoped work.
- Use `AddDbContextFactory<TContext>()` when one request or one background job needs multiple independent units of work.
- Use `AddDbContextPool<TContext>()` or `PooledDbContextFactory<TContext>` only when context setup cost is measurable and the context does not carry request-specific mutable state.

Pooling caution:

- EF context pooling is not the same as Npgsql connection pooling.
- `OnConfiguring` runs once for pooled contexts, so do not store tenant-specific or request-specific state there.
- If code manually opens or mutates the underlying ADO.NET connection, restore that state before the context returns to the pool.

## Base provider configuration

Centralize PostgreSQL provider setup inside `UseNpgsql(...)`.

Prefer this pattern on EF Core 9+ when the project uses the modern Npgsql provider APIs:

```csharp
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsql => npgsql
            .SetPostgresVersion(16, 0)
            .MapEnum<OrderStatus>("order_status")));
```

Use this when needed:

- `SetPostgresVersion(...)`: Pin SQL generation to the server version the app actually targets.
- `MapEnum<TEnum>(...)`: Map CLR enums to PostgreSQL enum types instead of falling back to integer storage.
- `ConfigureDataSource(...)`: Reach lower-level ADO.NET configuration, but do not vary this configuration opportunistically inside multiple registrations.

If the project must vary ADO.NET configuration materially, create an external `NpgsqlDataSource` and pass it to `UseNpgsql(...)`. Npgsql explicitly warns that varying configuration inside `ConfigureDataSource()` can produce the wrong internal data source reuse behavior.

## Transactions and concurrency

Relevant EF Core behavior:

- `SaveChanges` is transactional by default.
- If `SaveChanges` runs inside an existing transaction, EF creates a savepoint automatically before writing.
- Concurrency should usually be modeled explicitly with a concurrency token for contested rows and update flows.

Practical rules:

- Keep transactions as short as possible.
- Do not mix multiple `SaveChanges` calls into a long business flow unless the boundaries are deliberate.
- When using optimistic concurrency, decide the conflict policy up front: fail fast, merge, or reload-and-retry.
- When a workflow truly needs retries, retry the whole transaction or unit of work, not just the final command.

## Value conversions and comparers

Use `HasConversion(...)` when the domain type is better than the raw provider type. Typical examples are:

- strongly-typed IDs
- string-backed enums
- small immutable value objects

Check for `ValueComparer` when the converted type is mutable or has reference equality semantics. Microsoft’s guidance is explicit here: converted mutable types such as `List<int>` need comparer and snapshot logic, otherwise EF may miss in-place mutations or compare values incorrectly.

Practical rule:

- Prefer immutable value objects when using converters.
- If the converted type is mutable, define both conversion and comparison behavior together.

## Query-shape rules

Apply these in order before reaching for lower-level optimization:

1. Project only the columns the caller needs.
2. Limit result size with pagination or bounded queries.
3. Ensure the database can use an index that matches the filter and sort shape.
4. Verify the generated SQL instead of reasoning from LINQ alone.

Microsoft’s efficient querying guidance calls out these core patterns directly:

- projection over broad entity materialization
- bounded result sets
- avoiding cartesian explosion
- preferring eager loading or projection over accidental lazy loading

## Tracking defaults

Use tracking only when the context will persist changes to those entities.

Default approach:

- Read-only endpoint, report, or list screen: use `AsNoTracking()`.
- Read-only but needs identity resolution across repeated rows: consider `AsNoTrackingWithIdentityResolution()`.
- Command handler that will mutate loaded entities: use tracking.

Do not cargo-cult no-tracking everywhere. Tracking can still be appropriate if the same entities are reused for updates or identity resolution materially simplifies the flow.

## Loading related data

Prefer explicit intent:

- Use projection when the caller wants a DTO or a subset.
- Use eager loading when the related data is definitely needed.
- Use explicit loading for conditional follow-up loads.
- Avoid lazy loading on critical paths; it hides round-trips and commonly produces N+1 defects.

When joining multiple collections, inspect for cartesian explosion. EF Core’s split queries can reduce duplication, but current docs note they still execute multiple round-trips. Use them deliberately, not automatically.

## Streaming, hot paths, and cache shape

For large results, stream when the caller can consume incrementally:

```csharp
await foreach (var row in context.Posts
    .Where(p => p.Published)
    .Select(p => new PostRow(p.Id, p.Title))
    .AsAsyncEnumerable())
{
    // consume row
}
```

For hot paths:

- Parameterize repeated query shapes so EF and PostgreSQL can reuse work.
- Use compiled queries only for genuinely hot, repeatedly executed paths after measuring.
- Monitor query cache hit rate when diagnosing dynamic query generation.

PostgreSQL-specific note:

- EF documentation notes PostgreSQL does not maintain the same kind of implicit plan cache behavior as SQL Server; prepared statements can offer a similar effect. Do not assume every EF query-shape optimization has the same payoff across providers.

## Migrations and deployment

Use migrations as reviewed schema changes, not as hidden side effects.

Rules:

- Read the generated migration before accepting it.
- Check rename vs drop/add outcomes carefully.
- Keep destructive data motion explicit.
- Separate risky data backfills from trivial schema changes when that improves deploy safety.

When the project uses a separate migrations assembly, keep the design-time path explicit so tooling resolves the correct `DbContext`.

Microsoft’s current recommendation for production is clear: prefer generated SQL scripts. Bundles are also a strong option when the deployment model benefits from a single executable.

Practical deployment order:

1. Generate the migration.
2. Review the migration code.
3. Generate a SQL script or bundle.
4. Test against a production-like database state.
5. Apply through the deployment process, not via opportunistic app startup.

Be skeptical of runtime auto-migration in production because it couples schema changes to application startup, permissions, and multi-instance timing.

## Connection and pool tuning

Npgsql enables connection pooling by default. Important knobs from the official connection-string docs:

- `Maximum Pool Size`
- `Minimum Pool Size`
- `Connection Idle Lifetime`
- `Connection Lifetime`
- `Timeout`
- `Command Timeout`
- `Keepalive`
- `Max Auto Prepare`
- `Auto Prepare Min Usages`

Default posture:

- Do not tune pool sizes or auto-prepare blindly.
- Measure saturation, timeout behavior, and query mix first.
- Be careful with `No Reset On Close`; the docs explicitly warn it can leak state.

Remember:

- Npgsql connection pooling is driver-level.
- EF `DbContext` pooling is application-level.
- They are orthogonal and should be tuned independently.

## Diagnostics

Prefer ordinary logging first.

Use EF interceptors only when you need to modify or suppress behavior, not just observe it. Microsoft’s interceptor docs state that simple logging or `Microsoft.Extensions.Logging` are the better choices for logging.

Useful review steps:

- inspect generated SQL
- inspect command duration
- inspect tracking mode
- inspect transaction boundaries
- inspect whether the query plan matches the intended index strategy

## Common failure patterns

- Modeling a domain object graph first and only later discovering the relational shape is incoherent.
- Request-scoped services holding a `DbContext` too long.
- Background jobs reusing a context across unrelated operations.
- Applying migrations automatically at startup in production.
- Using `jsonb` as a dumping ground for data that should be relational.
- Serializing mutable custom types through converters without a comparer.
- Assuming EF or PostgreSQL will “figure out” the right index.
- Confusing context pooling with connection pooling.

## Official sources

Verified on 2026-05-22.

### EF Core official documentation

- DbContext lifetime, configuration, and initialization:
  `https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/`
- Efficient querying:
  `https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying`
- Advanced performance topics:
  `https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics`
- Tracking vs. no-tracking queries:
  `https://learn.microsoft.com/en-us/ef/core/querying/tracking`
- Transactions: `https://learn.microsoft.com/en-us/ef/core/saving/transactions`
- Handling concurrency conflicts:
  `https://learn.microsoft.com/en-us/ef/core/saving/concurrency`
- Applying migrations:
  `https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying`
- Value conversions:
  `https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions`
- Value comparers:
  `https://learn.microsoft.com/en-us/ef/core/modeling/value-comparers`
- Interceptors:
  `https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors`

### Npgsql official documentation

- EF Core provider landing page: `https://www.npgsql.org/efcore/`
- General type mapping: `https://www.npgsql.org/efcore/mapping/general.html`
- JSON mapping: `https://www.npgsql.org/efcore/mapping/json.html`
- Array mapping: `https://www.npgsql.org/efcore/mapping/array.html`
- Enum mapping: `https://www.npgsql.org/efcore/mapping/enum.html`
- PostgreSQL-specific indexes:
  `https://www.npgsql.org/efcore/modeling/indexes.html`
- Full-text search:
  `https://www.npgsql.org/efcore/mapping/full-text-search.html`
- Connection string parameters:
  `https://www.npgsql.org/doc/connection-string-parameters`

### PostgreSQL official documentation

- Current documentation landing page: `https://www.postgresql.org/docs/`
- Current index types:
  `https://www.postgresql.org/docs/current/indexes-types.html`
- Current index chapter: `https://www.postgresql.org/docs/current/indexes.html`
