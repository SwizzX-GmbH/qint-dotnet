namespace Qint;

/// <summary>
/// The <c>ping</c> test event, sent when you press <b>Send test event</b> in the dashboard.
/// It is signed and retried like any event — verify it, return 2xx and otherwise ignore it.
/// </summary>
public sealed record PingEvent
{
    /// <summary>Event type. Always <c>ping</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Identifier of the webhook endpoint the test was sent to.</summary>
    public string? EndpointId { get; init; }

    /// <summary>When the test event was sent.</summary>
    public DateTimeOffset? SentAt { get; init; }
}
