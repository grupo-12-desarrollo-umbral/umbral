# PostgreSQL Features With Npgsql

## Table of Contents

1. Type mapping strategy
2. JSON and `jsonb`
3. Arrays
4. Enums
5. Full-text search
6. PostgreSQL index design

## Type mapping strategy

Do not flatten PostgreSQL into a lowest-common-denominator relational model if native features are a better fit and remain queryable from EF.

Npgsql supports direct mapping for many PostgreSQL-native types. Also note these constraints:

- Some CLR types can map to several PostgreSQL types, so choose column types deliberately.
- Npgsql’s EF provider does not currently support PostgreSQL composite types, even though lower-level Npgsql ADO.NET support exists.

Use Fluent API for explicit database types when ambiguity matters:

```csharp
modelBuilder.Entity<Document>()
    .Property(x => x.Payload)
    .HasColumnType("jsonb");
```

## JSON and `jsonb`

Default choice:

- Prefer `jsonb` over plain `json` unless a project has a specific requirement otherwise.
- Prefer EF/Npgsql structured JSON mapping via `ToJson()` over opaque string storage or legacy POCO mapping when the JSON content is part of the query model.

Use modern structured mapping:

```csharp
modelBuilder.Entity<Customer>()
    .OwnsOne(c => c.Details, d =>
    {
        d.ToJson();
        d.OwnsMany(x => x.Orders);
    });
```

Important Npgsql guidance:

- `ToJson()` supports more query patterns than legacy POCO or DOM mapping.
- Mapping a `string` to `jsonb` is valid, but it treats the payload as raw text from EF’s perspective; use it only when that tradeoff is intentional.

## Arrays

PostgreSQL arrays are a real first-class option in Npgsql. Use them when the data is small, bounded, and naturally queried as a single field rather than as a relation.

Good fits:

- tag sets
- small permission code lists
- compact denormalized search helpers

Bad fits:

- unbounded collections
- data with its own lifecycle
- data that clearly wants a join table

Provider note:

- Multidimensional PostgreSQL arrays are not yet supported by the EF Core provider.

## Enums

Prefer PostgreSQL enums over storing business enums as integers when the database type should express the allowed values explicitly.

On EF Core 9+ with modern Npgsql:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        o => o.MapEnum<OrderStatus>("order_status")));
```

Review rule:

- If the project already uses string or integer enum storage, do not rewrite blindly.
- If you are designing a new schema and PostgreSQL owns the data model, mapped PostgreSQL enums are usually the cleaner choice.

## Full-text search

When the product needs language-aware text search, use PostgreSQL full-text search rather than improvised `LIKE '%term%'` patterns.

Npgsql supports generated `tsvector` columns and expression indexes:

```csharp
modelBuilder.Entity<Product>()
    .HasGeneratedTsVectorColumn(
        p => p.SearchVector,
        "english",
        p => new { p.Name, p.Description })
    .HasIndex(p => p.SearchVector)
    .HasMethod("GIN");
```

This is generally the right baseline for searchable text fields that need acceptable scale.

## PostgreSQL index design

Treat index choice as part of the model, not as an afterthought.

Relevant defaults from PostgreSQL docs:

- B-tree: default choice for equality, range, sorting, and prefix-pattern cases.
- GIN: common fit for full-text search, arrays, and `jsonb` containment workloads.
- GiST, SP-GiST, BRIN: niche but valuable when the data shape actually matches them.

Npgsql exposes PostgreSQL-specific index features through Fluent API:

- `HasMethod("gin")`
- `IncludeProperties(...)` for covering indexes
- `HasOperators(...)` for operator classes
- `IsCreatedConcurrently()` for production-friendlier index creation

Review heuristics:

- `LIKE 'prefix%'` usually points to B-tree with the right operator class.
- `jsonb @>` or array containment usually points to GIN.
- Large append-heavy tables with naturally ordered values may justify BRIN, but verify with real workload characteristics.
