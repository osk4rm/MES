using AsistOff.MES.Shared.Abstractions.Providers;

namespace AsistOff.MES.Shared.Infrastructure.Providers
{
    public class DateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
