namespace PromptNest.Core.Models;

public sealed record Prompt
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public string? FolderId { get; init; }

    public bool IsFavorite { get; init; }

    public int UseCount { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? DeletedAt { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<PromptVariable> Variables { get; init; } = [];
}