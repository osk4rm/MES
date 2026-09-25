using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.Products.ByScan;

public class GetProductByScanRequestValidator : RequestValidator<GetProductByScanRequest>
{
    public GetProductByScanRequestValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Scan value is required.");
    }
}
