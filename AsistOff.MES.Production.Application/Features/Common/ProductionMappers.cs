using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Application.Features.Common;

internal static class ProductionMappers
{
    public static RecipeResponse Map(Recipe recipe) => new(
        recipe.Id,
        recipe.Code,
        recipe.Name,
        recipe.Description,
        recipe.IsActive,
        recipe.PrimaryProductId,
        recipe.CurrentVersionId,
        recipe.SyncId,
        recipe.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(MapSummary)
            .ToList());

    public static RecipeVersionSummaryResponse MapSummary(RecipeVersion v) => new(
        v.Id, v.VersionNumber, v.Status, v.ReleasedAt, v.ValidFrom, v.ValidTo, v.ChangeNotes, v.CreatedAt);

    public static RecipeVersionDetailResponse MapDetail(RecipeVersion v) => new(
        v.Id,
        v.RecipeId,
        v.VersionNumber,
        v.Status,
        v.ReleasedAt,
        v.ValidFrom,
        v.ValidTo,
        v.ChangeNotes,
        v.CreatedAt,
        v.UpdatedAt,
        v.Operations.OrderBy(o => o.SortIndex).Select(Map).ToList());

    public static OperationNodeResponse Map(OperationNode op) => new(
        op.Id,
        op.Code,
        op.Name,
        op.Description,
        op.OperationType,
        op.SortIndex,
        op.SetupTimeMinutes,
        op.RunTimeMode,
        op.RunTimePerUnitSeconds,
        op.RunTimePerBatchMinutes,
        op.TeardownTimeMinutes,
        op.QueueTimeMinutes,
        op.IsOptional,
        op.AllowParallelExecution,
        op.ExpectedQuantity,
        op.Dependencies.Select(d => new OperationDependencyResponse(
            d.Id, d.PredecessorOperationNodeId, d.DependencyType, d.LagMinutes)).ToList(),
        op.BomItems.OrderBy(b => b.SortIndex).Select(b => new BomItemResponse(
            b.Id, b.ProductId, b.MeasureUnitId, b.Quantity, b.QuantityType, b.ScrapPercentage,
            b.IsOptional, b.PreferredWarehouseId, b.ConsumptionTiming, b.Notes, b.SortIndex)).ToList(),
        op.Outputs.OrderBy(o => o.SortIndex).Select(o => new OperationOutputResponse(
            o.Id, o.ProductId, o.MeasureUnitId, o.Quantity, o.QuantityType, o.OutputType,
            o.PreferredWarehouseId, o.Notes, o.SortIndex)).ToList(),
        op.ResourceRequirements.Select(r => new ResourceRequirementResponse(
            r.Id, r.PreferredDepartmentId, r.PreferredMachineId, r.RequiredCapability,
            r.RequiredOperatorCount, r.RequiredRole, r.Notes)).ToList());
}
