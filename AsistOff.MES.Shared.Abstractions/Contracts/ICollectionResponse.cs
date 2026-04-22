namespace AsistOff.MES.Shared.Abstractions.Contracts;

public interface ICollectionResponse<out T>
{
    IReadOnlyCollection<T> Items { get; }
}