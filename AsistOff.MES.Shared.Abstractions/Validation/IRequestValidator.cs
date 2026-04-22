using FluentValidation;
using FluentValidation.Results;

namespace AsistOff.MES.Shared.Abstractions.Validation;

public interface IRequestValidator<in T> : IValidator<T>
{
    Task ValidateAndThrowAsync(T request, CancellationToken cancellationToken = default);
}