---
name: ef-core-postgresql
description: Design, implement, review, or troubleshoot Entity Framework Core data access against PostgreSQL using the Npgsql provider. Use when Codex needs to create new EF Core models, configure DbContext and dependency injection, model entities and relationships, map PostgreSQL-specific types, write or optimize LINQ queries, plan migrations, tune performance, or diagnose EF Core/PostgreSQL integration issues in any .NET project.
---

# EF Core PostgreSQL

## Overview

Use this skill to build, extend, or review EF Core data access for PostgreSQL with current EF Core and Npgsql guidance. Keep this file loaded for workflow and decision rules; load the reference files only for the area you are actively touching.

## Workflow

1. Identify the EF Core and Npgsql versions already in the project.
2. If creating a new model, start from the domain boundary and relationship rules before writing EF classes.
3. Inspect how `DbContext` is registered and how its lifetime aligns with the unit of work.
4. Inspect the model for PostgreSQL-specific opportunities or mismatches before writing queries.
5. Inspect query shape, tracking mode, loading strategy, and indexes before assuming the bottleneck is EF itself.
6. Inspect migrations and deployment strategy before changing schema in production-facing code.

## New Model Workflow

1. Define the aggregate or table boundary first.
2. Choose the key strategy and required uniqueness constraints.
3. Decide which properties are scalar columns, value objects, owned types, join tables, arrays, enums, or JSON.
4. Define relationships and delete behavior explicitly.
5. Keep entity classes persistence-friendly, then move database-specific rules into Fluent API configuration.
6. Add the migration and review the generated schema, indexes, foreign keys, and column types.
7. Check whether the shape still produces efficient queries for the expected reads and writes.

## Operating Rules

- Treat each `DbContext` instance as a short-lived unit of work. Do not share it across threads.
- Prefer plain `AddDbContext` for normal request-scoped web work.
- Prefer `AddDbContextFactory` when one DI scope needs multiple units of work or the scope lifetime does not match the desired context lifetime.
- Use `AddDbContextPool` only after confirming the context is effectively stateless across requests. Pooling and connection pooling solve different problems.
- Keep provider configuration centralized in `UseNpgsql(...)`.
- Use Fluent API for PostgreSQL-specific schema features rather than scattering provider details across entities.
- Prefer projections, pagination, and explicit loading decisions over broad entity materialization.
- Default read-only queries to no-tracking unless identity resolution or updates are required.
- Avoid lazy loading in performance-sensitive code; it hides round-trips and makes N+1 defects easy to ship.
- Prefer PostgreSQL-native types and indexes when they simplify the model and remain queryable through EF translation.
- Review every migration before applying it. For production, prefer generated SQL scripts or bundles over runtime auto-migration.

## Quick Start

Use a registration shape like this when the project is on modern EF Core/Npgsql and the provider configuration is stable:

```csharp
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql
            .SetPostgresVersion(16, 0)
            .MapEnum<OrderStatus>("order_status")));
```

If the project needs per-operation contexts, switch to `AddDbContextFactory` or `PooledDbContextFactory`. If provider configuration must vary, create an external `NpgsqlDataSource` instead of varying `ConfigureDataSource()` calls ad hoc.

## Load These References

- Read [references/ef-core-practices.md](references/ef-core-practices.md) when creating new models or configuring context lifetime, transactions, concurrency, value conversions, query shape, tracking, migrations, deployment flow, pooling, or diagnostics.
- Read [references/postgresql-features.md](references/postgresql-features.md) when the schema should use `jsonb`, arrays, enums, full-text search, or PostgreSQL-specific index methods.

## Common Review Heuristics

- If the code returns entities only to map them immediately, project directly to the DTO.
- If a read path uses tracked queries by default, challenge it.
- If a query joins several collections, check whether projection or split queries would reduce row explosion.
- If a mutable custom type is stored via a value converter, check whether it also needs a `ValueComparer`.
- If JSON is modeled as a raw string or opaque POCO, check whether `ToJson()` would preserve better queryability.
- If the team added indexes without choosing a PostgreSQL method intentionally, verify whether plain B-tree is actually correct.
- If migrations are applied at app startup in production code, treat that as a risk unless the deployment model explicitly justifies it.
