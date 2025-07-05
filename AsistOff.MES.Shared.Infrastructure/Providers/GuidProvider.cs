using AsistOff.MES.Shared.Abstractions.Providers;

namespace AsistOff.MES.Shared.Infrastructure.Providers;

public class GuidProvider : IGuidProvider
{
    public Guid NewGuid()
        => Guid.NewGuid();
}