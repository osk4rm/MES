using AsistOff.MES.Configuration.Application.Features.Skills.Browse;
using AsistOff.MES.Configuration.Application.Features.Skills.Responses;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Get;

internal sealed class GetSkillRequestHandler(ISkillsRepository repository)
    : IRequestHandler<GetSkillRequest, SkillResponse>
{
    public async Task<SkillResponse> Handle(GetSkillRequest request, CancellationToken cancellationToken)
    {
        var skill = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Skill", request.Id);

        return BrowseSkillsRequestHandler.Map(skill);
    }
}
