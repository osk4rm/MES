using AsistOff.MES.Configuration.Application.Features.Operators.Browse;
using AsistOff.MES.Configuration.Application.Features.Operators.Responses;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Infrastructure.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Configuration.Api.Controllers;

[Route("api/operators")]
public class OperatorsController : ApiController
{
    private readonly ISender _sender;

    public OperatorsController(ISender sender)
    {
        _sender = sender;
    }
    
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OperatorResponse>>> BrowseAsync(
        [FromQuery] BrowseOperatorsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request, cancellationToken);

        return result;
    }
}