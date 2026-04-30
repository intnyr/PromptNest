using PromptNest.App.DeepLinks;

namespace PromptNest.UiTests;

public sealed class DeepLinkParserTests
{
    [Theory]
    [InlineData("promptnest://library", DeepLinkAction.OpenLibrary, null, null)]
    [InlineData("promptnest://open?promptId=abc", DeepLinkAction.OpenPrompt, "abc", null)]
    [InlineData("promptnest://prompt/abc", DeepLinkAction.OpenPrompt, "abc", null)]
    [InlineData("promptnest://create", DeepLinkAction.CreatePrompt, null, null)]
    [InlineData("promptnest://search?q=market%20analysis", DeepLinkAction.Search, null, "market analysis")]
    public void TryParseSupportsPromptNestRoutes(string value, DeepLinkAction action, string? promptId, string? searchText)
    {
        bool parsed = DeepLinkParser.TryParse(value, out DeepLinkRequest request);

        Assert.True(parsed);
        Assert.Equal(action, request.Action);
        Assert.Equal(promptId, request.PromptId);
        Assert.Equal(searchText, request.SearchText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("promptnest://prompt")]
    [InlineData("promptnest://search")]
    public void TryParseRejectsInvalidOrUnsupportedLinks(string value)
    {
        Assert.False(DeepLinkParser.TryParse(value, out _));
    }

    [Fact]
    public void TryParseFromArgumentsFindsFirstPromptNestLink()
    {
        DeepLinkRequest? request = DeepLinkParser.TryParseFromArguments(["--ignored", "promptnest://search?q=docs"]);

        Assert.NotNull(request);
        Assert.Equal(DeepLinkAction.Search, request.Action);
        Assert.Equal("docs", request.SearchText);
    }
}