using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qint;

/// <summary>
/// Helpers for verifying and parsing Qint webhook deliveries.
/// </summary>
/// <remarks>
/// <para>
/// Every delivery carries two headers:
/// <list type="bullet">
///   <item><description><c>X-Qint-Signature: sha256=&lt;hex&gt;</c> — a hex-encoded
///   HMAC-SHA256 of the <b>raw</b> request body, keyed with your endpoint secret
///   (<c>whsec_...</c>).</description></item>
///   <item><description><c>X-Qint-Event-Id: &lt;id&gt;</c> — a stable delivery id. Persist it
///   and skip events whose id you have already processed, since Qint may retry a delivery
///   (each retry re-uses the same event id). Always acknowledge with a fast 2xx.</description></item>
/// </list>
/// </para>
/// <para>
/// Verify against the exact bytes you received — do not re-serialize the parsed model first,
/// or the signature will not match.
/// </para>
/// </remarks>
public static class QintWebhooks
{
    /// <summary>The signature header name (<c>X-Qint-Signature</c>).</summary>
    public const string SignatureHeaderName = "X-Qint-Signature";

    /// <summary>The event-id / de-duplication header name (<c>X-Qint-Event-Id</c>).</summary>
    public const string EventIdHeaderName = "X-Qint-Event-Id";

    private const string SignaturePrefix = "sha256=";

    /// <summary>
    /// Verifies a webhook signature against the raw request body using a constant-time compare.
    /// </summary>
    /// <param name="rawBody">The exact bytes of the request body.</param>
    /// <param name="signatureHeader">The <c>X-Qint-Signature</c> header value (<c>sha256=&lt;hex&gt;</c>).</param>
    /// <param name="secret">Your endpoint signing secret (<c>whsec_...</c>).</param>
    /// <returns><see langword="true"/> if the signature is valid; otherwise <see langword="false"/>.</returns>
    public static bool VerifyWebhookSignature(ReadOnlySpan<byte> rawBody, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signatureHeader))
            return false;

        if (!signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal))
            return false;

        var hex = signatureHeader.AsSpan(SignaturePrefix.Length);

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[32]; // HMAC-SHA256 output size
        var written = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody, expected);
        return written == provided.Length && CryptographicOperations.FixedTimeEquals(expected[..written], provided);
    }

    /// <summary>
    /// Verifies a webhook signature against the raw request body (as a UTF-8 string).
    /// </summary>
    /// <param name="rawBody">The exact request body string.</param>
    /// <param name="signatureHeader">The <c>X-Qint-Signature</c> header value (<c>sha256=&lt;hex&gt;</c>).</param>
    /// <param name="secret">Your endpoint signing secret (<c>whsec_...</c>).</param>
    /// <returns><see langword="true"/> if the signature is valid; otherwise <see langword="false"/>.</returns>
    public static bool VerifyWebhookSignature(string rawBody, string? signatureHeader, string secret) =>
        VerifyWebhookSignature(Encoding.UTF8.GetBytes(rawBody ?? string.Empty), signatureHeader, secret);

    /// <summary>
    /// Parses a webhook request body into a <see cref="PaymentStatusEvent"/>. Does not verify
    /// the signature — call <see cref="VerifyWebhookSignature(string, string, string)"/> first, or use
    /// <see cref="ConstructEvent(string, string, string)"/> to do both.
    /// </summary>
    /// <param name="rawBody">The webhook request body.</param>
    /// <returns>The parsed <see cref="PaymentStatusEvent"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="rawBody"/> is null or empty.</exception>
    /// <exception cref="JsonException">The body is not valid JSON for a payment.status event.</exception>
    public static PaymentStatusEvent ParseEvent(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            throw new ArgumentException("The webhook body is empty.", nameof(rawBody));

        var evt = JsonSerializer.Deserialize<PaymentStatusEvent>(rawBody, QintJson.Options);
        return evt ?? throw new JsonException("The webhook body deserialized to null.");
    }

    /// <summary>
    /// Verifies the signature and parses the event in one step — the recommended entry point.
    /// </summary>
    /// <param name="rawBody">The exact webhook request body.</param>
    /// <param name="signatureHeader">The <c>X-Qint-Signature</c> header value.</param>
    /// <param name="secret">Your endpoint signing secret (<c>whsec_...</c>).</param>
    /// <returns>The verified, parsed <see cref="PaymentStatusEvent"/>.</returns>
    /// <exception cref="QintWebhookSignatureException">The signature did not match.</exception>
    /// <exception cref="JsonException">The body is not valid JSON for a payment.status event.</exception>
    public static PaymentStatusEvent ConstructEvent(string rawBody, string? signatureHeader, string secret)
    {
        if (!VerifyWebhookSignature(rawBody, signatureHeader, secret))
            throw new QintWebhookSignatureException("The webhook signature could not be verified.");

        return ParseEvent(rawBody);
    }
}
