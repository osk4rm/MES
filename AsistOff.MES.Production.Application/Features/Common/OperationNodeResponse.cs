using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Common;

public record BomItemResponse(
    Guid Id,
    Guid ProductId,
    Guid? MeasureUnitId,
    decimal Quantity,
    BomQuantityType QuantityType,
    decimal? ScrapPercentage,
    bool IsOptional,
    Guid? PreferredWarehouseId,
    ConsumptionTiming ConsumptionTiming,
    string? Notes,
    int SortIndex);

public record OperationOutputResponse(
    Guid Id,
    Guid ProductId,
    Guid? MeasureUnitId,
    decimal Quantity,
    BomQuantityType QuantityType,
    OperationOutputType OutputType,
    Guid? PreferredWarehouseId,
    string? Notes,
    int SortIndex);

public record ResourceRequirementResponse(
    Guid Id,
    Guid? PreferredDepartmentId,
    Guid? PreferredMachineId,
    string? RequiredCapability,
    int RequiredOperatorCount,
    string? RequiredRole,
    string? Notes);

public record OperationDependencyResponse(
    Guid Id,
    Guid PredecessorOperationNodeId,
    OperationDependencyType DependencyType,
    decimal? LagMinutes);

public record OperationNodeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? OperationType,
    int SortIndex,
    decimal? SetupTimeMinutes,
    RunTimeMode RunTimeMode,
    decimal? RunTimePerUnitSeconds,
    decimal? RunTimePerBatchMinutes,
    decimal? TeardownTimeMinutes,
    decimal? QueueTimeMinutes,
    bool IsOptional,
    bool AllowParallelExecution,
    decimal? ExpectedQuantity,
    IReadOnlyCollection<OperationDependencyResponse> Dependencies,
    IReadOnlyCollection<BomItemResponse> BomItems,
    IReadOnlyCollection<OperationOutputResponse> Outputs,
    IReadOnlyCollection<ResourceRequirementResponse> ResourceRequirements);

public record RecipeVersionDetailResponse(
    Guid Id,
    Guid RecipeId,
    int VersionNumber,
    RecipeVersionStatus Status,
    DateTime? ReleasedAt,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? ChangeNotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<OperationNodeResponse> Operations);
