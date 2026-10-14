namespace Qint;

/// <summary>
/// The stablecoins a merchant balance can be held in — the values of
/// <see cref="Intent.CreditAsset"/>. Merchants registered in the European Union settle in
/// <c>USDC</c> (USDT is not authorised under MiCA); all other merchants settle in <c>USDT</c>.
/// </summary>
/// <remarks>
/// Kept as plain strings so a settlement asset added later still deserializes; compare
/// against these constants rather than switching exhaustively.
/// </remarks>
public static class QintSettlementAssets
{
    /// <summary>Tether USD — the settlement asset for merchants outside the EU.</summary>
    public const string Usdt = "USDT";

    /// <summary>USD Coin — the settlement asset for merchants registered in the EU.</summary>
    public const string Usdc = "USDC";

    /// <summary>All settlement assets known to this SDK version.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Usdt, Usdc };
}
