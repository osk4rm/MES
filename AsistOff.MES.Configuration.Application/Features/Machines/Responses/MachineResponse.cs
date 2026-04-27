namespace AsistOff.MES.Configuration.Application.Features.Machines.Responses;

public record MachineResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    string? SyncId);
