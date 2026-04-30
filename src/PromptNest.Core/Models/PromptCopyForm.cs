namespace PromptNest.Core.Models;

public sealed record PromptCopyForm
{
    public required string PromptId { get; init; }

    public required string Title { get; init; }

    public IReadOnlyList<PromptCopyVariable> Variables { get; init; } = [];

    public bool RequiresInput => Variables.Count > 0;
}

public sealed record PromptCopyVariable
{
    public required string Name { get; init; }

    public string? DefaultValue { get; init; }

    public string? CurrentValue { get; init; }

    public VariableValueType Type { get; init; } = VariableValueType.Text;

    public bool IsRequired { get; init; }
}