namespace Qint;

/// <summary>
/// Machine-readable error codes the Qint API puts in a problem body's <c>code</c>
/// member (see <see cref="QintApiException.Code"/>).
/// </summary>
/// <remarks>
/// Not every 400 carries a code. A currency Qint has switched off (see
/// <see cref="QintClient.ListCurrenciesAsync"/>) is refused with a plain 400 whose
/// <see cref="QintApiException.Detail"/> names the enabled currencies, for example
/// <c>GBP is currently disabled on Qint. currency must be one of: CHF, EUR, USD.</c>
/// </remarks>
public static class QintErrorCodes
{
    /// <summary>
    /// HTTP 400 on create: the amount is outside the per-payment limits (CHF 10 to
    /// CHF 100'000 or the equivalent). <see cref="QintProblemDetails.MinChf"/>,
    /// <see cref="QintProblemDetails.MaxChf"/> and <see cref="QintProblemDetails.AmountChf"/>
    /// carry the numbers.
    /// </summary>
    public const string AmountOutOfRange = "amount_out_of_range";

    /// <summary>HTTP 503 on create: no exchange rate was available to judge the limits. Retry shortly.</summary>
    public const string RateUnavailable = "rate_unavailable";

    /// <summary>
    /// HTTP 400 from the payout endpoints of the dashboard API (not the merchant API): a crypto
    /// payout was requested in an asset other than the merchant's settlement asset
    /// (see <see cref="QintSettlementAssets"/>). Listed for completeness.
    /// </summary>
    public const string SettlementAssetMismatch = "settlement_asset_mismatch";
}
