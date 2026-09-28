using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for <c>/api/operator-skills</c> (issue
/// #397) — the tenant-scoped operator skill qualification matrix — plus the
/// production consumers: confirmation gating, and the dispatch / shift-queue
/// skill-gap flags. They drive the full pipeline (auth, tenant resolution,
/// MediatR, EF Core, exception handler) against a real PostgreSQL database.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OperatorSkillQualificationsEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/operator-skills";
    private const string DispatchUrl = "/api/schedule/dispatch";
    private const string QueueUrl = "/api/schedule/operator-queue";
    private const string ConfirmationsUrl = "/api/production-confirmations";
    private static readonly Guid EmptyUserId = Guid.Empty;

    [Fact]
    public async Task Browse_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assign_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId = Guid.NewGuid(),
            skillId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unassign_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assign_ReturnsCreated_AndAppearsInBrowseAndGet()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var skill = await CreateSkillAsync(client);

        var assignResponse = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId,
            skillId = skill.Id
        });

        assignResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<OperatorSkillQualificationDto>(assignResponse);
        created.Id.Should().NotBeEmpty();
        created.OperatorId.Should().Be(operatorId);
        created.SkillId.Should().Be(skill.Id);
        created.SkillCode.Should().Be(skill.Code);
        created.OperatorIdentifier.Should().NotBeNullOrWhiteSpace();

        var getResponse = await client.GetAsync($"{BaseUrl}/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var byOperator = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await client.GetAsync($"{BaseUrl}?operatorId={operatorId}"));
        byOperator.Items.Should().ContainSingle(item => item.Id == created.Id);

        var bySkill = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await client.GetAsync($"{BaseUrl}?skillId={skill.Id}"));
        bySkill.Items.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Assign_DuplicatePair_Returns409()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var skill = await CreateSkillAsync(client);
        var payload = new { operatorId, skillId = skill.Id };

        var first = await client.PostAsJsonAsync(BaseUrl, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(BaseUrl, payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Assign_WithUnknownOperator_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var skill = await CreateSkillAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId = Guid.NewGuid(),
            skillId = skill.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_WithUnknownSkill_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId,
            skillId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_WithEmptyIds_Returns400()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var skill = await CreateSkillAsync(client);

        var emptyOperator = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId = Guid.Empty,
            skillId = skill.Id
        });
        emptyOperator.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var emptySkill = await client.PostAsJsonAsync(BaseUrl, new
        {
            operatorId,
            skillId = Guid.Empty
        });
        emptySkill.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unassign_UnknownId_Returns404()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unassign_RemovesQualificationFromBrowse()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var skill = await CreateSkillAsync(client);
        var created = await AssignAsync(client, operatorId, skill.Id);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var browse = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await client.GetAsync($"{BaseUrl}?operatorId={operatorId}&skillId={skill.Id}"));
        browse.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Browse_FiltersByOperatorAndSkill()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorA = await CreateOperatorAsync(client);
        var operatorB = await CreateOperatorAsync(client);
        var skillA = await CreateSkillAsync(client);
        var skillB = await CreateSkillAsync(client);
        var keepA = await AssignAsync(client, operatorA, skillA.Id);
        var keepB = await AssignAsync(client, operatorA, skillB.Id);
        var other = await AssignAsync(client, operatorB, skillA.Id);

        var byOperator = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await client.GetAsync($"{BaseUrl}?operatorId={operatorA}"));
        byOperator.Items.Select(i => i.Id).Should().Contain([keepA.Id, keepB.Id]);
        byOperator.Items.Select(i => i.Id).Should().NotContain(other.Id);

        var bySkill = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await client.GetAsync($"{BaseUrl}?skillId={skillA.Id}"));
        bySkill.Items.Select(i => i.Id).Should().Contain([keepA.Id, other.Id]);
        bySkill.Items.Select(i => i.Id).Should().NotContain(keepB.Id);
    }

    [Fact]
    public async Task Assign_SamePairInAnotherTenant_Succeeds_AndStaysIsolated()
    {
        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherTenantClient = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var otherOperatorId = await CreateOperatorAsync(otherTenantClient);
        var otherSkill = await CreateSkillAsync(otherTenantClient);
        var created = await AssignAsync(otherTenantClient, otherOperatorId, otherSkill.Id);

        using var devClient = await Fixture.CreateAuthenticatedClientAsync();

        var browse = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await devClient.GetAsync($"{BaseUrl}?operatorId={otherOperatorId}"));
        browse.Items.Should().BeEmpty();

        var crossTenantGet = await devClient.GetAsync($"{BaseUrl}/{created.Id}");
        crossTenantGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var crossTenantDelete = await devClient.DeleteAsync($"{BaseUrl}/{created.Id}");
        crossTenantDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerGet = await otherTenantClient.GetAsync($"{BaseUrl}/{created.Id}");
        ownerGet.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteOperator_RemovesItsQualifications()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var skill = await CreateSkillAsync(client);
        await AssignAsync(client, operatorId, skill.Id);

        var deleteResponse = await client.DeleteAsync($"/api/operators/{operatorId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var browse = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await client.GetAsync($"{BaseUrl}?skillId={skill.Id}"));
        browse.Items.Should().NotContain(item => item.OperatorId == operatorId);
    }

    [Fact]
    public async Task DeleteSkill_RemovesItsQualifications()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var operatorId = await CreateOperatorAsync(client);
        var skill = await CreateSkillAsync(client);
        await AssignAsync(client, operatorId, skill.Id);

        var deleteResponse = await client.DeleteAsync($"/api/skills/{skill.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var browse = await ReadAsync<PagedResponseDto<OperatorSkillQualificationDto>>(
            await client.GetAsync($"{BaseUrl}?operatorId={operatorId}"));
        browse.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmation_UnqualifiedOperator_Blocked_ThenAcceptedAfterQualification()
    {
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var skill = await CreateSkillAsync(client);
        var operatorId = await CreateOperatorAsync(client);
        var order = await CreateReleasedOrderWithSkillAsync(client, skill.Code);

        var blocked = await client.PostAsJsonAsync(ConfirmationsUrl, new
        {
            productionOrderId = order.Id,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = operatorId,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 10m,
            scrapQuantity = 0m,
            notes = (string?)null
        });

        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain(skill.Code);

        await AssignAsync(client, operatorId, skill.Id);

        var accepted = await client.PostAsJsonAsync(ConfirmationsUrl, new
        {
            productionOrderId = order.Id,
            machineId = Guid.NewGuid(),
            reportedByOperatorId = operatorId,
            reportedAt = DateTime.UtcNow,
            goodQuantity = 10m,
            scrapQuantity = 0m,
            notes = (string?)null
        });

        accepted.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionConfirmationDto>(accepted);
        created.ReportedByOperatorId.Should().Be(operatorId);
    }

    [Fact]
    public async Task Dispatch_MarksOrderWithZeroQualifiedOperators_UntilQualified()
    {
        // Arrange - isolated tenant so the board holds exactly one order
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var skill = await CreateSkillAsync(client);
        var operatorId = await CreateOperatorAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var shift = await CreateShiftAsync(client);
        await CreateAssignmentAsync(client, operatorId, shift, today);
        var order = await CreateReleasedOrderWithSkillAsync(client, skill.Code, dueDate: DateTime.UtcNow);

        // Act - no qualified roster operator yet
        var flagged = await ReadAsync<DispatchBoardDto>(await client.GetAsync(
            $"{DispatchUrl}?from={today}&to={today}"));

        // Assert
        flagged.Orders.Should().ContainSingle(o => o.Code == order.Code)
            .Which.NoQualifiedOperator.Should().BeTrue();

        // Act - the rostered operator earns the skill
        await AssignAsync(client, operatorId, skill.Id);
        var cleared = await ReadAsync<DispatchBoardDto>(await client.GetAsync(
            $"{DispatchUrl}?from={today}&to={today}"));

        // Assert
        cleared.Orders.Should().ContainSingle(o => o.Code == order.Code)
            .Which.NoQualifiedOperator.Should().BeFalse();
    }

    [Fact]
    public async Task Queue_MarksOrderWithZeroQualifiedOperators_UntilQualified()
    {
        // Arrange - isolated tenant with a full-day shift covering now
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(email, password);
        var skill = await CreateSkillAsync(client);
        var identifier = $"OP-{Guid.NewGuid():N}"[..12];
        var operatorId = await CreateOperatorAsync(client, identifier);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var shift = await CreateShiftAsync(client, "00:00:00", "23:59:59");
        await CreateAssignmentAsync(client, operatorId, shift, today);
        var order = await CreateReleasedOrderWithSkillAsync(client, skill.Code, dueDate: DateTime.UtcNow);

        // Act - no qualified crew member yet
        var flagged = await ReadAsync<OperatorShiftQueueDto>(
            await client.GetAsync($"{QueueUrl}?operatorCode={identifier}"));

        // Assert
        flagged.Shift.Should().NotBeNull();
        flagged.Orders.Should().ContainSingle(o => o.Code == order.Code)
            .Which.NoQualifiedOperator.Should().BeTrue();

        // Act - the crew member earns the skill
        await AssignAsync(client, operatorId, skill.Id);
        var cleared = await ReadAsync<OperatorShiftQueueDto>(
            await client.GetAsync($"{QueueUrl}?operatorCode={identifier}"));

        // Assert
        cleared.Orders.Should().ContainSingle(o => o.Code == order.Code)
            .Which.NoQualifiedOperator.Should().BeFalse();
    }

    private static string UniqueCode(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<Guid> CreateOperatorAsync(HttpClient client, string? identifier = null)
    {
        var response = await client.PostAsJsonAsync("/api/operators", new
        {
            identifier = identifier ?? $"OP-{Guid.NewGuid():N}"[..12],
            firstName = "Jan",
            lastName = "Kowalski",
            ratePerHour = 10m,
            departmentId = (Guid?)null,
            userId = EmptyUserId
        });
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<OperatorDto>(response)).Id;
    }

    private static async Task<SkillDto> CreateSkillAsync(HttpClient client)
    {
        var code = UniqueCode("SK");
        var response = await client.PostAsJsonAsync("/api/skills", new
        {
            code,
            name = $"Skill {code}",
            description = (string?)null,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<SkillDto>(response);
    }

    private static async Task<ShiftDto> CreateShiftAsync(
        HttpClient client, string startTime = "06:00:00", string endTime = "14:00:00")
    {
        var code = UniqueCode("SH");
        var response = await client.PostAsJsonAsync("/api/shifts", new
        {
            code,
            name = code,
            description = (string?)null,
            startTime,
            endTime,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ShiftDto>(response);
    }

    private static async Task CreateAssignmentAsync(
        HttpClient client, Guid operatorId, ShiftDto shift, string date)
    {
        var response = await client.PostAsJsonAsync("/api/operator-shift-assignments", new
        {
            operatorId,
            shiftId = shift.Id,
            date,
            notes = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<OperatorSkillQualificationDto> AssignAsync(
        HttpClient client, Guid operatorId, Guid skillId)
    {
        var response = await client.PostAsJsonAsync(BaseUrl, new { operatorId, skillId });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<OperatorSkillQualificationDto>(response);
    }

    /// <summary>
    /// Builds a real recipe with one operation carrying a resource
    /// requirement for <paramref name="skillCode"/> (as the
    /// <c>"CODE — Name"</c> display string the recipe editor stores),
    /// releases the version and returns a released order against it.
    /// </summary>
    private static async Task<ProductionOrderDto> CreateReleasedOrderWithSkillAsync(
        HttpClient client, string skillCode, DateTime? dueDate = null)
    {
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = UniqueCode("R"),
            name = "Skill-gated recipe",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await ReadAsync<RecipeDto>(recipeResponse);
        var versionId = recipe.Versions.Single().Id;

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId,
            code = $"OP-{Guid.NewGuid():N}"[..8],
            name = "Skilled work",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = 0,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = 10m,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        operationResponse.EnsureSuccessStatusCode();
        var operationId = (await operationResponse.Content.ReadFromJsonAsync<OperationDto>())!.Id;

        var resourceResponse = await client.PostAsJsonAsync($"/api/operations/{operationId}/resources", new
        {
            operationId,
            preferredDepartmentId = (Guid?)null,
            preferredMachineId = (Guid?)null,
            requiredCapability = $"{skillCode} — Skill {skillCode}",
            requiredOperatorCount = 1,
            requiredRole = (string?)null,
            notes = (string?)null
        });
        resourceResponse.EnsureSuccessStatusCode();

        var releaseVersionResponse = await client.PostAsync($"/api/recipe-versions/{versionId}/release", null);
        releaseVersionResponse.EnsureSuccessStatusCode();

        var orderResponse = await client.PostAsJsonAsync("/api/production-orders", new
        {
            code = UniqueCode("PO"),
            productId = Guid.NewGuid(),
            recipeId = recipe.Id,
            recipeVersionId = versionId,
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate,
            notes = (string?)null,
            syncId = (string?)null
        });
        orderResponse.EnsureSuccessStatusCode();
        var order = await ReadAsync<ProductionOrderDto>(orderResponse);

        var releaseOrderResponse = await client.PostAsync($"/api/production-orders/{order.Id}/release", null);
        releaseOrderResponse.EnsureSuccessStatusCode();
        return await ReadAsync<ProductionOrderDto>(releaseOrderResponse);
    }

    private sealed record OperatorDto(Guid Id, string Identifier);
    private sealed record SkillDto(Guid Id, string Code);
    private sealed record ShiftDto(Guid Id, string Code);
    private sealed record OperationDto(Guid Id);
}
