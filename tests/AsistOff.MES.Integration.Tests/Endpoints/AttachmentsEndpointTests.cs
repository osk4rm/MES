using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint tests for hardened attachment uploads (issues #228, #234): allowlist +
/// magic-byte verification + size limit enforced before persistence, safe
/// download headers, and owner/tenant existence checks returning 404.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AttachmentsEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/attachments";

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    private static readonly byte[] PdfBytes = "%PDF-1.7 payload"u8.ToArray();

    [Fact]
    public async Task Upload_HappyPath_PersistsAndIsListed()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadAsync<AttachmentDto>(upload);
        created.FileName.Should().Be("photo.png");
        created.ContentType.Should().Be("image/png");

        var list = await client.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await ReadAsync<List<AttachmentDto>>(list);
        items.Should().ContainSingle(x => x.Id == created.Id);
    }

    [Fact]
    public async Task Upload_UppercaseExtensionAndMime_Accepted()
    {
        // Arrange - allowlist matching stays case-insensitive after options normalization
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "PHOTO.PNG", "IMAGE/PNG", PngBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadAsync<AttachmentDto>(upload);
        created.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task Upload_DisallowedType_Returns400AndPersistsNothing()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "evil.html", "text/html", "<html></html>"u8.ToArray()));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var list = await client.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<List<AttachmentDto>>(list)).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_MismatchedContent_Returns400()
    {
        // Arrange - PDF bytes declared as PNG
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "fake.png", "image/png", PdfBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Oversized_Returns400()
    {
        // Arrange - 11 MB exceeds the default 10 MiB maximum
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);
        var big = new byte[11_000_000];
        Array.Fill<byte>(big, 0x41);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "big.txt", "text/plain", big));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_UnknownOwner_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", Guid.NewGuid(), "photo.png", "image/png", PngBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_UnknownOwnerType_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("starship", ownerId, "photo.png", "image/png", PngBytes));

        // Assert
        upload.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_UnknownOwner_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var list = await client.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={Guid.NewGuid()}");

        // Assert
        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_ReturnsAttachmentDispositionAndNosniff()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        // Act
        var download = await client.GetAsync($"{BaseUrl}/{created.Id}/download");

        // Assert
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        download.Content.Headers.ContentDisposition.Should().NotBeNull();
        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        download.Content.Headers.ContentDisposition.FileName.Should().Contain("photo.png");
        download.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Download_UnknownId_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var download = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/download");

        // Assert
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_OtherTenantAttachment_Returns404()
    {
        // Arrange - attachment created under the seeded dev tenant
        using var devClient = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(devClient);
        var upload = await devClient.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        var (email, password) = await Fixture.CreateTenantAsync();
        using var otherClient = await Fixture.CreateAuthenticatedClientAsync(email, password);

        // Act
        var download = await otherClient.GetAsync($"{BaseUrl}/{created.Id}/download");
        var list = await otherClient.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={ownerId}");
        var delete = await otherClient.DeleteAsync($"{BaseUrl}/{created.Id}");

        // Assert - never 403 or data: tenant isolation surfaces as 404
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // And the owning tenant still sees its attachment untouched
        var ownDownload = await devClient.GetAsync($"{BaseUrl}/{created.Id}/download");
        ownDownload.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_HappyPath_RemovesAttachment()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "photo.png", "image/png", PngBytes));
        upload.EnsureSuccessStatusCode();
        var created = await ReadAsync<AttachmentDto>(upload);

        // Act
        var delete = await client.DeleteAsync($"{BaseUrl}/{created.Id}");

        // Assert
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var download = await client.GetAsync($"{BaseUrl}/{created.Id}/download");
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var list = await client.GetAsync($"{BaseUrl}?ownerType=operation&ownerId={Guid.NewGuid()}");
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", Guid.NewGuid(), "photo.png", "image/png", PngBytes));
        var download = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/download");
        var delete = await client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        // Assert
        list.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        upload.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        download.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private sealed record RecipeDto(Guid Id);
    private sealed record VersionDto(Guid Id);
    private sealed record OperationDto(Guid Id);
    private sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes);
}
