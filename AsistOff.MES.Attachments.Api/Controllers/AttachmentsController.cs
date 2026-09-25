using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Attachments.Application.Features.Delete;
using AsistOff.MES.Attachments.Application.Features.Download;
using AsistOff.MES.Attachments.Application.Features.List;
using AsistOff.MES.Attachments.Application.Features.Responses;
using AsistOff.MES.Attachments.Application.Features.Upload;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Attachments.Api.Controllers;

[Route("api/attachments")]
public class AttachmentsController(ISender sender, IOptions<AttachmentUploadOptions> uploadOptions) : ApiController
{
    /// <summary>Lists attachments for the given polymorphic owner.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AttachmentResponse>>> ListAsync(
        [FromQuery] string ownerType,
        [FromQuery] Guid ownerId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListAttachmentsRequest(ownerType, ownerId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Uploads a file as an attachment for the given owner.</summary>
    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<AttachmentResponse>> UploadAsync(
        [FromForm] string ownerType,
        [FromForm] Guid ownerId,
        [FromForm] IFormFile file,
        [FromForm] string? description,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        await using var stream = file.OpenReadStream();
        var request = new UploadAttachmentRequest(
            ownerType,
            ownerId,
            file.FileName,
            file.ContentType,
            file.Length,
            stream,
            description);

        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Streams the attachment's binary content as a forced download.</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<ActionResult> DownloadAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DownloadAttachmentRequest(id), cancellationToken);

        // Never echo an attacker-controlled content type: serve allowlisted
        // types as-is, everything else as application/octet-stream.
        var safeContentType = AttachmentUploadGuard.MapDownloadContentType(result.ContentType, uploadOptions.Value);
        var safeFileName = AttachmentFileNameSanitizer.Sanitize(result.FileName);

        // Force download disposition (File with a download name sets
        // "Content-Disposition: attachment") and block MIME sniffing so an
        // uploaded HTML/SVG payload cannot execute in another user's browser.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Content, safeContentType, safeFileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteAttachmentRequest(id), cancellationToken);
        return NoContent();
    }
}
