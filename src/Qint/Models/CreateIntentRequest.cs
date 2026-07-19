namespace Qint;

/// <summary>
/// Parameters for creating a payment intent via <see cref="QintClient.CreateIntentAsync"/>.
/// Null optional fields are omitted from the request body.
/// </summary>
public sealed record CreateIntentRequest
{
    /// <summary>Fiat amount to collect. Required.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Fiat currency code — one of <c>CHF</c>, <c>EUR</c> or <c>USD</c>. Required.</summary>
    public required string Currency { get; init; }

    /// <summary>Optional title shown on the hosted checkout.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Idempotency key, unique per merchant (e.g. your order id). Required — the API
    /// rejects creates without one. Reusing the same key returns the originally
    /// created intent (HTTP 200) instead of creating a duplicate.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Optional HTTPS URL (max 500 chars) the hosted checkout returns the buyer to,
    /// with <c>?qint_intent={id}&amp;status={status}</c> appended.
    /// </summary>
    public string? ReturnUrl { get; init; }
}
