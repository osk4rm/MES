using AsistOff.MES.Attachments.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Attachments.Infrastructure.Verification;

/// <summary>
/// Resolves polymorphic owners through the <see cref="DefaultContext"/> EF Core
/// model instead of hard module references: the <c>OwnerType</c> string is
/// matched (case-insensitively, ignoring dashes/underscores) against entity
/// type names, with explicit aliases for UI-facing shorthand (e.g.
/// <c>operation</c> → <c>OperationNode</c>).
///
/// Existence is checked with <c>FindAsync</c> plus an explicit tenant guard, so
/// unknown types, missing ids and other-tenant owners all report <c>false</c>
/// and callers return 404 without leaking cross-tenant existence. No manual
/// <c>TenantId == currentTenant</c> LINQ predicates are used; the tenant check
/// below only interprets the already tenant-filtered lookup result.
/// </summary>
internal sealed class EfAttachmentOwnerVerifier(
    DefaultContext context,
    ICurrentTenantAccessor tenantAccessor) : IAttachmentOwnerVerifier
{
    /// <summary>UI-facing shorthand → normalized entity type name.</summary>
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["operation"] = "operationnode",
            ["operations"] = "operationnode",
            ["recipeversion"] = "recipeversion",
            ["recipeversions"] = "recipeversion",
            ["productionorder"] = "productionorder",
            ["productionorders"] = "productionorder",
            ["confirmation"] = "productionconfirmation",
            ["productionconfirmation"] = "productionconfirmation",
            ["workcenter"] = "machine",
            ["workcenters"] = "machine",
            ["machine"] = "machine",
        };

    public async Task<bool> ExistsAsync(string ownerType, Guid ownerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerType) || ownerId == Guid.Empty)
            return false;

        var clrType = ResolveEntityType(ownerType);
        if (clrType is null)
            return false;

        var entity = await context.FindAsync(clrType, new object[] { ownerId }, cancellationToken);
        if (entity is null)
            return false;

        // FindAsync may bypass the global query filter for tracked/seeded rows;
        // enforce tenant visibility explicitly so cross-tenant owners stay 404.
        if (entity is ISaasy saasy)
        {
            if (!tenantAccessor.TryGetTenantId(out var tenantId))
                return false;

            if (saasy.TenantId != tenantId)
                return false;
        }

        return true;
    }

    private Type? ResolveEntityType(string ownerType)
    {
        var normalized = Normalize(ownerType);
        if (Aliases.TryGetValue(normalized, out var aliased))
            normalized = aliased;

        return context.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .FirstOrDefault(t =>
                typeof(IEntity).IsAssignableFrom(t) &&
                Normalize(t.Name) == normalized);
    }

    private static string Normalize(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '-' or '_' or ' ')
                continue;

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
