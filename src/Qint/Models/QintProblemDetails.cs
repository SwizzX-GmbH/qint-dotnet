namespace Qint;

/// <summary>
/// RFC 7807 <c>application/problem+json</c> body returned by the Qint API on errors.
/// Exposed on <see cref="QintApiException.ProblemDetails"/>.
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
}
