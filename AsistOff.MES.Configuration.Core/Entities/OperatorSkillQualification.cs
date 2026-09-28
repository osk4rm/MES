using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// Tenant-scoped qualification matrix row: records that an
/// <see cref="Operator"/> holds a <see cref="Skill"/>. Dispatch and
/// confirmation gating read this join to decide whether an operator may be
/// assigned to (or report) work whose recipe operation requires a skill.
/// Unique per (tenant, operator, skill); deleted by cascade with either side.
/// </summary>
public class OperatorSkillQualification : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OperatorId { get; set; }
    public Guid SkillId { get; set; }

    public virtual Operator? Operator { get; set; }
    public virtual Skill? Skill { get; set; }
}
