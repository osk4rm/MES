using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Common;

/// <summary>
/// Decides which callers see sensitive read projections (issue #372): the
/// full audit-event <c>Payload</c> JSON and the raw OPC UA <c>LastError</c>
/// provider message. Callers holding <see cref="RbacDefaults.ProductionWrite"/>
/// — the same permission code every production write already requires — are
/// privileged and keep the full values. Callers with only
/// <see cref="RbacDefaults.ProductionRead"/> (the read-only <c>user</c> role)
/// get <c>null</c> while liveness flags, tag counts and totals stay visible
/// to everyone. The split derives from the existing permission claim, so no
/// role strings are hardcoded here.
/// </summary>
public static class SensitiveProjectionPolicy
{
    public static bool CanSeeSensitiveDetails(IReadOnlyCollection<string> permissions) =>
        permissions.Contains(RbacDefaults.ProductionWrite, StringComparer.Ordinal);
}
