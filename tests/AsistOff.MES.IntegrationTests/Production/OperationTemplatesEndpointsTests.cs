using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Production;

public sealed class OperationTemplatesEndpointsTests : IntegrationTestBase
{
    public OperationTemplatesEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle()
    {
        var created = await Client.PostJsonAsync<OperationTemplatePayload>("/api/operation-templates", new
        {
            Code = "TPL-1",
            Name = "Cutting",
            Description = "Cutting template",
            OperationType = "machining",
            IsActive = true,
            SetupTimeMinutes = 5m,
            RunTimeMode = 1, // PerUnitSeconds
            RunTimePerUnitSeconds = 30m,
            RunTimePerBatchMinutes = (decimal?)null,
            TeardownTimeMinutes = 2m,
            QueueTimeMinutes = 1m,
        });
        created.Id.Should().NotBeEmpty();
        created.Code.Should().Be("TPL-1");

        var fetched = await Client.GetJsonAsync<OperationTemplatePayload>($"/api/operation-templates/{created.Id}");
        fetched.Name.Should().Be("Cutting");

        var page = await Client.GetJsonAsync<PagedOperationTemplatesPayload>("/api/operation-templates?code=TPL");
        page.Items.Should().ContainSingle(t => t.Id == created.Id);

        await Client.PutJsonAsync($"/api/operation-templates/{created.Id}", new
        {
            Id = created.Id,
            Code = "TPL-1",
            Name = "Cutting (renamed)",
            Description = "Cutting template",
            OperationType = "machining",
            IsActive = false,
            SetupTimeMinutes = 5m,
            RunTimeMode = 1,
            RunTimePerUnitSeconds = 30m,
            RunTimePerBatchMinutes = (decimal?)null,
            TeardownTimeMinutes = 2m,
            QueueTimeMinutes = 1m,
        });

        var afterUpdate = await Client.GetJsonAsync<OperationTemplatePayload>($"/api/operation-templates/{created.Id}");
        afterUpdate.Name.Should().Be("Cutting (renamed)");
        afterUpdate.IsActive.Should().BeFalse();

        var del = await Client.DeleteAsync($"/api/operation-templates/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/operation-templates/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record OperationTemplatePayload(
        Guid Id, string Code, string Name, string? Description, string? OperationType, bool IsActive,
        decimal? SetupTimeMinutes, int RunTimeMode, decimal? RunTimePerUnitSeconds,
        decimal? RunTimePerBatchMinutes, decimal? TeardownTimeMinutes, decimal? QueueTimeMinutes);

    private sealed record PagedOperationTemplatesPayload(IReadOnlyList<OperationTemplatePayload> Items, int TotalCount);
}
