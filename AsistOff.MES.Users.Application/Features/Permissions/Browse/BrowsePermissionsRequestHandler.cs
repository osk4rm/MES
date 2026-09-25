using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Permissions.Browse;

internal sealed class BrowsePermissionsRequestHandler(IPermissionsRepository permissionsRepository)
    : IRequestHandler<BrowsePermissionsRequest, IReadOnlyCollection<PermissionResponse>>
{
    public async Task<IReadOnlyCollection<PermissionResponse>> Handle(
        BrowsePermissionsRequest request, CancellationToken cancellationToken)
    {
        var permissions = await permissionsRepository.BrowseAsync(cancellationToken);

        return permissions
            .Select(x => new PermissionResponse(x.Id, x.Code, x.Name, x.Category))
            .ToList();
    }
}
