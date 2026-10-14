using System.Text.Json;

namespace Qint;

/// <summary>
/// Any Qint webhook delivery, as returned by <see cref="QintWebhooks.ParseWebhook(string)"/>
/// and <see cref="QintWebhooks.ConstructWebhook(string, string, string)"/>. Check
/// <see cref="Type"/>: exactly one of <see cref="PaymentStatus"/> / <see cref="Ping"/> is set
/// for the known types, and neither for an event type this SDK version does not know —
/// acknowledge those with a 2xx and ignore them (use <see cref="Raw"/> if you need the body).
/// </summary>
public sealed record WebhookEvent
{
    /// <summary>The event <c>type</c> (<see cref="QintWebhooks.PaymentStatusEventType"/>, <see cref="QintWebhooks.PingEventType"/>, or a newer type).</summary>
    public required string Type { get; init; }

    /// <summary>The parsed event when <see cref="Type"/> is <c>payment.status</c>.</summary>
    public PaymentStatusEvent? PaymentStatus { get; init; }

    /// <summary>The parsed event when <see cref="Type"/> is <c>ping</c>.</summary>
    public PingEvent? Ping { get; init; }

    /// <summary>The whole JSON body, for event types this SDK version does not model.</summary>
    public JsonElement Raw { get; init; }
}
