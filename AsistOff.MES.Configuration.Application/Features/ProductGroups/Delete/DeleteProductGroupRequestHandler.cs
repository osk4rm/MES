using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Delete;

internal sealed class DeleteProductGroupRequestHandler(
    IProductGroupsRepository productGroupsRepository,
    ILogger<DeleteProductGroupRequestHandler> logger)
    : IRequestHandler<DeleteProductGroupRequest>
{
    public async Task Handle(DeleteProductGroupRequest request, CancellationToken cancellationToken)
    {
        var entity = await productGroupsRepository.GetAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException("ProductGroup", request.Id);
        }

        try
        {
            await productGroupsRepository.DeleteAsync(entity.Id, cancellationToken);

            logger.LogInformation("ProductGroup {Code} with ID {Id} deleted successfully",
                entity.Code, request.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting ProductGroup with ID {Id}", request.Id);
            throw;
        }
    }
}
