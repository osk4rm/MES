using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Update;

internal sealed class UpdateProductGroupRequestHandler(
    IProductGroupsRepository productGroupsRepository,
    ILogger<UpdateProductGroupRequestHandler> logger)
    : IRequestHandler<UpdateProductGroupRequest, ProductGroupResponse>
{
    public async Task<ProductGroupResponse> Handle(UpdateProductGroupRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await productGroupsRepository
            .GetAsync(request.Id, cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException("ProductGroup", request.Id);
        }

        try
        {
            entity.Code = request.Code;
            entity.Name = request.Name;
            entity.Description = request.Description;
            entity.IsActive = request.IsActive;
            entity.ParentGroupId = request.ParentId;
            entity.SyncId = request.SyncId;

            await productGroupsRepository.UpdateAsync(entity, cancellationToken);

            logger.LogInformation("ProductGroup {Code} updated successfully with ID {Id}",
                request.Code, request.Id);

            return new ProductGroupResponse(
                entity.Id,
                entity.SyncId,
                entity.Code,
                entity.Name,
                entity.Description,
                entity.IsActive,
                entity.ParentGroupId.HasValue
                    ? new ParentGroupResponse(entity.ParentGroupId.Value, entity.ParentGroup?.Code ?? string.Empty)
                    : null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating ProductGroup {Code} with ID {Id}", request.Code, request.Id);
            throw;
        }
    }
}
