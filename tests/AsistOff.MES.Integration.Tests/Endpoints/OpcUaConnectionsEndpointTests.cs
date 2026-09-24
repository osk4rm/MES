using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/opcua-connections</c>. They
/// exercise the full request pipeline: authentication, tenant resolution,
/// MediatR handlers, EF Core persistence and the global exception handler —
/// against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OpcUaConnectionsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/opcua-connections";

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var endpointUrl = $"opc.tcp://plc-{Guid.NewGuid():N}.local:4840";

        var createResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            endpointUrl,
            securityPolicy = 1,
            pollIntervalSeconds = 30
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<OpcUaConnectionDto>(createResponse);
        created.MachineId.Should().Be(machineId);
        created.EndpointUrl.Should().Be(endpointUrl);
        created.IsEnabled.Should().BeTrue();
        created.LastSeenAtUtc.Should().BeNull();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<OpcUaConnectionDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.EndpointUrl.Should().Be(endpointUrl);
    }

    [Fact]
    public async Task Create_DuplicateEndpointForSameMachine_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var payload = new
        {
            machineId,
            endpointUrl = $"opc.tcp://plc-{Guid.NewGuid():N}.local:4840",
            securityPolicy = 1,
            pollIntervalSeconds = 30
        };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_MalformedUrl_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            endpointUrl = "not-a-url",
            securityPolicy = 1,
            pollIntervalSeconds = 30
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_PollIntervalOutOfRange_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            endpointUrl = $"opc.tcp://plc-{Guid.NewGuid():N}.local:4840",
            securityPolicy = 1,
            pollIntervalSeconds = 4
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Toggle_FlipsIsEnabled_UnknownIdReturns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var connection = await CreateConnectionAsync(client);

        var toggleResponse = await client.PostAsync($"{BaseUrl}/{connection.Id}/toggle", null);

        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var toggled = await ReadAsync<OpcUaConnectionDto>(toggleResponse);
        toggled.IsEnabled.Should().BeFalse();

        var unknownResponse = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/toggle", null);

        unknownResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Test_WellFormedUrl_ReturnsReachable()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var connection = await CreateConnectionAsync(client);

        var response = await client.PostAsync($"{BaseUrl}/{connection.Id}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await ReadAsync<OpcUaConnectionTestDto>(response);
        result.Id.Should().Be(connection.Id);
        result.Reachable.Should().BeTrue();
    }

    [Fact]
    public async Task Test_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"{BaseUrl}/{Guid.NewGuid()}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Test_CrossTenantId_Returns404()
    {
        // Arrange - a connection under a brand-new tenant
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherConnection = await CreateConnectionAsync(otherTenantClient);

        // Act - tested as the seeded dev tenant
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.PostAsync($"{BaseUrl}/{otherConnection.Id}/test", null);

        // Assert - the other tenant's connection is invisible, hence unknown
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_CrossTenantId_Returns404()
    {
        // Arrange - a connection under a brand-new tenant
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherConnection = await CreateConnectionAsync(otherTenantClient);

        // Act - fetched as the seeded dev tenant
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}/{otherConnection.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_SupportsPagingAndFilters()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var otherMachineId = await CreateMachineAsync(client);
        var wanted = await CreateConnectionAsync(client, machineId);
        await CreateConnectionAsync(client, otherMachineId);

        // Disable the wanted connection so the isEnabled filter can find it.
        var toggle = await client.PostAsync($"{BaseUrl}/{wanted.Id}/toggle", null);
        toggle.EnsureSuccessStatusCode();

        var byMachine = await client.GetAsync($"{BaseUrl}?machineId={machineId}");
        byMachine.StatusCode.Should().Be(HttpStatusCode.OK);
        var byMachinePage = await ReadAsync<PagedResponseDto<OpcUaConnectionDto>>(byMachine);
        byMachinePage.Items.Should().ContainSingle(item => item.Id == wanted.Id);

        var disabled = await client.GetAsync($"{BaseUrl}?isEnabled=false&machineId={machineId}");
        disabled.StatusCode.Should().Be(HttpStatusCode.OK);
        var disabledPage = await ReadAsync<PagedResponseDto<OpcUaConnectionDto>>(disabled);
        disabledPage.Items.Should().ContainSingle(item => item.Id == wanted.Id);

        var enabled = await client.GetAsync($"{BaseUrl}?isEnabled=true&machineId={machineId}");
        enabled.StatusCode.Should().Be(HttpStatusCode.OK);
        var enabledPage = await ReadAsync<PagedResponseDto<OpcUaConnectionDto>>(enabled);
        enabledPage.Items.Should().NotContain(item => item.Id == wanted.Id);
    }

    private static async Task<Guid> CreateMachineAsync(HttpClient client)
    {
        var code = $"OC-M-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code,
            name = code,
            description = (string?)null,
            departmentId = (Guid?)null,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<MachineDto>(response)).Id;
    }

    private static async Task<OpcUaConnectionDto> CreateConnectionAsync(HttpClient client, Guid? machineId = null)
    {
        machineId ??= await CreateMachineAsync(client);
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            machineId,
            endpointUrl = $"opc.tcp://plc-{Guid.NewGuid():N}.local:4840",
            securityPolicy = 1,
            pollIntervalSeconds = 30
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<OpcUaConnectionDto>(response);
    }
}
