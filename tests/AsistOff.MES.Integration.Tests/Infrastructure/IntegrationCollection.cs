namespace AsistOff.MES.Integration.Tests;

/// <summary>
/// Shares one PostgreSQL container / application host across every integration
/// test class and runs them sequentially, so several hosts never race to start
/// at once.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationCollection : ICollectionFixture<Infrastructure.MesApplicationFixture>
{
    public const string Name = "Integration";
}
