namespace PromptNest.Core.Models;

public enum PromptSortBy
{
    Relevance,
    Title,
    UpdatedAt,
    LastUsedAt,
    CreatedAt,
    UseCount
}

public sealed record PromptQuery
{
    public string? FolderId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool? IsFavorite { get; init; }

    public bool IncludeDeleted { get; init; }

    public PromptSortBy SortBy { get; init; } = PromptSortBy.UpdatedAt;

    public bool SortDescending { get; init; } = true;

    public int Skip { get; init; }

    public int Take { get; init; } = 50;
}

public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; }
}