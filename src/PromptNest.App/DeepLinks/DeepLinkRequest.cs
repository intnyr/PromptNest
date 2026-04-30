namespace PromptNest.App.DeepLinks;

public enum DeepLinkAction
{
    OpenLibrary,
    OpenPrompt,
    CreatePrompt,
    Search
}

public sealed record DeepLinkRequest
{
    public DeepLinkAction Action { get; init; }

    public string? PromptId { get; init; }

    public string? SearchText { get; init; }
}