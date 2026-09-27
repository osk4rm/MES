using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Blob durability contract for attachment storage (issue #373): an uploaded
/// file downloads back byte-identical through the real HTTP pipeline against
/// the content-root-resolved blob root, and an unknown attachment id is 404.
/// Compose-mount survival (down/up with the volume kept) cannot be proven by
/// Testcontainers and is recorded manually in the PR body instead.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AttachmentBlobStorageEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/attachments";

    private static readonly byte[] PdfBytes = "%PDF-1.7 durability-proof"u8.ToArray();

    [Fact]
    public async Task UploadDownload_RoundTrip_ReturnsByteIdenticalContent()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var ownerId = await CreateOperationOwnerAsync(client);

        // Act
        var upload = await client.PostAsync(BaseUrl,
            UploadContent("operation", ownerId, "control-chart.pdf", "application/pdf", PdfBytes));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await ReadAsync<AttachmentDto>(upload);

        var download = await client.GetAsync($"{BaseUrl}/{created.Id}/download");

        // Assert
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);
    }

    [Fact]
    public async Task Download_UnknownAttachmentId_Returns404()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var download = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/download");

        // Assert
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
