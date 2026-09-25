using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Configuration.Application.Features.Machines;

/// <summary>Shared validation and defaulting for Work Center capacity and efficiency.</summary>
internal static class MachineCapacityRules
{
    public const decimal DefaultCapacity = 1m;
    public const decimal DefaultEfficiencyFactor = 1.0m;

    public static decimal ResolveCapacity(decimal? capacity)
    {
        var value = capacity ?? DefaultCapacity;
        if (value <= 0)
            throw new ValidationException(nameof(capacity), "Capacity must be greater than zero.");
        return value;
    }

    public static decimal ResolveEfficiencyFactor(decimal? efficiencyFactor)
    {
        var value = efficiencyFactor ?? DefaultEfficiencyFactor;
        if (value <= 0 || value > 1)
            throw new ValidationException(nameof(efficiencyFactor), "Efficiency factor must be greater than zero and at most 1.");
        return value;
    }
}
