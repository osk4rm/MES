namespace AsistOff.MES.Attachments.Application.Features.Common;

/// <summary>
/// Checks that a polymorphic attachment owner exists and is visible to the
/// caller tenant. Unknown owner types, nonexistent ids and cross-tenant owners
/// all report <c>false</c> so callers return 404 without leaking existence.
/// </summary>
public interface IAttachmentOwnerVerifier
{
    Task<bool> ExistsAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken = default);
}
