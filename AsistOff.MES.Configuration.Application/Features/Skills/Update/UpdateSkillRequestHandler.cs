using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Update;

internal sealed class UpdateSkillRequestHandler(ISkillsRepository repository)
    : IRequestHandler<UpdateSkillRequest>
{
    public async Task Handle(UpdateSkillRequest request, CancellationToken cancellationToken)
    {
        var skill = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Skill", request.Id);

        if (await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
            throw new ConflictException($"Skill with code '{request.Code}' already exists.");

        skill.Code = request.Code;
        skill.Name = request.Name;
        skill.Description = request.Description;
        skill.IsActive = request.IsActive;

        await repository.UpdateAsync(skill, cancellationToken);
    }
}
