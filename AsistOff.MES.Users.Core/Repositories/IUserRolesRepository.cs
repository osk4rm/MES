using AsistOff.MES.Users.Core.Entities;

namespace AsistOff.MES.Users.Core.Repositories;

public interface IUserRolesRepository
{
    Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserRole>> BrowseByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserRole>> BrowseAsync(CancellationToken cancellationToken = default);
    Task<UserRole> AddAsync(UserRole userRole, CancellationToken cancellationToken = default);
}
