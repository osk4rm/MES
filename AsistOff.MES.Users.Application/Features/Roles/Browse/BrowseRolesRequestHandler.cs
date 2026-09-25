using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Roles.Browse;

internal sealed class BrowseRolesRequestHandler(
    IRolesRepository rolesRepository,
    IRolePermissionsRepository rolePermissionsRepository,
    IUserRolesRepository userRolesRepository)
    : IRequestHandler<BrowseRolesRequest, IReadOnlyCollection<RoleResponse>>
{
    public async Task<IReadOnlyCollection<RoleResponse>> Handle(
        BrowseRolesRequest request, CancellationToken cancellationToken)
    {
        var roles = await rolesRepository.BrowseAsync(cancellationToken);
        var assignments = await userRolesRepository.BrowseAsync(cancellationToken);
        var memberCounts = assignments
            .GroupBy(x => x.RoleId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<RoleResponse>(roles.Count);
        foreach (var role in roles)
        {
            var links = await rolePermissionsRepository.BrowseByRoleAsync(role.Id, cancellationToken);
            var codes = links
                .Select(x => x.Permission?.Code)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            result.Add(new RoleResponse(
                role.Id,
                role.Code,
                role.Name,
                role.Description,
                codes,
                memberCounts.TryGetValue(role.Id, out var count) ? count : 0));
        }

        return result;
    }
}
