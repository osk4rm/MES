using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using MediatR;

namespace AsistOff.MES.Integration.Tests.Outbox;

/// <summary>
/// Slice 2 (#259) test-only domain event for proving handler retry through
/// the real host MediatR pipeline. Member names avoid the outbox secret
/// fragments so rows stage cleanly.
/// </summary>
public sealed record FlakyOutboxEvent(Guid ProbeId, string Code) : IDomainEvent;

/// <summary>
/// Slice 2 (#259) controllable <see cref="FlakyOutboxEvent"/> handler. Test
/// orchestration only: <see cref="Reset"/> arms the failure mode before a
/// relay and clears it in a <c>finally</c> so no state leaks into other
/// tests. Fires exclusively for the test-only event, so production traffic
/// is unaffected.
/// </summary>
public sealed class FlakyOutboxHandler : INotificationHandler<FlakyOutboxEvent>
{
    private static int _calls;
    private static int _failuresRemaining;
    private static bool _alwaysFail;

    /// <summary>How many times the host pipeline invoked this handler.</summary>
    public static int Calls => _calls;

    /// <summary>
    /// Arms the handler: it throws for the first
    /// <paramref name="failuresRemaining"/> calls (or every call when
    /// <paramref name="alwaysFail"/> is set) and succeeds afterwards.
    /// </summary>
    public static void Reset(int failuresRemaining = 0, bool alwaysFail = false)
    {
        _calls = 0;
        _failuresRemaining = failuresRemaining;
        _alwaysFail = alwaysFail;
    }

    public Task Handle(FlakyOutboxEvent notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);

        if (_alwaysFail)
        {
            throw new InvalidOperationException("flaky probe down");
        }

        if (Interlocked.Decrement(ref _failuresRemaining) >= 0)
        {
            throw new InvalidOperationException("flaky probe down");
        }

        return Task.CompletedTask;
    }
}
