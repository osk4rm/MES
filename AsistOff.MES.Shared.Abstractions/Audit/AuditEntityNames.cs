namespace AsistOff.MES.Shared.Abstractions.Audit;

/// <summary>
/// Pilot entity names covered by the append-only audit history (slice 2/2,
/// issue #246). The single source of truth shared by the
/// <c>AuditHistoryInterceptor</c> write path (Shared.Infrastructure) and the
/// history browse feature (Production.Application) so both sides agree on
/// which tables are traced without a module-to-shared project reference.
/// Full-table coverage beyond these three pilots is explicitly out of scope.
/// </summary>
public static class AuditEntityNames
{
    public const string ProductionOrder = "ProductionOrder";
    public const string Machine = "Machine";
    public const string ProductionConfirmation = "ProductionConfirmation";

    public static readonly IReadOnlySet<string> Pilots =
        new HashSet<string>(StringComparer.Ordinal)
        {
            ProductionOrder,
            Machine,
            ProductionConfirmation,
        };

    public static bool IsPilot(string? entityName) =>
        entityName is not null && Pilots.Contains(entityName);
}
