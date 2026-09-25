using System.Reflection;
using AsistOff.MES.Attachments.Application.Features.List;
using AsistOff.MES.Configuration.Application.Features.Products.Create;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Requests.Commands.Create;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Create;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Application.Features.Authentication.SignIn;
using AsistOff.MES.Users.Core.Rbac;
using FluentAssertions;
using MediatR;

namespace AsistOff.MES.Shared.Tests.Auth;

/// <summary>
/// Slice-1 + slice-2 authorization coverage guard (issues #231, #233). Every MediatR
/// request in the Users, Multitenancy, Configuration, Production and Attachments
/// modules must carry either a <see cref="RequirePermissionAttribute"/> or an entry
/// on the documented <see cref="AuthorizationAllowlist"/> — otherwise the
/// default-deny <c>AuthorizationBehavior</c> rejects it with 403. This test fails
/// when a new request is added without coverage. Gateway owns no MediatR requests
/// (only the errors controller), so it needs no coverage.
/// </summary>
public class AuthorizationCoverageTests
{
    private static readonly Assembly[] CoveredAssemblies =
    [
        typeof(SignInRequest).Assembly, // AsistOff.MES.Users.Application
        typeof(CreateTenantCommand).Assembly, // AsistOff.MES.Multitenancy
        typeof(CreateProductRequest).Assembly, // AsistOff.MES.Configuration.Application
        typeof(CreateProductionOrderRequest).Assembly, // AsistOff.MES.Production.Application
        typeof(ListAttachmentsRequest).Assembly, // AsistOff.MES.Attachments.Application
    ];

    private static IEnumerable<Type> CoveredRequests() =>
        CoveredAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IBaseRequest).IsAssignableFrom(t))
            .OrderBy(t => t.FullName);

    [Fact]
    public void EveryCoveredRequest_HasPermissionOrAllowlistEntry()
    {
        // Arrange & Act
        var uncovered = CoveredRequests()
            .Where(t => !HasRequirePermission(t) && !AuthorizationAllowlist.IsAllowed(t))
            .Select(t => t.FullName)
            .ToList();

        // Assert — a new write added without coverage must fail here, not in production.
        uncovered.Should().BeEmpty(
            "each request needs RequirePermission or an AuthorizationAllowlist entry (default-deny, issues #231/#233)");
    }

    [Fact]
    public void EveryRequirePermissionCode_ComesFromRbacDefaults()
    {
        // Arrange & Act — no permission string literals outside RbacDefaults plus the seed.
        var unknown = CoveredRequests()
            .SelectMany(t => t.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
                .Cast<RequirePermissionAttribute>()
                .Select(a => (Request: t.FullName, Permission: a.Permission)))
            .Where(x => !RbacDefaults.AllPermissionCodes.Contains(x.Permission, StringComparer.Ordinal))
            .ToList();

        // Assert
        unknown.Should().BeEmpty("permission codes must be RbacDefaults constants");
    }

    [Fact]
    public void OnlySignInAndTenantProvisioning_AreAnonymousWrites()
    {
        // Arrange & Act — the only IAllowAnonymousRequest types in the five modules
        // are the sign-in bootstrap, the refresh-token rotation bootstrap (#236),
        // the tenant provisioning bootstrap and the single-tenant lookup; all
        // writes are pre-authentication by definition (refresh presents only the
        // opaque token because the access token already expired; tenant binding
        // comes from the stored refresh-token row, never from caller input).
        // Slice 2/2 (#233) introduces no new anonymous requests.
        var anonymous = CoveredRequests()
            .Where(t => typeof(IAllowAnonymousRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToList();

        // Assert
        anonymous.Should().BeEquivalentTo(
            "AsistOff.MES.Users.Application.Features.Authentication.SignIn.SignInRequest",
            "AsistOff.MES.Users.Application.Features.Authentication.Refresh.RefreshTokenRequest",
            "AsistOff.MES.Multitenancy.Requests.Commands.Create.CreateTenantCommand",
            "AsistOff.MES.Multitenancy.Requests.Queries.GetTenantQuery");
    }

    [Fact]
    public void EveryCoveredWrite_IsTenantScoped()
    {
        // Arrange & Act — all covered writes are ITenantRequest; only the
        // pre-authentication bootstraps (sign-in, refresh rotation, tenant
        // provisioning) opt out via IAllowAnonymousRequest + allowlist.
        var nonTenantWrites = CoveredRequests()
            .Where(HasRequirePermission)
            .Where(t => !IsTenantScoped(t))
            .Select(t => t.FullName)
            .ToList();

        // Assert
        nonTenantWrites.Should().BeEmpty("covered writes must be ITenantRequest");
    }

    [Fact]
    public void ProductionWrites_RequireProductionWrite()
    {
        // Arrange & Act — every Production write carries production.write (dot
        // naming convention, RbacDefaults constant, no literals).
        var violations = typeof(CreateProductionOrderRequest).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !AuthorizationAllowlist.IsAllowed(t))
            .Select(t => (Type: t,
                Permissions: t.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
                    .Cast<RequirePermissionAttribute>()
                    .Select(a => a.Permission)
                    .ToList()))
            .Where(x => !x.Permissions.Contains(RbacDefaults.ProductionWrite, StringComparer.Ordinal))
            .Select(x => x.Type.FullName)
            .ToList();

        // Assert
        violations.Should().BeEmpty("every Production write must require production.write");
    }

    [Fact]
    public void AttachmentWrites_RequireAttachmentsWrite()
    {
        // Arrange & Act — upload and delete carry attachments.write.
        var violations = typeof(ListAttachmentsRequest).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !AuthorizationAllowlist.IsAllowed(t))
            .Select(t => (Type: t,
                Permissions: t.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
                    .Cast<RequirePermissionAttribute>()
                    .Select(a => a.Permission)
                    .ToList()))
            .Where(x => !x.Permissions.Contains(RbacDefaults.AttachmentsWrite, StringComparer.Ordinal))
            .Select(x => x.Type.FullName)
            .ToList();

        // Assert
        violations.Should().BeEmpty("every Attachments write must require attachments.write");
    }

    private static bool HasRequirePermission(Type requestType) =>
        requestType
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Any();

    private static bool IsTenantScoped(Type requestType) =>
        typeof(ITenantRequest).IsAssignableFrom(requestType)
        || requestType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITenantRequest<>));
}
