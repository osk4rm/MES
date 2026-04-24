using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IChildEntitiesRepository
{
    Task<BomItem?> GetBomItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationOutput?> GetOperationOutputAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ResourceRequirement?> GetResourceRequirementAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddOperationOutputAsync(OperationOutput entity, CancellationToken cancellationToken = default);
    Task AddResourceRequirementAsync(ResourceRequirement entity, CancellationToken cancellationToken = default);

    Task RemoveBomItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task RemoveOperationOutputAsync(Guid id, CancellationToken cancellationToken = default);
    Task RemoveResourceRequirementAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
