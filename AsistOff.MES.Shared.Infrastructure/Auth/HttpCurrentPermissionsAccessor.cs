using AsistOff.MES.Shared.Abstractions.Auth;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// Reads the multi-valued <c>permissions</c> claim materialised at sign-in
/// time from the caller's role assignments. Returns an empty set outside an
/// authenticated HTTP request so permission checks fail closed.
/// </summary>
internal sealed class HttpCurrentPermissionsAccessor(IHttpContextAccessor httpContextAccessor)
    : ICurrentPermissionsAccessor
{
    public IReadOnlyCollection<string> Permissions =>
        httpContextAccessor.HttpContext?.User.FindAll("permissions").Select(c => c.Value).ToList()
        ?? (IReadOnlyCollection<string>)Array.Empty<string>();
}
