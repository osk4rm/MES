using ErrorOr;
using FluentValidation;
using MediatR;

namespace AsistOff.MES.Shared.Infrastructure.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse>(IValidator<TRequest>? validator = null) :
        IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IErrorOr
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (validator is null)
                return await next();

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (validationResult.IsValid)
                return await next();

            var errors = validationResult.Errors
                .ConvertAll(failure => Error.Validation(failure.PropertyName, failure.ErrorMessage));

            return (dynamic)errors;

        }
    }
}
