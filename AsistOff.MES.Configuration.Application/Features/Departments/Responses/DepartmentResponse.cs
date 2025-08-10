namespace AsistOff.MES.Configuration.Application.Features.Departments.Responses;

public record DepartmentResponse
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}
