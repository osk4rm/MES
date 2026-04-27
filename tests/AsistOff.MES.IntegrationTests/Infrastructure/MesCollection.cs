namespace AsistOff.MES.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection that shares a single <see cref="MesApiFactory"/> (and therefore
/// a single Postgres container + booted host) across all integration test classes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MesCollection : ICollectionFixture<MesApiFactory>
{
    public const string Name = "mes-integration";
}
