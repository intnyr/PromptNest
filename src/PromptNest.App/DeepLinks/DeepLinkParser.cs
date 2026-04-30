namespace PromptNest.App.DeepLinks;

public static class DeepLinkParser
{
    public static bool TryParse(string? value, out DeepLinkRequest request)
    {
        request = new DeepLinkRequest { Action = DeepLinkAction.OpenLibrary };
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, "promptnest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string host = uri.Host.ToLowerInvariant();
        string[] segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        Dictionary<string, string> query = ParseQuery(uri.Query);

        request = host switch
        {
            "" or "open" or "library" => new DeepLinkRequest
            {
                Action = DeepLinkAction.OpenLibrary,
                PromptId = query.GetValueOrDefault("promptId")
            },
            "prompt" => new DeepLinkRequest
            {
                Action = DeepLinkAction.OpenPrompt,
                PromptId = segments.FirstOrDefault() ?? query.GetValueOrDefault("id") ?? query.GetValueOrDefault("promptId")
            },
            "create" or "new" => new DeepLinkRequest { Action = DeepLinkAction.CreatePrompt },
            "search" => new DeepLinkRequest
            {
                Action = DeepLinkAction.Search,
                SearchText = query.GetValueOrDefault("q") ?? string.Join(' ', segments)
            },
            _ => new DeepLinkRequest { Action = DeepLinkAction.OpenLibrary }
        };

        if (request.Action == DeepLinkAction.OpenLibrary && !string.IsNullOrWhiteSpace(request.PromptId))
        {
            request = request with { Action = DeepLinkAction.OpenPrompt };
        }

        return request.Action switch
        {
            DeepLinkAction.OpenPrompt => !string.IsNullOrWhiteSpace(request.PromptId),
            DeepLinkAction.Search => !string.IsNullOrWhiteSpace(request.SearchText),
            _ => true
        };
    }

    public static DeepLinkRequest? TryParseFromArguments(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        foreach (string arg in args)
        {
            if (TryParse(arg, out DeepLinkRequest request))
            {
                return request;
            }
        }

        return null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace("+", " ", StringComparison.Ordinal)) : string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }
}