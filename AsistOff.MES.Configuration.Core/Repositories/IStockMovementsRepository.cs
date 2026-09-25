using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IStockMovementsRepository
{
    Task<IReadOnlyCollection<StockMovement>> ListForConfirmationAsync(
        Guid productionConfirmationId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<StockMovement> entities, CancellationToken cancellationToken = default);
}
