namespace Qint;

/// <summary>
/// The <c>payment.status</c> webhook event Qint delivers to a merchant endpoint on
/// every payment-intent status transition (and on every underpaid-flag flip).
/// Parse the raw request body with <see cref="QintWebhooks.ParseEvent(string)"/> after
/// verifying its signature.
/// </summary>
public sealed record PaymentStatusEvent
{
    /// <summary>Event type. Always <c>payment.status</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Identifier of the intent this event is about.</summary>
    public required string IntentId { get; init; }

    /// <summary>The intent's status at the time of the event.</summary>
    public required IntentStatus Status { get; init; }

    /// <summary>Fiat amount of the intent.</summary>
    public decimal Amount { get; init; }

    /// <summary>Fiat currency code of the intent.</summary>
    public required string Currency { get; init; }

    /// <summary>Crypto asset selected for the intent, if any.</summary>
    public string? AssetSymbol { get; init; }

    /// <summary>Identifier of the invoice this intent pays, if any.</summary>
    public string? InvoiceId { get; init; }

    /// <summary>Identifier of the payment link this intent came from, if any.</summary>
    public string? PaymentLinkId { get; init; }

    /// <summary>When the underlying transition occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>True when the buyer paid less crypto than expected.</summary>
    public bool Underpaid { get; init; }

    /// <summary>The crypto amount the intent expected, when known.</summary>
    public decimal? ExpectedCryptoAmount { get; init; }

    /// <summary>The crypto amount actually observed by the gateway, when known.</summary>
    public decimal? ReceivedCryptoAmount { get; init; }
}
