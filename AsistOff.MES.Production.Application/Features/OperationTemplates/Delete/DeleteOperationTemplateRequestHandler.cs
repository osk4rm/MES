using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Delete;

internal sealed class DeleteOperationTemplateRequestHandler(IOperationTemplatesRepository repository)
    : IRequestHandler<DeleteOperationTemplateRequest>
{
    public async Task Handle(DeleteOperationTemplateRequest request, CancellationToken cancellationToken)
    {
        var template = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OperationTemplate", request.Id);

        await repository.DeleteAsync(template.Id, cancellationToken);
    }
}
