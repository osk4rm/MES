namespace AsistOff.MES.Shared.Abstractions.Auth;

/// <summary>
/// Documented allowlist for the default-deny <c>AuthorizationBehavior</c> pipeline step
/// (issues #231 slice 1/2 and #233 slice 2/2: Users, Multitenancy, Configuration,
/// Production, Attachments and Gateway).
///
/// Every MediatR request in those modules must carry either a
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
/// <item>Production reads (any authenticated tenant user): browse / get / OEE / reliability /
/// dispatch / traceability / telemetry-export queries expose no mutation and stay
/// available to the read-only <c>user</c> role. The OPC UA connection test performs
/// shape-only validation with no persistence or network I/O, so it is a read.</item>
/// <item>Attachments reads (any authenticated tenant user): list and download expose
/// no mutation and stay available to the read-only <c>user</c> role.</item>
/// </list>
///
/// Gateway owns no MediatR requests (only the errors controller), so it needs no
/// entries here. The legacy pass-through is empty since slice 2/2: every write
/// carries <see cref="RequirePermissionAttribute"/>.
/// </summary>
public static class AuthorizationAllowlist
{
    /// <summary>
    /// Assemblies whose MediatR requests keep the legacy pass-through (no
    /// <see cref="RequirePermissionAttribute"/> required). Empty since slice 2/2
    /// (issue #233): Production, Attachments and Gateway writes are covered, so
    /// everything without coverage fails closed. Kept as an explicit empty set so
    /// the pipeline step and the coverage test keep a single choke point.
    /// </summary>
    public static readonly IReadOnlySet<string> LegacyPassthroughAssemblyNames =
        new HashSet<string>(StringComparer.Ordinal);

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
            "AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse.BrowseMaintenancePlansRequest",
            "AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Get.GetMaintenancePlanRequest",
            "AsistOff.MES.Configuration.Application.Features.MaterialReservations.BrowseMaterialReservationsRequest",
            "AsistOff.MES.Configuration.Application.Features.MaterialReservations.GetMaterialReservationRequest",
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

