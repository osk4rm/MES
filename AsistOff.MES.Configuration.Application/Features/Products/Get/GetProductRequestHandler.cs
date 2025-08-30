using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Products.Get;

internal sealed class GetProductRequestHandler(IProductsRepository productsRepository)
    : IRequestHandler<GetProductRequest, ProductResponse>
{
    public async Task<ProductResponse> Handle(GetProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productsRepository.GetAsync(request.Id, cancellationToken);

        if (product is null)
            throw new NotFoundException("Product", request.Id);

        var defaultMU = product.ProductMeasureUnits.FirstOrDefault(x => x.IsDefault)?.MeasureUnit;

        return new ProductResponse(
            product.Id,
            product.SyncId,
            product.Code,
            product.Name,
            product.Description,
            product.Ean,
            product.Barcode,
            product.ScanBy,
            product.IsActive,
            product.ProductGroupId.HasValue && product.ProductGroup is not null
                ? new ProductGroupShortResponse(product.ProductGroupId.Value, product.ProductGroup.Code, product.ProductGroup.Name)
                : null,
            defaultMU is not null ? new MeasureUnitShortResponse(defaultMU.Id, defaultMU.Name, defaultMU.Symbol) : null
        );
    }
}