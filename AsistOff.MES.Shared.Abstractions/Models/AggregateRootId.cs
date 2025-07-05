namespace AsistOff.MES.Shared.Abstractions.Models
{
    public abstract class AggregateRootId<TId>(TId value) : EntityId<TId>(value);
}
