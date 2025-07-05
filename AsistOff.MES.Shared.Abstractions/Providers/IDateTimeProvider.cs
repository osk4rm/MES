namespace AsistOff.MES.Shared.Abstractions.Providers
{
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
    }
}
