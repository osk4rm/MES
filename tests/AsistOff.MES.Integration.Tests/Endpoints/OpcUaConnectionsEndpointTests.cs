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

    [Fact]
    public async Task Status_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{BaseUrl}/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_ReturnsEntry_WithTagCounts_MatchingReadingRecency()
    {
        // Arrange - one connection on a fresh machine, one tag with a fresh
        // reading (seconds old, inside the 2x30s reporting threshold) plus
        // one tag that never reported. The OPC UA poller is disabled in the
        // integration host, so LastSeenAtUtc stays null (never seen).
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var connection = await CreateConnectionAsync(client, machineId);
        var reportingTag = await CreateTagAsync(client, machineId);
        var silentTag = await CreateTagAsync(client, machineId);
        await SubmitReadingAsync(client, reportingTag.Id, DateTime.UtcNow.AddSeconds(-10), 21.5);

        // Act - filtered to the fresh machine so the shared tenant database
        // cannot leak other tests' connections into the assertions.
        var response = await client.GetAsync($"{BaseUrl}/status?machineId={machineId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await ReadAsync<OpcUaConnectionStatusDto>(response);
        var entry = status.Connections.Should().ContainSingle(e => e.ConnectionId == connection.Id).Subject;
        entry.MachineId.Should().Be(machineId);
        entry.EndpointUrl.Should().Be(connection.EndpointUrl);
        entry.IsEnabled.Should().BeTrue();
        entry.LastSeenAtUtc.Should().BeNull();
        entry.IsLive.Should().BeFalse();
        entry.TotalTags.Should().Be(2);
        entry.ReportingTags.Should().Be(1);
        entry.StaleTags.Should().Be(1);
        status.TotalCount.Should().Be(1);
        status.LiveCount.Should().Be(0);
        status.StaleCount.Should().Be(1);
        status.DisabledCount.Should().Be(0);
    }

    [Fact]
    public async Task Status_DisabledConnection_IsNotLive_AndExcludedFromLiveTotals()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var connection = await CreateConnectionAsync(client, machineId);
        var toggle = await client.PostAsync($"{BaseUrl}/{connection.Id}/toggle", null);
        toggle.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync($"{BaseUrl}/status?machineId={machineId}");

        // Assert - disabled is not applicable: never live, never stale
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await ReadAsync<OpcUaConnectionStatusDto>(response);
        var entry = status.Connections.Should().ContainSingle(e => e.ConnectionId == connection.Id).Subject;
        entry.IsEnabled.Should().BeFalse();
        entry.IsLive.Should().BeFalse();
        status.TotalCount.Should().Be(1);
        status.LiveCount.Should().Be(0);
        status.StaleCount.Should().Be(0);
        status.DisabledCount.Should().Be(1);
    }

    [Fact]
    public async Task Status_UnknownMachineId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/status?machineId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Status_CrossTenantMachine_Returns404()
    {
        // Arrange - a machine owned by a brand-new tenant
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherMachineId = await CreateMachineAsync(otherTenantClient);

        // Act - requested as the seeded dev tenant
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}/status?machineId={otherMachineId}");

        // Assert - the other tenant's machine is invisible, hence unknown
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Status_DoesNotLeakCrossTenantConnections()
    {
        // Arrange - a connection under a brand-new tenant
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherConnection = await CreateConnectionAsync(otherTenantClient);

        // Act - status as the seeded dev tenant
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.GetAsync($"{BaseUrl}/status");

        // Assert - the other tenant's connection never appears
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await ReadAsync<OpcUaConnectionStatusDto>(response);
        status.Connections.Should().NotContain(e => e.ConnectionId == otherConnection.Id);
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

    private static async Task<MachineTelemetryTagDto> CreateTagAsync(HttpClient client, Guid machineId)
    {
        var response = await client.PostAsJsonAsync("/api/telemetry-tags", new
        {
            machineId,
            nodeId = $"ns=2;s={Guid.NewGuid():N}",
            displayName = "Sensor",
            dataType = 2,
            pollIntervalSeconds = 30,
            description = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineTelemetryTagDto>(response);
    }

    private static async Task SubmitReadingAsync(HttpClient client, Guid tagId, DateTime readAt, double value)
    {
        var response = await client.PostAsJsonAsync("/api/telemetry-readings", new
        {
            tagId,
            readAt,
            doubleValue = value,
            stringValue = (string?)null,
            quality = 1
        });
        response.EnsureSuccessStatusCode();
    }
}
