using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Application.Features.Products.Common;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Products.Update;

internal sealed class UpdateProductRequestHandler(
    IProductsRepository productsRepository,
    ILogger<UpdateProductRequestHandler> logger)
    : IRequestHandler<UpdateProductRequest, ProductResponse>
{
    public async Task<ProductResponse> Handle(UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var entity = await productsRepository.GetAsync(request.Id, cancellationToken);

        if (entity is null)
            throw new NotFoundException("Product", request.Id);

        var ean = GtinValidator.Normalize(request.Ean);

        if (!GtinValidator.IsValid(request.Ean))
            throw new ValidationException(nameof(request.Ean), "Ean must be 8, 12, 13 or 14 digits with a valid GTIN check digit.");

        if (ean is not null && await productsRepository.EanExistsAsync(ean, request.Id, cancellationToken))
            throw new ConflictException($"Product with EAN '{ean}' already exists.");

        try
        {
            entity.Code = request.Code;
            entity.Name = request.Name;
            entity.Description = request.Description;
            entity.Ean = ean;
            entity.Barcode = request.Barcode;
            entity.ScanBy = request.ScanBy;
            entity.IsActive = request.IsActive;
            entity.ProductGroupId = request.ProductGroupId;
            entity.SyncId = request.SyncId;

            await productsRepository.UpdateAsync(entity, cancellationToken);

            logger.LogInformation("Product {Code} updated successfully with ID {Id}", request.Code, request.Id);

            var defaultMU = entity.ProductMeasureUnits.FirstOrDefault(x => x.IsDefault)?.MeasureUnit;

            return new ProductResponse(
                entity.Id,
                entity.SyncId,
                entity.Code,
                entity.Name,
                entity.Description,
                entity.Ean,
                entity.Barcode,
                entity.ScanBy,
                entity.IsActive,
                entity.ProductGroupId.HasValue && entity.ProductGroup is not null
                    ? new ProductGroupShortResponse(entity.ProductGroupId.Value, entity.ProductGroup.Code, entity.ProductGroup.Name)
                    : null,
                defaultMU is not null ? new MeasureUnitShortResponse(defaultMU.Id, defaultMU.Name, defaultMU.Symbol) : null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating Product {Code} with ID {Id}", request.Code, request.Id);
            throw;
        }
    }
}