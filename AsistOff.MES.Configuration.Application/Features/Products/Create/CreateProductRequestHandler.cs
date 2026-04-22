using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Products.Create;

internal sealed class CreateProductRequestHandler(
    IProductsRepository productsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext,
    ILogger<CreateProductRequestHandler> logger)
    : IRequestHandler<CreateProductRequest, ProductResponse>
{
    public async Task<ProductResponse> Handle(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var entity = new Product
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            SyncId = request.SyncId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            Ean = request.Ean,
            Barcode = request.Barcode,
            ScanBy = request.ScanBy,
            IsActive = request.IsActive,
            ProductGroupId = request.ProductGroupId
        };

        try
        {
            var created = await productsRepository.AddAsync(entity, cancellationToken);

            logger.LogInformation("Product {Code} created successfully with ID {Id}", request.Code, created.Id);

            return new ProductResponse(
                created.Id,
                created.SyncId,
                created.Code,
                created.Name,
                created.Description,
                created.Ean,
                created.Barcode,
                created.ScanBy,
                created.IsActive,
                created.ProductGroupId.HasValue && created.ProductGroup is not null
                    ? new ProductGroupShortResponse(created.ProductGroupId.Value, created.ProductGroup.Code, created.ProductGroup.Name)
                    : null,
                null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Product {Code}", request.Code);
            throw;
        }
    }
}