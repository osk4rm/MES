using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using ErrorOr;
using MediatR;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Application.Features.Operators.Browse;

public class BrowseOperatorsRequestHandler : IRequestHandler<BrowseOperatorsRequest, ErrorOr<PagedResponse<OperatorResponse>>>
{
    private readonly IOperatorsRepository _operatorsRepository;

    public BrowseOperatorsRequestHandler(IOperatorsRepository operatorsRepository)
    {
        _operatorsRepository = operatorsRepository;
    }

    public async Task<ErrorOr<PagedResponse<OperatorResponse>>> Handle(BrowseOperatorsRequest request,
        CancellationToken cancellationToken)
    {
        var predicate = BuildPredicate(request);
        var operators =
            await _operatorsRepository.BrowseAsync(new Paginator<Operator>(predicate, request), cancellationToken);

        var items = operators.Select(x => new OperatorResponse()
        {
            Id = x.Id,
            Identifier = x.Identifier,
            FirstName = x.FirstName,
            LastName = x.LastName,
            RatePerHour = x.RatePerHour,
            Department = x.Department?.Name
        }).ToList();

        var totalCount = items.Count;
        return new PagedOperatorsResponse(items, totalCount, request.PageSize);
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