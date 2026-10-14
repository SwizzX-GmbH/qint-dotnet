namespace Qint;

/// <summary>
/// The fiat currencies Qint knows and whether each is offered for <b>new</b> payments right
/// now, as returned by <see cref="QintClient.ListCurrenciesAsync"/>
/// (<c>GET /api/v1/public/currencies</c>). Qint can switch a currency off and back on at any
/// time; while it is off, creating an intent in it is refused with HTTP 400.
/// </summary>
public sealed record CurrencyList
{
    /// <summary>Every currency, in display order.</summary>
    public required IReadOnlyList<CurrencyAvailability> Currencies { get; init; }

    /// <summary>The codes currently offered for new payments, in display order.</summary>
    public IReadOnlyList<string> EnabledCodes() =>
        Currencies.Where(c => c.Enabled).Select(c => c.Code).ToList();

    /// <summary>
    /// Whether <paramref name="code"/> is currently offered for new payments
    /// (case-insensitive). Unknown codes return <see langword="false"/>.
    /// </summary>
    public bool IsEnabled(string code) =>
        Currencies.Any(c => c.Enabled && string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One entry of a <see cref="CurrencyList"/>.</summary>
public sealed record CurrencyAvailability
{
    /// <summary>ISO 4217 currency code (for example <c>CHF</c>). A plain string so codes added later still parse.</summary>
    public required string Code { get; init; }

    /// <summary>Whether the currency is offered for new payments right now.</summary>
    public bool Enabled { get; init; }
}
