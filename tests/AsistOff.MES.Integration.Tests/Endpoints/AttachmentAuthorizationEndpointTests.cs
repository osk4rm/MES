using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for issue #315 (attachment object-level authorization).
/// Proves that list/download require <c>attachments.read</c>, delete requires
/// <c>attachments.write</c>, and that callers additionally need the
/// owner-module scope permission (production vs configuration) before any
/// bytes stream or any row is removed — while in-scope authorized callers and
/// unauthenticated (401) behavior stay unchanged.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AttachmentAuthorizationEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/attachments";
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RolesUrl = "/api/roles";
    private const string PermissionsUrl = "/api/permissions";

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    [Fact]
    public async Task ListAndDownload_WithoutReadPermission_Return403WithoutBytes()
    {
        // Arrange — admin uploads; scoped user holds only configuration.read
        // (no attachments.read at all).
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var adminClient = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var ownerId = await CreateOperationOwnerAsync(adminClient);
        var upload = await adminClient.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        using var scopedClient = await CreateScopedUserClientAsync(
            adminClient, tenantId, ["configuration.read"]);

        // Act
        var list = await scopedClient.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        var download = await scopedClient.GetAsync($"{BaseUrl}/{created.Id}/download");

        // Assert — 403 with no file bytes returned.
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        download.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        download.Content.Headers.ContentType!.MediaType.Should().NotBe("image/png");
        (await download.Content.ReadAsByteArrayAsync()).Should().NotEqual(PngBytes);
    }

    [Fact]
    public async Task Download_OutOfScopeAttachment_ReturnsDenialWithoutBytes()
    {
        // Arrange — attachment linked to a production owner; caller holds
        // attachments.read + configuration.read but not production.read.
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var adminClient = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var operationId = await CreateOperationOwnerAsync(adminClient);
        var machineId = await CreateMachineOwnerAsync(adminClient);

        var productionUpload = await adminClient.PostAsync(BaseUrl,
            UploadContent("operation", operationId, "photo.png", "image/png", PngBytes));
        productionUpload.EnsureSuccessStatusCode();
        var productionAttachment = await ReadAsync<AttachmentDto>(productionUpload);

        var configUpload = await adminClient.PostAsync(BaseUrl,
            UploadContent("machine", machineId, "manual.png", "image/png", PngBytes));
        configUpload.EnsureSuccessStatusCode();
        var configAttachment = await ReadAsync<AttachmentDto>(configUpload);

        using var scopedClient = await CreateScopedUserClientAsync(
            adminClient, tenantId, ["attachments.read", "configuration.read"]);

        // Act
        var denied = await scopedClient.GetAsync($"{BaseUrl}/{productionAttachment.Id}/download");
        var deniedList = await scopedClient.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={operationId}");
        var allowed = await scopedClient.GetAsync($"{BaseUrl}/{configAttachment.Id}/download");

        // Assert — out-of-scope is denied without streaming bytes; the
        // in-scope attachment from the same caller still downloads.
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        denied.Content.Headers.ContentType!.MediaType.Should().NotBe("image/png");
        (await denied.Content.ReadAsByteArrayAsync()).Should().NotEqual(PngBytes);
        deniedList.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await allowed.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Delete_WithoutWritePermission_Return403AndRecordRemains()
    {
        // Arrange — caller holds attachments.read + production.read but not
        // attachments.write.
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var adminClient = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var ownerId = await CreateOperationOwnerAsync(adminClient);
        var upload = await adminClient.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        using var scopedClient = await CreateScopedUserClientAsync(
            adminClient, tenantId, ["attachments.read", "production.read"]);

        // Act
        var delete = await scopedClient.DeleteAsync($"{BaseUrl}/{created.Id}");

        // Assert — denied and the attachment still exists afterwards.
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var download = await adminClient.GetAsync($"{BaseUrl}/{created.Id}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Delete_OutOfScopeAttachment_Return403AndRecordRemains()
    {
        // Arrange — caller holds attachments.write + configuration.read but
        // not production.read.
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var adminClient = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var ownerId = await CreateOperationOwnerAsync(adminClient);
        var upload = await adminClient.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        using var scopedClient = await CreateScopedUserClientAsync(
            adminClient, tenantId, ["attachments.read", "attachments.write", "configuration.read"]);

        // Act
        var delete = await scopedClient.DeleteAsync($"{BaseUrl}/{created.Id}");

        // Assert — scope enforcement leaves the record untouched.
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var download = await adminClient.GetAsync($"{BaseUrl}/{created.Id}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthorizedInScopeUser_CanListDownloadAndDelete()
    {
        // Arrange — caller holds attachments.read + attachments.write +
        // production.read.
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);
        using var adminClient = await Fixture.CreateAuthenticatedClientAsync(adminEmail, adminPassword);
        var ownerId = await CreateOperationOwnerAsync(adminClient);
        var upload = await adminClient.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        using var scopedClient = await CreateScopedUserClientAsync(
            adminClient, tenantId, ["attachments.read", "attachments.write", "production.read"]);

        // Act & Assert — success statuses as before.
        var list = await scopedClient.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<AttachmentDto>>(list)).Should().ContainSingle(x => x.Id == created.Id);

        var download = await scopedClient.GetAsync($"{BaseUrl}/{created.Id}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);

        var delete = await scopedClient.DeleteAsync($"{BaseUrl}/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await scopedClient.GetAsync($"{BaseUrl}/{created.Id}/download");
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var list = await client.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={Guid.NewGuid()}");
        var download = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/download");
        var delete = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        // Assert — unauthenticated requests still return 401 regardless of id.
        list.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        download.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> CreateScopedUserClientAsync(
        HttpClient adminClient, Guid tenantId, string[] permissionCodes)
    {
        var (email, password, userId) = await CreateUserAsync(tenantId);

        var roleCode = $"att315-{Guid.NewGuid():N}"[..16].ToLowerInvariant().Replace("_", "-");
        var createRole = await adminClient.PostAsJsonAsync(RolesUrl, new
        {
            code = roleCode,
            name = $"Attachment scope {roleCode}",
            description = (string?)null
        });
        createRole.EnsureSuccessStatusCode();
        var role = await ReadAsync<RoleDto>(createRole);

        var permissions = await ReadAsync<List<PermissionDto>>(await adminClient.GetAsync(PermissionsUrl));
        var permissionIds = permissions
            .Where(p => permissionCodes.Contains(p.Code, StringComparer.Ordinal))
            .Select(p => p.Id)
            .ToList();
        permissionIds.Should().HaveCount(
            permissionCodes.Length,
            "every permission code used by the test must exist in the tenant seed");

        var set = await adminClient.PutAsJsonAsync($"{RolesUrl}/{role.Id}/permissions", new
        {
            roleId = role.Id,
            permissionIds
        });
        set.EnsureSuccessStatusCode();

        var assign = await adminClient.PostAsJsonAsync($"{RolesUrl}/{role.Id}/members", new
        {
            roleId = role.Id,
            userId
        });
        assign.EnsureSuccessStatusCode();

        return await ClientForAsync(email, password);
    }

    private async Task<(string Email, string Password, Guid UserId)> CreateUserAsync(Guid tenantId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"att315-{suffix}@integration.local";
        const string password = "Passw0rd!";

        Guid userId;
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
            userId = user.Id;
        }

        return (email, password, userId);
    }

    private async Task<string> SignInAsync(string email, string password)
    {
        using var client = Fixture.CreateClient();
        await AuthCookieHelper.AttachCsrfAsync(client);
        var response = await client.PostAsJsonAsync(SignInUrl, new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessToken = AuthCookieHelper.GetAccessToken(response);
        accessToken.Should().NotBeNullOrWhiteSpace();
        return accessToken!;
    }

    private async Task<HttpClient> ClientForAsync(string email, string password)
    {
        var accessToken = await SignInAsync(email, password);
        var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<Guid> GetTenantIdByEmailAsync(string email)
    {
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MultitenancyDbContext>();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.ContactEmail == email);
        return tenant.Id;
    }

    private static MultipartFormDataContent UploadContent(
        string ownerType, Guid ownerId, string fileName, string contentType, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(ownerType), "ownerType");
        form.Add(new StringContent(ownerId.ToString()), "ownerId");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<Guid> CreateOperationOwnerAsync(HttpClient client)
    {
        var recipeCode = $"A315-{Guid.NewGuid():N}"[..12];
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            code = recipeCode,
            name = $"Attachment owner {recipeCode}",
            description = (string?)null,
            isActive = true,
            primaryProductId = (Guid?)null,
            syncId = (string?)null
        });
        recipeResponse.EnsureSuccessStatusCode();
        var recipe = await ReadAsync<RecipeDto>(recipeResponse);

        var versionResponse = await client.PostAsJsonAsync("/api/recipe-versions", new
        {
            recipeId = recipe.Id,
            changeNotes = (string?)null,
            validFrom = (DateTime?)null,
            validTo = (DateTime?)null
        });
        versionResponse.EnsureSuccessStatusCode();
        var version = await ReadAsync<VersionDto>(versionResponse);

        var operationResponse = await client.PostAsJsonAsync("/api/operations", new
        {
            versionId = version.Id,
            code = $"OP-{Guid.NewGuid():N}"[..10],
            name = "Attachment scope op",
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
        var operation = await ReadAsync<OperationDto>(operationResponse);

        return operation.Id;
    }

    private static async Task<Guid> CreateMachineOwnerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/machines", new
        {
            code = $"M315-{Guid.NewGuid():N}"[..12],
            name = "Attachment scope Work Center",
            description = (string?)null,
            departmentId = (Guid?)null,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        var machine = await ReadAsync<MachineDto>(response);
        return machine.Id;
    }

    private sealed record RecipeDto(Guid Id);

    private sealed record VersionDto(Guid Id);

    private sealed record OperationDto(Guid Id);

    private sealed record MachineDto(Guid Id);

    private sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes);

    private sealed record RoleDto(Guid Id, string Code);

    private sealed record PermissionDto(Guid Id, string Code);
}
