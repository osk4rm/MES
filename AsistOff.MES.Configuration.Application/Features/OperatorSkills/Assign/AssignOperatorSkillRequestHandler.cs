using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Assign;

internal sealed class AssignOperatorSkillRequestHandler(
    IOperatorSkillQualificationsRepository repository,
    IOperatorsRepository operatorsRepository,
    ISkillsRepository skillsRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<AssignOperatorSkillRequest, OperatorSkillQualificationResponse>
{
    public async Task<OperatorSkillQualificationResponse> Handle(
        AssignOperatorSkillRequest request, CancellationToken cancellationToken)
    {
        if (request.OperatorId == Guid.Empty)
            throw new ValidationException(nameof(request.OperatorId), "Operator is required.");
        if (request.SkillId == Guid.Empty)
            throw new ValidationException(nameof(request.SkillId), "Skill is required.");

        var operatorEntity = await operatorsRepository.GetByIdAsync(request.OperatorId, cancellationToken)
            ?? throw new NotFoundException("Operator", request.OperatorId);

        var skill = await skillsRepository.GetByIdAsync(request.SkillId, cancellationToken)
            ?? throw new NotFoundException("Skill", request.SkillId);

        if (await repository.ExistsAsync(request.OperatorId, request.SkillId, cancellationToken))
            throw new ConflictException(
                $"Operator '{request.OperatorId}' already holds skill '{skill.Code}'.");

        // The navigations are attached deliberately: the entities were just
        // loaded in this scope, and Map reads display fields (operator
        // identifier/name, skill code/name) so the Created response is
        // complete without an extra round-trip.
        var qualification = new OperatorSkillQualification
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            OperatorId = request.OperatorId,
            SkillId = request.SkillId,
            Operator = operatorEntity,
            Skill = skill
        };

        await repository.AddAsync(qualification, cancellationToken);
        return BrowseOperatorSkillsRequestHandler.Map(qualification);
    }
}
