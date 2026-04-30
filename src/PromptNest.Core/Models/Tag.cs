namespace PromptNest.Core.Models;

public sealed record Tag
{
    public required string Name { get; init; }

    public string? Color { get; init; }

    public int Count { get; init; }
}