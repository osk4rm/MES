using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.Reliability;

public sealed class GetReliabilitySnapshotValidator : RequestValidator<GetReliabilitySnapshotRequest>
{
    public GetReliabilitySnapshotValidator()
    {
        RuleFor(x => x.MachineId)
            .NotEmpty().WithMessage("Machine is required.");

        RuleFor(x => x.FromUtc)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.ToUtc)
            .NotEmpty().WithMessage("Time window is required.");

        RuleFor(x => x.FromUtc)
            .LessThan(x => x.ToUtc).WithMessage("FromUtc must be before ToUtc.");

        RuleFor(x => x)
            .Must(x => (x.ToUtc.ToUniversalTime() - x.FromUtc.ToUniversalTime()).TotalDays <= 93)
            .WithMessage("Time window cannot exceed 93 days.");
    }
}
