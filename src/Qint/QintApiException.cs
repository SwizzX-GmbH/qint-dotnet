namespace Qint;

/// <summary>
/// Thrown when the Qint API returns a non-2xx response. Carries the HTTP
/// <see cref="StatusCode"/> and, when present, the parsed
/// <see cref="ProblemDetails"/> (RFC 7807 <c>problem+json</c>). The exception
/// <see cref="System.Exception.Message"/> is the problem <c>detail</c> when available.
/// </summary>
public sealed class QintApiException : Exception
{
    /// <summary>The HTTP status code of the failed response.</summary>
    public int StatusCode { get; }

    /// <summary>The parsed problem-details body, if the response carried one.</summary>
    public QintProblemDetails? ProblemDetails { get; }

    /// <summary>The problem-details <c>detail</c> message, if any.</summary>
    public string? Detail => ProblemDetails?.Detail;

    /// <summary>Creates a new <see cref="QintApiException"/>.</summary>
    /// <param name="statusCode">HTTP status code of the failed response.</param>
    /// <param name="problemDetails">Parsed problem-details body, if any.</param>
    /// <param name="message">Human-readable error message.</param>
    public QintApiException(int statusCode, QintProblemDetails? problemDetails, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ProblemDetails = problemDetails;
    }
}
