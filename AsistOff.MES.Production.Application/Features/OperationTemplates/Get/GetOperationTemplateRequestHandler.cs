using AsistOff.MES.Production.Application.Features.OperationTemplates.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Get;

internal sealed class GetOperationTemplateRequestHandler(IOperationTemplatesRepository repository)
    : IRequestHandler<GetOperationTemplateRequest, OperationTemplateResponse>
{
    public async Task<OperationTemplateResponse> Handle(GetOperationTemplateRequest request, CancellationToken cancellationToken)
    {
        var template = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OperationTemplate", request.Id);

        return BrowseOperationTemplatesRequestHandler.Map(template);
    }
}
