namespace AsistOff.MES.Integration.Tests.TestData;

/// <summary>Shape of a reason code as returned by <c>/api/reason-codes</c>.</summary>
public sealed record ReasonCodeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    short Category,
    bool IsActive,
    int SortIndex);

/// <summary>Shape of the paged responses returned by browse endpoints.</summary>
public sealed record PagedResponseDto<T>(
    int TotalCount,
    int TotalPages,
    IReadOnlyCollection<T> Items);

/// <summary>Shape of a production order as returned by <c>/api/production-orders</c>.</summary>
public sealed record ProductionOrderDto(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid RecipeId,
    Guid RecipeVersionId,
    decimal PlannedQuantity,
    Guid? MeasureUnitId,
    int Priority,
    DateTime? DueDate,
    short Status,
    DateTime? ReleasedAt,
    Guid? ReleasedByUserId,
    string? Notes,
    string? SyncId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Shape of a recipe as returned by <c>/api/recipes</c>.</summary>
public sealed record RecipeDto(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyCollection<RecipeVersionDto> Versions);

/// <summary>Shape of a recipe version summary as returned by <c>/api/recipes</c>.</summary>
public sealed record RecipeVersionDto(
    Guid Id,
    int VersionNumber,
    short Status);
