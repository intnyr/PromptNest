namespace PromptNest.Core.Models;

public enum VariableValueType
{
    Text,
    Number,
    Date,
    Boolean
}

public sealed record PromptVariable
{
    public required string Name { get; init; }

    public string? DefaultValue { get; init; }

    public VariableValueType Type { get; init; } = VariableValueType.Text;

    public string? PreviewValue { get; init; }

    public bool IsRequired => string.IsNullOrWhiteSpace(DefaultValue);
}

public sealed record VariableValue
{
    public required string PromptId { get; init; }

    public required string VariableName { get; init; }

    public required string Value { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed record ResolvedPrompt
{
    public required string PromptId { get; init; }

    public required string Text { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}