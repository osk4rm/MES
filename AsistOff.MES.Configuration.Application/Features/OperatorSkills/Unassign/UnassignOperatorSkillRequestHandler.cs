using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Unassign;

internal sealed class UnassignOperatorSkillRequestHandler(
    IOperatorSkillQualificationsRepository repository)
    : IRequestHandler<UnassignOperatorSkillRequest>
{
    public async Task Handle(UnassignOperatorSkillRequest request, CancellationToken cancellationToken)
    {
        var qualification = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OperatorSkillQualification", request.Id);

        await repository.DeleteAsync(qualification.Id, cancellationToken);
    }
}
