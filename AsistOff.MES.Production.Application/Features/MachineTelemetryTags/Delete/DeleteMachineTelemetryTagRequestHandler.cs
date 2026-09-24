using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Delete;

internal sealed class DeleteMachineTelemetryTagRequestHandler(IMachineTelemetryTagsRepository repository)
    : IRequestHandler<DeleteMachineTelemetryTagRequest>
{
    public async Task Handle(DeleteMachineTelemetryTagRequest request, CancellationToken cancellationToken)
    {
        _ = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MachineTelemetryTag", request.Id);

        // Readings cascade at the database level (FK ON DELETE CASCADE).
        await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
