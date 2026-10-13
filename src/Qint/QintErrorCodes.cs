namespace Qint;

/// <summary>
/// Machine-readable error codes the Qint API puts in a problem body's <c>code</c>
/// member (see <see cref="QintApiException.Code"/>).
/// </summary>
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
}
