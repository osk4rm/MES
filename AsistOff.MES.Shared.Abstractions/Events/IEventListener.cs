namespace AsistOff.MES.Shared.Abstractions.Events;

public interface IEventListener<in TEvent> where TEvent : class, IEvent
{
    Task HandleAsync(TEvent @event);
}