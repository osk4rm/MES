using AsistOff.MES.Shared.Abstractions.Contracts.Sorting;
using AsistOff.MES.Shared.Abstractions.Contracts.Validators;
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
            Dictionary<string, string[]>? failures = null;

            // Defense in depth (issue #311): every ISortable browse request
            // must pass the per-request SupportedSortFields whitelist, even
            // when the concrete request has no dedicated validator (or its
            // validator does not cover sorting). The closed-generic
            // SortableValidator over ISortable is never resolved by MediatR
            // for concrete requests, so it is applied explicitly here.
            if (request is ISortable sortable)
            {
                var sortableValidator = new SortableValidator();
                var sortableResult = await sortableValidator.ValidateAsync(sortable, cancellationToken);
                if (!sortableResult.IsValid)
                    failures = Merge(failures, sortableResult.Errors.GroupBy(x => x.PropertyName)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(x => x.ErrorMessage).ToArray()));
            }

            if (validator is not null)
            {
                var validationResult = await validator.ValidateAsync(request, cancellationToken);

                if (!validationResult.IsValid)
                    failures = Merge(failures, validationResult.Errors.GroupBy(x => x.PropertyName)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(x => x.ErrorMessage).ToArray()));
            }

            if (failures is not null)
                throw new ValidationException(failures);

            return await next(cancellationToken);
        }

        private static Dictionary<string, string[]> Merge(
            Dictionary<string, string[]>? first,
            Dictionary<string, string[]> second)
        {
            if (first is null)
                return new Dictionary<string, string[]>(second, StringComparer.Ordinal);

            foreach (var (key, messages) in second)
            {
                if (first.TryGetValue(key, out var existing))
                    first[key] = [.. existing, .. messages];
                else
                    first[key] = messages;
            }

            return first;
        }
    }
}
