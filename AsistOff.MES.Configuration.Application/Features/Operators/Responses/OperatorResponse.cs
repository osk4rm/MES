namespace AsistOff.MES.Configuration.Application.Features.Operators.Responses;

public record OperatorResponse
{
    public Guid Id { get; set; }
    public required string Identifier { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public decimal RatePerHour { get; set; }
    public string? Department { get; set; }
}