using AsistOff.MES.Configuration.Application.Features.Products.Common;
using AsistOff.MES.Shared.Abstractions.Validation;
using FluentValidation;

namespace AsistOff.MES.Configuration.Application.Features.Products.Update;

public class UpdateProductRequestValidator : RequestValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Ean)
            .Must(ean => GtinValidator.IsValid(ean))
            .WithMessage("Ean must be 8, 12, 13 or 14 digits with a valid GTIN check digit.")
            .When(x => !string.IsNullOrWhiteSpace(x.Ean));
    }
}
