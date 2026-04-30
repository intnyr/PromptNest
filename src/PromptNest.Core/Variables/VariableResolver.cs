using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Variables;

public sealed class VariableResolver : IVariableResolver
{
    private readonly IVariableValueRepository _variableValueRepository;

    public VariableResolver(IVariableValueRepository variableValueRepository)
    {
        _variableValueRepository = variableValueRepository;
    }

    public async Task<OperationResult<ResolvedPrompt>> ResolveAsync(
        Prompt prompt,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(values);

        var lastUsedValues = await _variableValueRepository.GetLastUsedValuesAsync(prompt.Id, cancellationToken);
        var resolvedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resolvedText = prompt.Body;

        foreach (var variable in prompt.Variables)
        {
            var value = ResolveValue(variable, values, lastUsedValues);
            if (string.IsNullOrWhiteSpace(value) && variable.IsRequired)
            {
                return OperationResultFactory.Failure<ResolvedPrompt>(
                    "VariableRequired",
                    $"Variable '{variable.Name}' is required.");
            }

            value ??= string.Empty;
            resolvedValues[variable.Name] = value;
            resolvedText = resolvedText
                .Replace($"{{{{{variable.Name}}}}}", value, StringComparison.Ordinal)
                .Replace($"{{{{{variable.Name}|{variable.DefaultValue}}}}}", value, StringComparison.Ordinal);
        }

        await _variableValueRepository.SaveLastUsedValuesAsync(prompt.Id, resolvedValues, cancellationToken);

        return OperationResultFactory.Success(
            new ResolvedPrompt { PromptId = prompt.Id, Text = resolvedText, Values = resolvedValues });
    }

    private static string? ResolveValue(
        PromptVariable variable,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> lastUsedValues)
    {
        if (values.TryGetValue(variable.Name, out var suppliedValue))
        {
            return suppliedValue;
        }

        if (lastUsedValues.TryGetValue(variable.Name, out var lastUsedValue))
        {
            return lastUsedValue;
        }

        return variable.DefaultValue;
    }
}