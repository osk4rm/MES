using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
/// Endpoint tests for issue #354: an authenticated attachment upload stores the
/// calling user id in <c>UploadedByUserId</c> and returns it in the response and
/// list payloads. A second user in the same tenant is attributed independently,
/// and other tenants never see uploader ids (isolation surfaces as 404).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AttachmentUploadAttributionEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/attachments";
    private const string SignInUrl = "/api/auth/sign-in";

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    [Fact]
    public async Task Upload_AsTwoUsersInSameTenant_AttributesEachUploader()
    {
        // Arrange - isolated tenant so the two users are unambiguous.
        var (adminEmail, adminPassword) = await Fixture.CreateTenantAsync();
        var adminToken = await SignInAsync(adminEmail, adminPassword);
        var firstUserId = DecodeSub(adminToken);
        using var first = ClientWithToken(adminToken);
        var tenantId = await GetTenantIdByEmailAsync(adminEmail);

        var ownerId = await CreateOperationOwnerAsync(first);

        var (secondEmail, secondPassword, secondUserId) = await CreateUserAsync(tenantId, isAdmin: true);
        var secondToken = await SignInAsync(secondEmail, secondPassword);
        DecodeSub(secondToken).Should().Be(secondUserId);
        using var second = ClientWithToken(secondToken);

        // Act - each user uploads to the same owner.
        var firstUpload = await first.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "first.png", "image/png", PngBytes));
        firstUpload.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstCreated = await ReadAsync<AttachmentDto>(firstUpload);

        var secondUpload = await second.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "second.png", "image/png", PngBytes));
        secondUpload.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondCreated = await ReadAsync<AttachmentDto>(secondUpload);

        // Assert - upload responses carry the calling user.
        firstCreated.UploadedByUserId.Should().Be(firstUserId);
        secondCreated.UploadedByUserId.Should().Be(secondUserId);

        // Assert - list echoes the stored uploader per row.
        var list = await first.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAsync<List<AttachmentDto>>(list);
        items.Should().ContainSingle(x => x.Id == firstCreated.Id && x.UploadedByUserId == firstUserId);
        items.Should().ContainSingle(x => x.Id == secondCreated.Id && x.UploadedByUserId == secondUserId);
    }

    [Fact]
    public async Task Upload_OtherTenant_DoesNotSeeUploaderIds()
    {
        // Arrange - attachment uploaded under tenant A carries user A's id.
        var (emailA, passwordA) = await Fixture.CreateTenantAsync();
        var tokenA = await SignInAsync(emailA, passwordA);
        var userAId = DecodeSub(tokenA);
        using var clientA = ClientWithToken(tokenA);
        var ownerId = await CreateOperationOwnerAsync(clientA);

        var upload = await clientA.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadAsync<AttachmentDto>(upload);
        created.UploadedByUserId.Should().Be(userAId);

        var (emailB, passwordB) = await Fixture.CreateTenantAsync();
        using var clientB = await Fixture.CreateAuthenticatedClientAsync(emailB, passwordB);

        // Act - tenant B probes the foreign owner and attachment id.
        var list = await clientB.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        var download = await clientB.GetAsync($"{BaseUrl}/{created.Id}/download");

        // Assert - isolation surfaces as 404 with no uploader leak.
        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // And tenant A still sees its own uploader attribution untouched.
        var ownList = await clientA.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        ownList.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownItems = await ReadAsync<List<AttachmentDto>>(ownList);
        ownItems.Should().ContainSingle(x => x.Id == created.Id && x.UploadedByUserId == userAId);
    }

    [Fact]
    public async Task Upload_WithoutToken_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", Guid.NewGuid(), "photo.png", "image/png", PngBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        var recipeCode = $"ATT-{Guid.NewGuid():N}"[..12];
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
            name = "Attachment owner op",
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

    private async Task<string> SignInAsync(string email, string password)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessToken = AuthCookieHelper.GetAccessToken(response);
        accessToken.Should().NotBeNullOrWhiteSpace();
        return accessToken!;
    }

    private HttpClient ClientWithToken(string accessToken)
    {
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

    private async Task<(string Email, string Password, Guid UserId)> CreateUserAsync(
        Guid tenantId, bool isAdmin = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"attachuser-{suffix}@integration.local";
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
                IsTenantAdmin = isAdmin
            };
            user.Password = hasher.HashPassword(user, password);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            userId = user.Id;
        }

        return (email, password, userId);
    }

    private static Guid DecodeSub(string accessToken)
    {
        var payload = accessToken.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var document = JsonDocument.Parse(json);

        var sub = document.RootElement.TryGetProperty("sub", out var subProp)
            ? subProp.GetString()
            : document.RootElement.TryGetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", out var ni)
                ? ni.GetString()
                : null;

        Guid.TryParse(sub, out var userId).Should().BeTrue("JWT must carry the caller user id in sub");
        return userId;
    }

    private sealed record RecipeDto(Guid Id);
    private sealed record VersionDto(Guid Id);
    private sealed record OperationDto(Guid Id);
    private sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, Guid? UploadedByUserId);
}
