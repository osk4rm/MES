using AsistOff.MES.Users.Core.Entities;

namespace AsistOff.MES.Users.Core.Repositories;

public interface IRolePermissionsRepository
{
    Task<RolePermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RolePermission>> BrowseByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RolePermission>> BrowseAsync(CancellationToken cancellationToken = default);
    Task<RolePermission> AddAsync(RolePermission rolePermission, CancellationToken cancellationToken = default);
}
