using System.Net.Http.Headers;
using AsistOff.MES.IntegrationTests.Infrastructure;

namespace AsistOff.MES.IntegrationTests.Attachments;

/// <summary>
/// Covers <c>/api/attachments</c> List / Upload / Download / Delete. Owner is
/// polymorphic — for the test we use a synthetic owner type and a recipe ID.
/// </summary>
public sealed class AttachmentsEndpointsTests : IntegrationTestBase
{
    public AttachmentsEndpointsTests(MesApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Upload_list_download_delete_lifecycle()
    {
        const string ownerType = "test-owner";
        var ownerId = Guid.NewGuid();
        var fileBytes = "Hello, integration tests!\n"u8.ToArray();

        var uploaded = await UploadAsync(ownerType, ownerId, "hello.txt", "text/plain", fileBytes, "greeting");
        uploaded.OwnerType.Should().Be(ownerType);
        uploaded.OwnerId.Should().Be(ownerId);
        uploaded.FileName.Should().Be("hello.txt");
        uploaded.SizeBytes.Should().Be(fileBytes.Length);

        // List by owner
        var list = await Client.GetJsonAsync<IReadOnlyList<AttachmentPayload>>(
            $"/api/attachments?ownerType={ownerType}&ownerId={ownerId}");
        list.Should().ContainSingle(a => a.Id == uploaded.Id);

        // Download
        var downloadResponse = await Client.GetAsync($"/api/attachments/{uploaded.Id}/download");
        downloadResponse.EnsureSuccessStatusCode();
        downloadResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        var downloaded = await downloadResponse.Content.ReadAsByteArrayAsync();
        downloaded.Should().BeEquivalentTo(fileBytes);

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/attachments/{uploaded.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // List should be empty
        var afterDelete = await Client.GetJsonAsync<IReadOnlyList<AttachmentPayload>>(
            $"/api/attachments?ownerType={ownerType}&ownerId={ownerId}");
        afterDelete.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_returns_bad_request_when_file_is_empty()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("test-owner"), "ownerType" },
            { new StringContent(Guid.NewGuid().ToString()), "ownerId" },
            { CreateFileContent(Array.Empty<byte>(), "empty.txt", "text/plain"), "file", "empty.txt" },
        };

        var response = await Client.PostAsync("/api/attachments", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Download_returns_not_found_for_unknown_id()
    {
        var response = await Client.GetAsync($"/api/attachments/{Guid.NewGuid()}/download");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<AttachmentPayload> UploadAsync(
        string ownerType, Guid ownerId, string fileName, string contentType, byte[] content, string? description)
    {
        using var multipart = new MultipartFormDataContent
        {
            { new StringContent(ownerType), "ownerType" },
            { new StringContent(ownerId.ToString()), "ownerId" },
            { CreateFileContent(content, fileName, contentType), "file", fileName },
        };

        if (description is not null)
        {
            multipart.Add(new StringContent(description), "description");
        }

        var response = await Client.PostAsync("/api/attachments", multipart);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AttachmentPayload>(Json);
        return payload!;
    }

    private static ByteArrayContent CreateFileContent(byte[] bytes, string fileName, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"file\"",
            FileName = $"\"{fileName}\"",
        };
        return content;
    }

    private sealed record AttachmentPayload(
        Guid Id, string OwnerType, Guid OwnerId, string FileName, string ContentType,
        long SizeBytes, string? Description, DateTime CreatedAt, Guid? UploadedByUserId);
}
