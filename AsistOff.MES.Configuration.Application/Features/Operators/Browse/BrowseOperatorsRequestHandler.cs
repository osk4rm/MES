using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Browse;

public sealed class BrowseOperatorsRequestHandler(
    IOperatorsRepository operatorsRepository)
    : IRequestHandler<BrowseOperatorsRequest, PagedResponse<OperatorResponse>>
{
    public async Task<PagedResponse<OperatorResponse>> Handle(BrowseOperatorsRequest request,
        CancellationToken cancellationToken)
    {
        var predicate = BuildPredicate(request);
        var operators =
            await operatorsRepository.BrowseAsync(new Paginator<Operator>(predicate, request), cancellationToken);

        var items = operators.Select(x => new OperatorResponse()
        {
            Id = x.Id,
            Identifier = x.Identifier,
            FirstName = x.FirstName,
            LastName = x.LastName,
            RatePerHour = x.RatePerHour,
            Department = x.Department?.Name
        }).ToList();

        return new PagedOperatorsResponse(items, 969, request.PageSize);
    }

    private ExpressionStarter<Operator> BuildPredicate(BrowseOperatorsRequest request)
    {
        var predicate = PredicateBuilder.New<Operator>(true);

        if (!string.IsNullOrWhiteSpace(request.Identifier))
        {
            predicate = predicate.And(o => o.Identifier.Contains(request.Identifier));
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            predicate = predicate.And(o => o.FirstName.Contains(request.FirstName));
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            predicate = predicate.And(o => o.LastName.Contains(request.LastName));
        }

        if (request.RatePerHourFrom.HasValue)
        {
            predicate = predicate.And(o => o.RatePerHour >= request.RatePerHourFrom.Value);
        }

        if (request.RatePerHourTo.HasValue)
        {
            predicate = predicate.And(o => o.RatePerHour <= request.RatePerHourTo.Value);
        }

        if (request.DepartmentId != Guid.Empty)
        {
            predicate = predicate.And(o => o.DepartmentId == request.DepartmentId);
        }

        return predicate;
    }
}