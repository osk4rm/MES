using System.Globalization;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders;

/// <summary>
/// Shared optimistic-concurrency guard for Production Order writes
/// (issue #263). The token is the PostgreSQL <c>xmin</c> value rendered as
/// an opaque string. Database-level enforcement comes from the
/// <c>IsRowVersion</c> mapping; these checks fail fast with a typed 409
/// (carrying the current token) before any write is attempted.
/// Retry steps for callers: <c>GET /api/production-orders/{id}</c>, take the
/// fresh <c>concurrencyToken</c>, re-apply the intended change, resubmit.
/// </summary>
internal static class ProductionOrderConcurrency
{
    public const string TokenParameter = "concurrencyToken";

    public static string TokenOf(ProductionOrder order)
        => order.Xmin.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Update semantics: the token is required. Missing or malformed tokens
    /// are 400s; a well-formed but stale token is a 409 with the current token.
    /// </summary>
    public static void RequireMatchForUpdate(ProductionOrder order, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException(TokenParameter, "Concurrency token is required. Reload the order and retry with the current token.");

        RequireMatch(order, token);
    }

    /// <summary>
    /// Transition (release/complete/close) semantics: the token is optional
    /// for backward compatibility, but when supplied it must match.
    /// </summary>
    public static void RequireMatchIfPresent(ProductionOrder order, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        RequireMatch(order, token);
    }

    private static void RequireMatch(ProductionOrder order, string token)
    {
        if (!uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var provided))
            throw new ValidationException(TokenParameter, "Concurrency token is invalid. Reload the order and retry with the current token.");

        if (provided != order.Xmin)
            throw new ConcurrencyConflictException(
                TokenOf(order),
                $"Production order '{order.Code}' was modified by another user. Reload the order and retry with the current concurrency token.");
    }
}
