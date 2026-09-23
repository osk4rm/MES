using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Base class for endpoint-scoped integration tests. Every integration test
/// class joins the shared <c>Integration</c> collection, so all of them reuse
/// one PostgreSQL container and application host (see
/// <see cref="Infrastructure.MesApplicationFixture"/>).
/// </summary>
public abstract class IntegrationTestBase(MesApplicationFixture fixture)
{
    protected MesApplicationFixture Fixture { get; } = fixture;

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        value.Should().NotBeNull();
        return value!;
    }
}
