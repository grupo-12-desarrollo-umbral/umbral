namespace umbral_backend.Infrastructure.IntegrationTests;

// Binds every integration test class to a SINGLE shared Postgres container.
//
// Without this, each class declares IClassFixture<PostgreSqlFixture>, and xUnit
// runs classes (each its own collection) in parallel — so the suite boots one
// container per class simultaneously. On a cold Docker daemon that contention
// makes Npgsql connections time out and the whole run fails. One shared
// container removes the race by construction; tests stay isolated because each
// one truncates the schema before acting (see ResetDatabaseAsync in each class).
//
// Classes in a single collection run sequentially, which the per-test truncate
// already requires.
[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSql integration";
}
