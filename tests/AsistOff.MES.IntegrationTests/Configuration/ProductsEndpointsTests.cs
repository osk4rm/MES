using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Configuration;

public sealed class ProductsEndpointsTests : IntegrationTestBase
{
    public ProductsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Full_crud_lifecycle_with_group()
    {
        var group = await Client.PostJsonAsync<IdPayload>("/api/product-groups", new
        {
            Code = "PG",
            Name = "Product group",
            Description = (string?)null,
            IsActive = true,
            ParentId = (Guid?)null,
            SyncId = (string?)null,
        });

        var created = await Client.PostJsonAsync<ProductPayload>("/api/products", new
        {
            Code = "PROD-1",
            Name = "Product 1",
            Description = "Test product",
            Ean = (string?)null,
            Barcode = (string?)null,
            ScanBy = 2, // Code
            IsActive = true,
            ProductGroupId = group.Id,
            SyncId = (string?)null,
        });
        created.Id.Should().NotBeEmpty();
        created.Code.Should().Be("PROD-1");

        var fetched = await Client.GetJsonAsync<ProductPayload>($"/api/products/{created.Id}");
        fetched.Name.Should().Be("Product 1");

        var page = await Client.GetJsonAsync<PagedProductsPayload>("/api/products?code=PROD-1");
        page.Items.Should().ContainSingle(p => p.Id == created.Id);

        await Client.PutJsonAsync($"/api/products/{created.Id}", new
        {
            Id = created.Id,
            Code = "PROD-1",
            Name = "Product 1 - renamed",
            Description = "Test product",
            Ean = (string?)null,
            Barcode = (string?)null,
            ScanBy = 2,
            IsActive = false,
            ProductGroupId = group.Id,
            SyncId = (string?)null,
        });

        var afterUpdate = await Client.GetJsonAsync<ProductPayload>($"/api/products/{created.Id}");
        afterUpdate.Name.Should().Be("Product 1 - renamed");

        var del = await Client.DeleteAsync($"/api/products/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFound = await Client.GetAsync($"/api/products/{created.Id}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_mismatched_route_id_returns_bad_request()
    {
        var created = await Client.PostJsonAsync<ProductPayload>("/api/products", new
        {
            Code = "PROD-X",
            Name = "X",
            Description = (string?)null,
            Ean = (string?)null,
            Barcode = (string?)null,
            ScanBy = 2,
            IsActive = true,
            ProductGroupId = (Guid?)null,
            SyncId = (string?)null,
        });

        var response = await Client.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}", new
        {
            Id = created.Id,
            Code = "PROD-X",
            Name = "X",
            Description = (string?)null,
            Ean = (string?)null,
            Barcode = (string?)null,
            ScanBy = 2,
            IsActive = true,
            ProductGroupId = (Guid?)null,
            SyncId = (string?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record IdPayload(Guid Id);
    private sealed record ProductPayload(Guid Id, string Code, string Name, bool IsActive);
    private sealed record PagedProductsPayload(IReadOnlyList<ProductPayload> Items, int TotalCount);
}
