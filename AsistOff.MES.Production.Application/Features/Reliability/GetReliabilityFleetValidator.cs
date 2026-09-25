using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.Reliability;

public sealed class GetReliabilityFleetValidator : RequestValidator<GetReliabilityFleetRequest>
{
    public GetReliabilityFleetValidator()
    {
        RuleFor(x => x.FromUtc)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.ToUtc)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.FromUtc)
            .LessThan(x => x.ToUtc).WithMessage("FromUtc must be before ToUtc.");

        RuleFor(x => x)
            .Must(x => (x.ToUtc.ToUniversalTime() - x.FromUtc.ToUniversalTime()).TotalDays <= 93)
            .WithMessage("Time window cannot exceed 93 days.");

        RuleFor(x => x.DepartmentId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Department is invalid.");
    }
}
