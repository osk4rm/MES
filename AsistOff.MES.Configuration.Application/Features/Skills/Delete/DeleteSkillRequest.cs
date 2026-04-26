using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Delete;

public record DeleteSkillRequest(Guid Id) : ITenantRequest;
