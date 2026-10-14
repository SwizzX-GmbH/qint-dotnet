using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qint;

/// <summary>
/// RFC 7807 <c>application/problem+json</c> body returned by the Qint API on errors.
/// Exposed on <see cref="QintApiException.ProblemDetails"/>. The API serialises problem
/// extensions flat at the top level of the body; the known ones are typed below and
/// any others land in <see cref="Extensions"/>.
/// </summary>
public sealed record QintProblemDetails
{
    /// <summary>A URI reference identifying the problem type.</summary>
    public string? Type { get; init; }

    /// <summary>A short, human-readable summary of the problem.</summary>
    public string? Title { get; init; }

    /// <summary>The HTTP status code.</summary>
    public int? Status { get; init; }

    /// <summary>A human-readable explanation specific to this occurrence.</summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Machine-readable error code, if any — for example
    /// <see cref="QintErrorCodes.AmountOutOfRange"/> or <see cref="QintErrorCodes.RateUnavailable"/>.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>Request correlation id — include it in support requests.</summary>
    public string? TraceId { get; init; }

    /// <summary><c>amount_out_of_range</c>: the smallest allowed payment, in CHF.</summary>
    public decimal? MinChf { get; init; }

    /// <summary><c>amount_out_of_range</c>: the largest allowed payment, in CHF.</summary>
    public decimal? MaxChf { get; init; }

    /// <summary><c>amount_out_of_range</c>: the requested amount converted to CHF.</summary>
    public decimal? AmountChf { get; init; }

    /// <summary>Any other top-level members of the problem body .</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extensions { get; init; }
}
