using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Users.Core.Entities;

/// <summary>
/// Server-side opaque refresh token. Only the SHA-256 hash is persisted;
/// the opaque value is returned once to the caller. Rotation is single-use:
/// refreshing marks the old row revoked (with <c>ReplacedByHash</c>) and
/// inserts a successor sharing the same <c>FamilyId</c>. Reuse of a replaced
/// token revokes the whole family (see refresh handler).
/// Tenant isolation relies on the global EF query filter (<see cref="ISaasy"/>);
/// handlers never accept a caller-supplied tenant id.
/// </summary>
public class RefreshToken : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByHash { get; set; }
    public string? RevocationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;
    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;
    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);
}
