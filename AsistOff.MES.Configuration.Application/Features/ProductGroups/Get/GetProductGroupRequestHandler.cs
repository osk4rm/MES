using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Get;

internal sealed class GetProductGroupRequestHandler(IProductGroupsRepository productGroupsRepository)
    : IRequestHandler<GetProductGroupRequest, ProductGroupResponse>
{
    public async Task<ProductGroupResponse> Handle(GetProductGroupRequest request, CancellationToken cancellationToken)
    {
        var productGroup = await productGroupsRepository.GetAsync(request.Id, cancellationToken);

        if (productGroup is null)
        {
            throw new NotFoundException("ProductGroup", request.Id);
        }

        return new ProductGroupResponse(
            productGroup.Id,
            productGroup.SyncId,
            productGroup.Code,
            productGroup.Name,
            productGroup.Description,
            productGroup.IsActive,
            productGroup.ParentGroupId.HasValue
                ? new ParentGroupResponse(productGroup.ParentGroupId.Value, productGroup.ParentGroup!.Code)
                : null);
    }
}