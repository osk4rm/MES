namespace AsistOff.MES.Shared.Abstractions.Exceptions;

/// <summary>
/// Optimistic-concurrency conflict: the caller wrote with a stale token.
/// Carries the current token so the caller can reload and retry.
/// Maps to HTTP 409 with a <c>concurrencyToken</c> problem-details extension.
/// </summary>
public class ConcurrencyConflictException : ConflictException
{
    public string CurrentToken { get; }

    public ConcurrencyConflictException(string currentToken)
        : base("The record was modified by another user. Reload it and retry with the current concurrency token.")
    {
        CurrentToken = currentToken;
    }

    public ConcurrencyConflictException(string currentToken, string message)
        : base(message)
    {
        CurrentToken = currentToken;
    }

    public ConcurrencyConflictException(string currentToken, string message, Exception innerException)
        : base(message, innerException)
    {
        CurrentToken = currentToken;
    }
}
