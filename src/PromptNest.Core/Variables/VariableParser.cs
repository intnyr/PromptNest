using System.Text.RegularExpressions;

using PromptNest.Core.Abstractions;
using PromptNest.Core.Models;

namespace PromptNest.Core.Variables;

public sealed partial class VariableParser : IVariableParser
{
    public IReadOnlyList<PromptVariable> Parse(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var variables = new Dictionary<string, PromptVariable>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in PlaceholderRegex().Matches(body))
        {
            var name = match.Groups["name"].Value;
            var defaultValue = match.Groups["default"].Success ? match.Groups["default"].Value : null;

            variables.TryAdd(
                name,
                new PromptVariable
                {
                    Name = name,
                    DefaultValue = string.IsNullOrEmpty(defaultValue) ? null : defaultValue
                });
        }

        return variables.Values.ToList();
    }

    [GeneratedRegex(@"\{\{(?<name>[a-zA-Z_][a-zA-Z0-9_]*)(?:\|(?<default>[^}]*))?\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}