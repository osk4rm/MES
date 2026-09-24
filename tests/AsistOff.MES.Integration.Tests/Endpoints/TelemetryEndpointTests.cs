using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/telemetry-tags</c> and
/// <c>/api/telemetry-readings</c>. They exercise the full request pipeline:
/// authentication, tenant resolution, MediatR handlers, EF Core persistence
/// and the global exception handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class TelemetryEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string TagsUrl = "/api/telemetry-tags";
    private const string ReadingsUrl = "/api/telemetry-readings";

    [Fact]
    public async Task BrowseTags_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(TagsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BrowseReadings_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(ReadingsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTag_ReturnsCreated_AndIsRetrievableById()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var nodeId = $"ns=2;s={Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync(TagsUrl, new
        {
            machineId,
            nodeId,
            displayName = "Oven temperature",
            dataType = 2,
            pollIntervalSeconds = 30,
            description = (string?)null
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<MachineTelemetryTagDto>(createResponse);
        created.MachineId.Should().Be(machineId);
        created.NodeId.Should().Be(nodeId);
        created.IsEnabled.Should().BeTrue();

        var getResponse = await client.GetAsync($"{TagsUrl}/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadAsync<MachineTelemetryTagDto>(getResponse);
        fetched.Id.Should().Be(created.Id);
        fetched.DisplayName.Should().Be("Oven temperature");
    }

    [Fact]
    public async Task CreateTag_DuplicateMachineAndNode_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);
        var nodeId = $"ns=2;s={Guid.NewGuid():N}";
        var payload = new
        {
            machineId,
            nodeId,
            displayName = "Pressure",
            dataType = 2,
            pollIntervalSeconds = 30,
            description = (string?)null
        };

        var first = await client.PostAsJsonAsync(TagsUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(TagsUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateTag_EmptyNode_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var machineId = await CreateMachineAsync(client);

        var response = await client.PostAsJsonAsync(TagsUrl, new
        {
            machineId,
            nodeId = "  ",
            displayName = "Nameless",
            dataType = 2,
            pollIntervalSeconds = 30,
            description = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleTag_FlipsIsEnabled_UnknownIdReturns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = await CreateTagAsync(client);

        var toggleResponse = await client.PostAsync($"{TagsUrl}/{tag.Id}/toggle", null);

        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var toggled = await ReadAsync<MachineTelemetryTagDto>(toggleResponse);
        toggled.IsEnabled.Should().BeFalse();

        var unknownResponse = await client.PostAsync($"{TagsUrl}/{Guid.NewGuid()}/toggle", null);

        unknownResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitReading_ReturnsCreated_AndIsVisibleInBrowse()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = await CreateTagAsync(client);
        var readAt = DateTime.UtcNow.AddMinutes(-5);

        var submitResponse = await client.PostAsJsonAsync(ReadingsUrl, new
        {
            tagId = tag.Id,
            readAt,
            doubleValue = 21.5,
            stringValue = (string?)null,
            quality = 1
        });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<TelemetryReadingDto>(submitResponse);
        created.TagId.Should().Be(tag.Id);
        created.MachineId.Should().Be(tag.MachineId);
        created.DoubleValue.Should().Be(21.5);

        var browseResponse = await client.GetAsync($"{ReadingsUrl}?tagId={tag.Id}");

        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<TelemetryReadingDto>>(browseResponse);
        page.Items.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task SubmitReading_FutureReadAt_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = await CreateTagAsync(client);

        var response = await client.PostAsJsonAsync(ReadingsUrl, new
        {
            tagId = tag.Id,
            readAt = DateTime.UtcNow.AddHours(1),
            doubleValue = 21.5,
            stringValue = (string?)null,
            quality = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitReading_TypeMismatchedValue_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = await CreateTagAsync(client);

        var response = await client.PostAsJsonAsync(ReadingsUrl, new
        {
            tagId = tag.Id,
            readAt = DateTime.UtcNow.AddMinutes(-5),
            doubleValue = (double?)null,
            stringValue = "hot",
            quality = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitReading_OtherTenantTag_Returns404()
    {
        // Arrange - create a tag under a brand-new tenant
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherTag = await CreateTagAsync(otherTenantClient);

        // Act - submit a reading for it as the seeded dev tenant
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var response = await devClient.PostAsJsonAsync(ReadingsUrl, new
        {
            tagId = otherTag.Id,
            readAt = DateTime.UtcNow.AddMinutes(-5),
            doubleValue = 21.5,
            stringValue = (string?)null,
            quality = 1
        });

        // Assert - the other tenant's tag is invisible, hence unknown
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BrowseReadings_LatestOnly_ReturnsNewestPerTag()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = await CreateTagAsync(client);
        var olderAt = DateTime.UtcNow.AddMinutes(-20);
        var newerAt = DateTime.UtcNow.AddMinutes(-10);

        await SubmitAsync(client, tag.Id, olderAt, 18.0);
        await SubmitAsync(client, tag.Id, newerAt, 22.5);

        var response = await client.GetAsync($"{ReadingsUrl}?tagId={tag.Id}&latestOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<TelemetryReadingDto>>(response);
        page.Items.Should().ContainSingle();
        page.Items.Single().DoubleValue.Should().Be(22.5);
    }

    [Fact]
    public async Task Readings_HaveNoUpdateOrDeleteEndpoints()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var tag = await CreateTagAsync(client);
        var reading = await SubmitAsync(client, tag.Id, DateTime.UtcNow.AddMinutes(-5), 20.0);

        var putResponse = await client.PutAsJsonAsync($"{ReadingsUrl}/{reading.Id}", new { doubleValue = 99.0 });
        var deleteResponse = await client.DeleteAsync($"{ReadingsUrl}/{reading.Id}");

        putResponse.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetStatus_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync($"{TagsUrl}/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStatus_ReturnsOnlyCallerTenantEntries()
    {
        // Arrange - a tag under a brand-new tenant plus one under the seeded dev tenant
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherTag = await CreateTagAsync(otherTenantClient);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var ownTag = await CreateTagAsync(devClient);

        // Act
        var response = await devClient.GetAsync($"{TagsUrl}/status");

        // Assert - own tag present, other-tenant tag invisible
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await ReadAsync<TelemetryStatusDto>(response);
        status.Tags.Should().ContainSingle(t => t.TagId == ownTag.Id);
        status.Tags.Should().NotContain(t => t.TagId == otherTag.Id);
    }

    [Fact]
    public async Task SubmitThenStatus_ShowsRecentReading_AndNeverReadTag()
    {
        // Arrange - staleness threshold in the integration host is 2x30s,
        // so the reported reading must be seconds (not minutes) old.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var reportedTag = await CreateTagAsync(client);
        var silentTag = await CreateTagAsync(client);
        await SubmitAsync(client, reportedTag.Id, DateTime.UtcNow.AddSeconds(-20), 21.5);

        // Act
        var response = await client.GetAsync($"{TagsUrl}/status");

        // Assert - simulator is disabled in the integration host
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await ReadAsync<TelemetryStatusDto>(response);
        status.SimulatorEnabled.Should().BeFalse();

        var reported = status.Tags.Should().ContainSingle(t => t.TagId == reportedTag.Id).Subject;
        reported.Stale.Should().BeFalse();
        reported.LastReadAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(-20), TimeSpan.FromMinutes(1));
        reported.ReadingsLastHour.Should().BeGreaterThanOrEqualTo(1);

        var silent = status.Tags.Should().ContainSingle(t => t.TagId == silentTag.Id).Subject;
        silent.LastReadAt.Should().BeNull();
        silent.Stale.Should().BeFalse();
        silent.ReadingsLastHour.Should().Be(0);
    }

    private static async Task<Guid> CreateMachineAsync(HttpClient client)
    {
        var code = $"TL-M-{Guid.NewGuid():N}"[..12];
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

    private static async Task<MachineTelemetryTagDto> CreateTagAsync(HttpClient client)
    {
        var machineId = await CreateMachineAsync(client);
        var response = await client.PostAsJsonAsync(TagsUrl, new
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

    private static async Task<TelemetryReadingDto> SubmitAsync(HttpClient client, Guid tagId, DateTime readAt, double value)
    {
        var response = await client.PostAsJsonAsync(ReadingsUrl, new
        {
            tagId,
            readAt,
            doubleValue = value,
            stringValue = (string?)null,
            quality = 1
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<TelemetryReadingDto>(response);
    }
}
