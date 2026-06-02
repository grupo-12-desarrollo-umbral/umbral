namespace umbral_backend.Infrastructure.IntegrationTests;

[CollectionDefinition("MissionDesignIntegrationTests", DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgreSqlFixture>
{
}
