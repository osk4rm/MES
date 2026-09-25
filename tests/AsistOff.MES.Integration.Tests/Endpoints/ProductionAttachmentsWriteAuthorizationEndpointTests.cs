using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for issue #233 (slice 2/2: default-deny write
/// authorization for Production, Attachments and Gateway).
/// Proves that an authenticated caller holding only the <c>user</c> role receives
/// 403 on writes (CreateProductionOrder, UploadAttachment) while
/// <c>tenant_admin</c> succeeds, and that read paths (dispatch board, attachment
/// download/list) keep working under existing read grants. Gateway owns no
/// MediatR requests, so it needs no cases here.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProductionAttachmentsWriteAuthorizationEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string ProductionOrdersUrl = "/api/production-orders";
    private const string AttachmentsUrl = "/api/attachments";
    private const string DispatchUrl = "/api/schedule/dispatch";

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    [Fact]
    public async Task CreateProductionOrder_AsUserRole_Returns403()
    {
        // Arrange — caller holds only the read set, lacking production.write.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        // Act
        var response = await client.PostAsJsonAsync(ProductionOrdersUrl, ValidOrderBody());

        // Assert — rejected by the default-deny authorization pipeline step.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProductionOrder_AsTenantAdmin_ReturnsCreated()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);

        // Act
        var create = await client.PostAsJsonAsync(ProductionOrdersUrl, ValidOrderBody());

        // Assert
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadAsync<ProductionOrderDto>(create);

        var get = await client.GetAsync($"{ProductionOrdersUrl}/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadAttachment_AsUserRole_Returns403()
    {
        // Arrange — valid owner created by the tenant admin, upload attempted
        // by the read-only user in the same tenant (403 proves the permission
        // check runs before the handler, not a 404 owner miss).
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var adminClient = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var ownerId = await CreateOperationOwnerAsync(adminClient);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var userClient = await ClientForAsync(email, password);

        // Act
        var upload = await userClient.PostAsync(AttachmentsUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UploadAttachment_AsTenantAdmin_Succeeds()
    {
        // Arrange
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        using var client = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(AttachmentsUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadAsync<AttachmentDto>(upload);
        created.FileName.Should().Be("photo.png");
    }

    [Fact]
    public async Task DispatchBoard_AsUserRole_Returns200()
    {
        // Arrange — read-path regression: dispatch stays available to the user role.
        var (adminEmail, _) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var client = await ClientForAsync(email, password);

        // Act
        var response = await client.GetAsync($"{DispatchUrl}?from=2030-01-01&to=2030-01-02");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AttachmentDownload_AsUserRole_Returns200()
    {
        // Arrange — admin uploads, read-only user in the same tenant downloads
        // and lists (no read regression).
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var adminClient = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var ownerId = await CreateOperationOwnerAsync(adminClient);
        var upload = await adminClient.PostAsync(AttachmentsUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        var (email, password) = await CreateUserWithRoleAsync(tenantId, RbacDefaults.UserRoleCode);
        using var userClient = await ClientForAsync(email, password);

        // Act
        var download = await userClient.GetAsync($"{AttachmentsUrl}/{created.Id}/download");
        var list = await userClient.GetAsync($"{AttachmentsUrl}?ownerType=operation&ownerId={ownerId}");

        // Assert
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> SignInAsync(string email, string password)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return MesApplicationFixture.ExtractCookie(response, AuthCookies.AccessCookieName);
    }

    private async Task<HttpClient> ClientForAsync(string email, string password)
    {
        var accessToken = await SignInAsync(email, password);
        var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<(string Email, string Password)> CreateUserWithRoleAsync(
        Guid tenantId, string roleCode)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"authz233-{suffix}@integration.local";
        const string password = "Passw0rd!";

        using (BackgroundTenantContext.BeginScope(tenantId))
        {
            using var scope = Fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = email,
                Password = string.Empty,
                IsTenantAdmin = false
            };
            user.Password = hasher.HashPassword(user, password);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var roles = scope.ServiceProvider.GetRequiredService<IRolesRepository>();
            var role = await roles.GetByCodeAsync(roleCode);
            role.Should().NotBeNull();

            var userRoles = scope.ServiceProvider.GetRequiredService<IUserRolesRepository>();
            await userRoles.AddAsync(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                RoleId = role!.Id
            });
        }

        return (email, password);
    }

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }

    private static object ValidOrderBody()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        return new
        {
            code = $"PO233-{suffix}",
            productId = Guid.NewGuid(),
            recipeId = Guid.NewGuid(),
            recipeVersionId = Guid.NewGuid(),
            plannedQuantity = 100m,
            measureUnitId = (Guid?)null,
            priority = 0,
            dueDate = (DateTime?)null,
            notes = (string?)null,
            syncId = (string?)null
        };
    }

    private static MultipartFormDataContent UploadContent(
        string ownerType, Guid ownerId, string fileName, string contentType, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(ownerType), "ownerType");
        form.Add(new StringContent(ownerId.ToString()), "ownerId");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<Guid> CreateOperationOwnerAsync(HttpClient client)
    {
        var recipeCode = $"A233-{Guid.NewGuid():N}"[..12];
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = recipeCode,
            name = $"AuthZ233 owner {recipeCode}",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<RecipeDto>();

        var versionResponse = await client.PostAsJsonAsync("/api/recipe-versions", new
        {
            recipeId = recipe!.Id,
            changeNotes = (string?)null,
            validFrom = (DateTime?)null,
            validTo = (DateTime?)null
        });
        versionResponse.EnsureSuccessStatusCode();
        var version = await versionResponse.Content.ReadFromJsonAsync<VersionDto>();

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId = version!.Id,
            code = $"OP-{Guid.NewGuid():N}"[..10],
            name = "AuthZ233 owner op",
            description = (string?)null,
            operationType = (string?)null,
            sortIndex = (int?)null,
            setupTimeMinutes = (decimal?)null,
            runTimeMode = 1,
            runTimePerUnitSeconds = (decimal?)30,
            runTimePerBatchMinutes = (decimal?)null,
            teardownTimeMinutes = (decimal?)null,
            queueTimeMinutes = (decimal?)null,
            isOptional = false,
            allowParallelExecution = false,
            expectedQuantity = (decimal?)null
        });
        operationResponse.EnsureSuccessStatusCode();
        var operation = await operationResponse.Content.ReadFromJsonAsync<OperationDto>();

        return operation!.Id;
    }

    private sealed record ProductionOrderDto(Guid Id);

    private sealed record RecipeDto(Guid Id);

    private sealed record VersionDto(Guid Id);

    private sealed record OperationDto(Guid Id);

    private sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes);
}
