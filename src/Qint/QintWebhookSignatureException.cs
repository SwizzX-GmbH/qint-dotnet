namespace Qint;

/// <summary>
/// Thrown by <see cref="QintWebhooks.ConstructEvent(string, string, string)"/> when a
/// webhook payload's signature does not match the expected HMAC.
/// </summary>
public sealed class QintWebhookSignatureException : Exception
{
    /// <summary>Creates a new <see cref="QintWebhookSignatureException"/>.</summary>
    /// <param name="message">A description of the verification failure.</param>
    public QintWebhookSignatureException(string message) : base(message)
    {
    }
}