            // Production reads — any authenticated tenant user (including the
            // read-only user role). Browse / get / analytics queries perform no mutation.
            "AsistOff.MES.Production.Application.Features.AndonSignals.Browse.BrowseAndonSignalsRequest",
            "AsistOff.MES.Production.Application.Features.AndonSignals.Get.GetAndonSignalRequest",
            "AsistOff.MES.Production.Application.Features.AuditEvents.Browse.BrowseAuditEventsRequest",
            "AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse.BrowseDowntimeEventsRequest",
            "AsistOff.MES.Production.Application.Features.DowntimeEvents.Get.GetDowntimeEventRequest",
            "AsistOff.MES.Production.Application.Features.Kanban.Cards.Browse.BrowseKanbanCardsRequest",
            "AsistOff.MES.Production.Application.Features.Kanban.Cards.Get.GetKanbanCardRequest",
            "AsistOff.MES.Production.Application.Features.Kanban.Loops.Browse.BrowseKanbanLoopsRequest",
            "AsistOff.MES.Production.Application.Features.Kanban.Loops.Get.GetKanbanLoopRequest",
            "AsistOff.MES.Production.Application.Features.LotGenealogy.Browse.BrowseLotGenealogyEdgesRequest",
            "AsistOff.MES.Production.Application.Features.LotGenealogy.Downstream.GetDownstreamTraceabilityRequest",
            "AsistOff.MES.Production.Application.Features.LotGenealogy.Get.GetLotGenealogyEdgeRequest",
            "AsistOff.MES.Production.Application.Features.LotGenealogy.Upstream.GetUpstreamTraceabilityRequest",
            "AsistOff.MES.Production.Application.Features.Lots.Browse.BrowseLotsRequest",
            "AsistOff.MES.Production.Application.Features.Lots.Get.GetLotRequest",
            "AsistOff.MES.Production.Application.Features.Lots.GetByCode.GetLotByCodeRequest",
            "AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Browse.BrowseMachineTelemetryTagsRequest",
            "AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Get.GetMachineTelemetryTagRequest",
            "AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Status.GetTelemetryStatusRequest",
            "AsistOff.MES.Production.Application.Features.Oee.Losses.GetOeeLossesRequest",
            "AsistOff.MES.Production.Application.Features.Oee.Snapshot.GetOeeSnapshotRequest",
            "AsistOff.MES.Production.Application.Features.Oee.Summary.GetOeeSummaryRequest",
            "AsistOff.MES.Production.Application.Features.Oee.Trend.GetOeeTrendRequest",
            "AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse.BrowseOpcUaConnectionsRequest",
            "AsistOff.MES.Production.Application.Features.OpcUaConnections.Get.GetOpcUaConnectionRequest",
            "AsistOff.MES.Production.Application.Features.OpcUaConnections.Status.GetOpcUaConnectionStatusRequest",
            "AsistOff.MES.Production.Application.Features.OpcUaConnections.Test.TestOpcUaConnectionRequest",
            "AsistOff.MES.Production.Application.Features.OperationTemplates.Browse.BrowseOperationTemplatesRequest",
            "AsistOff.MES.Production.Application.Features.OperationTemplates.Get.GetOperationTemplateRequest",
            "AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse.BrowseProductionConfirmationsRequest",
            "AsistOff.MES.Production.Application.Features.ProductionConfirmations.Get.GetProductionConfirmationRequest",
            "AsistOff.MES.Production.Application.Features.ProductionConfirmations.Movements.BrowseConfirmationMovementsRequest",
            "AsistOff.MES.Production.Application.Features.ProductionOrders.Browse.BrowseProductionOrdersRequest",
            "AsistOff.MES.Production.Application.Features.ProductionOrders.Get.GetProductionOrderRequest",
            "AsistOff.MES.Production.Application.Features.ProductionOrders.Movements.BrowseOrderMovementsRequest",
            "AsistOff.MES.Production.Application.Features.RecipeVersions.Get.GetRecipeVersionRequest",
            "AsistOff.MES.Production.Application.Features.Recipes.Browse.BrowseRecipesRequest",
            "AsistOff.MES.Production.Application.Features.Recipes.Get.GetRecipeRequest",
            "AsistOff.MES.Production.Application.Features.Reliability.GetReliabilityFleetRequest",
            "AsistOff.MES.Production.Application.Features.Reliability.GetReliabilitySnapshotRequest",
            "AsistOff.MES.Production.Application.Features.Reliability.Trend.GetReliabilityTrendRequest",
            "AsistOff.MES.Production.Application.Features.Schedule.GetDispatchBoardRequest",
            "AsistOff.MES.Production.Application.Features.ScrapEvents.Browse.BrowseScrapEventsRequest",
            "AsistOff.MES.Production.Application.Features.ScrapEvents.Get.GetScrapEventRequest",
            "AsistOff.MES.Production.Application.Features.ShiftHandovers.GetShiftHandoverContextRequest",
            "AsistOff.MES.Production.Application.Features.SpcCharacteristics.Browse.BrowseSpcCharacteristicsRequest",
            "AsistOff.MES.Production.Application.Features.SpcCharacteristics.Get.GetSpcCharacteristicRequest",
            "AsistOff.MES.Production.Application.Features.SpcMeasurements.Browse.BrowseSpcMeasurementsRequest",
            "AsistOff.MES.Production.Application.Features.SpcMeasurements.Chart.GetSpcMeasurementChartRequest",
            "AsistOff.MES.Production.Application.Features.SpcMeasurements.Get.GetSpcMeasurementRequest",
            "AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse.BrowseTelemetryReadingsRequest",
            "AsistOff.MES.Production.Application.Features.TelemetryReadings.Export.ExportTelemetryReadingsRequest",
            "AsistOff.MES.Production.Application.Features.TelemetryReadings.Get.GetTelemetryReadingRequest",
            "AsistOff.MES.Production.Application.Features.TelemetryReadings.Trend.BrowseTelemetryTrendRequest",

            // Attachments reads — any authenticated tenant user (including the
            // read-only user role). List and download perform no mutation.
            "AsistOff.MES.Attachments.Application.Features.Download.DownloadAttachmentRequest",
            "AsistOff.MES.Attachments.Application.Features.List.ListAttachmentsRequest",
        };

    public static bool IsAllowed(Type? requestType) =>
        requestType?.FullName is not null && IsAllowed(requestType.FullName);

    public static bool IsAllowed(string? requestFullName) =>
        requestFullName is not null && AllowedRequestFullNames.Contains(requestFullName);

    public static bool IsLegacyPassthrough(Type? requestType) =>
        requestType?.Assembly.GetName().Name is string assemblyName &&
        LegacyPassthroughAssemblyNames.Contains(assemblyName);
}
