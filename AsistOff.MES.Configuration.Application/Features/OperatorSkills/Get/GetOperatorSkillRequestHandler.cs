using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Browse;
using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Get;

internal sealed class GetOperatorSkillRequestHandler(
    IOperatorSkillQualificationsRepository repository)
    : IRequestHandler<GetOperatorSkillRequest, OperatorSkillQualificationResponse>
{
    public async Task<OperatorSkillQualificationResponse> Handle(
        GetOperatorSkillRequest request, CancellationToken cancellationToken)
    {
        var qualification = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OperatorSkillQualification", request.Id);

        return BrowseOperatorSkillsRequestHandler.Map(qualification);
    }
}
