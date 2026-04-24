using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Delete;

internal sealed class DeleteSkillRequestHandler(ISkillsRepository repository)
    : IRequestHandler<DeleteSkillRequest>
{
    public async Task Handle(DeleteSkillRequest request, CancellationToken cancellationToken)
    {
        var skill = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Skill", request.Id);

        await repository.DeleteAsync(skill.Id, cancellationToken);
    }
}
