namespace Qint;

/// <summary>The fiat currency codes the Qint API accepts for <see cref="CreateIntentRequest.Currency"/>.</summary>
public static class QintCurrencies
{
    /// <summary>Swiss franc.</summary>
    public const string Chf = "CHF";

    /// <summary>Euro.</summary>
    public const string Eur = "EUR";

    /// <summary>US dollar.</summary>
    public const string Usd = "USD";

    /// <summary>Pound sterling.</summary>
    public const string Gbp = "GBP";

    /// <summary>All supported currency codes.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Chf, Eur, Usd, Gbp };
}
