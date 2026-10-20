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

    /// <summary>Fiat currency code — one of <c>CHF</c>, <c>EUR</c>, <c>USD</c> or <c>GBP</c> (see <see cref="QintCurrencies"/>).</summary>
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

    /// <summary>
    /// When the execution partner reversed the payment after settlement. The status
    /// stays <see cref="IntentStatus.Settled"/>; treat a reversed payment as not paid.
    /// Null otherwise.
    /// </summary>
    public DateTimeOffset? ReversedAt { get; init; }

    /// <summary>
    /// True once the payment has been reversed after settlement (<see cref="ReversedAt"/> is set).
    /// A flag, not a status: <see cref="Status"/> stays <see cref="IntentStatus.Settled"/>.
    /// </summary>
    public bool Reversed { get; init; }

    /// <summary>
    /// True when the buyer sent less crypto than quoted. A flag, not a status — see
    /// <see cref="ReceivedAmount"/> and <see cref="UnderpaidBps"/>.
    /// </summary>
    public bool Underpaid { get; init; }

    /// <summary>Qint's fee on this payment, in <see cref="Currency"/>. Null until the payment is settled.</summary>
    public decimal? FeeAmount { get; init; }

    /// <summary>What the merchant is credited for this payment, in <see cref="Currency"/>. Null until the payment is settled.</summary>
    public decimal? NetAmount { get; init; }

    /// <summary>The amount the execution partner booked as realised. Null until booked.</summary>
    public decimal? SettledAmount { get; init; }

    /// <summary>
    /// The fiat amount actually paid, in <see cref="Currency"/>, when the payment settled
    /// short within the merchant's accepted underpayment. Null otherwise.
    /// </summary>
    public decimal? ReceivedAmount { get; init; }

    /// <summary>How short the payment was, in basis points of <see cref="Amount"/>. Null unless underpaid.</summary>
    public int? UnderpaidBps { get; init; }

    /// <summary>
    /// The amount credited to the merchant balance for this payment, in <see cref="CreditCurrency"/>
    /// (2 decimal places). Null until the payment is settled and credited, and on payments settled
    /// before the fiat balance (20 October 2026).
    /// </summary>
    public decimal? CreditAmount { get; init; }

    /// <summary>
    /// The merchant's Account Currency at the moment the buyer was quoted — the currency
    /// <see cref="CreditAmount"/> is in (e.g. <c>CHF</c>). A string, so a currency added later still
    /// deserializes. Null until credited.
    /// </summary>
    public string? CreditCurrency { get; init; }

    /// <summary>
    /// Units of <see cref="CreditCurrency"/> per 1 unit of <see cref="Currency"/>, fixed when the
    /// buyer was quoted (1 when the two match). Null until credited.
    /// </summary>
    public decimal? CreditRate { get; init; }
}
