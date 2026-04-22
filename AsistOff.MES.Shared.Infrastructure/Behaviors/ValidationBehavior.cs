using AsistOff.MES.Shared.Abstractions.Validation;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Shared.Infrastructure.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse>(IRequestValidator<TRequest>? validator = null) 
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (validator is null)
                return await next(cancellationToken);

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (validationResult.IsValid)
                return await next(cancellationToken);

            var errors = validationResult.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(x => x.ErrorMessage).ToArray());

            throw new ValidationException(errors);
        }
    }
}
