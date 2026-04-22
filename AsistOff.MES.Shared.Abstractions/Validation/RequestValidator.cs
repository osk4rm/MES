using FluentValidation;
using FluentValidation.Results;

namespace AsistOff.MES.Shared.Abstractions.Validation;

public class RequestValidator<T> : AbstractValidator<T>, IRequestValidator<T>
{
    public async Task ValidateAndThrowAsync(T model, CancellationToken cancellationToken = default)
    {
        var result = await ValidateAsync(model, cancellationToken);

        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }
    
    public async Task<ValidationResult> ValidateRequestAsync(T request, CancellationToken cancellationToken = default)
        => await ValidateAsync(request, cancellationToken);
}