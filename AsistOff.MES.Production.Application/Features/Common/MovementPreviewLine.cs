namespace AsistOff.MES.Production.Application.Features.Common;

/// <summary>
/// One read-only RW/PW movement preview line. Polish shopfloor practice
/// records production with RW (Rozchod Wewnetrzny, material issue) and PW
/// (Przyjecie Wewnetrzne, finished goods receipt) documents; this preview
/// shows what would be issued and received without posting real inventory
/// balances. No EF Core entity backs this type.
/// </summary>
public record MovementPreviewLine(
    /// <summary>Either <c>"PW"</c> (finished goods receipt) or <c>"RW"</c> (material issue).</summary>
    string MovementType,
    Guid ProductId,
    decimal Quantity,
    Guid? MeasureUnitId,
    /// <summary>Planning hint taken from the BOM item; null for PW lines.</summary>
    Guid? PreferredWarehouseId);
