using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class ProductGroupsEndpointsTests : IntegrationTestBase
{
    public ProductGroupsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle_with_parent()
    {
        var parent = await Client.PostJsonAsync<ProductGroupPayload>("/api/product-groups", new
        {
            Code = "ROOT",
            Name = "Root group",
            Description = (string?)null,
            IsActive = true,
            ParentId = (Guid?)null,
            SyncId = (string?)null,
        });

        var child = await Client.PostJsonAsync<ProductGroupPayload>("/api/product-groups", new
        {
            Code = "CHILD",
            Name = "Child group",
            Description = (string?)null,
            IsActive = true,
            ParentId = parent.Id,
            SyncId = (string?)null,
        });
        child.Id.Should().NotBeEmpty();

        var fetched = await Client.GetJsonAsync<ProductGroupPayload>($"/api/product-groups/{child.Id}");
        fetched.Code.Should().Be("CHILD");

        var page = await Client.GetJsonAsync<PagedProductGroupsPayload>("/api/product-groups?code=CHILD");
        page.Items.Should().ContainSingle(g => g.Id == child.Id);

        await Client.PutJsonAsync($"/api/product-groups/{child.Id}", new
        {
            Id = child.Id,
            Code = "CHILD",
            Name = "Child group renamed",
            Description = (string?)null,
            IsActive = false,
            ParentId = parent.Id,
            SyncId = (string?)null,
        });

        var afterUpdate = await Client.GetJsonAsync<ProductGroupPayload>($"/api/product-groups/{child.Id}");
        afterUpdate.Name.Should().Be("Child group renamed");
        afterUpdate.IsActive.Should().BeFalse();

        var del = await Client.DeleteAsync($"/api/product-groups/{child.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/product-groups/{child.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record ProductGroupPayload(Guid Id, string Code, string Name, string? Description, bool IsActive);
    private sealed record PagedProductGroupsPayload(IReadOnlyList<ProductGroupPayload> Items, int TotalCount);
}
