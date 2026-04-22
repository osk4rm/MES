namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;

public record ProductGroupResponse(
    Guid Id,
    string? SyncId,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    ParentGroupResponse? Parent
);