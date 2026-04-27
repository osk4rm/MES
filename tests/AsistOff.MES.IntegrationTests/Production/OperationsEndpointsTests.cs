using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Production;

/// <summary>
/// Covers everything under <c>/api/operations</c> — operations themselves plus
/// child resources (BOM items, outputs, resource requirements, dependencies)
/// and reorder. Each test builds the prerequisite recipe + version + operation
/// inline to remain self-contained against a Respawn-cleared DB.
/// </summary>
public sealed class OperationsEndpointsTests : IntegrationTestBase
{
    public OperationsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Operation_full_lifecycle_including_children()
    {
        var versionId = await CreateRecipeAndDraftVersionAsync();
        var productId = await CreateProductAsync("BOM-PROD");
        var outputProductId = await CreateProductAsync("OUT-PROD");
        var departmentId = await CreateDepartmentAsync("OP-DEPT");

        // Add operation
        var op = await Client.PostJsonAsync<OperationNodePayload>("/api/operations", new
        {
            VersionId = versionId,
            Code = "OP-1",
            Name = "Operation 1",
            Description = (string?)null,
            OperationType = (string?)null,
            SortIndex = (int?)null,
            SetupTimeMinutes = 5m,
            RunTimeMode = 1, // PerUnitSeconds
            RunTimePerUnitSeconds = 30m,
            RunTimePerBatchMinutes = (decimal?)null,
            TeardownTimeMinutes = 2m,
            QueueTimeMinutes = (decimal?)null,
            IsOptional = false,
            AllowParallelExecution = false,
            ExpectedQuantity = 100m,
        });
        op.Id.Should().NotBeEmpty();

        // Update operation
        await Client.PutJsonAsync($"/api/operations/{op.Id}", new
        {
            OperationId = op.Id,
            Code = "OP-1",
            Name = "Operation 1 (renamed)",
            Description = "edited",
            OperationType = "machining",
            SortIndex = 10,
            SetupTimeMinutes = 5m,
            RunTimeMode = 1,
            RunTimePerUnitSeconds = 30m,
            RunTimePerBatchMinutes = (decimal?)null,
            TeardownTimeMinutes = 2m,
            QueueTimeMinutes = (decimal?)null,
            IsOptional = true,
            AllowParallelExecution = true,
            ExpectedQuantity = 200m,
        });

        // Add BOM item
        var bomResponse = await Client.PostAsJsonAsync($"/api/operations/{op.Id}/bom-items", new
        {
            OperationId = op.Id,
            ProductId = productId,
            MeasureUnitId = (Guid?)null,
            Quantity = 1.5m,
            QuantityType = 1, // PerUnit
            ScrapPercentage = 0.1m,
            IsOptional = false,
            PreferredWarehouseId = (Guid?)null,
            ConsumptionTiming = 1, // AtStart
            Notes = (string?)null,
            SortIndex = 0,
        });
        bomResponse.EnsureSuccessStatusCode();
        var bomId = await bomResponse.Content.ReadFromJsonAsync<Guid>(Json);
        bomId.Should().NotBeEmpty();

        // Update BOM item
        await Client.PutJsonAsync($"/api/bom-items/{bomId}", new
        {
            BomItemId = bomId,
            ProductId = productId,
            MeasureUnitId = (Guid?)null,
            Quantity = 2m,
            QuantityType = 1,
            ScrapPercentage = (decimal?)null,
            IsOptional = false,
            PreferredWarehouseId = (Guid?)null,
            ConsumptionTiming = 1,
            Notes = "edited",
            SortIndex = 0,
        });

        // Add output
        var outputResponse = await Client.PostAsJsonAsync($"/api/operations/{op.Id}/outputs", new
        {
            OperationId = op.Id,
            ProductId = outputProductId,
            MeasureUnitId = (Guid?)null,
            Quantity = 1m,
            QuantityType = 1,
            OutputType = 1, // Product
            PreferredWarehouseId = (Guid?)null,
            Notes = (string?)null,
            SortIndex = 0,
        });
        outputResponse.EnsureSuccessStatusCode();
        var outputId = await outputResponse.Content.ReadFromJsonAsync<Guid>(Json);

        // Update output
        await Client.PutJsonAsync($"/api/outputs/{outputId}", new
        {
            OutputId = outputId,
            ProductId = outputProductId,
            MeasureUnitId = (Guid?)null,
            Quantity = 2m,
            QuantityType = 1,
            OutputType = 1,
            PreferredWarehouseId = (Guid?)null,
            Notes = "edited",
            SortIndex = 0,
        });

        // Add resource requirement
        var resourceResponse = await Client.PostAsJsonAsync($"/api/operations/{op.Id}/resources", new
        {
            OperationId = op.Id,
            PreferredDepartmentId = departmentId,
            PreferredMachineId = (Guid?)null,
            RequiredCapability = "WELD — Welder",
            RequiredOperatorCount = 1,
            RequiredRole = "Operator",
            Notes = (string?)null,
        });
        resourceResponse.EnsureSuccessStatusCode();
        var resourceId = await resourceResponse.Content.ReadFromJsonAsync<Guid>(Json);

        // Update resource
        await Client.PutJsonAsync($"/api/resources/{resourceId}", new
        {
            ResourceRequirementId = resourceId,
            PreferredDepartmentId = departmentId,
            PreferredMachineId = (Guid?)null,
            RequiredCapability = "WELD — Welder",
            RequiredOperatorCount = 2,
            RequiredRole = "Senior operator",
            Notes = "edited",
        });

        // Verify everything appears in the version detail
        var detail = await Client.GetJsonAsync<RecipeVersionDetailPayload>($"/api/recipe-versions/{versionId}");
        var op1 = detail.Operations.Single(o => o.Id == op.Id);
        op1.Name.Should().Be("Operation 1 (renamed)");
        op1.BomItems.Should().HaveCount(1);
        op1.Outputs.Should().HaveCount(1);
        op1.ResourceRequirements.Should().HaveCount(1);

        // Remove children
        (await Client.DeleteAsync($"/api/bom-items/{bomId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/outputs/{outputId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/resources/{resourceId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Delete operation
        (await Client.DeleteAsync($"/api/operations/{op.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await Client.GetJsonAsync<RecipeVersionDetailPayload>($"/api/recipe-versions/{versionId}");
        afterDelete.Operations.Should().NotContain(o => o.Id == op.Id);
    }

    [Fact]
    public async Task Reorder_and_set_dependencies_work_together()
    {
        var versionId = await CreateRecipeAndDraftVersionAsync();
        var op1 = await CreateOperationAsync(versionId, "A", sortIndex: null);
        var op2 = await CreateOperationAsync(versionId, "B", sortIndex: null);

        // Reorder
        var reorderResponse = await Client.PostAsJsonAsync(
            $"/api/recipe-versions/{versionId}/operations/reorder",
            new
            {
                Order = new[]
                {
                    new { OperationId = op2.Id, SortIndex = 0 },
                    new { OperationId = op1.Id, SortIndex = 1 },
                },
            });
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterReorder = await Client.GetJsonAsync<RecipeVersionDetailPayload>($"/api/recipe-versions/{versionId}");
        afterReorder.Operations.Single(o => o.Id == op2.Id).SortIndex.Should().Be(0);
        afterReorder.Operations.Single(o => o.Id == op1.Id).SortIndex.Should().Be(1);

        // Set op2 -> op1 (op1 depends on op2 finishing first)
        var depResponse = await Client.PutAsJsonAsync($"/api/operations/{op1.Id}/dependencies", new
        {
            Dependencies = new[]
            {
                new
                {
                    PredecessorOperationId = op2.Id,
                    DependencyType = 1, // FinishToStart
                    LagMinutes = (decimal?)null,
                },
            },
        });
        depResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDeps = await Client.GetJsonAsync<RecipeVersionDetailPayload>($"/api/recipe-versions/{versionId}");
        afterDeps.Operations.Single(o => o.Id == op1.Id).Dependencies.Should().ContainSingle(
            d => d.PredecessorOperationNodeId == op2.Id);

        // Clearing dependencies works too
        var clearResponse = await Client.PutAsJsonAsync($"/api/operations/{op1.Id}/dependencies", new
        {
            Dependencies = Array.Empty<object>(),
        });
        clearResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterClear = await Client.GetJsonAsync<RecipeVersionDetailPayload>($"/api/recipe-versions/{versionId}");
        afterClear.Operations.Single(o => o.Id == op1.Id).Dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_operation_with_mismatched_route_id_returns_bad_request()
    {
        var versionId = await CreateRecipeAndDraftVersionAsync();
        var op = await CreateOperationAsync(versionId, "OP-X", sortIndex: null);

        var response = await Client.PutAsJsonAsync($"/api/operations/{Guid.NewGuid()}", new
        {
            OperationId = op.Id,
            Code = "OP-X",
            Name = "name",
            Description = (string?)null,
            OperationType = (string?)null,
            SortIndex = 0,
            SetupTimeMinutes = (decimal?)null,
            RunTimeMode = 1,
            RunTimePerUnitSeconds = (decimal?)null,
            RunTimePerBatchMinutes = (decimal?)null,
            TeardownTimeMinutes = (decimal?)null,
            QueueTimeMinutes = (decimal?)null,
            IsOptional = false,
            AllowParallelExecution = false,
            ExpectedQuantity = (decimal?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateRecipeAndDraftVersionAsync()
    {
        var recipe = await Client.PostJsonAsync<RecipePayload>("/api/recipes", new
        {
            Code = "OPS-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Recipe",
            Description = (string?)null,
            IsActive = true,
            PrimaryProductId = (Guid?)null,
            SyncId = (string?)null,
        });
        return recipe.Versions[0].Id;
    }

    private async Task<OperationNodePayload> CreateOperationAsync(Guid versionId, string code, int? sortIndex)
        => await Client.PostJsonAsync<OperationNodePayload>("/api/operations", new
        {
            VersionId = versionId,
            Code = code,
            Name = code,
            Description = (string?)null,
            OperationType = (string?)null,
            SortIndex = sortIndex,
            SetupTimeMinutes = (decimal?)null,
            RunTimeMode = 1,
            RunTimePerUnitSeconds = (decimal?)null,
            RunTimePerBatchMinutes = (decimal?)null,
            TeardownTimeMinutes = (decimal?)null,
            QueueTimeMinutes = (decimal?)null,
            IsOptional = false,
            AllowParallelExecution = false,
            ExpectedQuantity = (decimal?)null,
        });

    private async Task<Guid> CreateProductAsync(string code)
    {
        var product = await Client.PostJsonAsync<IdPayload>("/api/products", new
        {
            Code = code,
            Name = code,
            Description = (string?)null,
            Ean = (string?)null,
            Barcode = (string?)null,
            ScanBy = 2,
            IsActive = true,
            ProductGroupId = (Guid?)null,
            SyncId = (string?)null,
        });
        return product.Id;
    }

    private async Task<Guid> CreateDepartmentAsync(string code)
    {
        var dept = await Client.PostJsonAsync<IdPayload>("/api/departments", new
        {
            Code = code,
            Name = code,
        });
        return dept.Id;
    }

    private sealed record IdPayload(Guid Id);

    private sealed record VersionSummary(Guid Id, int VersionNumber, int Status);
    private sealed record RecipePayload(Guid Id, IReadOnlyList<VersionSummary> Versions);

    private sealed record DependencyPayload(Guid Id, Guid PredecessorOperationNodeId, int DependencyType);

    private sealed record OperationNodePayload(
        Guid Id, string Code, string Name, int SortIndex,
        IReadOnlyCollection<DependencyPayload> Dependencies,
        IReadOnlyCollection<JsonNodePayload> BomItems,
        IReadOnlyCollection<JsonNodePayload> Outputs,
        IReadOnlyCollection<JsonNodePayload> ResourceRequirements);

    private sealed record JsonNodePayload(Guid Id);

    private sealed record RecipeVersionDetailPayload(Guid Id, IReadOnlyList<OperationNodePayload> Operations);
}
