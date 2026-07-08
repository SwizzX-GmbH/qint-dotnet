namespace Qint;

/// <summary>A single page of <see cref="Intent"/> results from <see cref="QintClient.ListIntentsAsync"/>.</summary>
public sealed record IntentList
{
    /// <summary>The intents on this page, newest first.</summary>
    public required IReadOnlyList<Intent> Items { get; init; }

    /// <summary>Total number of intents matching the filter across all pages.</summary>
    public int Total { get; init; }

    /// <summary>The 1-based page number these items came from.</summary>
    public int Page { get; init; }

    /// <summary>The page size used for this response.</summary>
    public int PageSize { get; init; }
}
