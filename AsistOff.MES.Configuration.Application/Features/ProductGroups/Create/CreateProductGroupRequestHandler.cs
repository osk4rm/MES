using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Create;

internal sealed class CreateProductGroupRequestHandler(
    IProductGroupsRepository productGroupsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext,
    ILogger<CreateProductGroupRequestHandler> logger)
    : IRequestHandler<CreateProductGroupRequest, ProductGroupResponse>
{
    public async Task<ProductGroupResponse> Handle(CreateProductGroupRequest request,
        CancellationToken cancellationToken)
    {
        var entity = new ProductGroup
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            SyncId = request.SyncId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            ParentGroupId = request.ParentId
        };

        try
        {
            var created = await productGroupsRepository.AddAsync(entity, cancellationToken);

            logger.LogInformation("ProductGroup {Code} created successfully with ID {Id}",
                request.Code, created.Id);

            return new ProductGroupResponse(
                created.Id,
                created.SyncId,
                created.Code,
                created.Name,
                created.Description,
                created.IsActive,
                created.ParentGroupId.HasValue
                    ? new ParentGroupResponse(created.ParentGroupId.Value, created.ParentGroup?.Code ?? string.Empty)
                    : null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating ProductGroup {Code}", request.Code);
            throw;
        }
    }
}
