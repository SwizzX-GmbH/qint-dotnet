namespace Qint;

/// <summary>
/// A Qint payment intent. Returned by <see cref="QintClient.CreateIntentAsync"/>,
/// <see cref="QintClient.GetIntentAsync"/> and inside <see cref="IntentList"/>.
/// </summary>
public sealed record Intent
{
    /// <summary>Stable identifier of the intent (for example <c>pi_...</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public required IntentStatus Status { get; init; }

    /// <summary>Fiat amount to collect, in the smallest sensible decimal (not minor units).</summary>
    public decimal Amount { get; init; }

    /// <summary>Fiat currency code — one of <c>CHF</c>, <c>EUR</c> or <c>USD</c>.</summary>
    public required string Currency { get; init; }

    /// <summary>Optional human-readable title shown on the hosted checkout.</summary>
    public string? Title { get; init; }

    /// <summary>Crypto asset the buyer selected, once chosen (for example <c>USDT</c>).</summary>
    public string? AssetSymbol { get; init; }

    /// <summary>Buyer-facing crypto amount as an 8-decimal-place string, once an asset is selected.</summary>
    public string? CryptoAmount { get; init; }

    /// <summary>On-chain deposit address, once an asset is selected.</summary>
    public string? DepositAddress { get; init; }

    /// <summary>Hosted checkout URL — redirect the buyer here to pay.</summary>
    public required string CheckoutUrl { get; init; }

    /// <summary>Optional URL the checkout returns the buyer to after payment.</summary>
    public string? ReturnUrl { get; init; }

    /// <summary>When the intent was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the intent expires if left unpaid.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>When the payment was confirmed on-chain, if it has been.</summary>
    public DateTimeOffset? ConfirmedAt { get; init; }

    /// <summary>When funds were settled to the merchant, if they have been.</summary>
    public DateTimeOffset? SettledAt { get; init; }
}
