using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Production.Application.Features.Reliability.Trend;

public sealed class GetReliabilityTrendValidator : RequestValidator<GetReliabilityTrendRequest>
{
    public GetReliabilityTrendValidator()
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

        RuleFor(x => x.Bucket)
            .NotEmpty().WithMessage("Bucket must be either Day or Week.")
            .Must(bucket => Enum.TryParse<ReliabilityTrendBucket>(bucket, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed))
            .WithMessage("Bucket must be either Day or Week.");
    }
}
