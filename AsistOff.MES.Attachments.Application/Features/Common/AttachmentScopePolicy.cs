using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Attachments.Application.Features.Common;

/// <summary>
/// Object-level scope rule for attachments (issue #315).
///
/// Attachments are polymorphic (<c>OwnerType</c> + <c>OwnerId</c>). Tenant
/// isolation alone lets any authenticated tenant user read or delete another
/// department's records, so every read/delete path additionally requires the
/// caller to hold the read permission of the owner's module, derived from the
/// linked owner type through <see cref="RbacDefaults"/> (no hardcoded role
/// strings):
/// <list type="bullet">
/// <item>Production owners (operations, recipes, Production Orders,
/// confirmations, lots, Andon/downtime/scrap, Kanban, telemetry, SPC, …)
/// require <c>production.read</c>.</item>
/// <item>Configuration owners (Work Centers/machines, products, warehouses,
/// departments, operators, shifts, skills, maintenance, stock, …) require
/// <c>configuration.read</c>.</item>
/// <item>Users owners (users, roles) require <c>users.read</c>.</item>
/// <item>Unknown owner types impose no extra scope beyond the
/// <c>attachments.read</c> / <c>attachments.write</c> permission already
/// enforced by the <c>AuthorizationBehavior</c> pipeline step, so legacy or
/// future owners stay reachable until they are registered here.</item>
/// </list>
/// The same scope permission gates list, download and delete: the check runs
/// before any bytes are streamed and before any row is removed, so denied
/// callers observe 403 with no side effects.
/// </summary>
public static class AttachmentScopePolicy
{
    private static readonly ISet<string> ProductionOwners = new HashSet<string>(StringComparer.Ordinal)
    {
        "andonsignal", "andonsignals",
        "bomitem", "bomitems",
        "confirmation", "confirmations",
        "downtimeevent", "downtimeevents",
        "ganttschedule", "dispatchboard",
        "kanban", "kanbancard", "kanbancards", "kanbanloop", "kanbanloops",
        "lot", "lots", "lotgenealogy", "lotgenealogyedge",
        "machinetelemetrytag", "machinetelemetrytags",
        "opcuaconnection", "opcuaconnections",
        "operation", "operations", "operationnode", "operationnodes",
        "operationdependency", "operationdependencies",
        "operationoutput", "operationoutputs",
        "operationtemplate", "operationtemplates",
        "productionconfirmation", "productionconfirmations",
        "productionorder", "productionorders",
        "recipe", "recipes", "recipeversion", "recipeversions",
        "reliability",
        "resourcerequirement", "resourcerequirements",
        "scheduledoperation", "scheduledoperations",
        "scrapevent", "scrapevents",
        "shifthandover", "shifthandovers",
        "spc", "spccharacteristic", "spccharacteristics",
        "spcmeasurement", "spcmeasurements",
        "telemetry", "telemetryreading", "telemetryreadings",
        "oee",
    };

    private static readonly ISet<string> ConfigurationOwners = new HashSet<string>(StringComparer.Ordinal)
    {
        "currencies", "currency",
        "department", "departments",
        "machine", "machines",
        "workcenter", "workcenters", "workcentercalendar",
        "maintenanceplan", "maintenanceplans",
        "maintenanceworkorder", "maintenanceworkorders",
        "materialreservation", "materialreservations",
        "measureunit", "measureunits",
        "operator", "operators", "operatorshiftassignment", "operatorshiftassignments",
        "product", "products", "productgroup", "productgroups",
        "productmeasureunit", "productprice",
        "reasoncode", "reasoncodes",
        "shift", "shifts",
        "skill", "skills",
        "stockmovement", "stockmovements", "stockonhand",
        "warehouse", "warehouses",
    };

    private static readonly ISet<string> UsersOwners = new HashSet<string>(StringComparer.Ordinal)
    {
        "user", "users",
        "role", "roles",
        "permission", "permissions",
    };

    /// <summary>
    /// Returns the owner-module read permission required for the given owner
    /// type, or <c>null</c> when the owner type imposes no extra scope.
    /// </summary>
    public static string? GetRequiredPermission(string ownerType)
    {
        if (string.IsNullOrWhiteSpace(ownerType))
            return null;

        var normalized = Normalize(ownerType);

        if (ProductionOwners.Contains(normalized))
            return RbacDefaults.ProductionRead;

        if (ConfigurationOwners.Contains(normalized))
            return RbacDefaults.ConfigurationRead;

        if (UsersOwners.Contains(normalized))
            return RbacDefaults.UsersRead;

        return null;
    }

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> (HTTP 403) when the caller's
    /// permission set lacks the owner-module read permission for the given
    /// owner type. Unknown owner types pass through: the pipeline-level
    /// <see cref="RequirePermissionAttribute"/> (<c>attachments.read</c> on
    /// reads, <c>attachments.write</c> on delete) already applies.
    /// </summary>
    public static void EnsureScopeAccess(string ownerType, IReadOnlyCollection<string> permissions)
    {
        var required = GetRequiredPermission(ownerType);
        if (required is null)
            return;

        if (permissions.Contains(required, StringComparer.Ordinal))
            return;

        throw new ForbiddenException(
            $"Missing required scope permission: {required} for owner type '{ownerType}'.");
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
