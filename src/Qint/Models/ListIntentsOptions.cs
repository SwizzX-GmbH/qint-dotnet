namespace Qint;

/// <summary>
/// Optional filters and paging for <see cref="QintClient.ListIntentsAsync"/>.
/// Every field is optional; unset fields are omitted from the query string and the
/// API applies its defaults (page 1, page size 20, no status filter).
/// </summary>
public sealed record ListIntentsOptions
{
    /// <summary>Restrict results to a single status.</summary>
    public IntentStatus? Status { get; init; }

    /// <summary>1-based page number.</summary>
    public int? Page { get; init; }

    /// <summary>Page size (the API clamps this to a maximum of 100).</summary>
    public int? PageSize { get; init; }
}
