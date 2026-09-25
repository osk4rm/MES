using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/operator-shift-assignments</c> -
/// the tenant-scoped roster assigning operators to shifts per date. They drive
/// the full pipeline (auth, tenant resolution, MediatR, EF Core, exception
/// handler) against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OperatorShiftAssignmentsEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/operator-shift-assignments";
    private static readonly Guid EmptyUserId = Guid.Empty;

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ReturnsCreated_AndAppearsInBrowseForThatDate()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var shiftId = await CreateShiftAsync(client);
        var date = $"2026-09-{Random.Shared.Next(10, 28):D2}";

        var createResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId,
            shiftId,
            date,
            notes = "Night cover"
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<OperatorShiftAssignmentDto>(createResponse);
        created.Id.Should().NotBeEmpty();
        created.OperatorId.Should().Be(operatorId);
        created.ShiftId.Should().Be(shiftId);
        created.Date.Should().Be(date);

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var browseResponse = await client.GetAsync($"{BaseUrl}?date={date}");
        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<OperatorShiftAssignmentDto>>(browseResponse);
        page.Items.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Create_DuplicateAssignment_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var shiftId = await CreateShiftAsync(client);
        var date = $"2026-10-{Random.Shared.Next(10, 28):D2}";
        var payload = new { operatorId, shiftId, date, notes = (string?)null };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithUnknownOperator_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var shiftId = await CreateShiftAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId = Guid.NewGuid(),
            shiftId,
            date = "2026-09-24",
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithUnknownShift_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId,
            shiftId = Guid.NewGuid(),
            date = "2026-09-24",
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithoutDate_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var shiftId = await CreateShiftAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId,
            shiftId,
            notes = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_RemovesAssignmentFromBrowse()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var shiftId = await CreateShiftAsync(client);
        var date = $"2026-11-{Random.Shared.Next(10, 28):D2}";
        var created = await CreateAssignmentAsync(client, operatorId, shiftId, date);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var browseResponse = await client.GetAsync($"{BaseUrl}?date={date}&operatorId={operatorId}&shiftId={shiftId}");
        browseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<OperatorShiftAssignmentDto>>(browseResponse);
        page.Items.Should().NotContain(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Browse_FiltersByDateRangeOperatorAndShift()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorA = await CreateOperatorAsync(client);
        var operatorB = await CreateOperatorAsync(client);
        var shiftA = await CreateShiftAsync(client);
        var shiftB = await CreateShiftAsync(client);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var dateA = "2026-12-10";
        var dateB = "2026-12-11";
        var dateC = "2026-12-12";
        await CreateAssignmentAsync(client, operatorA, shiftA, dateA, suffix);
        await CreateAssignmentAsync(client, operatorA, shiftB, dateB, suffix);
        await CreateAssignmentAsync(client, operatorB, shiftA, dateC, suffix);

        var byOperator = await ReadAsync<PagedResponseDto<OperatorShiftAssignmentDto>>(
            await client.GetAsync($"{BaseUrl}?operatorId={operatorA}&dateFrom=2026-12-01&dateTo=2026-12-31"));
        byOperator.Items.Should().Contain(item => item.Date == dateA);
        byOperator.Items.Should().Contain(item => item.Date == dateB);
        byOperator.Items.Should().NotContain(item => item.Date == dateC);

        var byShift = await ReadAsync<PagedResponseDto<OperatorShiftAssignmentDto>>(
            await client.GetAsync($"{BaseUrl}?shiftId={shiftA}&dateFrom=2026-12-01&dateTo=2026-12-31"));
        byShift.Items.Should().Contain(item => item.Date == dateA);
        byShift.Items.Should().Contain(item => item.Date == dateC);

        var byRange = await ReadAsync<PagedResponseDto<OperatorShiftAssignmentDto>>(
            await client.GetAsync($"{BaseUrl}?dateFrom=2026-12-11&dateTo=2026-12-12"));
        byRange.Items.Select(item => item.Date).Should().Contain(dateB);
        byRange.Items.Select(item => item.Date).Should().Contain(dateC);
    }

    [Fact]
    public async Task Browse_AssignmentCreatedInAnotherTenant_IsNotVisible()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherOperatorId = await CreateOperatorAsync(otherTenantClient);
        var otherShiftId = await CreateShiftAsync(otherTenantClient);
        var date = $"2026-09-{Random.Shared.Next(10, 28):D2}";
        var created = await CreateAssignmentAsync(otherTenantClient, otherOperatorId, otherShiftId, date);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();

        var browse = await devClient.GetAsync($"{BaseUrl}?date={date}");
        browse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ReadAsync<PagedResponseDto<OperatorShiftAssignmentDto>>(browse);
        page.Items.Should().NotContain(item => item.Id == created.Id);

        var crossTenantCreate = await devClient.PostAsJsonAsync(BaseUrl, new
        {
            operatorId = otherOperatorId,
            shiftId = otherShiftId,
            date,
            notes = (string?)null
        });
        crossTenantCreate.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_AssignmentCreatedInAnotherTenant_Returns404()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var created = await CreateAssignmentAsync(
            otherTenantClient,
            await CreateOperatorAsync(otherTenantClient),
            await CreateShiftAsync(otherTenantClient),
            $"2026-09-{Random.Shared.Next(10, 28):D2}");

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();

        var response = await devClient.GetAsync($"{BaseUrl}/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AssignmentCreatedInAnotherTenant_Returns404_AndAssignmentSurvives()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var created = await CreateAssignmentAsync(
            otherTenantClient,
            await CreateOperatorAsync(otherTenantClient),
            await CreateShiftAsync(otherTenantClient),
            $"2026-09-{Random.Shared.Next(10, 28):D2}");

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();

        var deleteResponse = await devClient.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerGet = await otherTenantClient.GetAsync($"{BaseUrl}/{created.Id}");
        ownerGet.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid> CreateOperatorAsync(HttpClient client)
    {
        var identifier = $"OP-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/operators", new
        {
            identifier,
            firstName = "Jan",
            lastName = "Kowalski",
            ratePerHour = 10m,
            departmentId = (Guid?)null,
            userId = EmptyUserId
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<OperatorDto>();
        return created!.Id;
    }

    private static async Task<Guid> CreateShiftAsync(HttpClient client)
    {
        var code = $"IT-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/shifts", new
        {
            code,
            name = "Morning",
            description = (string?)null,
            startTime = "06:00:00",
            endTime = "14:00:00",
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ShiftDto>();
        return created!.Id;
    }

    private static async Task<OperatorShiftAssignmentDto> CreateAssignmentAsync(
        HttpClient client, Guid operatorId, Guid shiftId, string date, string? notes = null)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId,
            shiftId,
            date,
            notes
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OperatorShiftAssignmentDto>())!;
    }

    private sealed record OperatorDto(Guid Id, string Identifier);
    private sealed record ShiftDto(Guid Id, string Code);
}
