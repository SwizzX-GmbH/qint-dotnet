namespace Qint;

/// <summary>
/// Lifecycle status of a payment <see cref="Intent"/>. Serialized on the wire as
/// its lowercase name (for example <c>confirmed</c>), matching the Qint public API.
/// </summary>
public enum IntentStatus
{
    /// <summary>The intent was created but the buyer has not started paying.</summary>
    Initiated,

    /// <summary>A payment is in progress / awaiting on-chain confirmations.</summary>
    Pending,

    /// <summary>The payment has been confirmed on-chain.</summary>
    Confirmed,

    /// <summary>Funds have been settled to the merchant.</summary>
    Settled,

    /// <summary>The payment failed.</summary>
    Failed,

    /// <summary>The intent expired before it was paid.</summary>
    Expired,

    /// <summary>The intent was cancelled.</summary>
    Cancelled,
}
