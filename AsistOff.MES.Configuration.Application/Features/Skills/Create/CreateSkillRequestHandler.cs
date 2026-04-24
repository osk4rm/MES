using AsistOff.MES.Configuration.Application.Features.Skills.Browse;
using AsistOff.MES.Configuration.Application.Features.Skills.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Create;

internal sealed class CreateSkillRequestHandler(
    ISkillsRepository repository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateSkillRequest, SkillResponse>
{
    public async Task<SkillResponse> Handle(CreateSkillRequest request, CancellationToken cancellationToken)
    {
        if (await repository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Skill with code '{request.Code}' already exists.");

        var skill = new Skill
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive
        };
        await repository.AddAsync(skill, cancellationToken);
        return BrowseSkillsRequestHandler.Map(skill);
    }
}
