namespace PromptNest.Core.Models;

public sealed record Folder
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? ParentId { get; init; }

    public int SortOrder { get; init; }

    public int PromptCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}