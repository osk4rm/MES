using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Configuration.Application.Features.Products.Delete;

internal sealed class DeleteProductRequestHandler(
    IProductsRepository productsRepository,
    ILogger<DeleteProductRequestHandler> logger)
    : IRequestHandler<DeleteProductRequest>
{
    public async Task Handle(DeleteProductRequest request, CancellationToken cancellationToken)
    {
        var entity = await productsRepository.GetAsync(request.Id, cancellationToken);

        if (entity is null)
            throw new NotFoundException("Product", request.Id);

        try
        {
            await productsRepository.DeleteAsync(entity.Id, cancellationToken);
            logger.LogInformation("Product {Code} with ID {Id} deleted successfully", entity.Code, request.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting Product with ID {Id}", request.Id);
            throw;
        }
    }
}