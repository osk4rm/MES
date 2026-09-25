namespace AsistOff.MES.Configuration.Application.Features.Machines.Responses;

public record MachineResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    decimal Capacity,
    decimal EfficiencyFactor,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    string? SyncId);
