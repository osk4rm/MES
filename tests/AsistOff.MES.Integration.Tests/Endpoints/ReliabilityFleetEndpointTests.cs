using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/reliability/fleet</c>.
/// They exercise the full request pipeline: authentication, tenant
/// resolution, the read-time per-machine MTBF/MTTR computation over downtime
/// and maintenance rows, MTBF-ascending ranking with nulls last and the
/// global exception handler - against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ReliabilityFleetEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/reliability/fleet";
    private const string SnapshotUrl = "/api/reliability/snapshot";

    [Fact]
    public async Task Fleet_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Fleet_RankingMatchesSnapshot_NullsSortLast()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var department = await CreateDepartmentAsync(client);
        var fromUtc = DateTime.UtcNow.AddDays(-30);
        var reasonId = await CreateReasonAsync(client);

        // Worst machine: two 60-min closed stops (mtbf 180 over the window
        // minus 120 downtime); best: one 60-min stop; clean: no failures.
        var worst = await CreateMachineAsync(client, department.Id, "W");
        var best = await CreateMachineAsync(client, department.Id, "B");
        var clean = await CreateMachineAsync(client, department.Id, "C");

        var stopStart = DateTime.UtcNow.AddDays(-10);
        await StartAndCloseDowntimeAsync(client, worst.Id, reasonId, stopStart, stopStart.AddMinutes(60));
        await StartAndCloseDowntimeAsync(client, worst.Id, reasonId, stopStart.AddHours(2), stopStart.AddHours(2).AddMinutes(60));
        await StartAndCloseDowntimeAsync(client, best.Id, reasonId, stopStart, stopStart.AddMinutes(60));

        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&departmentId={department.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await ReadAsync<List<ReliabilityFleetRowDto>>(response);

        rows.Should().HaveCount(3);
        rows[0].MachineId.Should().Be(worst.Id);
        rows[0].FailureCount.Should().Be(2);
        rows[0].TotalDowntimeMinutes.Should().BeApproximately(120, 0.05);
        rows[0].MtbfMinutes.Should().NotBeNull();
        rows[0].MttrMinutes.Should().NotBeNull();
        rows[0].MttrMinutes!.Value.Should().BeApproximately(60, 0.05);
        rows[1].MachineId.Should().Be(best.Id);
        rows[1].FailureCount.Should().Be(1);
        rows[1].MtbfMinutes.Should().NotBeNull();
        rows[1].MttrMinutes.Should().NotBeNull();
        rows[2].MachineId.Should().Be(clean.Id);
        rows[2].FailureCount.Should().Be(0);
        rows[2].MtbfMinutes.Should().BeNull();
        rows[2].MttrMinutes.Should().BeNull();

        // MTBF ascending with nulls last.
        rows[0].MtbfMinutes!.Value.Should().BeLessThan(rows[1].MtbfMinutes!.Value);

        // Parity with the snapshot for the same window.
        foreach (var row in rows)
        {
            var snapshotResponse = await client.GetAsync(
                $"{SnapshotUrl}?machineId={row.MachineId}&fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}");
            snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var snapshot = await ReadAsync<ReliabilitySnapshotDto>(snapshotResponse);

            row.FailureCount.Should().Be(snapshot.FailureCount);
            row.TotalDowntimeMinutes.Should().BeApproximately(snapshot.TotalDowntimeMinutes, 0.05);
            row.UptimeMinutes.Should().BeApproximately(snapshot.UptimeMinutes, 0.05);
            row.MtbfMinutes.Should().Be(snapshot.MtbfMinutes);
            row.MttrMinutes.Should().Be(snapshot.MttrMinutes);
            row.RepairCount.Should().Be(snapshot.RepairCount);
            row.AvgRepairMinutes.Should().Be(snapshot.AvgRepairMinutes);
        }
    }

    [Fact]
    public async Task Fleet_DepartmentFilter_ExcludesOtherDepartments()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var wanted = await CreateDepartmentAsync(client);
        var other = await CreateDepartmentAsync(client);
        var wantedMachine = await CreateMachineAsync(client, wanted.Id);
        var otherMachine = await CreateMachineAsync(client, other.Id);

        var fromUtc = DateTime.UtcNow.AddHours(-8);
        var toUtc = DateTime.UtcNow.AddMinutes(1);
        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(fromUtc)}&toUtc={Qs(toUtc)}&departmentId={wanted.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await ReadAsync<List<ReliabilityFleetRowDto>>(response);

        rows.Should().ContainSingle(r => r.MachineId == wantedMachine.Id);
        rows.Should().NotContain(r => r.MachineId == otherMachine.Id);
        rows.Should().OnlyContain(r => r.DepartmentId == wanted.Id);
    }

    [Fact]
    public async Task Fleet_InactiveMachines_Excluded()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var department = await CreateDepartmentAsync(client);
        var active = await CreateMachineAsync(client, department.Id, isActive: true);
        var inactive = await CreateMachineAsync(client, department.Id, isActive: false);

        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow.AddMinutes(1))}&departmentId={department.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await ReadAsync<List<ReliabilityFleetRowDto>>(response);

        rows.Should().ContainSingle(r => r.MachineId == active.Id);
        rows.Should().NotContain(r => r.MachineId == inactive.Id);
    }

    [Fact]
    public async Task Fleet_CrossTenantMachines_Excluded()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherDepartment = await CreateDepartmentAsync(otherTenantClient);
        var foreignMachine = await CreateMachineAsync(otherTenantClient, otherDepartment.Id);

        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow.AddMinutes(1))}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await ReadAsync<List<ReliabilityFleetRowDto>>(response);

        rows.Should().NotContain(r => r.MachineId == foreignMachine.Id);
    }

    [Fact]
    public async Task Fleet_UnknownDepartment_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(DateTime.UtcNow.AddHours(-8))}&toUtc={Qs(DateTime.UtcNow)}&departmentId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Fleet_ReversedWindow_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(DateTime.UtcNow)}&toUtc={Qs(DateTime.UtcNow.AddHours(-8))}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Fleet_WindowOver93Days_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"{BaseUrl}?fromUtc={Qs(DateTime.UtcNow.AddDays(-100))}&toUtc={Qs(DateTime.UtcNow)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string Qs(DateTime value) => Uri.EscapeDataString(value.ToString("O"));

    private static async Task<DepartmentDto> CreateDepartmentAsync(HttpClient client)
    {
        var code = $"FLT-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/departments", new
        {
            code,
            name = $"Fleet {code}"
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<DepartmentDto>(response);
    }

    private static async Task<MachineDto> CreateMachineAsync(
        HttpClient client, Guid? departmentId = null, string? suffix = null, bool isActive = true)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code = $"FLT-{Guid.NewGuid():N}"[..12],
            name = $"Fleet Work Center {suffix ?? Guid.NewGuid().ToString("N")[..6]}",
            description = (string?)null,
            departmentId,
            isActive
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<MachineDto>(response);
    }

    private static async Task<Guid> CreateReasonAsync(HttpClient client)
    {
        var code = $"FLT-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync("/api/reason-codes", new
        {
            code,
            name = code,
            description = (string?)null,
            category = (short)1,
            isActive = true,
            sortIndex = 0
        });

        response.EnsureSuccessStatusCode();
        return (await ReadAsync<ReasonCodeDto>(response)).Id;
    }

    private static async Task StartAndCloseDowntimeAsync(
        HttpClient client, Guid machineId, Guid reasonId, DateTime startedAt, DateTime endedAt)
    {
        var startResponse = await client.PostAsJsonAsync("/api/downtime-events", new
        {
            machineId,
            reasonCodeId = reasonId,
            startedAt,
            notes = (string?)null,
            reportedByOperatorId = (Guid?)null
        });
        startResponse.EnsureSuccessStatusCode();
        var started = await ReadAsync<DowntimeEventDto>(startResponse);

        var closeResponse = await client.PostAsJsonAsync(
            $"/api/downtime-events/{started.Id}/close", new { endedAt });
        closeResponse.EnsureSuccessStatusCode();
    }

    private sealed record DepartmentDto(Guid Id, string Code, string Name);
}
