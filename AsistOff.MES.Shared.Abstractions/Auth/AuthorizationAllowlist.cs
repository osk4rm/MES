namespace AsistOff.MES.Shared.Abstractions.Auth;

/// <summary>
/// Documented allowlist for the default-deny <c>AuthorizationBehavior</c> pipeline step
/// (issue #231, slice 1/2: Users, Multitenancy and Configuration).
///
/// Every MediatR request in those three modules must carry either a
/// <see cref="RequirePermissionAttribute"/> or an entry here. Requests with neither
/// are rejected for authenticated callers with <c>ForbiddenException</c> (HTTP 403)
/// instead of executing.
///
/// Groups:
/// <list type="bullet">
/// <item>Anonymous bootstrap (pre-authentication, no tenant data access):
/// sign-in and tenant provisioning. Justification: no token exists yet, so no
/// permission claim can be evaluated; both operate without touching tenant-scoped rows.</item>
/// <item>Anonymous read: single-tenant lookup used by the provisioning / health surface.</item>
/// <item>Session maintenance (any authenticated tenant user): refresh-token rotation and
/// sign-out are scoped to the ambient tenant and caller user id, so no extra permission applies.</item>
/// <item>Configuration reads (any authenticated tenant user): browse / get queries expose
/// no mutation and stay available to the read-only <c>user</c> role.</item>
/// </list>
///
/// Production, Attachments and Gateway writes are NOT listed here — they keep the
/// legacy pass-through until slice 2/2 (see <see cref="LegacyPassthroughAssemblyNames"/>).
/// </summary>
public static class AuthorizationAllowlist
{
    /// <summary>
    /// Assemblies whose MediatR requests keep the legacy pass-through (no
    /// <see cref="RequirePermissionAttribute"/> required) until slice 2/2 covers
    /// Production, Attachments and Gateway writes. Intentionally narrow: only the
    /// two application assemblies that own requests today. Everything else fails closed.
    /// </summary>
    public static readonly IReadOnlySet<string> LegacyPassthroughAssemblyNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "AsistOff.MES.Production.Application",
            "AsistOff.MES.Attachments.Application",
        };

    /// <summary>
    /// Full type names (<c>Namespace.Type</c>) allowed without a
    /// <see cref="RequirePermissionAttribute"/>. Sorted by module for reviewability.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedRequestFullNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // Users — anonymous bootstrap: pre-auth sign-in carries no token, so no
            // permission claim exists to evaluate. No tenant data is read without credentials.
            "AsistOff.MES.Users.Application.Features.Authentication.SignIn.SignInRequest",

            // Users — session maintenance: refresh rotation and sign-out resolve tokens
            // through the ambient tenant and caller user id only. Any authenticated
            // tenant user may rotate or revoke their own session.
            "AsistOff.MES.Users.Application.Features.Authentication.Refresh.RefreshTokenRequest",
            "AsistOff.MES.Users.Application.Features.Authentication.SignOut.SignOutRequest",

            // Multitenancy — anonymous bootstrap: tenant provisioning creates the tenant
            // and its admin user. Pre-authentication by definition; no tenant data accessed.
            "AsistOff.MES.Multitenancy.Requests.Commands.Create.CreateTenantCommand",

            // Multitenancy — anonymous read: single-tenant lookup on the public
            // provisioning surface. Returns only the requested tenant's public record.
            "AsistOff.MES.Multitenancy.Requests.Queries.GetTenantQuery",

            // Configuration reads — any authenticated tenant user (including the
            // read-only user role). Browse / get queries perform no mutation.
            "AsistOff.MES.Configuration.Application.Features.Departments.Browse.BrowseDepartmentsRequest",
            "AsistOff.MES.Configuration.Application.Features.Departments.Get.GetDepartmentRequest",
            "AsistOff.MES.Configuration.Application.Features.Machines.Browse.BrowseMachinesRequest",
            "AsistOff.MES.Configuration.Application.Features.Machines.Get.GetMachineRequest",
            "AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse.BrowseMaintenanceWorkOrdersRequest",
            "AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Get.GetMaintenanceWorkOrderRequest",
            "AsistOff.MES.Configuration.Application.Features.MeasureUnits.Browse.BrowseMeasureUnitsRequest",
            "AsistOff.MES.Configuration.Application.Features.MeasureUnits.Get.GetMeasureUnitRequest",
            "AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Browse.BrowseOperatorShiftAssignmentsRequest",
            "AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Get.GetOperatorShiftAssignmentRequest",
            "AsistOff.MES.Configuration.Application.Features.Operators.Browse.BrowseOperatorsRequest",
            "AsistOff.MES.Configuration.Application.Features.Operators.Get.GetOperatorRequest",
            "AsistOff.MES.Configuration.Application.Features.ProductGroups.Browse.BrowseProductGroupsRequest",
            "AsistOff.MES.Configuration.Application.Features.ProductGroups.Get.GetProductGroupRequest",
            "AsistOff.MES.Configuration.Application.Features.Products.Browse.BrowseProductsRequest",
            "AsistOff.MES.Configuration.Application.Features.Products.ByScan.GetProductByScanRequest",
            "AsistOff.MES.Configuration.Application.Features.Products.Get.GetProductRequest",
            "AsistOff.MES.Configuration.Application.Features.ReasonCodes.Browse.BrowseReasonCodesRequest",
            "AsistOff.MES.Configuration.Application.Features.ReasonCodes.Get.GetReasonCodeRequest",
            "AsistOff.MES.Configuration.Application.Features.Shifts.Browse.BrowseShiftsRequest",
            "AsistOff.MES.Configuration.Application.Features.Shifts.Get.GetShiftRequest",
            "AsistOff.MES.Configuration.Application.Features.Skills.Browse.BrowseSkillsRequest",
            "AsistOff.MES.Configuration.Application.Features.Skills.Get.GetSkillRequest",
            "AsistOff.MES.Configuration.Application.Features.StockMovements.Browse.BrowseStockMovementsRequest",
            "AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand.GetStockOnHandRequest",
            "AsistOff.MES.Configuration.Application.Features.Warehouses.Browse.BrowseWarehousesRequest",
            "AsistOff.MES.Configuration.Application.Features.Warehouses.Get.GetWarehouseRequest",
            "AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Get.GetWorkCenterCalendarRequest",
        };

    public static bool IsAllowed(Type? requestType) =>
        requestType?.FullName is not null && IsAllowed(requestType.FullName);

    public static bool IsAllowed(string? requestFullName) =>
        requestFullName is not null && AllowedRequestFullNames.Contains(requestFullName);

    public static bool IsLegacyPassthrough(Type? requestType) =>
        requestType?.Assembly.GetName().Name is string assemblyName &&
        LegacyPassthroughAssemblyNames.Contains(assemblyName);
}
