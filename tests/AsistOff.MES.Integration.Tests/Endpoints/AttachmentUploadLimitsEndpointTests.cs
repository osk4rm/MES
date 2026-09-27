using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Attachments.Application.Features.Upload;
using AsistOff.MES.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for issue #348 (attachment upload hardening): the edge size
/// limit rejects oversized payloads before persistence, the per-tenant quota
/// rejects uploads that would exceed it (and delete frees quota), and the
/// malware-scan hook rejects infected uploads with nothing stored.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AttachmentUploadLimitsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/attachments";

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    [Fact]
    public async Task Upload_ElevenMegabytes_Returns400Or413AndPersistsNothing()
    {
        // Arrange - 11 MB exceeds the default 10 MiB app cap; the 11 MiB edge
        // limit plus the handler cap must reject it before anything is stored.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);
        var big = new byte[11_000_000];
        Array.Fill<byte>(big, 0x41);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "big.txt", "text/plain", big));

        // Assert - 400 (app cap) or 413 (edge limit), and nothing persisted
        upload.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);

        var list = await client.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<AttachmentDto>>(list)).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_WhenTenantAtQuota_IsRejectedAndDeleteFreesQuotaForRetry()
    {
        // Arrange - isolated host with a tiny 32-byte quota against the shared DB
        await using var factory = new MesWebApplicationFactory(
            Fixture.PostgresConnectionString,
            configureTestServices: services => services.Configure<AttachmentUploadOptions>(
                options => options.MaxTotalBytesPerTenant = 32));
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await SignInAsync(factory, email, password);
        var ownerId = await CreateOperationOwnerAsync(client);

        // Fill the quota: two 12-byte uploads fit (24/32)...
        var first = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "a.png", "image/png", PngBytes));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstId = (await ReadAsync<AttachmentDto>(first)).Id;

        var second = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "b.png", "image/png", PngBytes));
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - a third 12-byte file (36/32) must be rejected naming quota + usage
        var rejected = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "c.png", "image/png", PngBytes));

        // Assert
        rejected.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
        var body = await rejected.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("quota");
        body.Should().Contain("32");

        // Act - deleting an attachment frees quota so the retry succeeds
        var delete = await client.DeleteAsync($"{BaseUrl}/{firstId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var retry = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "c.png", "image/png", PngBytes));

        // Assert
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Upload_WhenScannerReportsInfected_Returns400AndPersistsNothing()
    {
        // Arrange - isolated host whose scanner stub flags every file
        await using var factory = new MesWebApplicationFactory(
            Fixture.PostgresConnectionString,
            configureTestServices: services =>
            {
                services.RemoveAll<IAttachmentMalwareScanner>();
                services.AddSingleton<IAttachmentMalwareScanner>(new AlwaysInfectedScanner());
            });
        var (email, password) = await Fixture.CreateTenantAsync();
        using var client = await SignInAsync(factory, email, password);
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));

        // Assert - rejected with nothing stored
        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await upload.Content.ReadAsStringAsync()).Should().ContainEquivalentOf("malware");

        var list = await client.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<AttachmentDto>>(list)).Should().BeEmpty();
    }

    private static async Task<HttpClient> SignInAsync(MesWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        await AuthCookieHelper.AttachCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { email, password });
        response.EnsureSuccessStatusCode();

        var accessToken = AuthCookieHelper.GetAccessToken(response);
        accessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
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

    private sealed class AlwaysInfectedScanner : IAttachmentMalwareScanner
    {
        public Task<AttachmentScanVerdict> ScanAsync(
            ReadOnlyMemory<byte> content, string fileName, string contentType, CancellationToken cancellationToken)
            => Task.FromResult(AttachmentScanVerdict.Infected);
    }

    private sealed record RecipeDto(Guid Id);
    private sealed record VersionDto(Guid Id);
    private sealed record OperationDto(Guid Id);
    private sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes);
}
